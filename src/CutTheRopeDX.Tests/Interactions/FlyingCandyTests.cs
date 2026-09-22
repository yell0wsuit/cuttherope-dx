using System;

using CutTheRopeDX.Framework;
using CutTheRopeDX.Framework.Core;
using CutTheRopeDX.Framework.Helpers;
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

        [Theory]
        [InlineData((int)CandyInteraction.Transport)]
        [InlineData((int)CandyInteraction.Pump)]
        [InlineData((int)CandyInteraction.Steam)]
        public void TransportAndPushersLeaveAFlyingCandyAloneUntilItsWingsBreak(int interactionValue)
        {
            CandyInteraction interaction = (CandyInteraction)interactionValue;
            GameScene scene = Scenario.New()
                .OmNom(160, 400)
                .Candy(80, 100, "first")
                .Candy(220, 200, "second", isDriven: true)
                .Build();
            CandyContext leader = scene.Candies()[0];
            CandyContext flier = scene.Candies()[1];
            Interaction.Hover(leader);

            Assert.DoesNotContain(flier.WholeBody, scene.ActiveBodies(interaction));
            Assert.Contains(leader.WholeBody, scene.ActiveBodies(interaction));

            Act.Eat(scene, leader);

            Assert.Contains(flier.WholeBody, scene.ActiveBodies(interaction));
        }

        [Fact]
        public void AHandTakesAFlyingCandyAndBreaksItsWings()
        {
            AssertCarrierGroundsTheFlier(
                s => s.Hand(20, 40, segmentLength: 20, segmentAngle: 90f),
                (scene, flier) => Act.GrabWithHand(scene, flier));
        }

        [Fact]
        public void AMouseTakesAFlyingCandyAndBreaksItsWings()
        {
            AssertCarrierGroundsTheFlier(
                s => s.Mouse(220, 200, index: 1).Mouse(300, 300, index: 2),
                (scene, flier) => Act.CarryByMouse(scene, flier));
        }

        [Fact]
        public void AntsTakeAFlyingCandyAndBreakItsWings()
        {
            AssertCarrierGroundsTheFlier(
                s => s.Ants(180, 200, path: "80,0"),
                (scene, flier) => Act.CarryByAnts(scene, flier));
        }

        [Fact]
        public void ALanternTakesAFlyingCandyAndBreaksItsWings()
        {
            AssertCarrierGroundsTheFlier(
                s => s.Lantern(20, 40),
                (scene, flier) => Act.CaptureInLantern(scene, flier));
        }

        /// <summary>
        /// A carrier taking a flying candy breaks its wings with the full burst, and leaves the
        /// leader where it was.
        /// </summary>
        private static void AssertCarrierGroundsTheFlier(Func<Scenario, Scenario> addCarrier, Action<GameScene, CandyContext> take)
        {
            GameScene scene = addCarrier(Scenario.New()
                    .OmNom(300, 440)
                    .Candy(80, 100, "first")
                    .Candy(220, 200, "second", isDriven: true))
                .Build();
            CandyContext leader = scene.Candies()[0];
            CandyContext flier = scene.Candies()[1];
            Interaction.Hover(leader);
            Assert.True(flier.IsFlying);

            take(scene, flier);

            Assert.False(flier.IsFlying);
            Assert.False(flier.Flight.Wings.visible);
            Assert.Equal(2, scene.WingsBreakEffectCount());
            Assert.True(leader.WholeBody.Point.pos.Y < Scenario.WorldY(120), "the leader should still be hovering where it started");
        }

        [Fact]
        public void AHatDoesNotSwallowAFlyingCandy()
        {
            GameScene scene = Scenario.New()
                .OmNom(300, 20)
                .Candy(80, 100, "first")
                .Candy(220, 200, "second", isDriven: true)
                .Hat(160, 300)
                .Hat(40, 40)
                .Build();
            CandyContext flier = scene.Candies()[1];
            Sock hat = scene.Hats()[0];

            // Keep the hat on the falling flier with its mouth turned into the candy's motion - what
            // swallows an ordinary candy within a frame or two.
            bool swallowed = Interaction.StepUntil(
                scene,
                () =>
                {
                    Act.MoveTo(hat, flier.WholeBody.Point.pos);
                    hat.rotation = Act.MouthAngleFacing(flier.WholeBody.Point.posDelta, hat.rotation);
                    hat.UpdateRotation();
                },
                () => flier.Lifecycle.Transport != null,
                maxFrames: 30);

            Assert.False(swallowed, "the hat swallowed the flying candy");
            Assert.True(flier.IsFlying);
        }

        [Fact]
        public void AFlyingCandyRejoinsItsLeaderFromAHatAtRest()
        {
            GameScene scene = Scenario.New()
                .OmNom(300, 20)
                .Candy(80, 100, "first")
                .Candy(220, 200, "second", isDriven: true)
                .Hat(80, 200)
                .Hat(280, 60)
                .Build();
            CandyContext leader = scene.Candies()[0];
            CandyContext flier = scene.Candies()[1];
            Vector startOffset = VectSub(flier.WholeBody.Point.pos, leader.WholeBody.Point.pos);

            Act.EnterHat(scene, leader);
            Assert.True(Interaction.StepUntil(scene, () => leader.Lifecycle.Transport == null), "the leader never left the far hat");
            Interaction.Hover(leader);

            // Rejoining is a jump across the level, taken in the same frame the leader comes out.
            // Read back as velocity it would throw the flier as far again past its leader, so the
            // check starts on that frame.
            for (int frame = 0; frame < 10; frame++)
            {
                Vector offset = VectSub(flier.WholeBody.Point.pos, leader.WholeBody.Point.pos);
                Assert.True(
                    MathF.Abs(offset.X - startOffset.X) < 5f && MathF.Abs(offset.Y - startOffset.Y) < 5f,
                    $"frame {frame}: the flier sits at {offset.X},{offset.Y} from its leader instead of {startOffset.X},{startOffset.Y}");
                HeadlessGame.StepFrames(scene, 1);
            }

            Assert.True(flier.IsFlying);
        }

        [Fact]
        public void ARocketRidesAFlyingCandyAndOnlyFliesItOnceTheWingsBreak()
        {
            GameScene scene = Scenario.New()
                .OmNom(160, 400)
                .Candy(80, 100, "first")
                .Candy(220, 200, "second", isDriven: true)
                .Rocket(260, 440, angle: 90f)
                .Build();
            CandyContext leader = scene.Candies()[0];
            CandyContext flier = scene.Candies()[1];
            Interaction.Hover(leader);

            Rocket rocket = Act.BindRocket(scene, flier);
            GameObject visual = flier.WholeBody.Visual;
            Vector held = new(visual.x, visual.y);
            HeadlessGame.StepFrames(scene, 30);

            // Bound and thrusting, but the flier is its leader's: it is drawn where it was. Each
            // frame's thrust lands on the point after it is drawn and the next follow step discards
            // it, so the drawn position is the one to read.
            Assert.Same(rocket, flier.Lifecycle.Attachments.Rocket);
            Assert.Equal(held.X, visual.x, 1f);
            Assert.Equal(held.Y, visual.y, 1.5f);

            Act.Eat(scene, leader);
            Assert.False(flier.IsFlying);
            Vector grounded = flier.WholeBody.Point.pos;
            HeadlessGame.StepFrames(scene, 20);

            Assert.True(
                VectDistance(grounded, flier.WholeBody.Point.pos) > 30f,
                "the rocket did not take the candy once its wings broke");
        }

        [Fact]
        public void AFlyingCandySetsOffABombButKeepsItsWingsAndPlace()
        {
            GameScene scene = Scenario.New()
                .OmNom(300, 20)
                .Candy(20, 20, "first")
                .Candy(220, 200, "second", isDriven: true)
                .Bomb(225, 205)
                .Build();
            CandyContext leader = scene.Candies()[0];
            CandyContext flier = scene.Candies()[1];
            CandyContext bomb = Assert.Single(scene.Bombs());
            // The leader sits outside the blast; one inside it would be thrown, and the flier with it.
            Interaction.Hover(leader);
            Assert.True(VectDistance(leader.WholeBody.Point.pos, bomb.WholeBody.Point.pos) > BombDefinition.BlastRadius);
            Vector start = flier.WholeBody.Point.pos;

            HeadlessGame.StepFrames(scene, 20);

            Assert.True(bomb.bomb.Exploded);
            Assert.True(flier.IsFlying);
            Assert.Equal(start.X, flier.WholeBody.Point.pos.X, 1f);
            Assert.Equal(start.Y, flier.WholeBody.Point.pos.Y, 1.5f);
        }

        [Fact]
        public void AMovingBouncerRunsAnExtraStepWhileACandyFlies()
        {
            float withFlier = BouncerTravel(isDriven: true);
            float without = BouncerTravel(isDriven: false);

            Assert.True(without > 5f, "the bouncer never moved");
            Assert.Equal(2f, withFlier / without, 0.1f);
        }

        private static float BouncerTravel(bool isDriven)
        {
            GameScene scene = Scenario.New()
                .OmNom(300, 20)
                .Candy(40, 100, "first")
                .Candy(80, 100, "second", isDriven: isDriven)
                .Bouncer(200, 400, size: 1, path: "100,0", moveSpeed: 20f)
                .Build();
            Interaction.Hover(scene.Candies()[0]);
            Interaction.Hover(scene.Candies()[1]);
            Bouncer bouncer = Assert.Single(scene.Bouncers());
            float startX = bouncer.x;

            HeadlessGame.StepFrames(scene, 20);

            return bouncer.x - startX;
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
