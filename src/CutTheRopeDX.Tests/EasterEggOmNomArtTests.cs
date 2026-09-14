using System.Collections.Generic;
using System.Numerics;

using CutTheRopeDX.Framework.Visual;
using CutTheRopeDX.GameMain;

using Xunit;

namespace CutTheRopeDX.Tests
{
    /// <summary>
    /// Pins the artwork to its measured shape, so an edit to the path data that silently drops
    /// or duplicates geometry fails here rather than on screen.
    /// </summary>
    public sealed class EasterEggOmNomArtTests
    {
        [Fact]
        public void HasSixLayersInDrawOrder()
        {
            Assert.Equal(6, EasterEggOmNomArt.Layers.Length);
            Assert.Equal(100f / 255f, EasterEggOmNomArt.Layers[0].Fill.RedColor, 3);
            Assert.Equal(1f, EasterEggOmNomArt.Layers[3].Fill.RedColor, 3);
            Assert.Equal(0f, EasterEggOmNomArt.Layers[5].Fill.RedColor, 3);
        }

        [Theory]
        [InlineData(0, 7)]
        [InlineData(1, 4)]
        [InlineData(2, 9)]
        [InlineData(3, 5)]
        [InlineData(4, 1)]
        [InlineData(5, 1)]
        public void FlattensToTheMeasuredSubpathCount(int layer, int expectedSubpaths)
        {
            List<List<Vector2>> contours =
                VectorPath.Flatten(EasterEggOmNomArt.Layers[layer].Commands, 0.1f);

            Assert.Equal(expectedSubpaths, contours.Count);
        }

        [Fact]
        public void StaysWithinTheMeasuredExtent()
        {
            float minX = float.MaxValue;
            float minY = float.MaxValue;
            float maxX = float.MinValue;
            float maxY = float.MinValue;

            foreach (EasterEggOmNomArt.Layer layer in EasterEggOmNomArt.Layers)
            {
                foreach (List<Vector2> contour in VectorPath.Flatten(layer.Commands, 0.1f))
                {
                    foreach (Vector2 point in contour)
                    {
                        minX = System.MathF.Min(minX, point.X);
                        minY = System.MathF.Min(minY, point.Y);
                        maxX = System.MathF.Max(maxX, point.X);
                        maxY = System.MathF.Max(maxY, point.Y);
                    }
                }
            }

            Assert.InRange(minX, -1f, 1f);
            // The source's lowest control point sits at -1.7, but the curve through it stays above 0.
            Assert.InRange(minY, -1f, 1f);
            Assert.InRange(maxX, 244f, 247f);
            Assert.InRange(maxY, 243f, 246f);
        }
    }
}
