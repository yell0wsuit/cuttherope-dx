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
    /// 25 seconds and plays again, as the iOS menu schedules it. Unlike iOS, where the hands hold
    /// one pose, they show the local time.
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

        /// <summary>
        /// Clock angle the hour hand is drawn at, in degrees clockwise from twelve, measured from
        /// its art.
        /// </summary>
        public const float HourHandArtAngle = 24f;

        /// <summary>
        /// Clock angle the minute hand is drawn at, in degrees clockwise from twelve, measured from
        /// its art.
        /// </summary>
        public const float MinuteHandArtAngle = 120f;

        /// <summary>
        /// How far each hand is pulled in toward the hub along its length, in animation stage
        /// units: 8 clock-sheet pixels, enough for the center point to cover the base at every
        /// angle without the base's flat end showing past it.
        /// </summary>
        public const float HubOverlap = 8f / FlashXmlScale.AtlasToFlashPointScale;

        /// <summary>The DX title quad in the logo sheet.</summary>
        public const int ArtQuad = 52;

        /// <summary>The clock's center quad in the clock sheet.</summary>
        private const int CenterPointQuad = 0;

        /// <summary>The short hand's quad in the clock sheet.</summary>
        private const int HourHandQuad = 1;

        /// <summary>The long hand's quad in the clock sheet.</summary>
        private const int MinuteHandQuad = 2;

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
        private Func<DateTime> now;
        private FlashXmlStageRoot clock;
        private Image hourHand;
        private Image minuteHand;
        private Vector hourHandRest;
        private Vector minuteHandRest;
        private Vector hubCenter;

        /// <summary>Gets the timeline playing now, or -1 while waiting for the next idle.</summary>
        public int PlayingTimeline { get; private set; } = -1;

        /// <summary>Gets the seconds left before the idle flourish plays.</summary>
        public float IdleDelay { get; private set; }

        /// <summary>Builds the logo and starts its intro.</summary>
        /// <param name="random">Source of the idle waits.</param>
        /// <param name="candyDelegate">
        /// Receives the candy-select press; when <see langword="null"/> the logo carries no candy.
        /// </param>
        /// <param name="now">Source of the local time the hands show; the system clock when omitted.</param>
        /// <returns>The logo, sized to its art.</returns>
        public static TimeTravelLogo Create(Random random, IButtonDelegation candyDelegate = null, Func<DateTime> now = null)
        {
            TimeTravelLogo logo = new() { random = random, now = now ?? (() => DateTime.Now), anchor = 9, parentAnchor = 9 };
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
            logo.ShowTime();
            logo.Play(IntroTimeline);
            return logo;
        }

        /// <summary>Switches the source of the time the hands show, and turns them to it at once.</summary>
        /// <param name="clock">Source of the local time.</param>
        internal void UseClock(Func<DateTime> clock)
        {
            now = clock;
            ShowTime();
        }

        /// <inheritdoc />
        public override void Update(float delta)
        {
            base.Update(delta);
            ShowTime();
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
            hourHand = clockParts.Find(part => part.quadToDraw == HourHandQuad);
            minuteHand = clockParts.Find(part => part.quadToDraw == MinuteHandQuad);
            Image hub = clockParts.Find(part => part.quadToDraw == CenterPointQuad);
            hourHandRest = RestPosition(definition, hourHand);
            minuteHandRest = RestPosition(definition, minuteHand);
            Vector hubRest = RestPosition(definition, hub);
            hubCenter = hub == null ? hubRest : new Vector(hubRest.X + (hub.width / 2f), hubRest.Y + (hub.height / 2f));
            foreach (int id in new[] { IntroTimeline, IdleTimeline })
            {
                if (clock.GetTimeline(id) is { } timeline)
                {
                    timeline.delegateTimelineDelegate = this;
                }
            }
            _ = AddChild(clock);
        }

        /// <summary>Where a clock part rests between timelines: the idle timeline's position.</summary>
        /// <param name="definition">The parsed clock animation.</param>
        /// <param name="part">One of the clock's parts, or <see langword="null"/>.</param>
        /// <returns>The part's resting top left, in animation stage units.</returns>
        private Vector RestPosition(FlashXmlAnimationDefinition definition, Image part)
        {
            if (part == null)
            {
                return default;
            }
            int index = clockParts.IndexOf(part);
            return index >= 0
                && definition.Parts[index].Timelines.TryGetValue(IdleTimeline, out FlashXmlTimelineDefinition idle)
                && idle.PositionKeyFrames.Count > 0
                ? new Vector(idle.PositionKeyFrames[0].X, idle.PositionKeyFrames[0].Y)
                : new Vector(part.x, part.y);
        }

        /// <summary>
        /// Turns the hands to the local time. The animation never rotates them, so each is turned
        /// from the angle its art is drawn at; both sweep continuously.
        /// </summary>
        private void ShowTime()
        {
            TimeSpan time = now().TimeOfDay;
            float minutes = (float)(time.TotalMinutes % 60.0);
            float hours = (float)(time.TotalHours % 12.0);
            Turn(hourHand, hourHandRest, hours * 30f, HourHandArtAngle);
            Turn(minuteHand, minuteHandRest, minutes * 6f, MinuteHandArtAngle);
        }

        /// <summary>
        /// Points a hand at a clock angle, turning it about the hub's center. Each hand's art
        /// pivots on its sprite corner, a little off the hub, so a translation, which the element
        /// applies before its rotation, moves that pivot onto the hub's center for the current turn
        /// and slides the hand in along its length by <see cref="HubOverlap"/>.
        /// </summary>
        /// <param name="hand">The hand, or <see langword="null"/> when the animation lacks it.</param>
        /// <param name="rest">The hand's resting top left.</param>
        /// <param name="clockAngle">Angle to point at, in degrees clockwise from twelve.</param>
        /// <param name="artAngle">Clock angle the hand's art is drawn at.</param>
        private void Turn(Image hand, Vector rest, float clockAngle, float artAngle)
        {
            if (hand == null)
            {
                return;
            }
            float turn = clockAngle - artAngle;
            hand.rotation = turn;

            float toHubX = hubCenter.X - (rest.X + (hand.width >> 1) + hand.rotationCenterX);
            float toHubY = hubCenter.Y - (rest.Y + (hand.height >> 1) + hand.rotationCenterY);
            float turnRadians = turn * MathF.PI / 180f;
            float cos = MathF.Cos(turnRadians);
            float sin = MathF.Sin(turnRadians);
            float artRadians = artAngle * MathF.PI / 180f;
            hand.translateX = (toHubX * cos) + (toHubY * sin) - toHubX - (MathF.Sin(artRadians) * HubOverlap);
            hand.translateY = (toHubY * cos) - (toHubX * sin) - toHubY + (MathF.Cos(artRadians) * HubOverlap);
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
