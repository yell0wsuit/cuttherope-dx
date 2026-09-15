using CutTheRopeDX.GameMain;
using CutTheRopeDX.Tests.Interactions;

using Xunit;

namespace CutTheRopeDX.Tests
{
    /// <summary>
    /// Covers the controller-level inputs that reach past the scene - the HUD buttons and the
    /// back and menu keys - while the easter egg holds the level: they only send it away.
    /// </summary>
    public sealed class EasterEggControllerInputTests
    {
        private static (GameController Controller, GameScene Scene) LoadWithEggPlaying(bool dismissible = true)
        {
            _ = HeadlessGame.Boot();
            GameController controller = HeadlessGame.LoadLevelWithController(pack: 1, level: 4);
            GameScene scene = (GameScene)controller.GetView(0).GetChild(GameView.VIEW_ELEMENT_GAME_SCENE);
            HeadlessGame.StepFrames(scene, 60);
            scene.SetEasterEggChance(1f);
            scene.TapOmNom();
            if (dismissible)
            {
                // Past the 150ms before a press can dismiss him.
                HeadlessGame.StepFrames(scene, 60);
            }
            Assert.True(scene.EasterEggHoldsLevel);
            return (controller, scene);
        }

        [Fact]
        public void ThePauseButtonIsSwallowedBeforeTheEggCanBeDismissed()
        {
            (GameController controller, GameScene scene) = LoadWithEggPlaying(dismissible: false);

            controller.OnButtonPressed(GameControllerButtonId.Pause);

            Assert.True(scene.EasterEggHoldsLevel);
            Assert.False(PauseMenuOpen(controller));
        }

        private static bool PauseMenuOpen(GameController controller)
        {
            return controller.GetView(0).GetChild(GameView.VIEW_ELEMENT_PAUSE_MENU).IsEnabled();
        }

        /// <summary>Asserts the egg was sent away: it holds the level through its fade, then is gone.</summary>
        private static void AssertDismissed(GameScene scene)
        {
            Assert.True(scene.IsEasterEggPlaying());
            HeadlessGame.StepFrames(scene, 20);
            Assert.False(scene.IsEasterEggPlaying());
        }

        [Fact]
        public void ThePauseButtonDismissesTheEggInsteadOfPausing()
        {
            (GameController controller, GameScene scene) = LoadWithEggPlaying();

            controller.OnButtonPressed(GameControllerButtonId.Pause);

            AssertDismissed(scene);
            Assert.True(scene.updateable);
            Assert.False(PauseMenuOpen(controller));
        }

        [Fact]
        public void ThePauseButtonPausesAgainOnceTheEggHasLetGo()
        {
            (GameController controller, GameScene scene) = LoadWithEggPlaying();
            controller.OnButtonPressed(GameControllerButtonId.Pause);
            // Presses stay swallowed until the dismissal fade is gone.
            HeadlessGame.StepFrames(scene, 20);

            controller.OnButtonPressed(GameControllerButtonId.Pause);

            Assert.False(scene.updateable);
            Assert.True(PauseMenuOpen(controller));
        }

        [Fact]
        public void TheRestartButtonDismissesTheEggInsteadOfRestarting()
        {
            (GameController controller, GameScene scene) = LoadWithEggPlaying();

            controller.OnButtonPressed(GameControllerButtonId.Restart);

            AssertDismissed(scene);
            Assert.Equal(RestartPhase.Playing, scene.gameplayFlow.Phase);
        }

        [Fact]
        public void TheBackKeyDismissesTheEggInsteadOfPausing()
        {
            (GameController controller, GameScene scene) = LoadWithEggPlaying();

            _ = controller.BackButtonPressed();

            AssertDismissed(scene);
            Assert.Equal(0, controller.exitCode);
            Assert.False(PauseMenuOpen(controller));
        }

        [Fact]
        public void TheMenuKeyDismissesTheEggInsteadOfPausing()
        {
            (GameController controller, GameScene scene) = LoadWithEggPlaying();

            _ = controller.MenuButtonPressed();

            AssertDismissed(scene);
            Assert.False(PauseMenuOpen(controller));
        }

        [Fact]
        public void AForcedPauseRemovesTheEggOutright()
        {
            (GameController controller, GameScene scene) = LoadWithEggPlaying();

            Assert.True(controller.EnsurePaused());

            Assert.False(scene.IsEasterEggPlaying());
            Assert.True(PauseMenuOpen(controller));
        }

        [Fact]
        public void ReloadingTheLevelRemovesTheEgg()
        {
            (_, GameScene scene) = LoadWithEggPlaying();

            scene.Reload();

            Assert.False(scene.IsEasterEggPlaying());
        }
    }
}
