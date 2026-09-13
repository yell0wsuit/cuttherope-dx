using CutTheRopeDX.Framework.Core;

using Xunit;

namespace CutTheRopeDX.Tests
{
    public class RootControllerTransitionClockTests
    {
        private const float Step = 0.016f;

        [Fact]
        public void CatchUpTicksWithoutADrawDoNotAdvanceTheTransition()
        {
            RootController root = new(null);
            root.BeginTransition();

            // A stalled frame is replayed as a batch of ticks before the next draw.
            for (int i = 0; i < 30; i++)
            {
                root.PerformTick(Step);
            }

            Assert.True(root.IsTransitionActive());
            Assert.Equal(0f, root.TransitionProgress);
        }

        [Fact]
        public void EachDrawnFrameLetsTheNextTickAdvanceTheTransition()
        {
            RootController root = new(null);
            root.BeginTransition();

            root.OnTransitionFrameDrawn();
            root.PerformTick(Step);
            root.PerformTick(Step);

            Assert.Equal(Step / RootController.TRANSITION_DEFAULT_DELAY, root.TransitionProgress, 4);
        }

        [Fact]
        public void TransitionReachesTheEndOnlyAfterEnoughDrawnFrames()
        {
            RootController root = new(null);
            root.BeginTransition();

            int frames = 0;
            while (root.TransitionProgress < 1f && frames < 100)
            {
                root.OnTransitionFrameDrawn();
                root.PerformTick(Step);
                root.PerformTick(Step);
                root.PerformTick(Step);
                frames++;
            }

            // 0.4 s at one 16 ms step per drawn frame; float accumulation can land either side.
            Assert.InRange(frames, 25, 26);
        }
    }
}
