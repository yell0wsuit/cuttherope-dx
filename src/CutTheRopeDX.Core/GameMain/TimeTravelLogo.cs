using System;
using System.Collections.Generic;

using CutTheRopeDX.Framework;
using CutTheRopeDX.Framework.Core;
using CutTheRopeDX.Framework.Visual;
using CutTheRopeDX.Helpers;

namespace CutTheRopeDX.GameMain
{
    /// <summary>
    /// The title for the Time Travel menus: the DX logo with Time Travel's clock hands animated on
    /// it. The intro plays once; after every timeline it ends, the idle flourish waits a random 5 to
    /// 25 seconds and plays again, as the iOS menu schedules it.
    /// </summary>
    internal sealed class TimeTravelLogo : BaseElement, ITimelineDelegate
    {
        /// <summary>Timeline played once when the logo appears.</summary>
        public const int IntroTimeline = 0;

        /// <summary>Timeline replayed after each random wait.</summary>
        public const int IdleTimeline = 1;

        /// <summary>Shortest wait before the idle flourish, in seconds.</summary>
        public const float MinIdleDelay = 5f;

        /// <summary>Number of whole seconds the wait can add to <see cref="MinIdleDelay"/>.</summary>
        public const int IdleDelaySpread = 21;

        /// <summary>The DX title quad in the logo sheet.</summary>
        public const int ArtQuad = 52;

        /// <summary>The clock's center quad in the clock sheet.</summary>
        private const int CenterPointQuad = 0;

        /// <summary>
        /// Where the clock center goes on the logo: the hole in the O of ROPE, in logo pixels.
        /// </summary>
        private static readonly Vector ClockCenterOnArt = new(388.5f, 425f);

        /// <summary>
        /// Top left of the clock's center point where the intro leaves it, in animation stage units.
        /// </summary>
        private static readonly Vector CenterPointRest = new(374.6f, 386.35f);

        private readonly List<Image> clockParts = [];
        private Random random;
        private FlashXmlStageRoot clock;

        /// <summary>Gets the timeline playing now, or -1 while waiting for the next idle.</summary>
        public int PlayingTimeline { get; private set; } = -1;

        /// <summary>Gets the seconds left before the idle flourish plays.</summary>
        public float IdleDelay { get; private set; }

        /// <summary>Builds the logo and starts its intro.</summary>
        /// <param name="random">Source of the idle waits.</param>
        /// <param name="candyDelegate">
        /// Receives the candy-select press; when <see langword="null"/> the logo carries no candy.
        /// </param>
        /// <returns>The logo, sized to its art.</returns>
        public static TimeTravelLogo Create(Random random, IButtonDelegation candyDelegate = null)
        {
            TimeTravelLogo logo = new() { random = random, anchor = 9, parentAnchor = 9 };
            Image art = Image.FromResource(Resources.Img.MenuLogoNew, ArtQuad);
            art.anchor = art.parentAnchor = 9;
            logo.width = art.width;
            logo.height = art.height;
            _ = logo.AddChild(art);
            logo.AttachClock();
            if (candyDelegate != null)
            {
                _ = art.AddChild(MenuController.CreateLogoCandyButton(candyDelegate));
            }
            logo.Play(IntroTimeline);
            return logo;
        }

        /// <inheritdoc />
        public override void Update(float delta)
        {
            base.Update(delta);
            if (PlayingTimeline >= 0)
            {
                return;
            }
            IdleDelay -= delta;
            if (IdleDelay <= 0f)
            {
                Play(IdleTimeline);
            }
        }

        /// <inheritdoc />
        public void TimelinereachedKeyFramewithIndex(Timeline t, KeyFrame k, int i)
        {
        }

        /// <inheritdoc />
        public void TimelineFinished(Timeline t)
        {
            PlayingTimeline = -1;
            IdleDelay = MinIdleDelay + random.Next(IdleDelaySpread);
        }

        /// <summary>Builds the clock hands' animation over the logo.</summary>
        private void AttachClock()
        {
            FlashXmlAnimationDefinition definition = FlashXmlImporter.ParseFile(
                ContentPaths.GetAnimationXmlAbsolutePath(TimeTravelArt.LogoAnimationXml));
            Texture2D sheet = Application.GetTexture(Resources.Img.LogoClockTimeTravel);
            clock = new FlashXmlStageRoot();
            _ = clock.InitWithTexture(sheet);
            clock.SetDrawQuad(0);
            clock.color = RGBAColor.transparentRGBA;
            clock.passColorToChilds = false;
            clock.width = (int)MathF.Round(definition.StageWidth);
            clock.height = (int)MathF.Round(definition.StageHeight);
            clock.anchor = clock.parentAnchor = 9;

            // The stage is in the animation's units and the logo in asset pixels. The stage scales
            // about its own center like every element, so its position is solved for the point
            // that must land on the logo: the center point's resting middle.
            float scale = FlashXmlScale.AtlasToFlashPointScale;
            float pivotX = CenterPointRest.X + (FlashXmlScale.NormalizeAtlasValue(sheet.quadRects[CenterPointQuad].w) / 2f);
            float pivotY = CenterPointRest.Y + (FlashXmlScale.NormalizeAtlasValue(sheet.quadRects[CenterPointQuad].h) / 2f);
            clock.scaleX = clock.scaleY = scale;
            clock.x = ClockCenterOnArt.X - (clock.width >> 1) - ((pivotX - (clock.width >> 1)) * scale);
            clock.y = ClockCenterOnArt.Y - (clock.height >> 1) - ((pivotY - (clock.height >> 1)) * scale);

            FlashXmlTargetAnimationBackend.BuildParts(definition, clock, clockParts, -1, -1);
            FlashXmlTargetAnimationBackend.BuildRootTimelines(definition, clock, -1, -1);
            foreach (int id in new[] { IntroTimeline, IdleTimeline })
            {
                if (clock.GetTimeline(id) is { } timeline)
                {
                    timeline.delegateTimelineDelegate = this;
                }
            }
            _ = AddChild(clock);
        }

        /// <summary>Starts one of the clock's timelines.</summary>
        /// <param name="timelineId">Timeline to play.</param>
        private void Play(int timelineId)
        {
            PlayingTimeline = timelineId;
            FlashXmlTargetAnimationBackend.PlayTimeline(clockParts, timelineId);
            FlashXmlTargetAnimationBackend.PlayRootTimeline(clock, timelineId);
        }
    }
}
