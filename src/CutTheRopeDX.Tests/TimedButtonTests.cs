using CutTheRopeDX.Framework.Visual;
using CutTheRopeDX.GameMain;

using Xunit;

namespace CutTheRopeDX.Tests
{
    public sealed class TimedButtonTests
    {
        private sealed class Recorder : IButtonDelegation
        {
            public int Presses { get; private set; }

            public void OnButtonPressed(ButtonId buttonId)
            {
                Presses++;
            }
        }

        private static (TimedButton Button, Recorder Recorder) Make()
        {
            BaseElement up = new() { width = 100, height = 50 };
            BaseElement down = new() { width = 100, height = 50 };
            TimedButton button = new();
            _ = button.InitWithUpElementDownElementandID(up, down, MenuButtonId.ConfirmResetYes);
            Recorder recorder = new();
            button.delegateButtonDelegate = recorder;
            BaseElement.CalculateTopLeft(button);
            return (button, recorder);
        }

        private static void Hold(TimedButton button, float seconds)
        {
            for (float t = 0f; t < seconds - 0.0001f; t += 0.05f)
            {
                button.Update(0.05f);
            }
        }

        [Fact]
        public void ReleasingBeforeTheHoldEndsFiresNothing()
        {
            (TimedButton button, Recorder recorder) = Make();
            Assert.True(button.OnTouchDownXY(10f, 10f));
            Hold(button, 2.9f);
            Assert.False(button.IsArmed);

            _ = button.OnTouchUpXY(10f, 10f);

            Assert.Equal(0, recorder.Presses);
        }

        [Fact]
        public void ReleasingAfterTheHoldFiresOnce()
        {
            (TimedButton button, Recorder recorder) = Make();
            _ = button.OnTouchDownXY(10f, 10f);
            Hold(button, 3.05f);
            Assert.True(button.IsArmed);

            _ = button.OnTouchUpXY(10f, 10f);
            _ = button.OnTouchUpXY(10f, 10f);

            Assert.Equal(1, recorder.Presses);
        }

        [Fact]
        public void ReleasingOutsideAfterTheHoldFiresNothing()
        {
            (TimedButton button, Recorder recorder) = Make();
            _ = button.OnTouchDownXY(10f, 10f);
            Hold(button, 3.05f);

            _ = button.OnTouchUpXY(500f, 500f);

            Assert.Equal(0, recorder.Presses);
        }

        [Fact]
        public void DraggingOffAndBackDoesNotConfirm()
        {
            (TimedButton button, Recorder recorder) = Make();
            _ = button.OnTouchDownXY(10f, 10f);
            Hold(button, 2f);
            _ = button.OnTouchMoveXY(500f, 500f);
            _ = button.OnTouchMoveXY(10f, 10f);
            Hold(button, 2f);

            _ = button.OnTouchUpXY(10f, 10f);

            Assert.Equal(0, recorder.Presses);
        }

        [Fact]
        public void ASecondPressRestartsTheTimer()
        {
            (TimedButton button, Recorder recorder) = Make();
            _ = button.OnTouchDownXY(10f, 10f);
            Hold(button, 3.05f);
            _ = button.OnTouchUpXY(10f, 10f);
            _ = button.OnTouchDownXY(10f, 10f);
            Hold(button, 1f);

            _ = button.OnTouchUpXY(10f, 10f);

            Assert.Equal(1, recorder.Presses);
        }

        [Fact]
        public void TheArmedFlashTogglesThePlatesAndEndsPressed()
        {
            (TimedButton button, _) = Make();
            _ = button.OnTouchDownXY(10f, 10f);
            Hold(button, 3.0f);
            button.Update(0.001f);
            Assert.True(button.GetChild(0).visible);
            button.Update(0.1f);
            Assert.True(button.GetChild(1).visible);
            button.Update(0.2f);
            Assert.True(button.GetChild(0).visible);
            button.Update(0.1f);
            Assert.True(button.GetChild(1).visible);
            Assert.False(button.GetChild(0).visible);
        }
    }
}
