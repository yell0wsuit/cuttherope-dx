using System.Numerics;

using Xunit;
namespace CutTheRopeDX.Desktop.Platform.Tests
{
    public sealed class WindowCoordinateTests
    {
        [Fact]
        public void ConvertsWindowPointsBeforeSubtractingPixelMargins()
        {
            Assert.Equal(new Vector2(180, 80), SdlWindowService.MapWindowToView(100, 50, 800, 600, 1600, 1200, 20, 20));
            Assert.Equal(new Vector2(80, 30), SdlWindowService.MapWindowToView(100, 50, 800, 600, 800, 600, 20, 20));
        }
        [Fact]
        public void CursorBaseSizeIsHalfTheArtWhateverTheWindow()
        {
            Assert.Equal((31, 36), SdlCursorService.GetBaseSize(62, 71));
            Assert.Equal((28, 31), SdlCursorService.GetBaseSize(55, 62));
            Assert.Equal((1, 1), SdlCursorService.GetBaseSize(1, 1));
        }

        [Theory]
        // A size that fits is kept as it is.
        [InlineData(1280, 1920, 320, 1280)]
        // Below the playable minimum is raised to it, however the preference file got that way.
        [InlineData(100, 1920, 320, 320)]
        [InlineData(1, 1920, 480, 480)]
        // Above the cap is brought back to it.
        [InlineData(9000, 20000, 320, 4096)]
        // A saved size from a larger machine is brought within this display.
        [InlineData(3000, 1920, 320, 1920)]
        // Zero asks for a default, which is the display less a margin.
        [InlineData(0, 1920, 320, 1820)]
        // A display smaller than the minimum wins: asking for a window that cannot be shown is
        // worse than asking for one that is too small to play in comfortably.
        [InlineData(0, 200, 320, 200)]
        [InlineData(1280, 200, 320, 200)]
        public void AWindowSizeIsFittedToTheDisplayItWillOpenOn(
            int requested, int usable, int minimum, int expected)
        {
            Assert.Equal(expected, SdlWindowService.ClampWindowSide(requested, usable, minimum, 0));
        }

        [Theory]
        // A display-sized request leaves room for the title bar, so the frame stays on screen
        // and can still be grabbed.
        [InlineData(1080, 1032, 480, 39, 993)]
        [InlineData(1920, 1920, 320, 16, 1904)]
        // A size that already fits with its frame is kept.
        [InlineData(900, 1032, 480, 39, 900)]
        // The default margin is taken from the display, and the frame bound still applies after it.
        [InlineData(0, 1032, 480, 39, 932)]
        [InlineData(0, 1032, 480, 150, 882)]
        // The display still wins over the minimum once the frame is taken from it.
        [InlineData(1280, 520, 480, 39, 481)]
        // A frame as large as the display leaves the smallest size SDL will accept.
        [InlineData(1280, 30, 480, 39, 1)]
        public void TheWindowFrameIsTakenFromTheDisplayBeforeFitting(
            int requested, int usable, int minimum, int decoration, int expected)
        {
            Assert.Equal(expected, SdlWindowService.ClampWindowSide(requested, usable, minimum, decoration));
        }

        [Fact]
        public void EachAxisIsFittedWithoutReferenceToTheOther()
        {
            // The minimums differ per axis, so a shared clamp would let a window through that is
            // tall enough and too narrow, or the reverse.
            Assert.Equal(
                SdlWindowService.MinimumWidth,
                SdlWindowService.ClampWindowSide(10, 1920, SdlWindowService.MinimumWidth, 0));
            Assert.Equal(
                SdlWindowService.MinimumHeight,
                SdlWindowService.ClampWindowSide(10, 1080, SdlWindowService.MinimumHeight, 0));
        }
    }
}
