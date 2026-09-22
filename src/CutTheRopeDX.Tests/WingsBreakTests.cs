using CutTheRopeDX.Framework.Core;
using CutTheRopeDX.Framework.Visual;
using CutTheRopeDX.GameMain;

using Xunit;

namespace CutTheRopeDX.Tests
{
    /// <summary>
    /// The broken-wing burst against Time Travel's <c>WingsBreak</c>: it steps its particles twice
    /// per frame, each step <c>dir += gravity * dt; pos += dir * dt</c>, and stops itself once its
    /// duration has run.
    /// </summary>
    public sealed class WingsBreakTests
    {
        private const float FrameDelta = 0.016f;

        [Fact]
        public void AFeatherMovesAsTheOriginalsTwoStepsPerFrame()
        {
            WingsBreak burst = Burst(angle: -45f, drift: 0f);
            burst.angleVar = 0f;
            burst.speedVar = 0f;
            burst.radialAccelVar = 0f;
            burst.tangentialAccelVar = 0f;
            burst.StartSystem(1);
            Vector start = burst.particles[0].pos;
            Vector dir = burst.particles[0].dir;

            const int frames = 20;
            for (int f = 0; f < frames; f++)
            {
                burst.Update(FrameDelta);
            }

            // Two steps per frame, as Time Travel calls its particle update twice.
            Vector pos = start;
            for (int step = 0; step < 2 * frames; step++)
            {
                dir = new Vector(dir.X + (burst.gravity.X * FrameDelta), dir.Y + (burst.gravity.Y * FrameDelta));
                pos = new Vector(pos.X + (dir.X * FrameDelta), pos.Y + (dir.Y * FrameDelta));
            }

            Assert.Equal(pos.X, burst.particles[0].pos.X, 0.01f);
            Assert.Equal(pos.Y, burst.particles[0].pos.Y, 0.01f);
        }

        [Fact]
        public void TheBurstUsesTheOriginalsSpeedAndGravityInDxUnits()
        {
            WingsBreak burst = Burst(angle: -135f, drift: 12f);

            // Time Travel's 100 +/- 10 launch and 75 fall, stretched from its world to DX's.
            Assert.Equal(150f, burst.speed);
            Assert.Equal(15f, burst.speedVar);
            Assert.Equal(112.5f, burst.gravity.Y);
            Assert.Equal(12f, burst.gravity.X);
        }

        [Fact]
        public void TheFinishedBurstClearsItself()
        {
            WingsBreak burst = Burst(angle: -45f, drift: 0f);
            int finished = 0;
            burst.particlesDelegate = _ => finished++;
            burst.StartSystem(WingsBreak.FeatherCount);

            // Five seconds of life at two steps per frame runs out in about 2.5 seconds.
            for (int f = 0; f < 200 && finished == 0; f++)
            {
                burst.Update(FrameDelta);
            }

            Assert.Equal(1, finished);
        }

        private static WingsBreak Burst(float angle, float drift)
        {
            _ = HeadlessGame.Boot();
            Image grid = Image.FromResource(Resources.Img.ObjCandyTimeTravel);
            WingsBreak burst = new WingsBreak().Init(grid, angle, drift);
            Assert.NotNull(burst);
            return burst;
        }
    }
}
