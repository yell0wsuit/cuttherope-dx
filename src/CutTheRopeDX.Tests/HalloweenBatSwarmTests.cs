using System;

using CutTheRopeDX.GameMain;

using Xunit;

namespace CutTheRopeDX.Tests
{
    /// <summary>
    /// Covers the Halloween bat swarm that stands in for the three-star confetti.
    /// </summary>
    public sealed class HalloweenBatSwarmTests
    {
        [Fact]
        public void CurveIsPolynomialPlusSine()
        {
            HalloweenBatSwarm.Curve curve = new(1f, 2f, 3f, 4f, 5f, 6f);
            float t = 0.5f;
            Assert.Equal(1f + (2f * t) + (3f * t * t) + (4f * MathF.Sin((5f * t) + 6f)), curve.At(t), 5);
        }

        [Fact]
        public void SpawnedPathStartsAtItsConstantTerm()
        {
            HalloweenBatSwarm.Curve path = new HalloweenBatSwarm.Curve(10f, 0f, 0f, 3f, 2f, 1f).StartingAtA0();
            Assert.Equal(10f, path.At(0f), 5);
        }

        [Fact]
        public void RangeRollsFromValueTowardsValuePlusDelta()
        {
            HalloweenBatSwarm.Range up = new(100f, 1000f);
            HalloweenBatSwarm.Range down = new(100f, -200f);
            for (int i = 0; i < 200; i++)
            {
                Assert.InRange(up.Sample(), 100f, 1100f);
                Assert.InRange(down.Sample(), -100f, 100f);
            }
        }

        [Fact]
        public void LayersReleaseAfterTheirPostponeAndAllBatsLeaveAfterTheirLifetime()
        {
            _ = HeadlessGame.Boot();
            HalloweenBatSwarm swarm = new();
            Assert.Equal(0, swarm.LiveBats);

            // The third layer has no postpone; the others wait 0.1-0.2 and 0.2-0.3 seconds.
            swarm.Update(0.01f);
            Assert.Equal(30, swarm.LiveBats);
            Tick(swarm, 0.3f);
            Assert.Equal(70, swarm.LiveBats);
            Assert.False(swarm.Finished);

            // Every bat lives three seconds from its own release.
            Tick(swarm, 3.1f);
            Assert.Equal(0, swarm.LiveBats);
            Assert.True(swarm.Finished);
        }

        [Fact]
        public void HalloweenThreeStarsBurstIntoBats()
        {
            WithStyle(MenuStyle.Classic, () => SeasonalDate.With(SeasonalDate.Halloween, () =>
            {
                BoxOpenClose box = LoadBox();
                box.ShowConfetti();
                Assert.Equal(1, box.confettiAnims.ChildsCount());
                _ = Assert.IsType<HalloweenBatSwarm>(box.confettiAnims.GetChild(0));
            }));
        }

        [Fact]
        public void OutsideHalloweenThreeStarsBurstIntoConfetti()
        {
            WithStyle(MenuStyle.Classic, () => SeasonalDate.With(SeasonalDate.NoEvent, () =>
            {
                BoxOpenClose box = LoadBox();
                box.ShowConfetti();
                Assert.Equal(70, box.confettiAnims.ChildsCount());
                Assert.IsNotType<HalloweenBatSwarm>(box.confettiAnims.GetChild(0));
            }));
        }

        [Fact]
        public void ExperimentsMenusKeepTheirConfetti()
        {
            WithStyle(MenuStyle.Experiments, () => SeasonalDate.With(SeasonalDate.Halloween, () =>
            {
                Assert.False(HalloweenBatSwarm.ReplacesConfetti);
                BoxOpenClose box = LoadBox();
                box.ShowConfetti();
                Assert.Equal(70, box.confettiAnims.ChildsCount());
                Assert.IsNotType<HalloweenBatSwarm>(box.confettiAnims.GetChild(0));
            }));
        }

        private static BoxOpenClose LoadBox()
        {
            _ = HeadlessGame.Boot();
            GameController controller = HeadlessGame.LoadLevelWithController(1, 4);
            BoxOpenClose box = (BoxOpenClose)controller.GetView(0).GetChild(4);
            box.confettiAnims.RemoveAllChilds();
            return box;
        }

        private static void Tick(HalloweenBatSwarm swarm, float seconds)
        {
            const float step = 1f / 60f;
            for (float t = 0f; t < seconds; t += step)
            {
                swarm.Update(step);
            }
        }

        private static void WithStyle(MenuStyle style, Action body)
        {
            MenuStyle previous = MenuTheme.Current;
            MenuTheme.Current = style;
            try
            {
                body();
            }
            finally
            {
                MenuTheme.Current = previous;
            }
        }
    }
}
