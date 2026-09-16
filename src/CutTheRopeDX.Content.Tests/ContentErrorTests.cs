using CutTheRopeDX.Content.Commands;

using Xunit;

namespace CutTheRopeDX.Content.Tests
{
    /// <summary>
    /// The shape of a builder failure, which MSBuild only promotes to a build error when it
    /// matches its canonical error format on a single line.
    /// </summary>
    public sealed class ContentErrorTests
    {
        [Fact]
        public void WritesTheCanonicalErrorFormat()
        {
            using StringWriter writer = new();

            ContentError.Write(writer, ContentError.DownloadFailed, "Could not download.");

            Assert.Equal(
                "CutTheRopeDX.Content : error CTRDX002: Could not download." + Environment.NewLine,
                writer.ToString());
        }

        [Fact]
        public void FoldsLineBreaksSoTheMessageStaysOneError()
        {
            using StringWriter writer = new();

            ContentError.Write(writer, ContentError.Unexpected, "first\r\nsecond\nthird");

            Assert.Equal(
                "CutTheRopeDX.Content : error CTRDX005: first second third" + Environment.NewLine,
                writer.ToString());
        }
    }
}
