using CutTheRopeDX.Desktop.Platform.Graphics;

using Xunit;

namespace CutTheRopeDX.Desktop.Platform.Tests
{
    /// <summary>Which context each renderer asks SDL for.</summary>
    public sealed class GlContextProfileTests
    {
        [Fact]
        public void TheDesktopProfileAsksTheSystemDriverForCoreThreeTwo()
        {
            GlContextProfile profile = GlContextProfile.DesktopCore;

            Assert.Equal(GlContextProfile.CoreMask, profile.ProfileMask);
            Assert.Equal(3, profile.Major);
            Assert.Equal(2, profile.Minor);
            Assert.False(profile.UsesAngle);
            Assert.Null(profile.EglLibrary);
            Assert.Null(profile.GlesLibrary);
        }

        [Fact]
        public void TheDesktopProfileHasNoRetry()
        {
            Assert.Null(GlContextProfile.DesktopCore.Retry);
        }

        [Fact]
        public void TheAngleProfileAsksForEsThreeFromItsOwnLibraries()
        {
            GlContextProfile profile = GlContextProfile.Angle("/opt/angle/libEGL.dll", "/opt/angle/libGLESv2.dll");

            Assert.Equal(GlContextProfile.EsMask, profile.ProfileMask);
            Assert.Equal(3, profile.Major);
            Assert.Equal(0, profile.Minor);
            Assert.True(profile.UsesAngle);
            Assert.Equal("/opt/angle/libEGL.dll", profile.EglLibrary);
            Assert.Equal("/opt/angle/libGLESv2.dll", profile.GlesLibrary);
        }

        [Fact]
        public void TheAngleProfileRetriesOnceAtEsTwoWithTheSameLibraries()
        {
            GlContextProfile retry = GlContextProfile.Angle("egl", "gles").Retry;

            Assert.NotNull(retry);
            Assert.Equal(GlContextProfile.EsMask, retry.ProfileMask);
            Assert.Equal(2, retry.Major);
            Assert.Equal(0, retry.Minor);
            Assert.Equal("egl", retry.EglLibrary);
            Assert.Equal("gles", retry.GlesLibrary);
        }

        [Fact]
        public void TheRetryDoesNotItselfRetry()
        {
            Assert.Null(GlContextProfile.Angle("egl", "gles").Retry.Retry);
        }
    }
}
