using System;

using CutTheRopeDX.Framework;

namespace CutTheRopeDX.GameMain
{
    /// <summary>
    /// What the Time Travel pack pages draw at a scroll position (iOS
    /// <c>LevelSelectController::update</c>). Pack icons do not slide with the pages: each turns
    /// and fades in place as its page scrolls past, the backgrounds cross-fade, and the rays take
    /// both pages' tints and one extra turn per page.
    /// </summary>
    internal static class TimeTravelPager
    {
        /// <summary>
        /// Length of an icon's turn, in seconds of its iOS timeline: half of it brings the icon in
        /// from one page away, the other half takes it out to the next.
        /// </summary>
        public const float IconTurnLength = 1.2f;

        /// <summary>Pages of scroll over which an icon's whole half turn plays.</summary>
        private const float PagesPerHalfTurn = 1f;

        private static readonly float[] TurnTimes = [0f, 0.2f, 0.4f, 0.6f, 0.8f, 1f, 1.2f];

        private static readonly float[] TurnAngles = [0f, -180f, -90f, 0f, 90f, 180f, 180f];

        private static readonly float[] FadeTimes = [0f, 0.2f, 0.6f, 1f, 1.2f];

        private static readonly float[] FadeAlphas = [0f, 0f, 1f, 0f, 0f];

        /// <summary>Smallest share of a locked icon drawn opaque (iOS 0.15).</summary>
        public const float LockedShareMin = 0.15f;

        /// <summary>Largest share of a locked icon drawn opaque (iOS 0.85).</summary>
        public const float LockedShareMax = 0.85f;

        /// <summary>
        /// Where the pages are scrolled to, in pages from the first.
        /// </summary>
        /// <param name="scrollX">The page container's horizontal scroll.</param>
        /// <param name="pageWidth">Width of a page.</param>
        /// <param name="pageCount">Number of pages.</param>
        /// <returns>The position, held within the pages.</returns>
        public static float Position(float scrollX, float pageWidth, int pageCount)
        {
            return pageWidth <= 0f || pageCount <= 1 ? 0f : Math.Clamp(scrollX / pageWidth, 0f, pageCount - 1);
        }

        /// <summary>How an icon is drawn some way from its own page.</summary>
        /// <param name="distance">Scroll position minus the icon's page, in pages.</param>
        /// <returns>Its turn in degrees, clockwise, and its opacity.</returns>
        public static (float Rotation, float Alpha) IconPose(float distance)
        {
            float time = Math.Clamp((IconTurnLength / 2f) + (distance / PagesPerHalfTurn * IconTurnLength / 2f), 0f, IconTurnLength);
            return (Sample(TurnTimes, TurnAngles, time), Sample(FadeTimes, FadeAlphas, time));
        }

        /// <summary>The rays' extra turn partway between two pages, eased in and out.</summary>
        /// <param name="fraction">How far from one page to the next, 0 to 1.</param>
        /// <returns>The turn in degrees.</returns>
        public static float RaysTurn(float fraction)
        {
            float t = Math.Clamp(fraction, 0f, 1f);
            float doubled = t + t;
            float eased = doubled < 1f ? 0.5f * doubled * doubled : -0.5f * (((doubled - 2f) * (doubled - 2f)) - 2f);
            return 360f * eased;
        }

        /// <summary>Mixes two tints.</summary>
        /// <param name="from">Tint at 0.</param>
        /// <param name="to">Tint at 1.</param>
        /// <param name="t">How far toward <paramref name="to"/>.</param>
        /// <returns>The mix.</returns>
        public static RGBAColor Blend(RGBAColor from, RGBAColor to, float t)
        {
            float u = 1f - t;
            return RGBAColor.MakeRGBA(
                (from.RedColor * u) + (to.RedColor * t),
                (from.GreenColor * u) + (to.GreenColor * t),
                (from.BlueColor * u) + (to.BlueColor * t),
                (from.AlphaChannel * u) + (to.AlphaChannel * t));
        }

        /// <summary>
        /// How much of a locked icon, from its bottom, is drawn opaque: the stars collected toward
        /// opening it, measured against the shortest icon so every icon fills alike, and centered
        /// on the icon's own height.
        /// </summary>
        /// <param name="stars">Stars collected.</param>
        /// <param name="needed">Stars that open the pack.</param>
        /// <param name="iconHeight">This icon's height.</param>
        /// <param name="shortestIcon">The shortest icon's height.</param>
        /// <returns>The opaque height, in the icon's units.</returns>
        public static float LockedOpaqueHeight(int stars, int needed, float iconHeight, float shortestIcon)
        {
            float share = needed <= 0 ? 1f : (float)stars / needed;
            share = Math.Clamp(share, LockedShareMin, LockedShareMax);
            return (share * shortestIcon) + ((iconHeight - shortestIcon) * 0.5f);
        }

        private static float Sample(float[] times, float[] values, float time)
        {
            for (int i = 1; i < times.Length; i++)
            {
                if (time <= times[i])
                {
                    float span = times[i] - times[i - 1];
                    float t = span <= 0f ? 1f : (time - times[i - 1]) / span;
                    return values[i - 1] + ((values[i] - values[i - 1]) * t);
                }
            }
            return values[^1];
        }
    }
}
