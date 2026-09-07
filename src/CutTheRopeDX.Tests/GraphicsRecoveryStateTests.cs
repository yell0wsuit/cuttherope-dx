using System.Linq;
using System.Reflection;

using CutTheRopeDX.Framework.Core;
using CutTheRopeDX.Framework.Platform;
using CutTheRopeDX.Framework.Visual;
using CutTheRopeDX.GameMain;
using CutTheRopeDX.Tests.Interactions;

using Xunit;

namespace CutTheRopeDX.Tests
{
    /// <summary>
    /// What losing and regaining a graphics device does to the running application.
    /// </summary>
    /// <remarks>
    /// These run headless, so they cover the application state and the resource bookkeeping and
    /// nothing about what ends up on the screen. Transitions in particular never start without a
    /// graphics device, so only the invariant recovery has to preserve for them - that dropping
    /// the captured frames leaves the transition's own clock alone - is checked here; how the
    /// missing captures look is a rendering question and is not answered by any of this.
    /// </remarks>
    public sealed class GraphicsRecoveryStateTests
    {
        // The policy enums are internal to Core, so the cases travel as their underlying values;
        // xUnit only discovers public signatures.
        [Theory]
        [InlineData(false, (int)GraphicsRecoveryStance.ResumeAndPause)]
        [InlineData(true, (int)GraphicsRecoveryStance.HoldMovie)]
        public void ACutsceneIsHeldWhileEverythingElseResumes(bool movieActive, int expected)
        {
            Assert.Equal(expected, (int)GraphicsRecovery.StanceFor(movieActive));
        }

        [Theory]
        [InlineData(false, false, (int)MoviePressAction.Ignore)]
        [InlineData(false, true, (int)MoviePressAction.Ignore)]
        [InlineData(true, true, (int)MoviePressAction.Resume)]
        [InlineData(true, false, (int)MoviePressAction.Skip)]
        public void TheFirstPressOnAHeldCutsceneStartsItRatherThanSkippingIt(
            bool armed, bool paused, int expected)
        {
            Assert.Equal(expected, (int)GraphicsRecovery.PressAction(armed, paused));
        }

        [Fact]
        public void GameplayBodiesAndTimersComeThroughARecoveryUnchanged()
        {
            _ = HeadlessGame.Boot();
            GameScene scene = Scenario.New().Candy(160, 100).OmNom(160, 400).Build();
            HeadlessGame.StepFrames(scene, 45);

            float time = scene.time;
            Vector[] positions = Positions(scene);
            Vector[] velocities = Velocities(scene);
            Assert.NotEmpty(positions);
            Assert.NotEqual(0f, time);

            Recover();

            Assert.Equal(time, scene.time);
            Assert.Equal(positions, Positions(scene));
            Assert.Equal(velocities, Velocities(scene));
        }

        [Fact]
        public void RecoveryHandsALiveGameplayScreenItsPauseMenu()
        {
            _ = HeadlessGame.Boot();
            GameController controller = HeadlessGame.LoadLevelWithController(pack: 1, level: 4);
            GameScene scene = (GameScene)controller.GetView(0).GetChild(GameView.VIEW_ELEMENT_GAME_SCENE);
            HeadlessGame.StepFrames(scene, 60);
            Assert.True(scene.updateable);

            Recover();

            Assert.False(scene.updateable);
            Assert.True(controller.GetView(0).GetChild(GameView.VIEW_ELEMENT_PAUSE_MENU).IsEnabled());
        }

        [Fact]
        public void RecoveryLeavesAMenuOnTheSameScreenWithTheSameSelectionAndScroll()
        {
            _ = HeadlessGame.Boot();
            MenuController controller = new((CTRRootController)Application.SharedRootController());
            try
            {
                controller.PreLevelSelect();
                controller.ShowView(MenuController.VIEW_LEVEL_SELECT);
                controller.OnButtonPressed(MenuButtonId.ForLevel(3));

                ScrollableContainer packs = PackContainer(controller);
                packs.SetScroll(new Vector(120f, 0f));
                Vector scroll = packs.GetScroll();
                int view = controller.activeViewID;
                int selected = PendingLevel(controller);
                Assert.Equal(3, selected);
                Assert.NotEqual(0f, scroll.X);

                Recover();

                Assert.Equal(view, controller.activeViewID);
                Assert.Equal(selected, PendingLevel(controller));
                Assert.Equal(scroll, PackContainer(controller).GetScroll());
            }
            finally
            {
                controller.Dispose();
            }
        }

        [Fact]
        public void DroppingTheTransitionCapturesLeavesTheTransitionsOwnClockAlone()
        {
            _ = HeadlessGame.Boot();
            RootController root = Application.SharedRootController();
            float transitionTime = root.transitionTime;
            try
            {
                root.transitionTime = 12.5f;
                SetCapture(root, "prevScreenImage", new CTRTexture2D());
                SetCapture(root, "nextScreenImage", new CTRTexture2D());

                Assert.Equal(2, root.DropTransitionCaptures());

                Assert.Equal(12.5f, root.transitionTime);
                Assert.True(root.IsTransitionActive());
                Assert.Equal(0, root.DropTransitionCaptures());
            }
            finally
            {
                root.transitionTime = transitionTime;
            }
        }

        [Fact]
        public void OnlyTexturesThatNameAContentPathAreLoadedAgain()
        {
            _ = HeadlessGame.Boot();
            _ = Scenario.New().Candy(160, 100).OmNom(160, 400).Build();

            int fileBacked = CTRTexture2D.Registered().Count(texture => texture._resName != null);
            int captures = CTRTexture2D.Registered().Count(texture => texture._resName == null);
            Assert.True(fileBacked > 0, "the scenario should have loaded at least one image");

            GraphicsRecoveryPlan plan = GraphicsRecovery.Begin();
            GraphicsRecoveryReport report = GraphicsRecovery.Complete(plan);

            Assert.Equal(fileBacked, report.ReloadedAssets);
            Assert.Equal(captures, CTRTexture2D.Registered().Count(texture => texture._resName == null));
        }

        /// <summary>Runs a whole loss and recovery over whatever is currently on screen.</summary>
        private static void Recover()
        {
            _ = GraphicsRecovery.Complete(GraphicsRecovery.Begin());
        }

        private static Vector[] Positions(GameScene scene)
        {
            return [.. scene.Candies().Select(candy => candy.WholeBody.Point.pos)];
        }

        private static Vector[] Velocities(GameScene scene)
        {
            return [.. scene.Candies().Select(candy => candy.WholeBody.Point.v)];
        }

        private static ScrollableContainer PackContainer(MenuController controller)
        {
            FieldInfo field = typeof(MenuController).GetField(
                "packContainer", BindingFlags.Instance | BindingFlags.NonPublic);
            return (ScrollableContainer)field.GetValue(controller);
        }

        private static int PendingLevel(MenuController controller)
        {
            FieldInfo field = typeof(MenuController).GetField(
                "level", BindingFlags.Instance | BindingFlags.NonPublic);
            return (int)field.GetValue(controller);
        }

        private static void SetCapture(RootController root, string name, CTRTexture2D capture)
        {
            FieldInfo field = typeof(RootController).GetField(
                name, BindingFlags.Instance | BindingFlags.NonPublic);
            field.SetValue(root, capture);
        }
    }
}
