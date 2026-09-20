using CutTheRopeDX.Framework.Core;
using CutTheRopeDX.Framework.Visual;
using CutTheRopeDX.GameMain;
using CutTheRopeDX.Tests.Interactions;

using Xunit;

namespace CutTheRopeDX.Tests
{
    public sealed class LevelEndTouchTests
    {
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void LevelEndReleasesSpikeButtonWithoutToggling(bool won)
        {
            GameScene scene = Scenario.New().Candy(160, 350).Spikes(160, 100, toggled: 1).Build();
            Spikes spike = Assert.Single(scene.SpikeStrips());
            Button button = spike.rotateButton;
            BaseElement.CalculateTopLeft(spike);
            BaseElement.CalculateTopLeft(button);
            Vector screen = scene.ScreenPositionOf(new Vector(button.drawX + (button.width / 2f), button.drawY + (button.height / 2f)));
            _ = scene.TouchDownXYIndex(screen.X, screen.Y, 3);
            Assert.Equal(3, spike.touchIndex);
            Assert.Equal(Button.BUTTON_STATE.BUTTON_DOWN, button.state);
            float rotation = spike.rotation;
            float buttonScale = button.scaleX;

            EndLevel(scene, won);

            Assert.Equal(-1, spike.touchIndex);
            Assert.Equal(Button.BUTTON_STATE.BUTTON_UP, button.state);
            _ = scene.TouchUpXYIndex(screen.X, screen.Y, 3);
            Assert.Equal(rotation, spike.rotation);
            Assert.Equal(buttonScale, button.scaleX);
        }

        [Theory]
        [InlineData(false, false)]
        [InlineData(false, true)]
        [InlineData(true, false)]
        [InlineData(true, true)]
        public void LevelEndReleasesHookWithoutMovingIt(bool won, bool wheel)
        {
            GameScene scene = Scenario.New().Candy(160, 350)
                .Grab(160, 100, length: 250, wheel: wheel, moveLength: wheel ? -1f : 100f)
                .Build();
            Grab grab = Assert.Single(scene.Grabs());
            Vector screen = scene.ScreenPositionOf(grab);
            _ = scene.TouchDownXYIndex(screen.X, screen.Y, 2);
            Assert.Equal(2, wheel ? grab.Wheel.OperatingTouch : grab.Rail.DraggingTouch);
            float x = grab.x;
            float y = grab.y;
            float ropeLength = grab.Rope.GetLength();

            EndLevel(scene, won);

            Assert.Equal(-1, wheel ? grab.Wheel.OperatingTouch : grab.Rail.DraggingTouch);
            _ = scene.TouchMoveXYIndex(screen.X + 90f, screen.Y + 90f, 2);
            _ = scene.TouchUpXYIndex(screen.X + 90f, screen.Y + 90f, 2);
            Assert.Equal(x, grab.x);
            Assert.Equal(y, grab.y);
            Assert.Equal(ropeLength, grab.Rope.GetLength());
        }

        private static void EndLevel(GameScene scene, bool won)
        {
            if (won)
            {
                scene.GameWon();
            }
            else
            {
                scene.GameLost();
            }
        }
    }
}
