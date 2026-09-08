using System.Reflection;

using CutTheRopeDX.Desktop;
using CutTheRopeDX.Desktop.Platform.Graphics;

using Xunit;

namespace CutTheRopeDX.Desktop.Tests
{
    /// <summary>
    /// The window title names the running version and the renderer that drew the frame.
    /// </summary>
    public class WindowTitleTests
    {
        [Theory]
        [InlineData(GraphicsBackendKind.Metal, "Metal")]
        [InlineData(GraphicsBackendKind.OpenGL, "OpenGL")]
        [InlineData(GraphicsBackendKind.Vulkan, "Vulkan")]
        [InlineData(GraphicsBackendKind.Angle, "Angle")]
        public void NamesTheRendererThatDrew(GraphicsBackendKind renderer, string expected)
        {
            Assert.Equal(
                $"Cut The Rope: DX v{SdlDesktopHost.Version} | {expected}",
                SdlDesktopHost.TitleFor(renderer));
        }

        /// <summary>
        /// The version is reported as the assembly records it. A development build stamps the
        /// source revision after a "+", and naming the exact commit is the point of showing a
        /// version on a build nobody released.
        /// </summary>
        [Fact]
        public void ReportsTheVersionAsRecorded()
        {
            Assert.NotEmpty(SdlDesktopHost.Version);
            Assert.Equal(
                typeof(SdlDesktopHost).Assembly
                    .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                    ?.InformationalVersion,
                SdlDesktopHost.Version);
        }
    }
}
