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
            scene.TapOmNom();
            if (dismissible)
            {
                // Past the 600ms before a press can dismiss him.
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

        [Fact]
        public void ThePauseButtonDismissesTheEggInsteadOfPausing()
        {
            (GameController controller, GameScene scene) = LoadWithEggPlaying();

            controller.OnButtonPressed(GameControllerButtonId.Pause);

            Assert.False(scene.EasterEggHoldsLevel);
            Assert.True(scene.updateable);
            Assert.False(PauseMenuOpen(controller));
        }

        [Fact]
        public void ThePauseButtonPausesAgainOnceTheEggHasLetGo()
        {
            (GameController controller, GameScene scene) = LoadWithEggPlaying();
            controller.OnButtonPressed(GameControllerButtonId.Pause);

            controller.OnButtonPressed(GameControllerButtonId.Pause);

            Assert.False(scene.updateable);
            Assert.True(PauseMenuOpen(controller));
        }

        [Fact]
        public void TheRestartButtonDismissesTheEggInsteadOfRestarting()
        {
            (GameController controller, GameScene scene) = LoadWithEggPlaying();

            controller.OnButtonPressed(GameControllerButtonId.Restart);

            Assert.False(scene.EasterEggHoldsLevel);
            Assert.Equal(RestartPhase.Playing, scene.gameplayFlow.Phase);
        }

        [Fact]
        public void TheBackKeyDismissesTheEggInsteadOfPausing()
        {
            (GameController controller, GameScene scene) = LoadWithEggPlaying();

            _ = controller.BackButtonPressed();

            Assert.False(scene.EasterEggHoldsLevel);
            Assert.Equal(0, controller.exitCode);
            Assert.False(PauseMenuOpen(controller));
        }

        [Fact]
        public void TheMenuKeyDismissesTheEggInsteadOfPausing()
        {
            (GameController controller, GameScene scene) = LoadWithEggPlaying();

            _ = controller.MenuButtonPressed();

            Assert.False(scene.EasterEggHoldsLevel);
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
