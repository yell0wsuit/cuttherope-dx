using SDL3;

using Xunit;
namespace CutTheRopeDX.Desktop.Platform.Tests
{
    public sealed class WindowMaximizedStateTests
    {
        private const int WindowedWidth = 1000;
        private const int WindowedHeight = 700;

        private static (bool Saved, bool Observed) Next(bool saved, bool observed, SDL.WindowFlags flags, int width, int height)
        {
            return SdlWindowService.NextMaximizedState(saved, observed, flags, width, height, WindowedWidth, WindowedHeight);
        }

        [Fact]
        public void MaximizingTheWindowIsRemembered()
        {
            Assert.Equal((true, true), Next(false, false, SDL.WindowFlags.Maximized, 1470, 820));
        }

        [Fact]
        public void RestoringTheWindowIsRemembered()
        {
            Assert.Equal((false, false), Next(true, true, 0, WindowedWidth, WindowedHeight));
        }

        [Fact]
        public void AScreenFillingWindowBackFromFullscreenIsNotTakenAsMaximized()
        {
            // macOS reports a window that fills the screen as maximized, whether or not the player
            // maximized it; back from fullscreen it is still at its windowed size.
            Assert.Equal((false, true), Next(false, false, SDL.WindowFlags.Maximized, WindowedWidth, WindowedHeight));
        }

        [Fact]
        public void AnUnchangedReadingKeepsTheSavedState()
        {
            // The window starts hidden and unmaximized, before the saved state is applied to it.
            Assert.Equal((true, false), Next(true, false, 0, WindowedWidth, WindowedHeight));
            Assert.Equal((false, true), Next(false, true, SDL.WindowFlags.Maximized, 1470, 820));
        }

        [Theory]
        [InlineData(SDL.WindowFlags.Fullscreen)]
        [InlineData(SDL.WindowFlags.Fullscreen | SDL.WindowFlags.Maximized)]
        [InlineData(SDL.WindowFlags.Minimized)]
        [InlineData(SDL.WindowFlags.Minimized | SDL.WindowFlags.Maximized)]
        public void FullscreenAndMinimizedReadingsAreIgnored(SDL.WindowFlags flags)
        {
            Assert.Equal((true, true), Next(true, true, flags, 1470, 956));
            Assert.Equal((false, false), Next(false, false, flags, 1470, 956));
        }
    }
}
