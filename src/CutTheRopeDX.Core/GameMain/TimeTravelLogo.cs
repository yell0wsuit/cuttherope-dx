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
    /// <remarks>
    /// A <see cref="FittedGroup"/>, so a menu that draws it scaled still hands the candy touches
    /// where the candy is drawn.
    /// </remarks>
    internal sealed class TimeTravelLogo : FittedGroup, ITimelineDelegate
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
        /// Clock angle the hour hand is drawn at, in degrees clockwise from twelve. Every hand
        /// measurement is of its white body, the hand as it reads without the thick shadow edge
        /// on one side, so the hand looks straight and centered rather than its outline.
        /// </summary>
        public const float HourHandArtAngle = 24.2f;

        /// <summary>Clock angle the minute hand is drawn at, in degrees clockwise from twelve.</summary>
        public const float MinuteHandArtAngle = 119.1f;

        /// <summary>
        /// How far each hand is pulled in toward the hub along its length, in animation stage
        /// units: 8 clock-sheet pixels, enough for the center point to cover the base at every
        /// angle without the base's flat end showing past it.
        /// </summary>
        public const float HubOverlap = 8f / FlashXmlScale.AtlasToFlashPointScale;

        /// <summary>
        /// How far the hour hand's centerline runs to its art's clockwise side of the hub at rest,
        /// in animation stage units: 1.7 clock-sheet pixels to the counterclockwise side.
        /// </summary>
        public const float HourHandCenterlineOffset = -1.7f / FlashXmlScale.AtlasToFlashPointScale;

        /// <summary>
        /// How far the minute hand's centerline runs to its art's clockwise side of the hub at rest,
        /// in animation stage units: 3.9 clock-sheet pixels.
        /// </summary>
        public const float MinuteHandCenterlineOffset = 3.9f / FlashXmlScale.AtlasToFlashPointScale;

        /// <summary>
        /// Clock angle shadows fall toward: down and right, from a light at the top left where the
        /// center point has its highlight.
        /// </summary>
        public const float ShadowAngle = 135f;

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
        private ClockHand hourHand;
        private ClockHand minuteHand;
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
            logo.Play(IntroTimeline);
            logo.ShowTime();
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
            // The timelines step a hand's scale from its current value, so they must never see the
            // mirror; it is taken off before they run and put back once they and any new timeline
            // have set the frame's scale.
            hourHand?.Unmirror();
            minuteHand?.Unmirror();
            base.Update(delta);
            if (PlayingTimeline < 0)
            {
                IdleDelay -= delta;
                if (IdleDelay <= 0f)
                {
                    Play(IdleTimeline);
                }
            }
            ShowTime();
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
            Image hub = clockParts.Find(part => part.quadToDraw == CenterPointQuad);
            Vector hubRest = RestPosition(definition, hub);
            hourHand = FindHand(definition, HourHandQuad, HourHandArtAngle, HourHandCenterlineOffset);
            minuteHand = FindHand(definition, MinuteHandQuad, MinuteHandArtAngle, MinuteHandCenterlineOffset);
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

        /// <summary>Finds one of the clock's hands.</summary>
        /// <param name="definition">The parsed clock animation.</param>
        /// <param name="quad">The hand's quad in the clock sheet.</param>
        /// <param name="artAngle">Clock angle its art is drawn at.</param>
        /// <param name="centerlineOffset">How far its centerline runs to the clockwise side of the hub.</param>
        /// <returns>The hand, or <see langword="null"/> when the animation lacks it.</returns>
        private ClockHand FindHand(FlashXmlAnimationDefinition definition, int quad, float artAngle, float centerlineOffset)
        {
            Image part = clockParts.Find(p => p.quadToDraw == quad);
            return part == null ? null : new ClockHand(part, RestPosition(definition, part), artAngle, centerlineOffset);
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
            hourHand?.PointAt(hours * 30f, hubCenter);
            minuteHand?.PointAt(minutes * 6f, hubCenter);
        }

        /// <summary>Starts one of the clock's timelines.</summary>
        /// <param name="timelineId">Timeline to play.</param>
        private void Play(int timelineId)
        {
            PlayingTimeline = timelineId;
            FlashXmlTargetAnimationBackend.PlayTimeline(clockParts, timelineId);
            FlashXmlTargetAnimationBackend.PlayRootTimeline(clock, timelineId);
        }

        /// <summary>
        /// One clock hand, turned about the hub's center. Its art pivots on a sprite corner a
        /// little off the hub, so a translation, which the element applies before its scale and
        /// rotation, carries the base of its centerline onto the hub for each pose. Its thick edge
        /// is drawn on its art's clockwise side; whenever that side would face the light, the art
        /// is mirrored across the sprite, so the edge always falls on the shadowed side.
        /// </summary>
        private sealed class ClockHand(Image part, Vector rest, float artAngle, float centerlineOffset)
        {
            private const float Degrees = MathF.PI / 180f;

            /// <summary>Takes the mirror off, leaving the scale the timelines last set.</summary>
            public void Unmirror()
            {
                part.scaleX = MathF.Abs(part.scaleX);
            }

            /// <summary>Points the hand at a clock angle.</summary>
            /// <param name="clockAngle">Angle to point at, in degrees clockwise from twelve.</param>
            /// <param name="hub">The hub's center, in animation stage units.</param>
            public void PointAt(float clockAngle, Vector hub)
            {
                bool mirrored = MathF.Cos((clockAngle + 90f - ShadowAngle) * Degrees) < 0f;
                float sign = mirrored ? -1f : 1f;
                float turn = mirrored ? clockAngle + artAngle : clockAngle - artAngle;
                part.rotation = turn;
                part.scaleX = MathF.Abs(part.scaleX) * sign;

                // The art point that must land on the hub: on the centerline, HubOverlap out from
                // where it passes the hub, so the center point covers the hand's base.
                float art = artAngle * Degrees;
                float baseX = hub.X + (MathF.Sin(art) * HubOverlap) + (MathF.Cos(art) * centerlineOffset);
                float baseY = hub.Y - (MathF.Cos(art) * HubOverlap) + (MathF.Sin(art) * centerlineOffset);

                // Drawn, an art point v lands at pivot + R * S * (v + translate - pivot); solving
                // for base landing on the hub gives translate = S^-1 * R^-1 * (hub - pivot) + pivot - base.
                float pivotX = rest.X + (part.width >> 1) + part.rotationCenterX;
                float pivotY = rest.Y + (part.height >> 1) + part.rotationCenterY;
                float toHubX = hub.X - pivotX;
                float toHubY = hub.Y - pivotY;
                float cos = MathF.Cos(turn * Degrees);
                float sin = MathF.Sin(turn * Degrees);
                part.translateX = (((toHubX * cos) + (toHubY * sin)) * sign) + pivotX - baseX;
                part.translateY = (toHubY * cos) - (toHubX * sin) + pivotY - baseY;
            }
        }
    }
}
