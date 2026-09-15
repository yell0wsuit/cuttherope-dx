using CutTheRopeDX.Framework.Core;
using CutTheRopeDX.GameMain;
using CutTheRopeDX.Tests.Interactions;

using Xunit;

namespace CutTheRopeDX.Tests
{
    /// <summary>
    /// Covers the tap that starts the easter egg and the tap that dismisses it: it takes a press
    /// and a release both on Om Nom, the level holds while it plays, and any press while it holds
    /// the level sends it away.
    /// </summary>
    public sealed class EasterEggTriggerTests
    {
        private static GameScene SceneWithOmNom(out float x, out float y)
        {
            GameScene scene = Scenario.New().Candy(160, 100).OmNom(160, 400).Build();
            Vector screen = scene.OmNomTapPoint();
            x = screen.X;
            y = screen.Y;
            return scene;
        }

        private static GameScene SceneWithEggPlaying(out float x, out float y)
        {
            GameScene scene = SceneWithOmNom(out x, out y);
            scene.TapOmNom();
            return scene;
        }

        [Fact]
        public void PressAndReleaseOnOmNomStartsTheEgg()
        {
            GameScene scene = SceneWithEggPlaying(out float _, out float _);

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
        public void AnOmNomInANonClassicSkinDoesNotStartTheEgg()
        {
            // The tap point comes from a classic Om Nom in the same spot, so the test does not
            // lean on the other skin's hit region to find him.
            _ = SceneWithOmNom(out float x, out float y);
            // Slot 2, the first manifest skin that is plainly not the classic look.
            GameScene scene = Scenario.New().Candy(160, 100).OmNom(160, 400, targetType: 3).Build();
            Assert.NotNull(scene.Targets()[0].controller.SkinDefinition);

            _ = scene.TouchDownXYIndex(x, y, 0);
            _ = scene.TouchUpXYIndex(x, y, 0);

            Assert.False(scene.IsEasterEggPlaying());
        }

        [Fact]
        public void OnlyThePrimaryOmNomStartsTheEgg()
        {
            GameScene scene = Scenario.New().Candy(160, 100).OmNom(80, 400).OmNom(240, 400).Build();
            Vector second = scene.OmNomTapPoint(index: 1);
            Assert.False(scene.OmNomTarget().PointInDrawQuad(
                scene.Targets()[1].targetObject.x, scene.Targets()[1].targetObject.y));

            _ = scene.TouchDownXYIndex(second.X, second.Y, 0);
            _ = scene.TouchUpXYIndex(second.X, second.Y, 0);

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
        public void TheLevelHoldsWhileTheEggPlays()
        {
            GameScene scene = SceneWithEggPlaying(out float _, out float _);
            float before = scene.Candy().WholeBody.Point.pos.Y;

            HeadlessGame.StepFrames(scene, 30);

            Assert.True(scene.EasterEggHoldsLevel);
            Assert.Equal(before, scene.Candy().WholeBody.Point.pos.Y);
        }

        [Fact]
        public void TheLevelResumesOnceTheEggIsDismissed()
        {
            GameScene scene = SceneWithEggPlaying(out float x, out float y);
            // Past the dim and the rise, so he is fully big and can be dismissed.
            HeadlessGame.StepFrames(scene, 60);
            float before = scene.Candy().WholeBody.Point.pos.Y;

            _ = scene.TouchDownXYIndex(x + 600f, y + 600f, 0);
            _ = scene.TouchUpXYIndex(x + 600f, y + 600f, 0);
            HeadlessGame.StepFrames(scene, 5);

            Assert.NotEqual(before, scene.Candy().WholeBody.Point.pos.Y);
        }

        [Fact]
        public void APressAnywhereDismissesTheEgg()
        {
            GameScene scene = SceneWithEggPlaying(out float x, out float y);
            HeadlessGame.StepFrames(scene, 60);

            _ = scene.TouchDownXYIndex(x + 600f, y + 600f, 0);
            _ = scene.TouchUpXYIndex(x + 600f, y + 600f, 0);

            // The 200ms fade-out, at 60 updates a second.
            HeadlessGame.StepFrames(scene, 14);
            Assert.False(scene.IsEasterEggPlaying());
        }

        [Fact]
        public void APressWhileOnlyTheDimIsUpIsSwallowedWithoutDismissing()
        {
            GameScene scene = SceneWithEggPlaying(out float x, out float y);
            float before = scene.Candy().WholeBody.Point.pos.Y;

            _ = scene.TouchDownXYIndex(x + 600f, y + 600f, 0);
            _ = scene.TouchUpXYIndex(x + 600f, y + 600f, 0);

            // Well past a dismissal's 200ms fade: the egg is still holding the level.
            HeadlessGame.StepFrames(scene, 30);
            Assert.True(scene.EasterEggHoldsLevel);
            Assert.Equal(before, scene.Candy().WholeBody.Point.pos.Y);
        }

        [Fact]
        public void TheDismissingPressOnOmNomDoesNotRestartTheEgg()
        {
            GameScene scene = SceneWithEggPlaying(out float x, out float y);
            HeadlessGame.StepFrames(scene, 60);

            _ = scene.TouchDownXYIndex(x, y, 0);
            _ = scene.TouchUpXYIndex(x, y, 0);

            HeadlessGame.StepFrames(scene, 14);
            Assert.False(scene.IsEasterEggPlaying());
        }

        [Fact]
        public void TheEggEndsOnItsOwn()
        {
            GameScene scene = SceneWithEggPlaying(out float _, out float _);

            // 200ms dim, 3600ms of motion and the 200ms closing fade, at 60 updates a second.
            HeadlessGame.StepFrames(scene, 251);

            Assert.False(scene.IsEasterEggPlaying());
        }
    }
}
