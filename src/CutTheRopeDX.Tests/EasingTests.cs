using CutTheRopeDX.Framework.Visual;

using Xunit;

namespace CutTheRopeDX.Tests
{
    /// <summary>
    /// Covers the easing curves the vector easter egg animates on. Endpoints are exact by
    /// contract; the overshoot is what distinguishes back easing from a plain ease.
    /// </summary>
    public sealed class EasingTests
    {
        [Fact]
        public void OutExpoRunsFromBaseToBasePlusChange()
        {
            Assert.Equal(0f, Easing.OutExpo(0f, 0f, 100f, 800f), 3);
            Assert.Equal(100f, Easing.OutExpo(800f, 0f, 100f, 800f), 3);
        }

        [Fact]
        public void InOutExpoRunsFromBaseToBasePlusChange()
        {
            Assert.Equal(0f, Easing.InOutExpo(0f, 0f, 50f, 600f), 3);
            Assert.Equal(50f, Easing.InOutExpo(600f, 0f, 50f, 600f), 3);
        }

        [Fact]
        public void InOutExpoPassesThroughItsMidpoint()
        {
            Assert.Equal(25f, Easing.InOutExpo(300f, 0f, 50f, 600f), 1);
        }

        [Fact]
        public void OutBackSettlesExactlyOnBasePlusChange()
        {
            Assert.Equal(0.1f, Easing.OutBack(0f, 0.1f, 5.9f, 600f, 1.5f), 4);
            Assert.Equal(6.0f, Easing.OutBack(600f, 0.1f, 5.9f, 600f, 1.5f), 4);
        }

        [Fact]
        public void OutBackOvershootsBeforeSettling()
        {
            float peak = 0f;
            for (int t = 0; t <= 600; t++)
            {
                peak = System.MathF.Max(peak, Easing.OutBack(t, 0.1f, 5.9f, 600f, 1.5f));
            }

            Assert.True(peak > 6.0f, $"expected an overshoot above 6.0, peaked at {peak}");
            Assert.Equal(6.47f, peak, 1);
        }

        [Fact]
        public void InOutBackRunsFromBaseToBasePlusChange()
        {
            Assert.Equal(0f, Easing.InOutBack(0f, 0f, 0.1f, 1000f, 6.0f), 5);
            Assert.Equal(0.1f, Easing.InOutBack(1000f, 0f, 0.1f, 1000f, 6.0f), 5);
        }
    }
}
