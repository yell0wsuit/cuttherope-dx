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
            bool wasHeld = false;
            Vector heldAt = default;
            for (int frame = 0; frame < 45; frame++)
            {
                HeadlessGame.StepFrames(scene, 1);
                hovered |= flier.Flight.Hovering;
                maxReach = MathF.Max(maxReach, VectDistance(rope.bungeeAnchor.pos, flier.WholeBody.Point.pos));

                // Held back, it stays exactly where it is: the rope settles around it rather than
                // reeling it in, which would make it saw up and down against its leader.
                if (wasHeld && flier.Flight.Hovering)
                {
                    Assert.Equal(heldAt.X, flier.WholeBody.Point.pos.X, 0.001f);
                    Assert.Equal(heldAt.Y, flier.WholeBody.Point.pos.Y, 0.001f);
                }
                wasHeld = flier.Flight.Hovering;
                heldAt = flier.WholeBody.Point.pos;
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
        public void AFlyingCandyStaysPutWhileALanternHoldsItsLeader()
        {
            // A lantern holds the leader at its own spot and hands it to its pair, clear across the
            // level; copying that would teleport the flying candy with it.
            GameScene scene = Scenario.New()
                .OmNom(100, 416)
                .Candy(40, 88, "0")
                .Candy(200, 104, "1", isDriven: true)
                .Lantern(44, 284)
                .Lantern(332, 156)
                .Build();
            CandyContext leader = scene.Candies()[0];
            CandyContext flier = scene.Candies()[1];

            Assert.True(
                Interaction.StepUntil(scene, () => leader.Lifecycle.Attachments.InLantern),
                "the lantern never took the leader");
            Vector heldAt = flier.WholeBody.Point.pos;
            float leaderX = leader.WholeBody.Point.pos.X;

            HeadlessGame.StepFrames(scene, 60);

            Assert.True(MathF.Abs(leader.WholeBody.Point.pos.X - leaderX) > 200f, "the lantern never carried the leader away");
            Assert.Equal(heldAt.X, flier.WholeBody.Point.pos.X, 1f);
            Assert.Equal(heldAt.Y, flier.WholeBody.Point.pos.Y, 1f);
            Assert.True(flier.IsFlying);
        }

        [Fact]
        public void AFlyingCandyStaysPutWhileAMouseHoldsItsLeader()
        {
            GameScene scene = Scenario.New()
                .OmNom(160, 460)
                .Candy(60, 120, "first")
                .Candy(260, 60, "second", isDriven: true)
                .Mouse(60, 120, index: 1)
                .Mouse(300, 430, index: 2)
                .Build();
            CandyContext leader = scene.Candies()[0];
            CandyContext flier = scene.Candies()[1];

            _ = Act.CarryByMouse(scene, leader);
            Vector heldAt = flier.WholeBody.Point.pos;

            HeadlessGame.StepFrames(scene, 60);

            Assert.Equal(heldAt.X, flier.WholeBody.Point.pos.X, 1f);
            Assert.Equal(heldAt.Y, flier.WholeBody.Point.pos.Y, 1f);
            Assert.True(flier.IsFlying);
        }

        [Fact]
        public void AHandCarryingTheLeaderTakesTheFlyingCandyAlong()
        {
            GameScene scene = Scenario.New()
                .OmNom(300, 440)
                .Candy(80, 100, "first")
                .Candy(220, 200, "second", isDriven: true)
                .Hand(40, 40, segmentLength: 20, segmentAngle: 90f)
                .Build();
            CandyContext leader = scene.Candies()[0];
            CandyContext flier = scene.Candies()[1];
            MechanicalHand hand = Act.GrabWithHand(scene, leader);

            // Swinging the arm walks the leader across, and the flying candy copies every step.
            Vector startFlier = flier.WholeBody.Point.pos;
            Vector startLeader = leader.WholeBody.Point.pos;
            for (int frame = 0; frame < 30; frame++)
            {
                Vector claw = hand.ClawPosition();
                Act.MoveClawTo(hand, new Vector(claw.X + 6f, claw.Y + 3f));
                HeadlessGame.StepFrames(scene, 1);
            }

            // It tracks its leader a step behind, so the two cover the same ground.
            Vector leaderTravel = VectSub(leader.WholeBody.Point.pos, startLeader);
            Vector flierTravel = VectSub(flier.WholeBody.Point.pos, startFlier);
            Assert.True(
                MathF.Sqrt((leaderTravel.X * leaderTravel.X) + (leaderTravel.Y * leaderTravel.Y)) > 50f,
                "the hand never carried the leader anywhere");
            Assert.True(
                MathF.Abs(flierTravel.X - leaderTravel.X) < 10f && MathF.Abs(flierTravel.Y - leaderTravel.Y) < 10f,
                $"the flier covered {flierTravel.X},{flierTravel.Y} where its leader covered {leaderTravel.X},{leaderTravel.Y}");
            Assert.True(flier.IsFlying);
        }

        [Fact]
        public void AFlyingCandyPicksTheChaseBackUpSmoothlyWhenItsLeaderIsDropped()
        {
            GameScene scene = Scenario.New()
                .OmNom(300, 440)
                .Candy(80, 100, "first")
                .Candy(220, 200, "second", isDriven: true)
                .Hand(40, 40, segmentLength: 20, segmentAngle: 90f)
                .Build();
            CandyContext leader = scene.Candies()[0];
            CandyContext flier = scene.Candies()[1];
            MechanicalHand hand = Act.GrabWithHand(scene, leader);

            // Held for a while: the flying candy hangs still, so it banks no speed of its own.
            HeadlessGame.StepFrames(scene, 60);
            Act.TapClaw(scene, hand);
            Assert.True(
                Interaction.StepUntil(scene, () => leader.Lifecycle.Attachments.Hand == null),
                "the claw never let the leader go");

            // Following again, it tracks the leader step for step instead of lurching after it.
            Vector lastFlier = flier.WholeBody.Point.pos;
            Vector lastLeader = leader.WholeBody.Point.pos;
            for (int frame = 0; frame < 10; frame++)
            {
                HeadlessGame.StepFrames(scene, 1);
                Vector flierStep = VectSub(flier.WholeBody.Point.pos, lastFlier);
                Vector leaderStep = VectSub(leader.WholeBody.Point.pos, lastLeader);
                Assert.True(
                    MathF.Abs(flierStep.X - leaderStep.X) < 5f && MathF.Abs(flierStep.Y - leaderStep.Y) < 5f,
                    $"frame {frame}: the flier moved {flierStep.X},{flierStep.Y} where its leader moved {leaderStep.X},{leaderStep.Y}");
                lastFlier = flier.WholeBody.Point.pos;
                lastLeader = leader.WholeBody.Point.pos;
            }

            Assert.True(flier.IsFlying);
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
        public void FlyingCandiesWithNothingToFollowPlayAsOrdinaryCandies()
        {
            GameScene scene = Scenario.New()
                .OmNom(300, 440)
                .Candy(80, 100, "first", isDriven: true)
                .Candy(220, 200, "second", isDriven: true)
                .Build();

            Assert.All(scene.Candies(), candy => Assert.Null(candy.Flight));
        }

        [Fact]
        public void TwoFlyingCandiesBothFollowTheOrdinaryOne()
        {
            GameScene scene = Scenario.New()
                .OmNom(300, 440)
                .Candy(80, 100, "first", isDriven: true)
                .Candy(160, 300, "second")
                .Candy(220, 200, "third", isDriven: true)
                .Build();
            CandyContext ordinary = scene.Candies()[1];

            Assert.Same(ordinary, scene.Candies()[0].Flight.Leader);
            Assert.Same(ordinary, scene.Candies()[2].Flight.Leader);
            Assert.Null(ordinary.Flight);
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
        [InlineData((int)CandyInteraction.Pump)]
        [InlineData((int)CandyInteraction.Steam)]
        public void PushersLeaveAFlyingCandyAloneUntilItsWingsBreak(int interactionValue)
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
                Act.CarryByAnts);
        }

        [Fact]
        public void ALanternTakesAFlyingCandyAndBreaksItsWings()
        {
            AssertCarrierGroundsTheFlier(
                s => s.Lantern(20, 40),
                Act.CaptureInLantern);
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
        public void AHatThrowsAFlyingCandyBackBesideItsLeader()
        {
            AssertTransportThrowsTheFlierBack(
                s => s.Hat(160, 300).Hat(40, 40),
                Act.EnterHat);
        }

        [Fact]
        public void ABambooTubeThrowsAFlyingCandyBackBesideItsLeader()
        {
            AssertTransportThrowsTheFlierBack(
                s => s.BambooTube(160, 300, TubeMouth.CatchesFalling),
                (scene, flier) => Act.EnterBambooTube(scene, flier, TubeMouth.CatchesFalling));
        }

        /// <summary>
        /// As in Time Travel, a sock or tube catches a flying candy and drops its ropes, but the step
        /// after it comes out pulls it straight back beside its leader, still flying - and at rest,
        /// not flung by the jump.
        /// </summary>
        private static void AssertTransportThrowsTheFlierBack(Func<Scenario, Scenario> addTransport, Action<GameScene, CandyContext> enter)
        {
            GameScene scene = addTransport(Scenario.New()
                    .OmNom(300, 440)
                    .Candy(80, 100, "first")
                    .Candy(220, 200, "second", isDriven: true)
                    .Rope(220, 150, 60, candyNumber: "second"))
                .Build();
            CandyContext leader = scene.Candies()[0];
            CandyContext flier = scene.Candies()[1];
            Interaction.Hover(leader);
            Vector startOffset = VectSub(flier.WholeBody.Point.pos, leader.WholeBody.Point.pos);
            Assert.Equal(1, scene.AttachedRopeCount(flier));

            enter(scene, flier);
            Assert.True(Interaction.StepUntil(scene, () => flier.Lifecycle.Transport == null), "the flying candy never came out");

            // It comes out and is pulled back within one frame, so the check starts on that frame.
            for (int frame = 0; frame < 5; frame++)
            {
                Vector offset = VectSub(flier.WholeBody.Point.pos, leader.WholeBody.Point.pos);
                Assert.True(
                    MathF.Abs(offset.X - startOffset.X) < 5f && MathF.Abs(offset.Y - startOffset.Y) < 5f,
                    $"frame {frame}: the flier sits at {offset.X},{offset.Y} from its leader instead of {startOffset.X},{startOffset.Y}");
                HeadlessGame.StepFrames(scene, 1);
            }

            Assert.True(flier.IsFlying);
            Assert.Equal(0, scene.AttachedRopeCount(flier));
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
