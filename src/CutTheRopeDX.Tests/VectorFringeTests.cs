using System.Collections.Generic;
using System.Numerics;

using CutTheRopeDX.Framework;
using CutTheRopeDX.Framework.Core;
using CutTheRopeDX.Framework.Visual;

using Xunit;

namespace CutTheRopeDX.Tests
{
    /// <summary>
    /// Covers the antialiasing band: it must lie on the empty side of every boundary, fade to
    /// transparent outward, and stay premultiplied.
    /// </summary>
    public sealed class VectorFringeTests
    {
        private static readonly RGBAColor Fill = RGBAColor.MakeRGBA(1f, 1f, 1f, 1f);

        private static List<Vector2> Square(float x, float y, float size, bool clockwise)
        {
            List<Vector2> contour =
            [
                new(x, y),
                new(x + size, y),
                new(x + size, y + size),
                new(x, y + size),
            ];
            if (clockwise)
            {
                contour.Reverse();
            }
            return contour;
        }

        private static VectorFringe FringeFor(IReadOnlyList<List<Vector2>> contours, float width)
        {
            VectorFringe fringe = new();
            fringe.Rebuild(contours, Fill, width);
            return fringe;
        }

        [Fact]
        public void ExpandsOutwardFromAnOuterBoundary()
        {
            VectorFringe fringe = FringeFor([Square(0f, 0f, 100f, false)], 2f);

            float minX = float.MaxValue;
            float maxX = float.MinValue;
            for (int i = 0; i < fringe.IndexCount; i++)
            {
                Vector3 position = fringe.Vertices[fringe.Indices[i]].Position;
                minX = System.MathF.Min(minX, position.X);
                maxX = System.MathF.Max(maxX, position.X);
            }

            Assert.Equal(-2f, minX, 1);
            Assert.Equal(102f, maxX, 1);
        }

        [Fact]
        public void ExpandsIntoTheEmptySpaceOfAHole()
        {
            // The hole spans 30..70. The band along its left edge must land at x=32, inside the
            // hole, never at x=28, inside the filled body.
            VectorFringe fringe = FringeFor(
                [Square(0f, 0f, 100f, false), Square(30f, 30f, 40f, true)], 2f);

            bool sawInsideHole = false;
            for (int i = 0; i < fringe.IndexCount; i++)
            {
                Vector3 position = fringe.Vertices[fringe.Indices[i]].Position;
                if (position.Y < 29.5f || position.Y > 70.5f)
                {
                    continue;
                }
                Assert.False(
                    System.MathF.Abs(position.X - 28f) < 0.01f,
                    "the hole's fringe grew into the filled body");
                if (System.MathF.Abs(position.X - 32f) < 0.01f)
                {
                    sawInsideHole = true;
                }
            }

            Assert.True(sawInsideHole, "the hole's fringe did not reach into the hole");
        }

        [Fact]
        public void FadesToPremultipliedTransparentOnTheOuterEdge()
        {
            VectorFringe fringe = FringeFor([Square(0f, 0f, 100f, false)], 2f);

            bool sawOpaque = false;
            bool sawTransparent = false;
            foreach (VertexPositionColor vertex in fringe.Vertices)
            {
                if (vertex.Color.A == 255)
                {
                    sawOpaque = true;
                }
                if (vertex.Color.A == 0)
                {
                    sawTransparent = true;
                    // Premultiplied: zero alpha means zero color, not the fill color at zero alpha.
                    Assert.Equal(0, vertex.Color.R);
                    Assert.Equal(0, vertex.Color.G);
                    Assert.Equal(0, vertex.Color.B);
                }
            }

            Assert.True(sawOpaque);
            Assert.True(sawTransparent);
        }

        [Fact]
        public void WidthScalesTheBandWithoutChangingTriangleCount()
        {
            VectorFringe narrow = FringeFor([Square(0f, 0f, 100f, false)], 1f);
            int narrowIndices = narrow.IndexCount;

            VectorFringe wide = FringeFor([Square(0f, 0f, 100f, false)], 4f);

            Assert.Equal(narrowIndices, wide.IndexCount);

            float widest = float.MinValue;
            for (int i = 0; i < wide.IndexCount; i++)
            {
                widest = System.MathF.Max(widest, wide.Vertices[wide.Indices[i]].Position.X);
            }

            Assert.Equal(104f, widest, 1);
        }

        [Fact]
        public void RebuildingReplacesThePreviousBand()
        {
            List<List<Vector2>> contours = [Square(0f, 0f, 100f, false)];
            VectorFringe fringe = new();

            fringe.Rebuild(contours, Fill, 1f);
            int first = fringe.IndexCount;
            fringe.Rebuild(contours, Fill, 1f);

            Assert.Equal(first, fringe.IndexCount);
        }

        [Fact]
        public void SkipsAContourThatDoesNotBoundTheFilledRegion()
        {
            // Same winding, so nonzero counts to two inside the inner square and it is not a
            // boundary at all. Only the outer square should produce a band.
            VectorFringe nested = FringeFor(
                [Square(0f, 0f, 100f, false), Square(30f, 30f, 40f, false)], 2f);
            VectorFringe alone = FringeFor([Square(0f, 0f, 100f, false)], 2f);

            Assert.Equal(alone.IndexCount, nested.IndexCount);
        }
    }
}
