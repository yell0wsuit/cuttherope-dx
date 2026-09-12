using System.Collections.Generic;

using CutTheRopeDX.Desktop.Platform.Graphics;

using SDL3;

using Xunit;

namespace CutTheRopeDX.Desktop.Platform.Tests
{
    /// <summary>Which hints have to be in place for SDL to reach ANGLE rather than the driver.</summary>
    public sealed class AngleHintRoutingTests
    {
        private static Dictionary<string, string> HintsFor(GlContextProfile profile)
        {
            Dictionary<string, string> applied = [];
            using GlHintScope scope = new(_ => null, (name, value) =>
            {
                applied[name] = value;
                return true;
            }, _ => true);
            SdlGlDevice.ApplyAngleHints(profile, scope);
            return applied;
        }

        [Fact]
        public void SdlIsForcedOntoItsEglPath()
        {
            // Without this SDL takes its Windows WGL path, opens the GLES library as though it
            // were the system's opengl32 and fails on the wgl entry points that are not in it.
            Assert.Equal("1", HintsFor(GlContextProfile.Angle("egl", "gles"))[SDL.Hints.VideoForceEGL]);
        }

        [Fact]
        public void BothLibrariesAreNamedBecauseEachLoaderReadsItsOwn()
        {
            Dictionary<string, string> applied = HintsFor(GlContextProfile.Angle("egl", "gles"));

            Assert.Equal("egl", applied[SDL.Hints.EGLLibrary]);
            Assert.Equal("gles", applied[SDL.Hints.OpenGLLibrary]);
        }

        [Fact]
        public void TheRetryProfileIsRoutedTheSameWay()
        {
            Dictionary<string, string> applied = HintsFor(GlContextProfile.Angle("egl", "gles").Retry);

            Assert.Equal("1", applied[SDL.Hints.VideoForceEGL]);
            Assert.Equal("egl", applied[SDL.Hints.EGLLibrary]);
            Assert.Equal("gles", applied[SDL.Hints.OpenGLLibrary]);
        }
    }
}
