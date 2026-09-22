using System;

using CutTheRopeDX.Framework;
using CutTheRopeDX.Framework.Core;
using CutTheRopeDX.GameMain;

using Xunit;

namespace CutTheRopeDX.Tests.Interactions
{
    /// <summary>
    /// Time Travel's flying candy (<c>isDriven</c>): it copies the other candy's movements, ropes
    /// and bouncers can stop it, and it drops as an ordinary candy once any candy leaves play.
    /// </summary>
    public sealed class FlyingCandyTests
    {
        [Fact]
        public void AFlyingCandyRepeatsItsLeadersMovement()
        {
            GameScene scene = Scenario.New()
                .OmNom(300, 20)
                .Candy(80, 100, "first")
                .Candy(220, 200, "second", isDriven: true)
                .Build();
            CandyContext leader = scene.Candies()[0];
            CandyContext flier = scene.Candies()[1];
            Vector startOffset = VectSub(flier.WholeBody.Point.pos, leader.WholeBody.Point.pos);
            float startX = flier.WholeBody.Point.pos.X;
            float startY = flier.WholeBody.Point.pos.Y;

            // A sideways shove only the leader gets: gravity alone would move both candies alike.
            leader.WholeBody.Point.prevPos = VectSub(leader.WholeBody.Point.pos, new Vector(4f, 0f));
            HeadlessGame.StepFrames(scene, 20);

            Assert.True(flier.IsFlying);
            Assert.Same(leader, flier.Flight.Leader);
            Vector offset = VectSub(flier.WholeBody.Point.pos, leader.WholeBody.Point.pos);
            Assert.Equal(startOffset.X, offset.X, 0.5f);
            Assert.Equal(startOffset.Y, offset.Y, 1.5f);
            Assert.True(flier.WholeBody.Point.pos.Y - startY > 20f, "the flying candy did not follow its falling leader");
            Assert.True(flier.WholeBody.Point.pos.X - startX > 20f, "the flying candy did not follow its leader sideways");
        }

        [Fact]
        public void AFlyingCandyDoesNotFallOnItsOwn()
        {
            GameScene scene = Scenario.New()
                .OmNom(300, 20)
                .Candy(80, 100, "first")
                .Candy(220, 200, "second", isDriven: true)
                .Build();
            CandyContext leader = scene.Candies()[0];
            CandyContext flier = scene.Candies()[1];
            Interaction.Hover(leader);
            Vector start = flier.WholeBody.Point.pos;

            HeadlessGame.StepFrames(scene, 60);

            Assert.Equal(start.X, flier.WholeBody.Point.pos.X, 0.5f);
            Assert.Equal(start.Y, flier.WholeBody.Point.pos.Y, 1.5f);
        }

        [Fact]
        public void ARopeHoldsAFlyingCandyBackAtItsStretchAllowance()
        {
            GameScene scene = Scenario.New()
                .OmNom(300, 20)
                .Candy(80, 100, "first")
                .Candy(220, 200, "second", isDriven: true)
                .Rope(220, 170, 60, candyNumber: "second")
                .Build();
            CandyContext leader = scene.Candies()[0];
            CandyContext flier = scene.Candies()[1];
            Vector startOffset = VectSub(flier.WholeBody.Point.pos, leader.WholeBody.Point.pos);
            Bungee rope = Assert.Single(scene.Grabs()).RopeOf();
            float allowance = (rope.parts.Count - 1)
                * ActivePhysicsConstants.BungeeRestLength
                * CandyFlightDefinition.RopeStretchAllowance;

            // The allowance is checked where the candy is about to be placed, and the candy still
            // integrates that frame, so it may overshoot by one frame's motion before the rope
            // takes it back - but it never runs on after its leader.
            bool hovered = false;
            float maxReach = 0f;
            for (int frame = 0; frame < 45; frame++)
            {
                HeadlessGame.StepFrames(scene, 1);
                hovered |= flier.Flight.Hovering;
                maxReach = MathF.Max(maxReach, VectDistance(rope.bungeeAnchor.pos, flier.WholeBody.Point.pos));
            }

            Assert.True(hovered, "the rope never held the flying candy back");
            Assert.True(maxReach < allowance * 1.05f, $"the rope let the flying candy reach {maxReach}, allowance {allowance}");
            Vector offset = VectSub(flier.WholeBody.Point.pos, leader.WholeBody.Point.pos);
            Assert.True(startOffset.Y - offset.Y > 100f, "the flying candy kept pace with its leader past the rope");
            Assert.True(flier.IsFlying);
        }

        [Fact]
        public void ABouncerBlocksAFlyingCandyInsteadOfBouncingIt()
        {
            GameScene scene = Scenario.New()
                .OmNom(300, 20)
                .Candy(80, 100, "first")
                .Candy(220, 200, "second", isDriven: true)
                .Bouncer(220, 260)
                .Build();
            CandyContext leader = scene.Candies()[0];
            CandyContext flier = scene.Candies()[1];
            float bouncerY = Scenario.WorldY(260);
            float startY = flier.WholeBody.Point.pos.Y;

            for (int frame = 0; frame < 40; frame++)
            {
                HeadlessGame.StepFrames(scene, 1);
                Assert.True(flier.WholeBody.Point.pos.Y < bouncerY, $"frame {frame}: the flying candy passed the bouncer");
                Assert.True(flier.WholeBody.Point.pos.Y >= startY - 1f, $"frame {frame}: the bouncer threw the flying candy back up");
            }

            Assert.True(flier.IsFlying);
            Assert.True(leader.WholeBody.Point.pos.Y - Scenario.WorldY(100) > 100f, "the leader stopped falling too");
        }

        [Fact]
        public void EatingTheLeaderBreaksTheWings()
        {
            GameScene scene = Scenario.New()
                .OmNom(160, 400)
                .Candy(80, 100, "first")
                .Candy(220, 200, "second", isDriven: true)
                .Build();
            CandyContext leader = scene.Candies()[0];
            CandyContext flier = scene.Candies()[1];
            Interaction.Hover(leader);

            Act.Eat(scene, leader);

            Assert.False(flier.IsFlying);
            Assert.False(flier.Flight.Wings.visible);
            Assert.Equal(2, scene.WingsBreakEffectCount());

            // Grounded, it falls like any other candy.
            float y = flier.WholeBody.Point.pos.Y;
            HeadlessGame.StepFrames(scene, 10);
            Assert.True(flier.WholeBody.Point.pos.Y > y + 5f, "the grounded candy did not fall");
        }

        [Fact]
        public void LosingTheLeaderOffScreenGroundsTheFlierWithoutABurst()
        {
            GameScene scene = Scenario.New()
                .OmNom(300, 20)
                .Candy(80, 100, "first")
                .Candy(220, 200, "second", isDriven: true)
                .Build();
            CandyContext leader = scene.Candies()[0];
            CandyContext flier = scene.Candies()[1];

            Act.LoseOffScreen(scene, leader);

            Assert.False(flier.IsFlying);
            Assert.Equal(0, scene.WingsBreakEffectCount());
        }

        [Fact]
        public void ACandyWithoutIsDrivenHasNoFlight()
        {
            GameScene scene = Scenario.New()
                .OmNom(300, 20)
                .Candy(80, 100, "first")
                .Candy(220, 200, "second")
                .Build();

            Assert.All(scene.Candies(), candy => Assert.Null(candy.Flight));
        }

        private static Vector VectSub(Vector a, Vector b)
        {
            return new Vector(a.X - b.X, a.Y - b.Y);
        }

        private static float VectDistance(Vector a, Vector b)
        {
            Vector d = VectSub(a, b);
            return MathF.Sqrt((d.X * d.X) + (d.Y * d.Y));
        }
    }
}
