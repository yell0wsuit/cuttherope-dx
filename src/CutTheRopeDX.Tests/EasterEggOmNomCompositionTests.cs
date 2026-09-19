using CutTheRopeDX.Framework;
using CutTheRopeDX.GameMain;

using Xunit;

namespace CutTheRopeDX.Tests
{
    /// <summary>
    /// Covers where the easter egg's 16:9 composition lands on other window shapes: fitted,
    /// centered across, and resting on the bottom edge.
    /// </summary>
    public sealed class EasterEggOmNomCompositionTests
    {
        [Fact]
        public void LeavesTheDesignViewportUntouched()
        {
            (float x, float y, float scale) = EasterEggOmNom.Composition(new Rectangle(0f, 0f, 2560f, 1440f));

            Assert.Equal(0f, x);
            Assert.Equal(0f, y);
            Assert.Equal(1f, scale);
        }

        [Fact]
        public void FitsAPortraitViewportByWidthAndRestsOnTheBottom()
        {
            (float x, float y, float scale) = EasterEggOmNom.Composition(new Rectangle(0f, 0f, 1440f, 2560f));

            Assert.Equal(0.5625f, scale, 4);
            Assert.Equal(0f, x, 2);
            // The scaled box is 810 tall, so its top sits 810 above the bottom edge.
            Assert.Equal(2560f - 810f, y, 2);
        }

        [Fact]
        public void CentersAnUltrawideViewportAtFullSize()
        {
            (float x, float y, float scale) = EasterEggOmNom.Composition(new Rectangle(0f, 0f, 3440f, 1440f));

            Assert.Equal(1f, scale, 4);
            Assert.Equal((3440f - 2560f) / 2f, x, 2);
            Assert.Equal(0f, y, 2);
        }

        [Theory]
        [InlineData(0f, -750f)]
        [InlineData(1f, 337f)]
        public void OffsetKeepsHimWithinTheDesignWidth(float position, float expected)
        {
            Rectangle screen = new(0f, 0f, 2560f, 1440f);

            float offset = EasterEggOmNom.HorizontalOffset(screen, EasterEggOmNom.Composition(screen), position);

            // At full size his left edge rests at 750 and he is 1473 wide.
            Assert.Equal(expected, offset, 0);
        }

        [Theory]
        [InlineData(0f, -1190f)]
        [InlineData(1f, 777f)]
        public void OffsetReachesBothEdgesOfAnUltrawideViewport(float position, float expected)
        {
            Rectangle screen = new(0f, 0f, 3440f, 1440f);

            float offset = EasterEggOmNom.HorizontalOffset(screen, EasterEggOmNom.Composition(screen), position);

            Assert.Equal(expected, offset, 0);
        }

        [Fact]
        public void OffsetOnPortraitMatchesTheDesignRangeInCompositionUnits()
        {
            Rectangle screen = new(0f, 0f, 1440f, 2560f);

            float offset = EasterEggOmNom.HorizontalOffset(screen, EasterEggOmNom.Composition(screen), 1f);

            Assert.Equal(337f, offset, 0);
        }

        [Fact]
        public void EachTriggerPicksWhereHeAppears()
        {
            EasterEggOmNom egg = new(new System.Random(7));
            System.Collections.Generic.HashSet<float> positions = [];

            for (int i = 0; i < 5; i++)
            {
                egg.Trigger();
                Assert.InRange(egg.Position, 0f, 1f);
                _ = positions.Add(egg.Position);
            }

            Assert.True(positions.Count > 1, "every trigger picked the same position");
        }

        [Fact]
        public void FollowsTheViewportOrigin()
        {
            (float x, float y, float scale) = EasterEggOmNom.Composition(new Rectangle(10f, 20f, 2560f, 1440f));

            Assert.Equal(10f, x);
            Assert.Equal(20f, y);
            Assert.Equal(1f, scale);
        }
    }
}
