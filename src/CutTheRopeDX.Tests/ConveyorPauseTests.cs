using CutTheRopeDX.Framework.Core;
using CutTheRopeDX.GameMain;
using CutTheRopeDX.Tests.Interactions;

using Xunit;

namespace CutTheRopeDX.Tests
{
    public sealed class ConveyorPauseTests
    {
        [Theory]
        [InlineData(0, false)]
        [InlineData(3, false)]
        [InlineData(0, true)]
        public void PausingDuringManualDragDoesNotMoveBeltOnResume(int pointerId, bool releaseBeforePause)
        {
            _ = Scenario.New()
                .Candy(160, 114)
                .Grab(159, 61, length: 10, wheel: true, breakable: false)
                .Conveyor(-76, 60, length: 468, width: 50, velocity: 10f, manual: true)
                .Build();
            CTRRootController root = (CTRRootController)Application.SharedRootController();
            root.SetPicker(false);
            GameController controller = new(root);
            controller.Activate();
            GameScene scene = (GameScene)controller.GetView(0).GetChild(0);
            ConveyorBelt belt = Assert.Single(scene.Conveyors().Iterator());
            ITransporterItem item = Assert.Single(belt.BoundObjects);
            float x = item.BindPoint.X - 150f;
            float y = belt.y;
            Vector screen = scene.ScreenPositionOf(new Vector(x, y));
            float screenX = screen.X;
            float screenY = screen.Y;
            float beforeDrag = item.PositionOnTransporter;
            _ = scene.TouchDownXYIndex(screenX, screenY, pointerId);
            _ = scene.TouchMoveXYIndex(screenX + 6f, screenY, pointerId);
            Assert.NotEqual(beforeDrag, item.PositionOnTransporter);
            float afterDrag = item.PositionOnTransporter;

            if (releaseBeforePause)
            {
                _ = scene.TouchUpXYIndex(screenX + 6f, screenY, pointerId);
            }

            controller.OnButtonPressed(GameControllerButtonId.Pause);
            Assert.False(scene.updateable);
            controller.OnButtonPressed(GameControllerButtonId.Continue);
            Assert.True(scene.updateable);
            HeadlessGame.StepFrames(scene, 10);

            // A stale move from the interrupted pointer must not restart the drag.
            _ = scene.TouchMoveXYIndex(screenX + 60f, screenY, pointerId);

            Assert.Equal(afterDrag, item.PositionOnTransporter);

            // A fresh drag must still work after the pause cancellation.
            _ = scene.TouchDownXYIndex(screenX, screenY, pointerId);
            _ = scene.TouchMoveXYIndex(screenX + 6f, screenY, pointerId);
            Assert.NotEqual(afterDrag, item.PositionOnTransporter);
            _ = scene.TouchUpXYIndex(screenX + 6f, screenY, pointerId);
        }
    }
}
