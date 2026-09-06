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
        public void CursorScaleUsesWindowUnitsAndTopLeftHotspot()
        {
            Assert.Equal((32, 16), SdlCursorService.GetScaledSize(32, 16, 2, 2));
            Assert.Equal((48, 24), SdlCursorService.GetScaledSize(32, 16, 3, 2));
        }
    }
}
