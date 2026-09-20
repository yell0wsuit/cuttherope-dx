using CutTheRopeDX.Framework.Core;
using CutTheRopeDX.GameMain;
using CutTheRopeDX.Tests.Interactions;

using Xunit;

namespace CutTheRopeDX.Tests
{
    public sealed class RocketPauseTests
    {
        [Theory]
        [InlineData(0)]
        [InlineData(3)]
        public void PauseShouldNotTurnAHeldRocket(int pointerId)
        {
            _ = Scenario.New().Candy(160, 350).Rocket(160, 100, isRotatable: true).Build();
            CTRRootController root = (CTRRootController)Application.SharedRootController();
            root.SetPicker(false);
            GameController controller = new(root);
            controller.Activate();
            GameScene scene = (GameScene)controller.GetView(0).GetChild(0);
            Rocket rocket = Assert.Single(scene.Rockets());
            Vector screen = scene.ScreenPositionOf(rocket);
            float before = rocket.startRotation;
            _ = scene.TouchDownXYIndex(screen.X, screen.Y, pointerId);
            Assert.Equal(pointerId, rocket.isOperating);
            controller.OnButtonPressed(GameControllerButtonId.Pause);
            Assert.False(scene.updateable);
            Assert.Equal(-1, rocket.isOperating);
            controller.OnButtonPressed(GameControllerButtonId.Continue);
            HeadlessGame.StepFrames(scene, 15);
            Assert.Equal(before, rocket.startRotation);

            // The eventual release of the interrupted touch must not turn the rocket either.
            _ = scene.TouchUpXYIndex(screen.X, screen.Y, pointerId);
            Assert.Equal(before, rocket.startRotation);

            // A new, ordinary tap after resume still turns it once.
            _ = scene.TouchDownXYIndex(screen.X, screen.Y, pointerId);
            _ = scene.TouchUpXYIndex(screen.X, screen.Y, pointerId);
            Assert.Equal(before + 45f, rocket.startRotation);
        }
    }
}
