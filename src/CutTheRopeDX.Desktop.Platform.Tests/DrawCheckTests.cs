using CutTheRopeDX.Desktop.Platform.Graphics;

using SkiaSharp;

using Xunit;

namespace CutTheRopeDX.Desktop.Platform.Tests
{
    /// <summary>What a device has to put on its render target to be accepted.</summary>
    public sealed class DrawCheckTests
    {
        private const int Width = 64;
        private const int Height = 48;

        private static SKSurface Raster()
        {
            return SKSurface.Create(new SKImageInfo(Width, Height, SKColorType.Rgba8888, SKAlphaType.Premul));
        }

        private static SKColor SampleOf(SKSurface surface)
        {
            using SKImage image = surface.Snapshot();
            using SKBitmap bitmap = SKBitmap.FromImage(image);
            SKPointI at = DrawCheck.Sample(Width, Height);
            return bitmap.GetPixel(at.X, at.Y);
        }

        [Fact]
        public void AShadedDrawLeavesSomethingOtherThanTheBackground()
        {
            using SKSurface surface = Raster();

            DrawCheck.Draw(surface.Canvas, Width, Height);

            SKColor sample = SampleOf(surface);
            Assert.NotEqual(DrawCheck.Background, sample);
            Assert.True(DrawCheck.Drew(sample));
        }

        [Fact]
        public void AClearAloneIsNotAccepted()
        {
            using SKSurface surface = Raster();

            surface.Canvas.Clear(DrawCheck.Background);

            SKColor sample = SampleOf(surface);
            Assert.Equal(DrawCheck.Background, sample);
            Assert.False(DrawCheck.Drew(sample));
        }

        [Fact]
        public void AReadbackThatWasNeverWrittenIsNotAccepted()
        {
            // ReadPixels hands back a zeroed bitmap and only reports failure through its return
            // value, so a driver that claims a successful readback without touching the buffer
            // arrives here as transparent black. The gradient is drawn over an opaque clear and
            // is opaque everywhere, so nothing it can produce is mistaken for this.
            Assert.False(DrawCheck.Drew(new SKColor(0, 0, 0, 0)));
        }

        [Fact]
        public void TheSampleSitsInsideTheTarget()
        {
            SKPointI at = DrawCheck.Sample(Width, Height);

            Assert.InRange(at.X, 0, Width - 1);
            Assert.InRange(at.Y, 0, Height - 1);
        }

        [Fact]
        public void TheDrawCoversTheWholeTargetSoNoCornerStaysBehind()
        {
            using SKSurface surface = Raster();

            DrawCheck.Draw(surface.Canvas, Width, Height);

            using SKImage image = surface.Snapshot();
            using SKBitmap bitmap = SKBitmap.FromImage(image);
            Assert.NotEqual(DrawCheck.Background, bitmap.GetPixel(0, 0));
            Assert.NotEqual(DrawCheck.Background, bitmap.GetPixel(Width - 1, Height - 1));
        }
    }
}
