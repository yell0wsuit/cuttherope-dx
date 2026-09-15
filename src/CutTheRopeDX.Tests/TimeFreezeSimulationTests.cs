using System;
using System.Linq;

using CutTheRopeDX.Framework;
using CutTheRopeDX.Framework.Core;
using CutTheRopeDX.Framework.Media;
using CutTheRopeDX.Framework.Physics;
using CutTheRopeDX.Framework.Visual;
using CutTheRopeDX.GameMain;
using CutTheRopeDX.Tests.Interactions;

using Xunit;

using static CutTheRopeDX.Framework.Helpers.CTRMathHelper;

namespace CutTheRopeDX.Tests
{
    public class TimeFreezeSimulationTests
    {
        private static GameScene FrozenSceneWithFallingCandy()
        {
            Scenario scenario = Scenario.New().Candy(160, 100).OmNom(160, 400).PauseSwitcher(60, 400);
            GameScene scene = scenario.Build();
            HeadlessGame.StepFrames(scene, 5);
            Freeze(scene);
            HeadlessGame.StepFrames(scene, 1);
            return scene;
        }

        private static void Freeze(GameScene scene)
        {
            Vector button = scene.ScreenPositionOf(scene.PauseSwitchers()[0]);
            _ = scene.TouchDownXYIndex(button.X, button.Y, 0);
            _ = scene.TouchUpXYIndex(button.X, button.Y, 0);
        }

        [Fact]
        public void CandyDoesNotDriftWhileFrozen()
        {
            GameScene scene = FrozenSceneWithFallingCandy();
            Vector before = scene.Candy().WholeBody.Point.pos;

            HeadlessGame.StepFrames(scene, 120);

            Vector after = scene.Candy().WholeBody.Point.pos;
            Assert.Equal(before.X, after.X, 3);
            Assert.Equal(before.Y, after.Y, 3);
        }

        [Fact]
        public void FrozenCandySkipsNormalPointIntegrationBeforeTheFinalHold()
        {
            GameScene scene = FrozenSceneWithFallingCandy();
            ConstraintedPoint point = scene.Candy().WholeBody.Point;
            point.totalForce = new Vector(123f, 456f);

            HeadlessGame.StepFrames(scene, 1);

            Assert.Equal(new Vector(123f, 456f), point.totalForce);
            Assert.Equal(default, point.a);
            Assert.Equal(default, point.v);
            Assert.Equal(default, point.posDelta);
        }

        [Fact]
        public void RopeSegmentsKeepSimulatingWhileFrozenCandyStaysPinned()
        {
            GameScene scene = Scenario.New()
                .Candy(160, 240, "first")
                .Rope(160, 100, 170, "first")
                .OmNom(160, 440)
                .PauseSwitcher(60, 440)
                .Build();
            HeadlessGame.StepFrames(scene, 5);
            Freeze(scene);
            Bungee rope = Assert.Single(scene.RegisteredRopes()).Rope;
            HeadlessGame.StepFrames(scene, 1);
            ConstraintedPoint middle = rope.parts[rope.parts.Count / 2];
            middle.prevPos = middle.pos;
            middle.pos = new Vector(middle.pos.X + 30f, middle.pos.Y);
            Vector displaced = middle.pos;
            Vector candyAt = scene.Candy().WholeBody.Point.pos;

            HeadlessGame.StepFrames(scene, 1);

            Assert.NotEqual(displaced, middle.pos);
            Assert.Equal(candyAt.X, scene.Candy().WholeBody.Point.pos.X, 3);
            Assert.Equal(candyAt.Y, scene.Candy().WholeBody.Point.pos.Y, 3);
        }

        [Fact]
        public void FrozenAxeHoldsItsBladeSpinAndBubbleAnimation()
        {
            GameScene scene = Scenario.New()
                .Candy(60, 100)
                .Axe(200, 180)
                .OmNom(160, 440)
                .PauseSwitcher(60, 440)
                .Build();
            CandyContext axeContext = Assert.Single(scene.Candies(), context => context.Capabilities == CandyCapabilities.Axe);
            Axe axe = Assert.IsType<Axe>(axeContext.WholeBody.Visual);
            axe.constraint.v = new Vector(100f, 0f);
            Freeze(scene);
            float rotation = axe.GetChild(1).rotation;
            float bubbleTime = axe.bubbleAnimation.GetTimeline(0).time;

            HeadlessGame.StepFrames(scene, 4);

            Assert.Equal(rotation, axe.GetChild(1).rotation);
            Assert.Equal(bubbleTime, axe.bubbleAnimation.GetTimeline(0).time);
        }

        [Fact]
        public void FrozenCandyBubbleAnimationHoldsItsFrameUntilTimeResumes()
        {
            // Time Travel clears the updateable flag on a bubbled candy's bubble animation when the
            // pause switcher stops time, and sets it again when time resumes.
            GameScene scene = Scenario.New()
                .Candy(160, 200)
                .Bubble(160, 200)
                .OmNom(160, 440)
                .PauseSwitcher(60, 440)
                .Build();
            CandyContext candy = scene.Candy();
            _ = Act.CaptureInBubble(scene, candy);
            Freeze(scene);
            Timeline timeline = candy.WholeBody.BubbleAnimation.GetTimeline(0);
            float before = timeline.time;

            HeadlessGame.StepFrames(scene, 4);

            Assert.Equal(before, timeline.time);

            Freeze(scene);
            HeadlessGame.StepFrames(scene, 1);

            Assert.NotEqual(before, timeline.time);
        }

        [Fact]
        public void FreezeStopsParticleUpdatesWithoutChangingDrawVisibility()
        {
            GameScene scene = Scenario.New()
                .Candy(160, 100)
                .OmNom(160, 400)
                .PauseSwitcher(60, 440)
                .Build();
            AnimationsPool pool = scene.ParticleAnimations();
            bool authoredVisibility = pool.visible;

            Freeze(scene);

            Assert.False(pool.updateable);
            Assert.Equal(authoredVisibility, pool.visible);

            Freeze(scene);

            Assert.True(pool.updateable);
            Assert.Equal(authoredVisibility, pool.visible);
        }

        [Fact]
        public void CandyFallsAgainAfterUnfreezing()
        {
            GameScene scene = FrozenSceneWithFallingCandy();
            HeadlessGame.StepFrames(scene, 30);
            Vector frozenAt = scene.Candy().WholeBody.Point.pos;

            Freeze(scene);
            HeadlessGame.StepFrames(scene, 30);

            Assert.True(scene.Candy().WholeBody.Point.pos.Y > frozenAt.Y);
        }

        [Fact]
        public void MovingSpikesHoldStillWhileFrozen()
        {
            Scenario scenario = Scenario.New()
                .Candy(160, 100)
                .OmNom(160, 440)
                .MovingSpikes(160, 300)
                .PauseSwitcher(60, 440);
            GameScene scene = scenario.Build();
            Spikes moving = scene.SpikeStrips()[0];
            HeadlessGame.StepFrames(scene, 20);
            Assert.NotNull(moving.mover);
            Freeze(scene);
            float x = moving.x;
            float y = moving.y;

            HeadlessGame.StepFrames(scene, 120);

            Assert.Equal(x, moving.x, 3);
            Assert.Equal(y, moving.y, 3);
        }

        [Fact]
        public void OmNomDoesNotOpenHisMouthWhileFrozen()
        {
            GameScene scene = Scenario.New()
                .Candy(160, 100)
                .OmNom(160, 400)
                .PauseSwitcher(60, 440)
                .Build();
            TargetContext target = scene.Targets()[0];
            Freeze(scene);
            Interaction.PlaceCandyAt(
                scene.Candy(),
                new Vector(target.targetObject.x, target.targetObject.y - 100f));

            HeadlessGame.StepFrames(scene, 2);

            Assert.Equal(TargetFeedingPhase.Idle, target.Feeding.Phase);
        }

        [Fact]
        public void OmNomAnimationHoldsItsCurrentFrameUntilTimeResumes()
        {
            GameScene scene = Scenario.New()
                .Candy(160, 100)
                .OmNom(160, 400)
                .PauseSwitcher(60, 440)
                .Build();
            Timeline timeline = Assert.IsType<Timeline>(scene.Targets()[0].targetObject.GetCurrentTimeline());
            HeadlessGame.StepFrames(scene, 4);
            Freeze(scene);
            float frozenAt = timeline.time;

            HeadlessGame.StepFrames(scene, 4);

            Assert.Equal(frozenAt, timeline.time);

            Freeze(scene);
            HeadlessGame.StepFrames(scene, 1);

            Assert.True(timeline.time > frozenAt);
        }

        [Fact]
        public void BothTimeTravelOmNomsHoldTheirCurrentFramesWhileFrozen()
        {
            GameScene scene = Scenario.New()
                .Candy(160, 100)
                .OmNom(100, 400)
                .OmNom(220, 400)
                .PauseSwitcher(60, 440)
                .Build();
            Timeline first = Assert.IsType<Timeline>(scene.Targets()[0].targetObject.GetCurrentTimeline());
            Timeline second = Assert.IsType<Timeline>(scene.Targets()[1].targetObject.GetCurrentTimeline());
            HeadlessGame.StepFrames(scene, 4);
            Freeze(scene);
            float firstFrozenAt = first.time;
            float secondFrozenAt = second.time;

            HeadlessGame.StepFrames(scene, 4);

            Assert.Equal(firstFrozenAt, first.time);
            Assert.Equal(secondFrozenAt, second.time);

            Freeze(scene);
            HeadlessGame.StepFrames(scene, 1);

            Assert.True(first.time > firstFrozenAt);
            Assert.True(second.time > secondFrozenAt);
        }

        [Fact]
        public void IdleRocketDoesNotBindCandyWhileFrozen()
        {
            GameScene scene = Scenario.New()
                .Candy(160, 200)
                .OmNom(160, 440)
                .Rocket(160, 200)
                .PauseSwitcher(60, 440)
                .Build();
            CandyContext candy = scene.Candy();
            Rocket rocket = scene.Rockets()[0];
            Interaction.Hover(candy);
            Interaction.PlaceCandyAt(candy, Interaction.At(rocket.x, rocket.y));
            Freeze(scene);

            HeadlessGame.StepFrames(scene, 2);

            Assert.False(candy.Lifecycle.Attachments.HasActiveRocket);
        }

        [Fact]
        public void RocketControlPointKeepsTheDesktopWeightWithoutTheTimeTravelFlag()
        {
            // The originals use 0.5, but desktop deliberately runs the point heavier so constraint
            // forces from connected rope points cannot drift the rocket off its mount. A map only
            // gets the original weight by asking for it through useTimeTravelRocketPhysics.
            GameScene scene = Scenario.New()
                .Candy(60, 100)
                .OmNom(160, 440)
                .Rocket(220, 200)
                .PauseSwitcher(60, 440)
                .Build();

            Rocket rocket = Assert.Single(scene.Rockets());

            Assert.Equal(2.5f, rocket.point.weight);
        }

        [Fact]
        public void ExperimentsRocketKeepsItsAuthoredImpulseInDx()
        {
            _ = Scenario.New()
                .Candy(60, 100)
                .OmNom(160, 440)
                .Rocket(220, 200, impulse: 20f)
                .Build();

            Assert.False(ActivePhysicsConstants.UseMobilePhysicsModel);
            Assert.Equal(200f, ActivePhysicsConstants.RocketReelSpeed);
            Assert.False(ActivePhysicsConstants.UseTimeTravelRocketModel);
            Assert.Equal(1f, ActivePhysicsConstants.RocketImpulseScale);
        }

        [Fact]
        public void MobileExperimentsRocketScalesItsAuthoredImpulseIntoDxWorldCoordinates()
        {
            _ = Scenario.New()
                .Design("useMobilePhysics", "true")
                .Candy(60, 100)
                .OmNom(160, 440)
                .Rocket(220, 200, impulse: 20f)
                .Build();

            Assert.True(ActivePhysicsConstants.UseMobilePhysicsModel);
            Assert.False(ActivePhysicsConstants.UseTimeTravelRocketModel);
            Assert.Equal(Scenario.Scale, ActivePhysicsConstants.RocketImpulseScale);
        }

        [Fact]
        public void TimeTravelRocketScalesItsAuthoredImpulseIntoDxWorldCoordinates()
        {
            GameScene scene = Scenario.New()
                .Design("useMobilePhysics", "true")
                .Design("useTimeTravelRocketPhysics", "true")
                .Candy(60, 100)
                .OmNom(160, 440)
                .Rocket(220, 200, impulse: 5f, impulseFactor: 0.6f)
                .Build();

            Rocket rocket = Assert.Single(scene.Rockets());
            Assert.True(ActivePhysicsConstants.UseTimeTravelRocketModel);
            Assert.Equal(5f, rocket.impulse);
            Assert.Equal(0.6f, rocket.impulseFactor);
            Assert.Equal(Scenario.Scale, ActivePhysicsConstants.RocketImpulseScale);
        }

        [Fact]
        public void ActiveRocketLeavesItsCandysForceSlotsEmpty()
        {
            // Time Travel never writes a force slot on any point - setForcewithID has no call site
            // in the binary - so a rocket's thrust builds unopposed rather than settling at the
            // terminal speed a velocity-opposing force would impose.
            GameScene scene = Scenario.New()
                .Design("useMobilePhysics", "true")
                .Design("useTimeTravelRocketPhysics", "true")
                .Candy(160, 200)
                .OmNom(160, 440)
                .Rocket(160, 200, time: 2f)
                .PauseSwitcher(60, 440)
                .Build();
            _ = Act.BindRocket(scene, scene.Candy());
            ConstraintedPoint candyPoint = scene.Candy().WholeBody.Point;
            candyPoint.v = new Vector(30f, -12f);

            HeadlessGame.StepFrames(scene, 1);

            Assert.Equal(default, candyPoint.GetForce(0));
        }

        [Fact]
        public void TimeTravelThrustKeepsAccelerating()
        {
            GameScene scene = Scenario.New()
                .Design("useMobilePhysics", "true")
                .Design("useTimeTravelRocketPhysics", "true")
                .Candy(160, 200)
                .OmNom(160, 440)
                .Rocket(160, 200, angle: 90f, impulse: 5f, time: -1f)
                .PauseSwitcher(60, 440)
                .Build();
            _ = Act.BindRocket(scene, scene.Candy());
            ConstraintedPoint candyPoint = scene.Candy().WholeBody.Point;

            HeadlessGame.StepFrames(scene, 15);
            float earlyStep = VectLength(candyPoint.posDelta);
            HeadlessGame.StepFrames(scene, 25);
            float lateStep = VectLength(candyPoint.posDelta);

            // Undamped thrust adds exactly one impulse-step of displacement per frame, forever.
            // A velocity-opposing force would bend that into a curve flattening at a terminal
            // speed, so the late gain would fall short of the early one.
            float gainPerFrame = (lateStep - earlyStep) / 25f;
            float impulseStep = 5f * ActivePhysicsConstants.RocketImpulseScale * 0.016f;
            Assert.Equal(impulseStep, gainPerFrame, 2);
        }

        [Fact]
        public void ExperimentsRocketDoesNotOwnTimeTravelsForceSlot()
        {
            GameScene scene = Scenario.New()
                .Design("useMobilePhysics", "true")
                .Candy(160, 200)
                .OmNom(160, 440)
                .Rocket(160, 200, time: 2f)
                .Build();
            _ = Act.BindRocket(scene, scene.Candy());
            ConstraintedPoint candyPoint = scene.Candy().WholeBody.Point;
            Vector unrelatedForce = new(10f, 20f);
            candyPoint.SetForcewithID(unrelatedForce, 0);

            HeadlessGame.StepFrames(scene, 1);

            Assert.Equal(unrelatedForce, candyPoint.GetForce(0));
        }

        [Fact]
        public void ExperimentsRocketKeepsItsOwnVelocityDampingDivisors()
        {
            bool previous = ActivePhysicsConstants.UseMobilePhysicsModel;
            try
            {
                ActivePhysicsConstants.UseMobilePhysicsModel = false;
                Assert.Equal(40f, ActivePhysicsConstants.RocketActiveVelocityDamping);

                ActivePhysicsConstants.UseMobilePhysicsModel = true;
                Assert.Equal(20f, ActivePhysicsConstants.RocketActiveVelocityDamping);
            }
            finally
            {
                ActivePhysicsConstants.UseMobilePhysicsModel = previous;
            }
        }

        [Fact]
        public void MovingRocketContinuesItsAuthoredPathWhileFrozen()
        {
            GameScene scene = Scenario.New()
                .Candy(60, 100)
                .OmNom(160, 440)
                .Rocket(220, 200, path: "80,0", moveSpeed: 30f)
                .PauseSwitcher(60, 440)
                .Build();
            Rocket rocket = scene.Rockets()[0];
            Freeze(scene);
            Vector before = new(rocket.x, rocket.y);

            HeadlessGame.StepFrames(scene, 60);

            Assert.NotEqual(before, new Vector(rocket.x, rocket.y));
        }

        [Fact]
        public void FlyingRocketDoesNotConsumeFuelWhileFrozen()
        {
            GameScene scene = Scenario.New()
                .Candy(160, 200)
                .OmNom(160, 440)
                .Rocket(160, 200, time: 2f)
                .PauseSwitcher(60, 440)
                .Build();
            Rocket rocket = Act.BindRocket(scene, scene.Candy());
            Freeze(scene);
            float before = rocket.time;

            HeadlessGame.StepFrames(scene, 60);

            Assert.Equal(before, rocket.time);
        }

        [Fact]
        public void RopesStillCutWhileFrozen()
        {
            GameScene scene = Scenario.New()
                .Candy(160, 200, "first")
                .Rope(160, 120, 110, "first")
                .OmNom(160, 440)
                .PauseSwitcher(60, 440)
                .Build();
            HeadlessGame.StepFrames(scene, 5);
            Freeze(scene);
            Bungee rope = Assert.Single(scene.RegisteredRopes()).Rope;
            Assert.Equal(-1, rope.cut);
            int segment = rope.parts.Count / 2;
            segment = Math.Min(segment, rope.parts.Count - 2);
            Vector from = rope.parts[segment].pos;
            Vector to = rope.parts[segment + 1].pos;
            Vector midpoint = new((from.X + to.X) / 2f, (from.Y + to.Y) / 2f);
            float dx = to.X - from.X;
            float dy = to.Y - from.Y;
            float length = MathF.Sqrt((dx * dx) + (dy * dy));
            Assert.True(length > 0f);
            const float reach = 40f;
            Vector start = scene.ScreenPositionOf(new Vector(
                midpoint.X + (dy / length * reach),
                midpoint.Y - (dx / length * reach)));
            Vector end = scene.ScreenPositionOf(new Vector(
                midpoint.X - (dy / length * reach),
                midpoint.Y + (dx / length * reach)));

            _ = scene.TouchDownXYIndex(start.X, start.Y, 0);
            _ = scene.TouchMoveXYIndex(end.X, end.Y, 0);
            _ = scene.TouchUpXYIndex(end.X, end.Y, 0);

            Assert.NotEqual(-1, rope.cut);
        }

        [Fact]
        public void BambooTubeDoesNotCatchTheCandyAgainWhileFrozen()
        {
            GameScene scene = Scenario.New()
                .Candy(160, 200)
                .OmNom(20, 460)
                .BambooTube(20, 40, TubeMouth.CatchesFalling)
                .PauseSwitcher(300, 460)
                .Build();
            CandyContext candy = scene.Candy();
            Act.EnterBambooTube(scene, candy, TubeMouth.CatchesFalling);
            Freeze(scene);

            // A candy already inside comes out on its own timer, as a sock's does in Time Travel.
            Assert.True(Interaction.StepUntil(scene, () => candy.Lifecycle.Transport?.BambooTube == null));
            HeadlessGame.StepFrames(scene, 60);

            Assert.Null(candy.Lifecycle.Transport?.BambooTube);
        }

        [Fact]
        public void CarryingMouseHoldsTheCandyInPlaceWhileFrozen()
        {
            // A short stay makes an unfrozen mouse retreat and hand the candy to the second hole
            // well inside the frozen window.
            GameScene scene = Scenario.New()
                .Candy(160, 200)
                .OmNom(20, 460)
                .Mouse(160, 200, activeTime: 1f)
                .Mouse(260, 100, index: 2, activeTime: 1f)
                .PauseSwitcher(300, 460)
                .Build();
            CandyContext candy = scene.Candy();
            Mouse mouse = Act.CarryByMouse(scene, candy);
            Freeze(scene);
            HeadlessGame.StepFrames(scene, 1);
            Vector frozenAt = candy.WholeBody.Point.pos;

            HeadlessGame.StepFrames(scene, 120);

            Assert.True(mouse.IsActive);
            Assert.True(scene.MouseCarries(candy));
            Assert.Equal(frozenAt.X, candy.WholeBody.Point.pos.X, 3);
            Assert.Equal(frozenAt.Y, candy.WholeBody.Point.pos.Y, 3);
        }

        [Fact]
        public void MouseDoesNotGrabFrozenCandy()
        {
            GameScene scene = Scenario.New()
                .Candy(160, 190)
                .OmNom(20, 460)
                .Mouse(160, 200)
                .PauseSwitcher(300, 460)
                .Build();
            CandyContext candy = scene.Candy();
            Freeze(scene);

            HeadlessGame.StepFrames(scene, 60);

            Assert.False(scene.MouseCarries(candy));
        }

        [Fact]
        public void AntsDoNotCarryTheCandyOnWhileFrozen()
        {
            GameScene scene = Scenario.New()
                .Candy(120, 200)
                .OmNom(20, 460)
                .Ants(100, 200, path: "200,0")
                .PauseSwitcher(300, 460)
                .Build();
            CandyContext candy = scene.Candy();
            Act.CarryByAnts(scene, candy);
            HeadlessGame.StepFrames(scene, 5);
            Freeze(scene);
            HeadlessGame.StepFrames(scene, 1);
            CandyAttachments attachments = candy.Lifecycle.Attachments;
            Vector marker = attachments.AntInteractionPoint;
            float carryTime = attachments.AntInteractionTime;
            Vector frozenAt = candy.WholeBody.Point.pos;

            HeadlessGame.StepFrames(scene, 120);

            Assert.Equal(marker, attachments.AntInteractionPoint);
            Assert.Equal(carryTime, attachments.AntInteractionTime);

            // Had the marker kept marching, the candy would snap the whole frozen distance at once.
            Freeze(scene);
            HeadlessGame.StepFrames(scene, 1);

            Assert.True(VectLength(VectSub(candy.WholeBody.Point.pos, frozenAt)) < 10f);
        }

        [Fact]
        public void AutomaticConveyorStopsCarryingItsItemsWhileFrozen()
        {
            GameScene scene = Scenario.New()
                .Candy(40, 40)
                .OmNom(20, 460)
                .Conveyor(160, 300, length: 160, velocity: 40f)
                .Star(200, 300)
                .PauseSwitcher(300, 460)
                .Build();
            HeadlessGame.StepFrames(scene, 5);
            Star star = scene.Stars()[0];
            _ = Assert.Single(scene.Conveyors().Iterator().First().BoundObjects);
            Freeze(scene);
            float frozenAt = star.x;

            HeadlessGame.StepFrames(scene, 60);

            Assert.Equal(frozenAt, star.x, 3);
        }

        [Fact]
        public void ManualConveyorStillDragsAndCoastsWhileFrozen()
        {
            GameScene scene = Scenario.New()
                .Candy(40, 40)
                .OmNom(20, 460)
                .Conveyor(160, 300, length: 160, manual: true)
                .Star(200, 300)
                .PauseSwitcher(300, 460)
                .Build();
            HeadlessGame.StepFrames(scene, 5);
            Star star = scene.Stars()[0];
            Freeze(scene);
            Vector grip = scene.ScreenPositionOf(star);
            float before = star.x;

            Assert.True(scene.TouchDownXYIndex(grip.X, grip.Y, 1));
            for (int step = 1; step <= 6; step++)
            {
                _ = scene.TouchMoveXYIndex(grip.X + (step * 15f), grip.Y, 1);
                HeadlessGame.StepFrames(scene, 1);
            }
            float dragged = star.x;
            _ = scene.TouchUpXYIndex(grip.X + 120f, grip.Y, 1);
            HeadlessGame.StepFrames(scene, 10);

            Assert.True(dragged > before);
            Assert.True(star.x > dragged);
        }

        [Fact]
        public void SteamColumnHoldsStillWhileFrozen()
        {
            GameScene scene = Scenario.New()
                .Candy(40, 40)
                .OmNom(20, 460)
                .SteamTube(160, 300)
                .PauseSwitcher(300, 460)
                .Build();
            SteamTube tube = scene.SteamTubes()[0];
            HeadlessGame.StepFrames(scene, 5);
            Freeze(scene);
            float height = tube.GetCurrentHeightModulated();

            HeadlessGame.StepFrames(scene, 30);

            Assert.Equal(height, tube.GetCurrentHeightModulated());
        }

        [Fact]
        public void SteamValveIgnoresTapsWhileFrozen()
        {
            GameScene scene = Scenario.New()
                .Candy(40, 40)
                .OmNom(20, 460)
                .SteamTube(160, 300)
                .PauseSwitcher(300, 460)
                .Build();
            SteamTube tube = scene.SteamTubes()[0];
            Vector valve = scene.ScreenPositionOf(new Vector(tube.x, tube.y + (28f * tube.GetHeightScale())));
            int state = tube.steamState;
            Freeze(scene);

            _ = scene.TouchDownXYIndex(valve.X, valve.Y, 1);
            _ = scene.TouchUpXYIndex(valve.X, valve.Y, 1);

            Assert.Equal(state, tube.steamState);

            Freeze(scene);
            _ = scene.TouchDownXYIndex(valve.X, valve.Y, 1);
            _ = scene.TouchUpXYIndex(valve.X, valve.Y, 1);

            Assert.NotEqual(state, tube.steamState);
        }

        [Fact]
        public void LanternDoesNotCaptureCandyWhileFrozen()
        {
            GameScene scene = Scenario.New()
                .Candy(160, 200)
                .OmNom(20, 460)
                .Lantern(40, 40)
                .PauseSwitcher(300, 460)
                .Build();
            CandyContext candy = scene.Candy();
            Interaction.Hover(candy);
            Freeze(scene);
            HeadlessGame.StepFrames(scene, 1);
            Act.MoveTo(Lantern.GetAllLanterns()[0], candy.WholeBody.Point.pos);

            HeadlessGame.StepFrames(scene, 10);

            Assert.False(candy.Lifecycle.Attachments.InLantern);
        }

        [Fact]
        public void EmptyLanternHoldsItsPathWhileFrozen()
        {
            GameScene scene = Scenario.New()
                .Candy(40, 40)
                .OmNom(20, 460)
                .Lantern(160, 200, path: "80,0", moveSpeed: 30f)
                .PauseSwitcher(300, 460)
                .Build();
            Lantern lantern = Lantern.GetAllLanterns()[0];
            HeadlessGame.StepFrames(scene, 5);
            Freeze(scene);
            Vector frozenAt = new(lantern.x, lantern.y);

            HeadlessGame.StepFrames(scene, 60);

            Assert.Equal(frozenAt, new Vector(lantern.x, lantern.y));
        }

        [Fact]
        public void RocketCandyMovedWhileFrozenDoesNotLurchWhenTimeResumes()
        {
            GameScene scene = Scenario.New()
                .Candy(160, 200)
                .OmNom(20, 460)
                .Rocket(160, 200, impulse: 0f)
                .PauseSwitcher(300, 460)
                .Build();
            CandyContext candy = scene.Candy();
            Rocket rocket = Act.BindRocket(scene, candy);
            Assert.Equal(Rocket.STATE_ROCKET_FLY, rocket.state);
            Freeze(scene);
            HeadlessGame.StepFrames(scene, 1);

            // A hand turning its arm drags a held candy like this while time is frozen: position and
            // previous position move together, so the candy itself carries no velocity.
            ConstraintedPoint point = candy.WholeBody.Point;
            Vector heldAt = new(point.pos.X - 120f, point.pos.Y - 120f);
            point.pos = heldAt;
            point.prevPos = heldAt;
            HeadlessGame.StepFrames(scene, 10);

            Freeze(scene);
            HeadlessGame.StepFrames(scene, 1);

            Assert.True(VectLength(VectSub(point.pos, heldAt)) < 10f);
        }

        [Fact]
        public void FlyingRocketTurnsWithItsCandyWhileFrozen()
        {
            GameScene scene = Scenario.New()
                .Candy(160, 200)
                .OmNom(20, 460)
                .Rocket(160, 200, impulse: 0f)
                .PauseSwitcher(300, 460)
                .Build();
            CandyContext candy = scene.Candy();
            Rocket rocket = Act.BindRocket(scene, candy);
            Assert.Equal(Rocket.STATE_ROCKET_FLY, rocket.state);
            Freeze(scene);
            HeadlessGame.StepFrames(scene, 1);
            float before = rocket.rotation;

            // A hand's rotating arm turns a held candy while time is frozen.
            candy.WholeBody.Main.rotation += 90f;
            HeadlessGame.StepFrames(scene, 2);

            float expected = (before + 90f) % 360f;
            Assert.Equal(expected < 0f ? expected + 360f : expected, rocket.rotation, 2);
        }

        [Fact]
        public void FlyingRocketHidesItsExhaustWhileFrozen()
        {
            GameScene scene = Scenario.New()
                .Candy(160, 200)
                .OmNom(20, 460)
                .Rocket(160, 200, time: 2f)
                .PauseSwitcher(300, 460)
                .Build();
            Rocket rocket = Act.BindRocket(scene, scene.Candy());
            Assert.NotNull(rocket.particles);
            Assert.NotNull(rocket.cloudParticles);
            Assert.False(rocket.ExhaustHidden);

            Freeze(scene);

            Assert.True(rocket.ExhaustHidden);
            Assert.False(rocket.particles.visible);
            Assert.False(rocket.cloudParticles.visible);

            Freeze(scene);

            Assert.False(rocket.ExhaustHidden);
            Assert.True(rocket.particles.visible);
            Assert.True(rocket.cloudParticles.visible);
        }

        [Fact]
        public void FrozenGhostBubbleHoldsItsBubbleButKeepsItsCloudsDrifting()
        {
            GameScene scene = Scenario.New()
                .Candy(160, 200)
                .OmNom(160, 440)
                .PauseSwitcher(60, 440)
                .Build();
            CandyInGhostBubbleAnimation ghost = scene.Candy().WholeBody.GhostBubbleAnimation;
            Freeze(scene);
            float bubbleTime = ghost.GetTimeline(0).time;
            float cloudTime = ghost.backCloud.GetTimeline(0).time;

            HeadlessGame.StepFrames(scene, 4);

            Assert.Equal(bubbleTime, ghost.GetTimeline(0).time);
            Assert.NotEqual(cloudTime, ghost.backCloud.GetTimeline(0).time);
        }

        [Fact]
        public void FrozenLightBulbBubbleAnimationHoldsItsFrame()
        {
            GameScene scene = Scenario.New()
                .Candy(40, 40)
                .LightBulb(160, 200)
                .Bubble(160, 200)
                .OmNom(20, 460)
                .PauseSwitcher(300, 460)
                .Build();
            CandyContext bulb = Assert.Single(scene.Candies(), context => context.LightBulb != null);
            _ = Act.CaptureInBubble(scene, bulb);
            Freeze(scene);
            Timeline timeline = bulb.LightBulb.BubbleAnimation.GetTimeline(0);
            float before = timeline.time;

            HeadlessGame.StepFrames(scene, 4);

            Assert.Equal(before, timeline.time);
        }

        [Fact]
        public void RocketExhaustComesBackWhereTheRocketIsWhenTimeResumes()
        {
            GameScene scene = Scenario.New()
                .Candy(160, 200)
                .OmNom(20, 460)
                .Rocket(160, 200, impulse: 0f)
                .PauseSwitcher(300, 460)
                .Build();
            CandyContext candy = scene.Candy();
            Rocket rocket = Act.BindRocket(scene, candy);
            HeadlessGame.StepFrames(scene, 2);
            float flameX = rocket.container.x;
            float sparksX = rocket.particles.x;
            float cloudsX = rocket.cloudParticles.x;
            Freeze(scene);

            // Particles already in the air belong to where the rocket was; they must not reappear.
            Assert.Equal(0, rocket.particles.particleCount);
            Assert.Equal(0, rocket.particles.particleIdx);
            Assert.Equal(0, rocket.cloudParticles.particleCount);
            Assert.Equal(0, rocket.cloudParticles.particleIdx);

            ConstraintedPoint point = candy.WholeBody.Point;
            Vector heldAt = new(point.pos.X - 120f, point.pos.Y);
            point.pos = heldAt;
            point.prevPos = heldAt;
            HeadlessGame.StepFrames(scene, 5);

            // Unfreezing can be drawn before the next update runs, so the exhaust has to be moved
            // onto the rocket at the moment it is shown again.
            Freeze(scene);

            Assert.Equal(flameX - 120f, rocket.container.x, 1);
            Assert.Equal(sparksX - 120f, rocket.particles.x, 1);
            Assert.Equal(cloudsX - 120f, rocket.cloudParticles.x, 1);
        }

        [Fact]
        public void KickedSuctionCupRopeHoldsStillWhileFrozen()
        {
            (GameScene scene, Grab hook) = FrozenKickedCup();
            Bungee rope = hook.Rope;
            ConstraintedPoint middle = rope.parts[rope.parts.Count / 2];
            Vector anchor = rope.bungeeAnchor.pos;
            Vector middleAt = middle.pos;

            HeadlessGame.StepFrames(scene, 30);

            Assert.Equal(anchor, rope.bungeeAnchor.pos);
            Assert.Equal(middleAt, middle.pos);
        }

        [Fact]
        public void KickedSuctionCupDoesNotReStickWhileFrozen()
        {
            (GameScene scene, Grab hook) = FrozenKickedCup();
            hook.Mount.BeginSticking();

            HeadlessGame.StepFrames(scene, (int)(Grab.STICK_DELAY / 0.016f) + 10);

            Assert.False(hook.Mount.IsMounted);
            Assert.Equal(0f, hook.Mount.StickTimer);
        }

        [Fact]
        public void PumpDoesNotBlowAKickedSuctionCupWhileFrozen()
        {
            (GameScene scene, Grab hook) = FrozenKickedCup(withPump: true);
            Pump pump = scene.Pumps()[0];
            Act.MoveTo(pump, new Vector(hook.x, hook.y + 60f));
            Vector anchor = hook.Rope.bungeeAnchor.pos;

            scene.OperatePump(pump);

            Assert.Equal(anchor, hook.Rope.bungeeAnchor.pos);
        }

        private static (GameScene Scene, Grab Hook) FrozenKickedCup(bool withPump = false)
        {
            Scenario scenario = Scenario.New()
                .Candy(160, 260, "first")
                .Grab(160, 120, length: 100, kickable: true, kicked: true, candyNumber: "first")
                .OmNom(20, 460)
                .PauseSwitcher(300, 460);
            GameScene scene = (withPump ? scenario.Pump(40, 40) : scenario).Build();
            Grab hook = Assert.Single(scene.Grabs(), grab => grab.Mount != null);
            Assert.False(hook.Mount.IsMounted);
            HeadlessGame.StepFrames(scene, 3);
            Freeze(scene);
            HeadlessGame.StepFrames(scene, 1);
            return (scene, hook);
        }

        [Fact]
        public void LoopingGameplaySoundsStopAndRestartAcrossTimeFreeze()
        {
            _ = HeadlessGame.Boot();
            SoundMgr manager = Application.SharedSoundMgr();
            RecordingAudioBackend backend = new();
            bool originalSoundPreference = Preferences.GetBooleanForKey("SOUND_ON");
            SoundMgr.SetBackend(backend);
            manager.StopAllSounds();
            Preferences.SetBooleanForKey(true, "SOUND_ON");

            try
            {
                GameScene scene = Scenario.New()
                    .Candy(160, 200)
                    .OmNom(160, 440)
                    .Rocket(160, 200, time: 2f)
                    .ElectroSpikes(260, 300)
                    .PauseSwitcher(60, 440)
                    .Build();
                Rocket rocket = Act.BindRocket(scene, scene.Candy());
                Spikes electro = scene.SpikeStrips()[0];
                Assert.True(Interaction.StepUntil(scene, () => electro.ElectricLoopPlaying));
                ISoundInstance originalRocketLoop = rocket.flyLoopSound;
                Assert.NotNull(originalRocketLoop);

                Freeze(scene);

                Assert.False(electro.ElectricLoopPlaying);
                Assert.Null(rocket.flyLoopSound);

                Freeze(scene);

                Assert.True(electro.ElectricLoopPlaying);
                Assert.NotNull(rocket.flyLoopSound);
                Assert.NotSame(originalRocketLoop, rocket.flyLoopSound);
            }
            finally
            {
                manager.StopAllSounds();
                SoundMgr.SetBackend(null);
                Preferences.SetBooleanForKey(originalSoundPreference, "SOUND_ON");
            }
        }

        private sealed class RecordingAudioBackend : IAudioBackend
        {
            public AudioPlaybackState MusicState => AudioPlaybackState.Stopped;

            public ISoundEffect LoadSound(string contentPath)
            {
                return new RecordingSoundEffect();
            }

            public IMusicTrack LoadMusic(string contentPath)
            {
                throw new NotSupportedException();
            }

            public void PlayMusic(IMusicTrack track, bool repeating)
            {
            }

            public void StopMusic()
            {
            }

            public void PauseMusic()
            {
            }

            public void ResumeMusic()
            {
            }
        }

        private sealed class RecordingSoundEffect : ISoundEffect
        {
            public ISoundInstance CreateInstance()
            {
                return new RecordingSoundInstance();
            }

            public void Dispose()
            {
            }
        }

        private sealed class RecordingSoundInstance : ISoundInstance
        {
            public bool IsLooped { get; set; }

            public float Volume { get; set; }

            public AudioPlaybackState State { get; private set; } = AudioPlaybackState.Stopped;

            public void Play()
            {
                State = AudioPlaybackState.Playing;
            }

            public void Stop()
            {
                State = AudioPlaybackState.Stopped;
            }

            public void Pause()
            {
                State = AudioPlaybackState.Paused;
            }

            public void Resume()
            {
                State = AudioPlaybackState.Playing;
            }

            public void Dispose()
            {
            }
        }
    }
}
