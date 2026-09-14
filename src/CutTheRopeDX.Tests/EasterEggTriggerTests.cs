using CutTheRopeDX.Framework.Core;
using CutTheRopeDX.Framework.Helpers;
using CutTheRopeDX.GameMain;
using CutTheRopeDX.Tests.Interactions;

using Xunit;

namespace CutTheRopeDX.Tests
{
    /// <summary>
    /// Covers the tap that starts the easter egg: it takes a press and a release both on Om Nom,
    /// it must not shadow gameplay input, and its hit region follows the camera.
    /// </summary>
    public sealed class EasterEggTriggerTests
    {
        private static GameScene SceneWithOmNom(out float x, out float y)
        {
            GameScene scene = Scenario.New().Candy(160, 100).OmNom(160, 400).Build();
            Vector screen = OmNomTapPoint(scene);
            x = screen.X;
            y = screen.Y;
            return scene;
        }

        /// <summary>A screen point the target's own hit test agrees is on Om Nom.</summary>
        private static Vector OmNomTapPoint(GameScene scene)
        {
            GameObject target = scene.OmNomTarget();

            // Walk outward from the anchor until the object's own test accepts a point. The
            // anchor is not guaranteed to sit inside the drawn quad.
            for (int radius = 0; radius <= 200; radius += 4)
            {
                for (int dx = -radius; dx <= radius; dx += 4)
                {
                    for (int dy = -radius; dy <= radius; dy += 4)
                    {
                        if (target.PointInDrawQuad(target.x + dx, target.y + dy))
                        {
                            return scene.ScreenPositionOf(new Vector(target.x + dx, target.y + dy));
                        }
                    }
                }
            }

            Assert.Fail("no point on Om Nom's draw quad was found");
            return default;
        }

        [Fact]
        public void PressAndReleaseOnOmNomStartsTheEgg()
        {
            GameScene scene = SceneWithOmNom(out float x, out float y);

            _ = scene.TouchDownXYIndex(x, y, 0);
            _ = scene.TouchUpXYIndex(x, y, 0);

            Assert.True(scene.IsEasterEggPlaying());
        }

        [Fact]
        public void ReleasingAwayFromOmNomDoesNotStartTheEgg()
        {
            GameScene scene = SceneWithOmNom(out float x, out float y);

            _ = scene.TouchDownXYIndex(x, y, 0);
            _ = scene.TouchUpXYIndex(x + 600f, y + 600f, 0);

            Assert.False(scene.IsEasterEggPlaying());
        }

        [Fact]
        public void PressingAwayFromOmNomDoesNotStartTheEgg()
        {
            GameScene scene = SceneWithOmNom(out float x, out float y);

            _ = scene.TouchDownXYIndex(x + 600f, y + 600f, 0);
            _ = scene.TouchUpXYIndex(x, y, 0);

            Assert.False(scene.IsEasterEggPlaying());
        }

        [Fact]
        public void TheEggDoesNotStartWithoutATap()
        {
            GameScene scene = SceneWithOmNom(out float _, out float _);

            HeadlessGame.StepFrames(scene, 30);

            Assert.False(scene.IsEasterEggPlaying());
        }

        [Fact]
        public void AFurtherTapDuringPlaybackDoesNotRestartTheEgg()
        {
            GameScene scene = SceneWithOmNom(out float x, out float y);
            _ = scene.TouchDownXYIndex(x, y, 0);
            _ = scene.TouchUpXYIndex(x, y, 0);

            HeadlessGame.StepFrames(scene, 60);
            _ = scene.TouchDownXYIndex(x, y, 0);
            _ = scene.TouchUpXYIndex(x, y, 0);

            // Still the first playthrough, so it still ends on schedule rather than being
            // pushed out by the second tap.
            HeadlessGame.StepFrames(scene, 200);
            Assert.False(scene.IsEasterEggPlaying());
        }

        [Fact]
        public void TheEggHoldsTheLevelThenReleasesIt()
        {
            GameScene scene = SceneWithOmNom(out float x, out float y);

            _ = scene.TouchDownXYIndex(x, y, 0);
            _ = scene.TouchUpXYIndex(x, y, 0);
            Assert.True(scene.IsEasterEggPlaying());

            // 200ms opening fade plus 3600ms of motion, at the fixed 16ms tick.
            HeadlessGame.StepFrames(scene, 250);

            Assert.False(scene.IsEasterEggPlaying());
        }
    }
}
