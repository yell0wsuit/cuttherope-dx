using System.Collections.Generic;
using System.Numerics;

using CutTheRopeDX.Framework;
using CutTheRopeDX.Framework.Core;
using CutTheRopeDX.Framework.Visual;

using Xunit;

namespace CutTheRopeDX.Tests
{
    /// <summary>
    /// Covers winding resolution and mesh construction. Area is the assertion that matters: a
    /// wrong winding rule fills holes solid, which no vertex count would reveal.
    /// </summary>
    public sealed class VectorPathMeshTests
    {
        private static readonly RGBAColor Red = RGBAColor.MakeRGBA(1f, 0f, 0f, 1f);

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

        private static List<Vector2> Triangle(float x, float y, float size)
        {
            return [new(x, y), new(x + size, y), new(x, y + size)];
        }

        private static float MeshArea(VectorPathMesh mesh)
        {
            float area = 0f;
            for (int i = 0; i + 2 < mesh.IndexCount; i += 3)
            {
                Vector3 a = mesh.Vertices[mesh.Indices[i]].Position;
                Vector3 b = mesh.Vertices[mesh.Indices[i + 1]].Position;
                Vector3 c = mesh.Vertices[mesh.Indices[i + 2]].Position;
                area += System.MathF.Abs(
                    ((b.X - a.X) * (c.Y - a.Y)) - ((c.X - a.X) * (b.Y - a.Y))) * 0.5f;
            }
            return area;
        }

        [Fact]
        public void FillsASimpleSquare()
        {
            VectorPathMesh mesh = VectorPathMesh.Build([Square(0f, 0f, 100f, false)], Red);

            Assert.Equal(10000f, MeshArea(mesh), 1f);
        }

        [Fact]
        public void FillsASlopedShapeExactly()
        {
            // A triangle exercises the trapezoid interpolation; a square alone would pass even
            // if the sweep used a single X per band.
            VectorPathMesh mesh = VectorPathMesh.Build([Triangle(0f, 0f, 100f)], Red);

            Assert.Equal(5000f, MeshArea(mesh), 1f);
        }

        [Fact]
        public void LeavesAnOppositelyWoundHoleEmpty()
        {
            VectorPathMesh mesh = VectorPathMesh.Build(
                [Square(0f, 0f, 100f, false), Square(30f, 30f, 40f, true)], Red);

            Assert.Equal(8400f, MeshArea(mesh), 1f);
        }

        [Fact]
        public void FillsANestedSameWoundContourSolid()
        {
            // Nonzero counts to two inside the inner square, so it stays filled. Even-odd would
            // punch a hole here, which is the distinction this pins.
            VectorPathMesh mesh = VectorPathMesh.Build(
                [Square(0f, 0f, 100f, false), Square(30f, 30f, 40f, false)], Red);

            Assert.Equal(10000f, MeshArea(mesh), 1f);
        }

        [Fact]
        public void FillsTwoDisjointShapes()
        {
            VectorPathMesh mesh = VectorPathMesh.Build(
                [Square(0f, 0f, 10f, false), Square(50f, 50f, 20f, false)], Red);

            Assert.Equal(500f, MeshArea(mesh), 1f);
        }

        [Fact]
        public void IndexCountIsAMultipleOfThree()
        {
            VectorPathMesh mesh = VectorPathMesh.Build(
                [Square(0f, 0f, 100f, false), Square(30f, 30f, 40f, true)], Red);

            Assert.Equal(0, mesh.IndexCount % 3);
            Assert.True(mesh.IndexCount > 0);
        }

        [Fact]
        public void CarriesTheFillColorOnEveryVertex()
        {
            RGBAColor fill = RGBAColor.MakeRGBA(100f / 255f, 150f / 255f, 40f / 255f, 1f);
            VectorPathMesh mesh = VectorPathMesh.Build([Square(0f, 0f, 10f, false)], fill);

            Color expected = fill.ToColor();
            foreach (VertexPositionColor vertex in mesh.Vertices)
            {
                Assert.Equal(expected.R, vertex.Color.R);
                Assert.Equal(expected.G, vertex.Color.G);
                Assert.Equal(expected.B, vertex.Color.B);
                Assert.Equal(expected.A, vertex.Color.A);
            }
        }

        [Fact]
        public void ProducesNothingForDegenerateInput()
        {
            VectorPathMesh mesh = VectorPathMesh.Build([], Red);

            Assert.Equal(0, mesh.IndexCount);
        }
    }
}
