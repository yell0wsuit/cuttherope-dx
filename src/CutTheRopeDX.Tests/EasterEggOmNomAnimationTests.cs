using CutTheRopeDX.GameMain;

using Xunit;

namespace CutTheRopeDX.Tests
{
    /// <summary>
    /// Pins the easter egg's timeline: the dim fading up ahead of Om Nom, phase boundaries, the
    /// eye sweep, the squash envelope, the closing fade, and early dismissal.
    /// </summary>
    public sealed class EasterEggOmNomAnimationTests
    {
        private const float Step = 0.016f;

        private static EasterEggOmNomAnimation AdvancedTo(float milliseconds)
        {
            EasterEggOmNomAnimation animation = new();
            animation.Start();
            for (float elapsed = 0f; elapsed < milliseconds; elapsed += Step * 1000f)
            {
                animation.Update(Step);
            }
            return animation;
        }

        [Fact]
        public void IsInactiveBeforeStarting()
        {
            EasterEggOmNomAnimation animation = new();

            Assert.False(animation.IsActive);
        }

        [Fact]
        public void FadesTheDimInBeforeOmNomAppears()
        {
            EasterEggOmNomAnimation animation = AdvancedTo(100f);

            Assert.True(animation.IsActive);
            Assert.False(animation.CurrentFrame.ShowsOmNom);
            Assert.InRange(animation.CurrentFrame.Alpha, 0.3f, 0.7f);
        }

        [Fact]
        public void ShowsOmNomOnceTheDimIsFullyUp()
        {
            EasterEggOmNomAnimation animation = AdvancedTo(250f);

            Assert.True(animation.CurrentFrame.ShowsOmNom);
            Assert.Equal(1f, animation.CurrentFrame.Alpha, 3);
        }

        [Fact]
        public void SettlesOnSixAtTheEndOfTheRise()
        {
            // 200ms opening fade, then the 600ms rise.
            EasterEggOmNomAnimation animation = AdvancedTo(200f + 600f);

            Assert.Equal(6.0f, animation.CurrentFrame.ScaleX, 1);
        }

        [Fact]
        public void OvershootsSixDuringTheRise()
        {
            EasterEggOmNomAnimation animation = new();
            animation.Start();

            float peak = 0f;
            for (int i = 0; i < 60; i++)
            {
                animation.Update(Step);
                peak = System.MathF.Max(peak, animation.CurrentFrame.ScaleX);
            }

            Assert.True(peak > 6.0f, $"expected a rise overshoot above 6.0, peaked at {peak}");
        }

        [Fact]
        public void HoldsScaleThroughTheEyeSweep()
        {
            Assert.Equal(6.0f, AdvancedTo(200f + 1200f).CurrentFrame.ScaleX, 1);
            Assert.Equal(6.0f, AdvancedTo(200f + 2500f).CurrentFrame.ScaleX, 1);
        }

        [Fact]
        public void DelaysTheEyeSweepByOneHundredMilliseconds()
        {
            Assert.Equal(0f, AdvancedTo(200f + 650f).CurrentFrame.EyeOffset, 2);
        }

        [Fact]
        public void SweepsTheEyesLeftThenRightThenBack()
        {
            Assert.Equal(-25f, AdvancedTo(200f + 1000f).CurrentFrame.EyeOffset, 1);
            Assert.Equal(25f, AdvancedTo(200f + 1600f).CurrentFrame.EyeOffset, 1);
            Assert.Equal(0f, AdvancedTo(200f + 2300f).CurrentFrame.EyeOffset, 1);
        }

        [Fact]
        public void HoldsTheEyesCenteredAfterTheSweep()
        {
            Assert.Equal(0f, AdvancedTo(200f + 2600f).CurrentFrame.EyeOffset, 1);
        }

        [Fact]
        public void SquashesVerticallyOnlyBetweenTheRiseAndTheHold()
        {
            EasterEggOmNomAnimation flat = AdvancedTo(200f + 600f);
            Assert.Equal(flat.CurrentFrame.ScaleX, flat.CurrentFrame.ScaleY, 2);

            EasterEggOmNomAnimation squashed = AdvancedTo(200f + 1600f);
            Assert.True(
                squashed.CurrentFrame.ScaleY > squashed.CurrentFrame.ScaleX,
                "expected the squash to stretch the Y scale at the apex");

            EasterEggOmNomAnimation settled = AdvancedTo(200f + 2800f);
            Assert.Equal(settled.CurrentFrame.ScaleX, settled.CurrentFrame.ScaleY, 2);
        }

        [Fact]
        public void PlacesHimFromTheUnsquashedScale()
        {
            // At the squash apex the Y scale is inflated, but placement must not move with it.
            EasterEggOmNomAnimation apex = AdvancedTo(200f + 1600f);
            // Sampled inside the hold: at 2800ms the fixed tick already lands in the sink.
            EasterEggOmNomAnimation hold = AdvancedTo(200f + 2500f);

            Assert.Equal(hold.CurrentFrame.Y, apex.CurrentFrame.Y, 1);
            Assert.Equal(1250f - 500f, apex.CurrentFrame.X, 1);
            Assert.Equal(1500f - 1000f, apex.CurrentFrame.Y, 1);
        }

        [Fact]
        public void SinksAndShrinksOnTheWayOut()
        {
            EasterEggOmNomAnimation animation = AdvancedTo(200f + 3600f);

            Assert.Equal(0.1f, animation.CurrentFrame.ScaleX, 1);
        }

        [Fact]
        public void KeepsDrawingThroughTheClosingFade()
        {
            EasterEggOmNomAnimation midFade = AdvancedTo(200f + 3600f + 100f);

            Assert.True(midFade.IsActive);
            Assert.InRange(midFade.CurrentFrame.Alpha, 0.01f, 0.99f);
        }

        [Fact]
        public void FreezesGameplayUntilTheClosingFade()
        {
            Assert.False(new EasterEggOmNomAnimation().FreezesGameplay);
            Assert.True(AdvancedTo(100f).FreezesGameplay);
            Assert.True(AdvancedTo(200f + 3000f).FreezesGameplay);
            Assert.False(AdvancedTo(200f + 3600f + 100f).FreezesGameplay);
        }

        [Fact]
        public void ReleasesGameplayAsSoonAsADismissalStarts()
        {
            EasterEggOmNomAnimation animation = AdvancedTo(200f + 1000f);

            Assert.True(animation.Cancel());

            Assert.False(animation.FreezesGameplay);
            Assert.True(animation.IsActive);
        }

        [Fact]
        public void GoesInactiveAfterTheClosingFade()
        {
            EasterEggOmNomAnimation animation = AdvancedTo(200f + 3600f + 300f);

            Assert.False(animation.IsActive);
        }

        [Fact]
        public void RestartingResetsTheTimeline()
        {
            EasterEggOmNomAnimation animation = AdvancedTo(200f + 2000f);
            animation.Start();

            Assert.True(animation.IsActive);
            Assert.False(animation.CurrentFrame.ShowsOmNom);
            Assert.Equal(0f, animation.CurrentFrame.Alpha, 3);
        }

        [Fact]
        public void CancelFadesOutFromTheCurrentPose()
        {
            EasterEggOmNomAnimation animation = AdvancedTo(200f + 1200f);
            EasterEggOmNomFrame before = animation.CurrentFrame;

            Assert.True(animation.Cancel());
            animation.Update(0.1f);

            Assert.True(animation.IsActive);
            Assert.Equal(0.5f, animation.CurrentFrame.Alpha, 2);
            Assert.Equal(before.ScaleX, animation.CurrentFrame.ScaleX);
            Assert.Equal(before.EyeOffset, animation.CurrentFrame.EyeOffset);

            animation.Update(0.11f);

            Assert.False(animation.IsActive);
        }

        [Fact]
        public void CancelDoesNothingForTheFirst600Milliseconds()
        {
            // While only the dim is up.
            Assert.False(AdvancedTo(100f).Cancel());

            // Showing, but early in the spring-up.
            EasterEggOmNomAnimation rising = AdvancedTo(500f);
            Assert.True(rising.CurrentFrame.ShowsOmNom);
            Assert.False(rising.Cancel());
            Assert.True(rising.FreezesGameplay);

            // Past 600ms.
            Assert.True(AdvancedTo(650f).Cancel());
        }

        [Fact]
        public void CancelDoesNothingWhenIdle()
        {
            EasterEggOmNomAnimation animation = new();

            Assert.False(animation.Cancel());
            Assert.False(animation.IsActive);
        }

        [Fact]
        public void CancelDoesNotInterruptTheClosingFade()
        {
            EasterEggOmNomAnimation animation = AdvancedTo(200f + 3600f + 50f);

            Assert.False(animation.Cancel());
            Assert.True(animation.IsActive);
        }

        [Fact]
        public void CancellingTwiceDoesNotRestartTheFade()
        {
            EasterEggOmNomAnimation animation = AdvancedTo(200f + 1000f);
            Assert.True(animation.Cancel());
            animation.Update(0.1f);

            Assert.False(animation.Cancel());
            animation.Update(0.11f);

            Assert.False(animation.IsActive);
        }
    }
}
