using System;
using System.Collections.Generic;
using System.Numerics;

namespace CutTheRopeDX.Framework.Visual
{
    /// <summary>The drawing operations a vector path is built from.</summary>
    internal enum VectorPathVerb
    {
        /// <summary>Starts a new subpath at the command's first point.</summary>
        MoveTo,

        /// <summary>Draws a straight segment to the command's first point.</summary>
        LineTo,

        /// <summary>Draws a cubic bezier through two control points to an endpoint.</summary>
        CubicTo,

        /// <summary>Closes the current subpath back to its starting point.</summary>
        Close,
    }

    /// <summary>
    /// One path operation. Which fields carry meaning depends on <paramref name="verb"/>:
    /// <see cref="VectorPathVerb.MoveTo"/> and <see cref="VectorPathVerb.LineTo"/> read only the
    /// first point, <see cref="VectorPathVerb.CubicTo"/> reads all three as control, control and
    /// endpoint, and <see cref="VectorPathVerb.Close"/> reads none.
    /// </summary>
    /// <param name="verb">The operation this command performs.</param>
    /// <param name="x0">First point X.</param>
    /// <param name="y0">First point Y.</param>
    /// <param name="x1">Second point X.</param>
    /// <param name="y1">Second point Y.</param>
    /// <param name="x2">Third point X.</param>
    /// <param name="y2">Third point Y.</param>
    internal readonly struct VectorPathCommand(
        VectorPathVerb verb, float x0, float y0, float x1, float y1, float x2, float y2)
    {
        /// <summary>The operation this command performs.</summary>
        public readonly VectorPathVerb Verb = verb;

        /// <summary>First point X.</summary>
        public readonly float X0 = x0;

        /// <summary>First point Y.</summary>
        public readonly float Y0 = y0;

        /// <summary>Second point X.</summary>
        public readonly float X1 = x1;

        /// <summary>Second point Y.</summary>
        public readonly float Y1 = y1;

        /// <summary>Third point X.</summary>
        public readonly float X2 = x2;

        /// <summary>Third point Y.</summary>
        public readonly float Y2 = y2;
    }

    /// <summary>Turns paths into closed polylines a tessellator can consume.</summary>
    internal static class VectorPath
    {
        /// <summary>Points closer together than this are treated as the same point.</summary>
        private const float WeldDistance = 1e-4f;

        /// <summary>
        /// Flattens every subpath to a closed polyline whose chords stay within
        /// <paramref name="tolerance"/> of the true curve.
        /// </summary>
        /// <param name="commands">The path to flatten.</param>
        /// <param name="tolerance">Maximum chord deviation, in path units.</param>
        /// <returns>One polyline per subpath, without a repeated closing point.</returns>
        public static List<List<Vector2>> Flatten(
            IReadOnlyList<VectorPathCommand> commands, float tolerance)
        {
            List<List<Vector2>> contours = [];
            List<Vector2> current = null;
            Vector2 cursor = Vector2.Zero;

            foreach (VectorPathCommand command in commands)
            {
                switch (command.Verb)
                {
                    case VectorPathVerb.MoveTo:
                        current = [];
                        contours.Add(current);
                        cursor = new Vector2(command.X0, command.Y0);
                        current.Add(cursor);
                        break;

                    case VectorPathVerb.LineTo:
                        if (current == null)
                        {
                            break;
                        }
                        cursor = new Vector2(command.X0, command.Y0);
                        Append(current, cursor);
                        break;

                    case VectorPathVerb.CubicTo:
                        if (current == null)
                        {
                            break;
                        }
                        Vector2 control1 = new(command.X0, command.Y0);
                        Vector2 control2 = new(command.X1, command.Y1);
                        Vector2 end = new(command.X2, command.Y2);
                        Subdivide(current, cursor, control1, control2, end, tolerance, 0);
                        cursor = end;
                        break;

                    case VectorPathVerb.Close:
                        break;
                }
            }

            foreach (List<Vector2> contour in contours)
            {
                // The polyline is implicitly closed, so a final point back on the first is a
                // duplicate edge of zero length.
                while (contour.Count > 1
                    && Vector2.Distance(contour[0], contour[^1]) <= WeldDistance)
                {
                    contour.RemoveAt(contour.Count - 1);
                }
            }

            contours.RemoveAll(contour => contour.Count < 3);
            return contours;
        }

        private static void Append(List<Vector2> contour, Vector2 point)
        {
            if (contour.Count > 0 && Vector2.Distance(contour[^1], point) <= WeldDistance)
            {
                return;
            }
            contour.Add(point);
        }

        private static void Subdivide(
            List<Vector2> contour,
            Vector2 p0,
            Vector2 p1,
            Vector2 p2,
            Vector2 p3,
            float tolerance,
            int depth)
        {
            // Distance of the control points from the chord, the standard flatness estimate. The
            // recursion is capped so a degenerate curve cannot spin.
            if (depth >= 24 || IsFlatEnough(p0, p1, p2, p3, tolerance))
            {
                Append(contour, p3);
                return;
            }

            Vector2 p01 = (p0 + p1) * 0.5f;
            Vector2 p12 = (p1 + p2) * 0.5f;
            Vector2 p23 = (p2 + p3) * 0.5f;
            Vector2 p012 = (p01 + p12) * 0.5f;
            Vector2 p123 = (p12 + p23) * 0.5f;
            Vector2 middle = (p012 + p123) * 0.5f;

            Subdivide(contour, p0, p01, p012, middle, tolerance, depth + 1);
            Subdivide(contour, middle, p123, p23, p3, tolerance, depth + 1);
        }

        private static bool IsFlatEnough(
            Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float tolerance)
        {
            float ux = (3f * p1.X) - (2f * p0.X) - p3.X;
            float uy = (3f * p1.Y) - (2f * p0.Y) - p3.Y;
            float vx = (3f * p2.X) - p0.X - (2f * p3.X);
            float vy = (3f * p2.Y) - p0.Y - (2f * p3.Y);

            float worst = MathF.Max(ux * ux, vx * vx) + MathF.Max(uy * uy, vy * vy);
            return worst <= 16f * tolerance * tolerance;
        }
    }
}
