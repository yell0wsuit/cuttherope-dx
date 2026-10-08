using System;
using System.Collections.Generic;

using CutTheRopeDX.Framework;

namespace CutTheRopeDX.GameMain
{
    /// <summary>
    /// Fills the room a screen wider or taller than the iOS one leaves around the Time Travel
    /// loading clocks with more of them. Each extra clock is a whole one from the animation, its
    /// hands included, packed at random into the free space the way the Halloween stickers find
    /// theirs: never on another clock, never inside the iOS screen, and allowed to run a little
    /// off the screen's edges as the animation's own edge clocks do.
    /// </summary>
    internal static class TimeTravelClockFill
    {
        /// <summary>The animation's stage, in iOS logical units.</summary>
        public const float StageWidth = 800f;

        /// <summary>The animation's stage, in iOS logical units.</summary>
        public const float StageHeight = 1280f;

        /// <summary>Smallest a copied clock is drawn, against its own size.</summary>
        public const float MinScale = 0.75f;

        /// <summary>Largest a copied clock is drawn, against its own size.</summary>
        public const float MaxScale = 1.3f;

        /// <summary>Most a copied clock is tilted either way, in degrees.</summary>
        public const float MaxTilt = 20f;

        /// <summary>Room kept between clocks, in iOS logical units.</summary>
        public const float Gap = 8f;

        /// <summary>Share of a clock's radius that may run off the screen's edge.</summary>
        public const float Bleed = 0.35f;

        /// <summary>How many places are tried for clocks; enough to pack the room full.</summary>
        public const int Attempts = 1500;

        /// <summary>
        /// The clocks that may be copied: every whole clock with its hands. The middle clock is the
        /// screen's subject and is not repeated; the edge clocks are cut off and have no hands.
        /// </summary>
        public static readonly FillClock[] Clocks =
        [
            new("clock_02", ["pointer_02_1", "pointer_02_2"]),
            new("clock_03", ["pointer_03_1", "pointer_03_2"]),
            new("clock_04", ["pointer_04_1", "pointer_04_2"]),
            new("clock_05", ["pointer_05_1", "pointer_05_2"]),
            new("clock_06", ["pointer_06_1", "pointer_06_2"]),
            new("clock_07", ["pointer_07_1", "pointer_07_2"]),
            new("clock_08", ["pointer_08_1", "pointer_08_2"]),
            new("clock_10", ["pointer_10_1", "pointer_10_2"]),
        ];

        /// <summary>
        /// The animation's edge clocks, each cut straight along one side where the iOS screen ended.
        /// Each is hidden once the screen shows past that cut, and its room is filled instead.
        /// </summary>
        public static readonly CutClock[] CutClocks =
        [
            new("clock_09", CutSide.Right, 829.4f),
            new("clock_11", CutSide.Top, 8.2f),
            new("clock_12", CutSide.Bottom, 1259.2f),
            new("clock_13", CutSide.Bottom, 1318.8f),
        ];

        /// <summary>The iOS screen within the stage, which the animation already fills.</summary>
        public static Rectangle IosScreen => new(
            (StageWidth - TimeTravelScreen.SceneWidth) / 2f,
            (StageHeight - TimeTravelScreen.SceneHeight) / 2f,
            TimeTravelScreen.SceneWidth,
            TimeTravelScreen.SceneHeight);

        /// <summary>The part of the stage a screen shows, the stage being centered on it.</summary>
        /// <param name="screen">The screen model.</param>
        /// <returns>The shown region, in stage units.</returns>
        public static Rectangle ShownRegion(TimeTravelScreen screen)
        {
            return new Rectangle(
                (StageWidth - screen.FullWidth) / 2f,
                (StageHeight - screen.FullHeight) / 2f,
                screen.FullWidth,
                screen.FullHeight);
        }

        /// <summary>Whether a screen shows past an edge clock's cut.</summary>
        /// <param name="cut">The edge clock.</param>
        /// <param name="shown">The shown region, in stage units.</param>
        /// <returns><see langword="true"/> when the cut would show.</returns>
        public static bool ShowsCut(CutClock cut, Rectangle shown)
        {
            return cut.Side switch
            {
                CutSide.Left => shown.x < cut.Edge,
                CutSide.Right => shown.x + shown.w > cut.Edge,
                CutSide.Top => shown.y < cut.Edge,
                CutSide.Bottom => shown.y + shown.h > cut.Edge,
                _ => false,
            };
        }

        /// <summary>
        /// Packs clocks into the shown region around the ones already there, trying random places
        /// and keeping every one that fits.
        /// </summary>
        /// <param name="shown">The shown region, in stage units.</param>
        /// <param name="occupied">Clocks already drawn, as circles in stage units.</param>
        /// <param name="radii">Each copyable clock's radius at its own size, by its index in <see cref="Clocks"/>.</param>
        /// <param name="random">Source of the places, sizes and tilts.</param>
        /// <returns>The clocks to add.</returns>
        public static List<FillPlacement> Plan(Rectangle shown, IReadOnlyList<Circle> occupied, IReadOnlyList<float> radii, Random random)
        {
            List<FillPlacement> placed = [];
            if (radii.Count == 0)
            {
                return placed;
            }

            Rectangle ios = IosScreen;
            List<Circle> taken = [.. occupied];
            for (int attempt = 0; attempt < Attempts; attempt++)
            {
                int clock = random.Next(radii.Count);
                float scale = MinScale + ((MaxScale - MinScale) * (float)random.NextDouble());
                float radius = radii[clock] * scale;

                // Anywhere its middle can go with no more than the bleed off the screen.
                float inset = radius * (1f - Bleed);
                float x = shown.x + inset + ((float)random.NextDouble() * MathF.Max(0f, shown.w - (2f * inset)));
                float y = shown.y + inset + ((float)random.NextDouble() * MathF.Max(0f, shown.h - (2f * inset)));
                if (!FitsInside(x, y, radius, shown) || Inside(x, y, ios) || Overlaps(x, y, radius, taken))
                {
                    continue;
                }
                float tilt = ((2f * (float)random.NextDouble()) - 1f) * MaxTilt;
                placed.Add(new FillPlacement(clock, x, y, scale, tilt));
                taken.Add(new Circle(x, y, radius));
            }
            return placed;
        }

        private static bool FitsInside(float x, float y, float radius, Rectangle shown)
        {
            float bleed = radius * Bleed;
            return x - radius >= shown.x - bleed
                && x + radius <= shown.x + shown.w + bleed
                && y - radius >= shown.y - bleed
                && y + radius <= shown.y + shown.h + bleed;
        }

        private static bool Inside(float x, float y, Rectangle box)
        {
            return x >= box.x && x <= box.x + box.w && y >= box.y && y <= box.y + box.h;
        }

        private static bool Overlaps(float x, float y, float radius, List<Circle> taken)
        {
            foreach (Circle other in taken)
            {
                float reach = radius + other.Radius + Gap;
                float dx = x - other.X;
                float dy = y - other.Y;
                if ((dx * dx) + (dy * dy) < reach * reach)
                {
                    return true;
                }
            }
            return false;
        }
    }

    /// <summary>A clock that can be copied: its face and its hands, by part name.</summary>
    /// <param name="Face">The face part.</param>
    /// <param name="Hands">The hand parts.</param>
    internal readonly record struct FillClock(string Face, string[] Hands);

    /// <summary>Which side of an edge clock is cut straight.</summary>
    internal enum CutSide
    {
        /// <summary>Cut along its left side.</summary>
        Left,

        /// <summary>Cut along its right side.</summary>
        Right,

        /// <summary>Cut along its top.</summary>
        Top,

        /// <summary>Cut along its bottom.</summary>
        Bottom,
    }

    /// <summary>An edge clock and where it is cut, in stage units.</summary>
    /// <param name="Part">The clock's part.</param>
    /// <param name="Side">Its cut side.</param>
    /// <param name="Edge">Where the cut lies across that side.</param>
    internal readonly record struct CutClock(string Part, CutSide Side, float Edge);

    /// <summary>A circle a clock takes up, in stage units.</summary>
    /// <param name="X">Middle X.</param>
    /// <param name="Y">Middle Y.</param>
    /// <param name="Radius">Radius.</param>
    internal readonly record struct Circle(float X, float Y, float Radius);

    /// <summary>One copied clock to add.</summary>
    /// <param name="Clock">Its index in <see cref="TimeTravelClockFill.Clocks"/>.</param>
    /// <param name="X">Where its middle goes, in stage units.</param>
    /// <param name="Y">Where its middle goes, in stage units.</param>
    /// <param name="Scale">Its size against the clock's own.</param>
    /// <param name="Tilt">Its tilt in degrees, clockwise.</param>
    internal readonly record struct FillPlacement(int Clock, float X, float Y, float Scale, float Tilt);
}
