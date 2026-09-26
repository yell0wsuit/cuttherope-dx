using System;

using CutTheRopeDX.GameMain;

using Xunit;

namespace CutTheRopeDX.Tests.Interactions
{
    /// <summary>Mobile-reference trajectories involving a candy binding to a rocket.</summary>
    public sealed class MobileRocketTrajectoryTests
    {
        [Fact]
        public void RightSideApproachMovesLowerRocketDuringReelIn()
        {
            GameScene scene = ReferenceScene();
            CandyContext candy = scene.Candy();
            Rocket rocket = scene.Rockets()[0];
            float authoredRocketX = rocket.x;
            float maximumRocketX = authoredRocketX;
            StartReferencePath(scene, candy);

            Assert.True(
                Interaction.StepUntil(scene, () => candy.Lifecycle.Attachments.Rocket == rocket, maxFrames: 180),
                "the right-side approach never bound the candy to the lower rocket");

            for (int frame = 0; frame < 45; frame++)
            {
                HeadlessGame.StepFrames(scene, 1);
                maximumRocketX = MathF.Max(maximumRocketX, rocket.x);
            }

            // The iOS footage shows the lower rocket move down-right while reeling in the candy;
            // its red body is not pinned at the authored location.
            Assert.True(maximumRocketX > authoredRocketX + Scenario.Scale);
        }

        [Fact]
        public void RocketNearTheOriginIgnoresCandyOnTheFirstFrame()
        {
            // Before its first update the candy has no draw position, which reads as the map
            // origin; a rocket there must not take that for the candy and reel toward it.
            GameScene scene = Scenario.New()
                .MapSize(853, 480)
                .Design("useMobilePhysics", "true")
                .Candy(425, 160)
                .Rope(423, 110, length: 25)
                .Rocket(118, 62, angle: 180f, impulse: 20f)
                .Build();
            Rocket rocket = scene.Rockets()[0];

            HeadlessGame.StepFrames(scene, 1);

            Assert.Equal(Rocket.STATE_ROCKET_IDLE, rocket.state);
            Assert.Null(scene.Candy().Lifecycle.Attachments.Rocket);
        }

        private static GameScene ReferenceScene()
        {
            Scenario scenario = Scenario.New()
                .MapSize(320, 480)
                .Design("useMobilePhysics", "true")
                .Candy(291, 143)
                .Rope(289, 56, length: 80)
                .BambooTube(289, 268, TubeMouth.CatchesFalling)
                .Rocket(108, 265, angle: -90f, impulse: 25f, time: 0.58f, impulseFactor: 0.6f);
            return scenario
                .Bouncer(157, 331, size: 2)
                .OmNom(156, 425)
                .Build();
        }

        private static void StartReferencePath(GameScene scene, CandyContext candy)
        {
            Act.CutRope(scene, scene.Grabs()[0]);
            Assert.True(
                Interaction.StepUntil(scene, () => candy.Lifecycle.Transport?.BambooTube != null, maxFrames: 180),
                "the falling candy never entered the right bamboo tube");
        }
    }
}
