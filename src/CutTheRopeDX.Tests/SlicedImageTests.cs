using CutTheRopeDX.Framework.Visual;

using Xunit;

namespace CutTheRopeDX.Tests
{
    public sealed class SlicedImageTests
    {
        [Fact]
        public void TheCapsKeepTheirArtAndOnlyTheCenterStripStretches()
        {
            (SlicedImage.Slice left, SlicedImage.Slice middle, SlicedImage.Slice right) = SlicedImage.Cut(363f, 500f);

            Assert.Equal(new SlicedImage.Slice(0f, 179f, 0f, 179f), left);
            Assert.Equal(new SlicedImage.Slice(179f, SlicedImage.StripWidth, 179f, 141f), middle);
            Assert.Equal(new SlicedImage.Slice(183f, 180f, 320f, 180f), right);
        }

        [Fact]
        public void ThePiecesMeetEdgeToEdge()
        {
            (SlicedImage.Slice left, SlicedImage.Slice middle, SlicedImage.Slice right) = SlicedImage.Cut(332f, 474f);

            Assert.Equal(left.DrawStart + left.DrawWidth, middle.DrawStart);
            Assert.Equal(middle.DrawStart + middle.DrawWidth, right.DrawStart);
            Assert.Equal(474f, right.DrawStart + right.DrawWidth);
            Assert.Equal(left.SourceStart + left.SourceWidth, middle.SourceStart);
            Assert.Equal(middle.SourceStart + middle.SourceWidth, right.SourceStart);
            Assert.Equal(332f, right.SourceStart + right.SourceWidth);
        }

        [Fact]
        public void AWidthAtOrBelowTheArtLeavesTheStripUnstretched()
        {
            (_, SlicedImage.Slice middle, SlicedImage.Slice right) = SlicedImage.Cut(363f, 300f);

            Assert.Equal(SlicedImage.StripWidth, middle.DrawWidth);
            Assert.Equal(363f, right.DrawStart + right.DrawWidth);
        }
    }
}
