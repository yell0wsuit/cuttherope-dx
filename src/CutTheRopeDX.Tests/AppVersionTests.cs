using CutTheRopeDX.Helpers;

using Xunit;

namespace CutTheRopeDX.Tests
{
    public class AppVersionTests
    {
        [Theory]
        [InlineData("1.0.0-prerelease+57", true)]
        [InlineData("1.0.0-prerelease+57.be4245b8fa894ced644cf10f1b4f66b9f3b1661b", true)]
        [InlineData("2.30.0", false)]
        [InlineData("1.0.0-dirty+be4245b8fa894ced644cf10f1b4f66b9f3b1661b", false)]
        [InlineData(null, false)]
        public void RecognizesPrereleaseBuilds(string version, bool expected)
        {
            Assert.Equal(expected, AppVersion.IsPrereleaseVersion(version));
        }

        [Theory]
        [InlineData("2.30.0", "2.30.0")]
        [InlineData("1.0.0-prerelease+57", "1.0.0-prerelease+57")]
        [InlineData("1.0.0-dirty+be4245b8fa894ced644cf10f1b4f66b9f3b1661b", "1.0.0-dirty+be4245b")]
        [InlineData("1.0.0-prerelease+57.be4245b8fa894ced644cf10f1b4f66b9f3b1661b", "1.0.0-prerelease+57.be4245b")]
        public void AbbreviatesOnlyTheRevisionHash(string version, string expected)
        {
            Assert.Equal(expected, AppVersion.Abbreviate(version));
        }
    }
}
