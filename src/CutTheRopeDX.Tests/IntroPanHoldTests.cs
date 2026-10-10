using System;
using System.Linq;

using CutTheRopeDX.Framework.Core;
using CutTheRopeDX.Framework.Visual;

using CutTheRopeDX.GameMain;
using CutTheRopeDX.Tests.Interactions;

using Xunit;

namespace CutTheRopeDX.Tests
{
    /// <summary>
    /// Covers the hold on gameplay while the opening camera pan is still crossing the level.
    /// </summary>
    public sealed class IntroPanHoldTests
    {
        /// <summary>Authored height that fills the design box exactly, so no pan is staged.</summary>
        private const int UnpannedHeight = 480;

        /// <summary>Authored height of a level two design boxes tall, which is panned across.</summary>
        private const int PannedHeight = 960;

        [Fact]
        public void NothingMovesWhileTheIntroPanIsStillRunning()
        {
            // The same unroped candy in a level small enough to need no pan falls straight away,
            // so the level below is held rather than merely having nothing to do.
            Assert.True(FallAfter(UnpannedHeight, frames: 30) > 1f);

            Assert.Equal(0f, FallAfter(PannedHeight, frames: 30), 0.001);
        }

        [Fact]
        public void GameplayResumesOnceTheIntroPanHasComeToRest()
        {
            Assert.True(FallAfter(PannedHeight, frames: 240) > 1f);
        }

        [Theory]
        // Every map shape in the shipped campaign, at the aspect the game was authored for. The
        // rule that stages a pan is measured against the viewport now rather than against the
        // design box; at this aspect it has to land exactly where the design-box comparison did.
        [InlineData(320, 480, false)]
        [InlineData(640, 480, false)]
        [InlineData(320, 700, true)]
        [InlineData(320, 960, true)]
        [InlineData(640, 960, true)]
        public void TheDesignAspectStagesAPanForExactlyTheLevelsItAlwaysDid(
            int mapWidth, int mapHeight, bool panned)
        {
            GameScene scene = Scenario.New()
                .MapSize(mapWidth, mapHeight)
                .Candy(mapWidth / 2, 60)
                .OmNom(mapWidth / 2, mapHeight - 60)
                .Build();

            Assert.Equal(panned, scene.IsIntroPanRunning());
        }

        [Theory]
        // 16:9 exposes one design box of a two-box-tall level, so there is a level to preview.
        [InlineData(2560, 1440, false)]
        // Taller, but still not enough to hold 2880 world units at once.
        [InlineData(720, 1280, false)]
        // Tall enough that the level is scaled down until all of it is on screen. A pan here could
        // not move the picture, so staging one would only hold the player away from a level they
        // can already see in full.
        [InlineData(400, 1280, true)]
        public void ThePanIsStagedOnlyWhenTheViewportCannotAlreadyHoldTheLevel(
            int width, int height, bool skipped)
        {
            float fall = 0f;
            LayoutSurfaces.WithSurface(width, height, () => fall = FallAfter(PannedHeight, frames: 30));

            Assert.Equal(skipped, fall > 1f);
        }

        [Fact]
        public void TheHoldLastsUntilThePanStopsRatherThanUntilInputComesBack()
        {
            GameScene scene = FreeCandyLevel(PannedHeight);
            float start = scene.Candy().WholeBody.Point.pos.Y;

            int inputBack = -1;
            int panStopped = -1;
            int firstMovement = -1;
            for (int frame = 0; frame < 600 && firstMovement < 0; frame++)
            {
                HeadlessGame.StepFrames(scene, 1);
                if (inputBack < 0 && !scene.ignoreTouches)
                {
                    inputBack = frame;
                }
                if (panStopped < 0 && !scene.IsIntroPanRunning())
                {
                    panStopped = frame;
                }
                if (MathF.Abs(scene.Candy().WholeBody.Point.pos.Y - start) > 0.001f)
                {
                    firstMovement = frame;
                }
            }

            // Input is handed back a hundred pixels out, so there is a real stretch of pan left
            // to run after it - and the candy stays put for all of it, moving on the first step
            // after the picture comes to rest.
            Assert.True(panStopped > inputBack + 1, $"pan stopped at {panStopped}, input back at {inputBack}");
            Assert.Equal(panStopped + 1, firstMovement);
        }

        [Fact]
        public void TheRestartFadeFinishesOverTheIntroPanRatherThanWaitingForIt()
        {
            GameScene scene = FreeCandyLevel(PannedHeight);
            scene.AnimateLevelRestart();

            // Long enough for both dim phases and the scene swap between them, and far short of
            // the pan this level stages - which is the point: the fade must not be held by it.
            HeadlessGame.StepFrames(scene, 40);

            Assert.Equal(0f, scene.gameplayFlow.DimTime, 0.001);
            Assert.Equal(RestartPhase.Playing, scene.gameplayFlow.Phase);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void TimeTravelRestartLightningAnimatesAndFinishesDuringTheCameraIntro(bool restart)
        {
            _ = HeadlessGame.Boot();
            MenuStyle previous = MenuTheme.Current;
            MenuTheme.Current = MenuStyle.TimeTravel;
            try
            {
                GameScene scene = Scenario.New()
                    .MapSize(320, PannedHeight)
                    .Candy(160, 60)
                    .OmNom(160, PannedHeight - 60)
                    .Build(level: 1);
                if (restart)
                {
                    scene.AnimateLevelRestart();
                    HeadlessGame.StepFrames(scene, 20);
                }
                Assert.True(scene.IsIntroPanRunning());
                BaseElement lightning = scene.GetChildWithName("ttLightningRT");
                Assert.NotNull(lightning);
                float start = scene.Candy().WholeBody.Point.pos.Y;
                HeadlessGame.StepFrames(scene, 10);
                Assert.True(lightning.GetCurrentTimeline().time > 0.1f);
                Assert.True(scene.IsIntroPanRunning());
                Assert.Equal(start, scene.Candy().WholeBody.Point.pos.Y);

                HeadlessGame.StepFrames(scene, 90);
                Assert.Null(scene.GetChildWithName("ttLightningRT"));
                Assert.Null(scene.GetChildWithName("ttLightningBL"));
                Assert.True(scene.IsIntroPanRunning());
                Assert.Equal(start, scene.Candy().WholeBody.Point.pos.Y);
            }
            finally
            {
                MenuTheme.Current = previous;
            }
        }

        [Fact]
        public void TheArrivalFadeWaitsWithOmNomUntilTheCameraIntroFinishes()
        {
            _ = HeadlessGame.Boot();
            MenuStyle previous = MenuTheme.Current;
            int previousSkin = Preferences.GetIntForKey("PREFS_SELECTED_OMNOM");
            MenuTheme.Current = MenuStyle.TimeTravel;
            try
            {
                int skin = OmNomSkinRegistry.XmlSkins.ToList().FindIndex(s => s.Id == "OM_NOM_ORIGINAL_FLASH");
                Preferences.SetIntForKey(skin + 1, "PREFS_SELECTED_OMNOM", false);
                RootController.SetShowGreeting(true);
                GameScene scene = FreeCandyLevel(PannedHeight);
                BaseElement fade = scene.GetChildWithName("ttOmNomBlackout");
                Assert.NotNull(fade);
                HeadlessGame.StepFrames(scene, 30);
                Assert.True(scene.IsIntroPanRunning());
                Assert.Equal(0f, fade.color.AlphaChannel);
                Assert.Null(scene.TimeTravelSpiral);
                for (int frame = 0; frame < 1000 && scene.IsIntroPanRunning(); frame++)
                {
                    HeadlessGame.StepFrames(scene, 1);
                }
                Assert.False(scene.IsIntroPanRunning());
                HeadlessGame.StepFrames(scene, 30);
                Assert.True(fade.color.AlphaChannel > 0.2f);
            }
            finally
            {
                RootController.SetShowGreeting(false);
                Preferences.SetIntForKey(previousSkin, "PREFS_SELECTED_OMNOM", false);
                MenuTheme.Current = previous;
            }
        }

        [Theory]
        [InlineData("OM_NOM_ORIGINAL_FLASH")]
        [InlineData("OM_NOM_HALLOWEEN")]
        [InlineData("OM_NOM_XMAS")]
        public void GameplayWaitsForOmNomsArrivalThenResumes(string skinId)
        {
            _ = HeadlessGame.Boot();
            MenuStyle previous = MenuTheme.Current;
            int previousSkin = Preferences.GetIntForKey("PREFS_SELECTED_OMNOM");
            MenuTheme.Current = MenuStyle.TimeTravel;
            try
            {
                int skin = OmNomSkinRegistry.XmlSkins.ToList().FindIndex(s => s.Id == skinId);
                Preferences.SetIntForKey(skin + 1, "PREFS_SELECTED_OMNOM", false);
                RootController.SetShowGreeting(true);
                GameScene scene = FreeCandyLevel(UnpannedHeight);
                Assert.False(scene.IsIntroPanRunning());
                Assert.True(scene.TimeTravelSpiralTarget >= 0);
                float start = scene.Candy().WholeBody.Point.pos.Y;
                HeadlessGame.StepFrames(scene, 100);
                Assert.Equal(start, scene.Candy().WholeBody.Point.pos.Y);
                Assert.NotNull(scene.TimeTravelSpiral);
                Assert.True(scene.TutorialDirector().PresentationPaused);
                HeadlessGame.StepFrames(scene, 100);
                Assert.Equal(start, scene.Candy().WholeBody.Point.pos.Y);
                for (int frame = 0; frame < 200 && scene.TutorialDirector().PresentationPaused; frame++)
                {
                    HeadlessGame.StepFrames(scene, 1);
                }
                Assert.False(scene.TutorialDirector().PresentationPaused);
                HeadlessGame.StepFrames(scene, 10);
                Assert.True(scene.Candy().WholeBody.Point.pos.Y > start + 1f);
            }
            finally
            {
                RootController.SetShowGreeting(false);
                Preferences.SetIntForKey(previousSkin, "PREFS_SELECTED_OMNOM", false);
                MenuTheme.Current = previous;
            }
        }

        /// <summary>
        /// Builds a level whose candy hangs from nothing, and reports how far it fell.
        /// </summary>
        /// <param name="mapHeight">Authored map height, which decides whether a pan is staged.</param>
        /// <param name="frames">Frames to advance.</param>
        /// <returns>World units the candy descended.</returns>
        private static float FallAfter(int mapHeight, int frames)
        {
            GameScene scene = FreeCandyLevel(mapHeight);
            float start = scene.Candy().WholeBody.Point.pos.Y;
            HeadlessGame.StepFrames(scene, frames);
            return scene.Candy().WholeBody.Point.pos.Y - start;
        }

        /// <summary>Builds a level whose candy hangs from nothing.</summary>
        /// <param name="mapHeight">Authored map height, which decides whether a pan is staged.</param>
        /// <returns>The loaded scene.</returns>
        private static GameScene FreeCandyLevel(int mapHeight)
        {
            return Scenario.New()
                .MapSize(320, mapHeight)
                .Candy(160, 60)
                .OmNom(160, mapHeight - 60)
                .Build();
        }
    }
}
