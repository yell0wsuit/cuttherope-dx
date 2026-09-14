using System.Collections.Generic;
using System.Numerics;

using CutTheRopeDX.Framework.Visual;

using Xunit;

namespace CutTheRopeDX.Tests
{
    /// <summary>Covers subpath splitting and curve flattening accuracy.</summary>
    public sealed class VectorPathTests
    {
        private static VectorPathCommand Move(float x, float y)
        {
            return new VectorPathCommand(VectorPathVerb.MoveTo, x, y, 0f, 0f, 0f, 0f);
        }

        private static VectorPathCommand Line(float x, float y)
        {
            return new VectorPathCommand(VectorPathVerb.LineTo, x, y, 0f, 0f, 0f, 0f);
        }

        private static VectorPathCommand Cubic(
            float x0, float y0, float x1, float y1, float x2, float y2)
        {
            return new VectorPathCommand(VectorPathVerb.CubicTo, x0, y0, x1, y1, x2, y2);
        }

        private static VectorPathCommand Close()
        {
            return new VectorPathCommand(VectorPathVerb.Close, 0f, 0f, 0f, 0f, 0f, 0f);
        }

        [Fact]
        public void SplitsOnEachMoveTo()
        {
            List<VectorPathCommand> commands =
            [
                Move(0f, 0f), Line(10f, 0f), Line(10f, 10f), Close(),
                Move(20f, 20f), Line(30f, 20f), Line(30f, 30f), Close(),
            ];

            List<List<Vector2>> contours = VectorPath.Flatten(commands, 0.1f);

            Assert.Equal(2, contours.Count);
            Assert.Equal(3, contours[0].Count);
            Assert.Equal(3, contours[1].Count);
        }

        [Fact]
        public void DoesNotRepeatTheClosingPoint()
        {
            List<VectorPathCommand> commands =
            [
                Move(0f, 0f), Line(10f, 0f), Line(10f, 10f), Line(0f, 0f), Close(),
            ];

            List<List<Vector2>> contours = VectorPath.Flatten(commands, 0.1f);

            Assert.Equal(3, contours[0].Count);
        }

        [Fact]
        public void FlattensAStraightCubicToItsEndpointAlone()
        {
            // A contour needs three points to enclose anything, so the straight cubic is closed
            // off by a line; the cubic itself must contribute only its endpoint.
            List<VectorPathCommand> commands =
            [
                Move(0f, 0f), Cubic(10f, 0f, 20f, 0f, 30f, 0f), Line(30f, 10f), Close(),
            ];

            List<List<Vector2>> contours = VectorPath.Flatten(commands, 0.1f);

            Assert.Equal(3, contours[0].Count);
        }

        [Fact]
        public void FlattenedCurveStaysWithinToleranceOfTheTrueCurve()
        {
            // A quarter-circle-ish cubic. Every flattened vertex must sit on the real curve,
            // and the chords between them must not sag further than the tolerance.
            List<VectorPathCommand> commands =
            [
                Move(0f, 0f), Cubic(0f, 55.2f, 44.8f, 100f, 100f, 100f), Close(),
            ];

            List<List<Vector2>> contours = VectorPath.Flatten(commands, 0.1f);
            List<Vector2> points = contours[0];

            float worst = 0f;
            for (int i = 0; i + 1 < points.Count; i++)
            {
                Vector2 midpoint = (points[i] + points[i + 1]) * 0.5f;
                float nearest = float.MaxValue;
                for (int step = 0; step <= 2000; step++)
                {
                    float t = step / 2000f;
                    float mt = 1f - t;
                    Vector2 onCurve = new(
                        (3f * mt * mt * t * 0f) + (3f * mt * t * t * 44.8f) + (t * t * t * 100f),
                        (3f * mt * mt * t * 55.2f) + (3f * mt * t * t * 100f) + (t * t * t * 100f));
                    nearest = System.MathF.Min(nearest, Vector2.Distance(midpoint, onCurve));
                }
                worst = System.MathF.Max(worst, nearest);
            }

            Assert.True(worst <= 0.1f, $"chord sag {worst} exceeded the 0.1 tolerance");
        }

        [Fact]
        public void TighterToleranceProducesMorePoints()
        {
            List<VectorPathCommand> commands =
            [
                Move(0f, 0f), Cubic(0f, 55.2f, 44.8f, 100f, 100f, 100f), Close(),
            ];

            int coarse = VectorPath.Flatten(commands, 1.0f)[0].Count;
            int fine = VectorPath.Flatten(commands, 0.01f)[0].Count;

            Assert.True(fine > coarse, $"expected more points at a tighter tolerance, got {fine} vs {coarse}");
        }
    }
}
