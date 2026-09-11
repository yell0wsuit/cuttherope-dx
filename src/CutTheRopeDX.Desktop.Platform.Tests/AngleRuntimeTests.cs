using System;
using System.IO;

using CutTheRopeDX.Desktop.Platform.Graphics;

using Xunit;

namespace CutTheRopeDX.Desktop.Platform.Tests
{
    /// <summary>Whether the ANGLE libraries the release ships are on disk.</summary>
    public sealed class AngleRuntimeTests : IDisposable
    {
        private readonly string root = Path.Combine(
            Path.GetTempPath(), $"ctrdx-angle-{Guid.NewGuid():N}");

        public void Dispose()
        {
            try
            {
                Directory.Delete(root, recursive: true);
            }
            catch (IOException)
            {
                // A temporary directory left behind fails nothing.
            }
        }

        private string Place(params string[] names)
        {
            string directory = Path.Combine(root, AngleRuntime.DirectoryName);
            _ = Directory.CreateDirectory(directory);
            foreach (string name in names)
            {
                File.WriteAllText(Path.Combine(directory, name), string.Empty);
            }

            return root;
        }

        [Fact]
        public void BothLibrariesResolveToAbsolutePathsBesideTheExecutable()
        {
            string baseDirectory = Place("libEGL.dll", "libGLESv2.dll");

            Assert.True(AngleRuntime.TryLocate(baseDirectory, out string egl, out string gles));
            Assert.Equal(Path.Combine(baseDirectory, "angle", "libEGL.dll"), egl);
            Assert.Equal(Path.Combine(baseDirectory, "angle", "libGLESv2.dll"), gles);
            Assert.True(Path.IsPathRooted(egl));
            Assert.True(Path.IsPathRooted(gles));
        }

        [Theory]
        [InlineData("libEGL.dll")]
        [InlineData("libGLESv2.dll")]
        public void HalfAnInstallIsNoInstall(string present)
        {
            string baseDirectory = Place(present);

            Assert.False(AngleRuntime.TryLocate(baseDirectory, out string egl, out string gles));
            Assert.Null(egl);
            Assert.Null(gles);
        }

        [Fact]
        public void AnEmptyDirectoryReportsNothingRatherThanThrowing()
        {
            Assert.False(AngleRuntime.TryLocate(root, out _, out _));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void AMissingBaseDirectoryReportsNothing(string baseDirectory)
        {
            Assert.False(AngleRuntime.TryLocate(baseDirectory, out _, out _));
        }
    }
}
