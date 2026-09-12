using System;

using CutTheRopeDX.Framework.Platform;

using SkiaSharp;

using Xunit;

namespace CutTheRopeDX.Rendering.Skia.Tests
{
    /// <summary>
    /// Covers the texture a decoded movie frame is written into. Video is the one path that
    /// rewrites a whole texture every frame, so the pixels are read back rather than assumed.
    /// </summary>
    public sealed class VideoFrameTextureTests
    {
        private const int Width = 4;
        private const int Height = 3;

        /// <summary>Builds a tightly packed opaque RGBA frame whose red channel counts pixels.</summary>
        private static byte[] Frame(byte green)
        {
            byte[] pixels = new byte[Width * Height * 4];
            for (int index = 0; index < Width * Height; index++)
            {
                pixels[(index * 4) + 0] = (byte)index;
                pixels[(index * 4) + 1] = green;
                pixels[(index * 4) + 2] = 0;
                pixels[(index * 4) + 3] = 255;
            }

            return pixels;
        }

        [Fact]
        public void AFrameTextureReportsTheRequestedSize()
        {
            using SkiaVideoFrameTexture texture = new(Width, Height);

            _ = Assert.IsAssignableFrom<IVideoFrameTexture>(texture);
            Assert.Equal(Width, texture.Width);
            Assert.Equal(Height, texture.Height);
        }

        [Fact]
        public void UpdatingAFrameStoresItsPixelsInOrder()
        {
            using SkiaVideoFrameTexture texture = new(Width, Height);

            texture.Update(Frame(green: 200));

            SKBitmap bitmap = texture.Bitmap;
            Assert.Equal(SKColorType.Rgba8888, bitmap.ColorType);
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    SKColor color = bitmap.GetPixel(x, y);
                    Assert.Equal((byte)((y * Width) + x), color.Red);
                    Assert.Equal(200, color.Green);
                    Assert.Equal(255, color.Alpha);
                }
            }
        }

        [Fact]
        public void AlaterFrameReplacesTheEarlierOne()
        {
            using SkiaVideoFrameTexture texture = new(Width, Height);
            texture.Update(Frame(green: 10));

            texture.Update(Frame(green: 240));

            Assert.Equal(240, texture.Bitmap.GetPixel(1, 1).Green);
        }

        [Fact]
        public void AShortFrameIsRejectedRatherThanPartlyApplied()
        {
            using SkiaVideoFrameTexture texture = new(Width, Height);
            texture.Update(Frame(green: 10));

            _ = Assert.Throws<ArgumentException>(() => texture.Update(new byte[8]));

            Assert.Equal(10, texture.Bitmap.GetPixel(0, 0).Green);
        }

        [Fact]
        public void AFrameTextureToleratesBeingDisposedTwice()
        {
            SkiaVideoFrameTexture texture = new(Width, Height);

            texture.Dispose();
            texture.Dispose();
        }

        [Fact]
        public void UpdatingAfterDisposalIsRefusedRatherThanWritingFreedMemory()
        {
            SkiaVideoFrameTexture texture = new(Width, Height);
            texture.Dispose();

            _ = Assert.Throws<ObjectDisposedException>(() => texture.Update(Frame(green: 1)));
        }
    }
}
