using CutTheRopeDX.Framework;
using CutTheRopeDX.Framework.Visual;

using Xunit;

namespace CutTheRopeDX.Tests
{
    /// <summary>Covers the draw-quad hit test against what the image actually paints.</summary>
    public sealed class ImagePointInDrawQuadTests
    {
        private static Image ImageWithQuad(float w, float h, float offsetX, float offsetY)
        {
            Texture2D texture = new()
            {
                _realWidth = 64,
                _realHeight = 32,
                quadRects = [new Rectangle(0f, 0f, w, h)],
                quadOffsets = [Framework.Helpers.MathHelper.Vect(offsetX, offsetY)],
            };

            Image image = new();
            _ = image.InitWithTexture(texture);
            image.x = 100f;
            image.y = 200f;
            return image;
        }

        [Fact]
        public void UsesTheWholeTextureWhenNoQuadIsSelected()
        {
            Image image = ImageWithQuad(10f, 10f, 0f, 0f);
            image.quadToDraw = -1;

            Assert.True(image.PointInDrawQuad(100f, 200f));
            Assert.True(image.PointInDrawQuad(163f, 231f));
            Assert.False(image.PointInDrawQuad(165f, 231f));
            Assert.False(image.PointInDrawQuad(99f, 200f));
        }

        [Fact]
        public void UsesTheSelectedQuadRect()
        {
            Image image = ImageWithQuad(20f, 8f, 0f, 0f);
            image.quadToDraw = 0;
            image.restoreCutTransparency = false;

            Assert.True(image.PointInDrawQuad(110f, 204f));
            Assert.False(image.PointInDrawQuad(121f, 204f));
            Assert.False(image.PointInDrawQuad(110f, 209f));
        }

        [Fact]
        public void ShiftsByTheTrimOffsetWhenRestoringCutTransparency()
        {
            Image image = ImageWithQuad(20f, 8f, 5f, 3f);
            image.quadToDraw = 0;
            image.restoreCutTransparency = true;

            // The quad now paints at 105,203 through 125,211.
            Assert.False(image.PointInDrawQuad(104f, 204f));
            Assert.True(image.PointInDrawQuad(106f, 204f));
            Assert.True(image.PointInDrawQuad(124f, 210f));
            Assert.False(image.PointInDrawQuad(126f, 210f));
        }

        [Fact]
        public void ReturnsFalseWithoutATexture()
        {
            Image image = new();

            Assert.False(image.PointInDrawQuad(0f, 0f));
        }
    }
}
