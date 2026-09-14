using System.Collections.Generic;
using System.Numerics;

using CutTheRopeDX.Framework.Visual;
using CutTheRopeDX.GameMain;

using Xunit;

namespace CutTheRopeDX.Tests
{
    /// <summary>
    /// Guards the property the fill sweep relies on: within any horizontal band between two
    /// vertex heights, no edge crosses another, so every span is exactly a trapezoid.
    /// </summary>
    public sealed class VectorArtArrangementTests
    {
        private static bool SegmentsCross(Vector2 p, Vector2 q, Vector2 r, Vector2 s)
        {
            float denominator = ((q.X - p.X) * (s.Y - r.Y)) - ((q.Y - p.Y) * (s.X - r.X));
            if (System.MathF.Abs(denominator) < 1e-12f)
            {
                return false;
            }
            float t = (((r.X - p.X) * (s.Y - r.Y)) - ((r.Y - p.Y) * (s.X - r.X))) / denominator;
            float u = (((r.X - p.X) * (q.Y - p.Y)) - ((r.Y - p.Y) * (q.X - p.X))) / denominator;
            return t > 1e-6f && t < 1f - 1e-6f && u > 1e-6f && u < 1f - 1e-6f;
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        [InlineData(4)]
        [InlineData(5)]
        public void NoEdgesCrossWithinALayer(int layer)
        {
            List<List<Vector2>> contours =
                VectorPath.Flatten(EasterEggOmNomArt.Layers[layer].Commands, 0.1f);

            List<(Vector2 Start, Vector2 End, int Contour)> edges = [];
            for (int c = 0; c < contours.Count; c++)
            {
                List<Vector2> contour = contours[c];
                for (int i = 0; i < contour.Count; i++)
                {
                    edges.Add((contour[i], contour[(i + 1) % contour.Count], c));
                }
            }

            for (int i = 0; i < edges.Count; i++)
            {
                for (int j = i + 1; j < edges.Count; j++)
                {
                    if (edges[i].Contour == edges[j].Contour
                        && (j == i + 1 || (i == 0 && j == edges.Count - 1)))
                    {
                        // Neighbors on the same contour share an endpoint by construction.
                        continue;
                    }
                    Assert.False(
                        SegmentsCross(
                            edges[i].Start, edges[i].End, edges[j].Start, edges[j].End),
                        $"layer {layer} edges {i} and {j} cross");
                }
            }
        }
    }
}
