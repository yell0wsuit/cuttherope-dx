using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using CutTheRopeDX.Framework;
using CutTheRopeDX.Framework.Core;
using CutTheRopeDX.Framework.Media;
using CutTheRopeDX.Framework.Platform;
using CutTheRopeDX.Framework.Visual;
using CutTheRopeDX.GameMain;
using CutTheRopeDX.Helpers;

using Xunit;

using static CutTheRopeDX.Tests.MenuTimeTravelTests;

namespace CutTheRopeDX.Tests
{
    /// <summary>
    /// Covers the Time Travel game screen: its HUD, pause menu, level-opening flash and
    /// lightning, the time spiral the original Om Nom travels through, and its result screen.
    /// Layout and logic only; what any of it looks like is checked by eye.
    /// </summary>
    public sealed class GameTimeTravelTests
    {
        private const float Frame = 0.016f;

        private static void WithTimeTravelGame(int pack, int level, Action<GameController, GameScene> body, bool originalFlashOmNom = false, bool fromMenu = false, MenuStyle style = MenuStyle.TimeTravel, int width = 1920, int height = 1080)
        {
            _ = HeadlessGame.Boot();
            MenuStyle previous = MenuTheme.Current;
            int previousSkin = Preferences.GetIntForKey("PREFS_SELECTED_OMNOM");
            MenuTheme.Current = style;

            // Winning saves the level and opens the next one; both are put back afterwards.
            int box = Application.SharedRootController().Box;
            int levels = Preferences.GetLevelsInPackCount(pack);
            (int Score, int Stars, UNLOCKEDSTATE Unlocked)[] progress = new (int, int, UNLOCKEDSTATE)[levels];
            for (int i = 0; i < levels; i++)
            {
                progress[i] = (Preferences.GetScoreForPackLevel(box, pack, i), Preferences.GetStarsForPackLevel(box, pack, i), Preferences.GetUnlockedForPackLevel(box, pack, i));
            }
            try
            {
                if (originalFlashOmNom)
                {
                    int flash = OmNomSkinRegistry.XmlSkins.ToList().FindIndex(skin => skin.Id == "OM_NOM_ORIGINAL_FLASH");
                    Preferences.SetIntForKey(flash + 1, "PREFS_SELECTED_OMNOM", false);
                }
                RootController.SetShowGreeting(fromMenu);
                LayoutSurfaces.WithSurface(width, height, () =>
                {
                    GameController controller = HeadlessGame.LoadLevelWithController(pack, level);
                    try
                    {
                        body(controller, (GameScene)controller.GetView(0).GetChild(GameView.VIEW_ELEMENT_GAME_SCENE));
                    }
                    finally
                    {
                        controller.Dispose();
                    }
                });
            }
            finally
            {
                for (int i = 0; i < levels; i++)
                {
                    Preferences.SetScoreForPackLevel(box, progress[i].Score, pack, i);
                    Preferences.SetStarsForPackLevel(box, progress[i].Stars, pack, i);
                    Preferences.SetUnlockedForPackLevel(box, progress[i].Unlocked, pack, i);
                }
                Preferences.SetIntForKey(previousSkin, "PREFS_SELECTED_OMNOM", false);
                RootController.SetShowGreeting(false);
                MenuTheme.Current = previous;
            }
        }

        private static void Step(GameScene scene, float seconds)
        {
            HeadlessGame.StepFrames(scene, (int)MathF.Ceiling(seconds / Frame));
        }

        private static void Step(BaseElement element, float seconds)
        {
            for (float t = 0f; t < seconds; t += Frame)
            {
                element.Update(Frame);
            }
        }

        /// <summary>Whether any of an Om Nom's parts is running a state's timeline.</summary>
        private static bool Runs(TargetContext target, TargetAnimationState state)
        {
            int id = target.animation.SkinDefinition.GetTimelineId(state);
            return All<Image>(target.targetObject).Any(part => part.CurrentTimelineIndex == id
                && part.GetCurrentTimeline()?.state == Timeline.TimelineState.TIMELINE_PLAYING);
        }

        [Fact]
        public void TheHudButtonsArePinnedToTheTopRightWhereTheIosCanvasPutsThem()
        {
            WithTimeTravelGame(0, 1, (controller, _) =>
            {
                View view = controller.GetView(0);
                ResolveDrawPositions(view);
                Rectangle visible = ScreenPresentation.Instance.Snapshot.VisibleBounds;
                float scale = new TimeTravelScreen(visible).AssetScale;

                Button pause = (Button)view.GetChild(GameView.VIEW_ELEMENT_PAUSE_BUTTON);
                Rectangle pauseBox = DrawnBox(pause);
                Vector pauseOffset = Image.GetQuadOffset(Resources.Img.HudTimeTravel, TimeTravelArt.HudPauseUp);
                Assert.Equal(visible.w - ((TimeTravelSceneGroup.Width - pauseOffset.X - pause.width) * scale), pauseBox.x + pauseBox.w, 1);
                Assert.Equal(pauseOffset.Y * scale, pauseBox.y, 1);

                // The restart disc sits left of the pill, as on the iOS canvas.
                Rectangle restartBox = DrawnBox((Button)view.GetChild(GameView.VIEW_ELEMENT_RESTART_BUTTON));
                Assert.True(restartBox.x + restartBox.w <= pauseBox.x + 1f);
            });
        }

        [Fact]
        public void TheHudStarsAreThreeIosStarsAlongTheTopLeftAndFillAsTheyAreCollected()
        {
            WithTimeTravelGame(0, 1, (_, scene) =>
            {
                Assert.Equal(3, scene.TimeTravelHudStars.Length);
                TimeTravelScreen screen = new(ScreenPresentation.Instance.Snapshot.VisibleBounds);
                for (int i = 0; i < 3; i++)
                {
                    FlashXmlStageRoot root = scene.TimeTravelHudStars[i].Root;
                    float originX = root.x + ((root.width >> 1) * (1f - root.scaleX));
                    float canvasX = TimeTravelArt.HudMarkers.Star.x + (TimeTravelArt.HudMarkers.Star.w / 2f) + ((i - 1) * TimeTravelArt.HudMarkers.StarStep);
                    Assert.Equal(canvasX / 2f * screen.Scale, originX, 1);
                    Assert.False(root.updateable);
                    Assert.True(scene.TimeTravelHudStars[i].Part("star_empty").visible);
                }
            });
        }

        [Fact]
        public void PausingDropsInTheRoundButtonsOverADimAndResumingFadesThemAway()
        {
            WithTimeTravelGame(0, 1, (controller, _) =>
            {
                View view = controller.GetView(0);
                BaseElement dim = view.GetChild(GameView.VIEW_ELEMENT_PAUSE_MENU);
                TimeTravelSceneGroup scene = (TimeTravelSceneGroup)view.GetChild(GameView.VIEW_ELEMENT_PAUSE_BUTTONS);
                Assert.False(dim.visible);

                controller.OnButtonPressed(GameControllerButtonId.Pause);
                Assert.True(dim.visible);
                Assert.True(scene.visible);

                // Level grid, replay and resume, then main menu, sound, music and skip.
                Assert.Equal(7, scene.GetChild(0).ChildsCount());
                Step(view, 0.5f);
                Assert.Equal(0.5f, dim.color.AlphaChannel, 3);

                controller.OnButtonPressed(GameControllerButtonId.Continue);
                Assert.True(dim.visible);
                Assert.False(scene.touchable);
                Step(view, 0.3f);
                Assert.False(dim.visible);
                Assert.False(scene.visible);
            });
        }

        [Fact]
        public void ThePauseMenusReplayLeavesTheMenuAndRestartsTheLevel()
        {
            WithTimeTravelGame(0, 1, (controller, scene) =>
            {
                controller.OnButtonPressed(GameControllerButtonId.Pause);
                controller.OnButtonPressed(GameControllerButtonId.PauseRestart);
                Assert.True(scene.touchable);
                Assert.Equal(RestartPhase.FadingOut, scene.gameplayFlow.Phase);
                Assert.Equal(BoxOpenClose.TimeTravelFlashSeconds, LevelFlowState.DimDuration);
            });

            // Outside the menu it does nothing.
            WithTimeTravelGame(0, 1, (controller, scene) =>
            {
                controller.OnButtonPressed(GameControllerButtonId.PauseRestart);
                Assert.Equal(RestartPhase.Playing, scene.gameplayFlow.Phase);
            });
        }

        [Fact]
        public void ALevelOpensFromWhiteWithLightningButAPacksFirstLevelOpensWithoutIt()
        {
            WithTimeTravelGame(0, 1, (controller, scene) =>
            {
                BoxOpenClose box = (BoxOpenClose)controller.GetView(0).GetChild(GameView.VIEW_ELEMENT_RESULTS);
                BaseElement white = box.GetChildWithName("ttLevelFlash");
                Assert.NotNull(white);
                Assert.Equal(1f, white.color.AlphaChannel, 3);
                Assert.NotNull(scene.GetChildWithName("ttLightningRT"));
                Assert.NotNull(scene.GetChildWithName("ttLightningBL"));
                Assert.Equal(180f, scene.GetChildWithName("ttLightningBL").rotation);
            });
            WithTimeTravelGame(0, 0, (_, scene) => Assert.Null(scene.GetChildWithName("ttLightningRT")));
        }

        [Fact]
        public void ThePacksFirstLevelDrawsTheOriginalOmNomOutOfTheSpiral()
        {
            WithTimeTravelGame(0, 0, (_, scene) =>
            {
                int original = scene.TimeTravelSpiralTarget;
                Assert.True(original >= 0);
                TargetContext target = scene.Targets[original];
                Assert.False(target.targetObject.visible);

                Step(scene, 0.7f);
                Assert.NotNull(scene.TimeTravelSpiral);
                Assert.False(target.targetObject.visible);

                Step(scene, 0.7f);
                Assert.True(target.targetObject.visible);
                Assert.True(Runs(target, TargetAnimationState.LevelIntro));
            }, originalFlashOmNom: true, fromMenu: true);

            // Restarted, or entered with another Om Nom, it does not.
            WithTimeTravelGame(0, 0, (_, scene) => Assert.Equal(-1, scene.TimeTravelSpiralTarget), originalFlashOmNom: true, fromMenu: false);
            WithTimeTravelGame(0, 0, (_, scene) => Assert.Equal(-1, scene.TimeTravelSpiralTarget), originalFlashOmNom: false, fromMenu: true);
        }

        [Fact]
        public void ThePacksLastLevelDrawsTheOriginalOmNomBackIntoTheSpiralOnceHeHasChewed()
        {
            int last = Preferences.GetLevelsInPackCount(0) - 1;
            WithTimeTravelGame(0, last, (_, scene) =>
            {
                TargetContext target = scene.Targets[0];
                Assert.True(target.Feeding.TryOpenMouth(1f));
                Assert.True(target.Feeding.TryBeginChewing());
                target.animation.Play(TargetAnimationState.Chewing);
                scene.GameWon();
                Assert.Equal(0, scene.TimeTravelSpiralTarget);
                float chewing = ((FlashXmlTargetAnimationBackend)target.animation).GetPlaybackSeconds(TargetAnimationState.Chewing);

                Step(scene, chewing + 0.35f);
                Assert.True(Runs(target, TargetAnimationState.LevelOutro));
                Step(scene, 0.35f);
                Assert.NotNull(scene.TimeTravelSpiral);
            }, originalFlashOmNom: true);
        }

        [Fact]
        public void AWonLevelShutsTheBlindAcrossTheScreenAndCountsTheScore()
        {
            WithTimeTravelGame(0, 1, (controller, _) =>
            {
                BoxOpenClose box = (BoxOpenClose)controller.GetView(0).GetChild(GameView.VIEW_ELEMENT_RESULTS);
                LevelResult result = LevelResultCalculator.Calculate(elapsedTime: 20f, starsCollected: 2);

                controller.LevelWon(result);
                TimeTravelResultScreen screen = box.TimeTravelResult;
                Assert.True(screen.visible);
                Assert.False(box.result.IsEnabled());

                // The blind is stretched over the whole screen, as iOS stretched it.
                Rectangle visible = ScreenPresentation.Instance.Snapshot.VisibleBounds;
                Assert.Equal(visible.w / TimeTravelScreen.SceneWidth, screen.Folds.Root.scaleX, 3);
                Assert.Equal(visible.h / TimeTravelScreen.SceneHeight, screen.Folds.Root.scaleY, 3);
                Assert.Equal(3, screen.Stars.Count);
                Assert.Equal(3, All<Button>(screen.Overlay).Count);

                Step(box, 6f);
                Assert.True(screen.CountFinished);
                Assert.Equal(result.FinalScore.ToString(System.Globalization.CultureInfo.InvariantCulture), screen.Score.GetString());
                Assert.Equal(Application.GetString("FINAL_SCORE"), screen.CountTitle.GetString());
                Assert.True(screen.Stars[1].Root.updateable);
                Assert.False(screen.Stars[2].Root.updateable);

                // Moving on takes it away at once and opens the next level from white.
                controller.OnButtonPressed(GameControllerButtonId.NextLevel);
                Assert.False(screen.visible);
            });
        }

        [Fact]
        public void ResultStarsEmitFifteenParticlesAtTheNativeBurstTimeAndCleanUpOnReplay()
        {
            WithTimeTravel(1920, 1080, _ =>
            {
                using TimeTravelResultScreen screen = new(null);
                LevelResult result = LevelResultCalculator.Calculate(20f, 2);
                screen.Show(result, false);
                screen.Update(screen.Folds.RootTimeline(0).Duration);
                screen.Update(0.29f);
                Assert.Empty(All<MultiParticles>(screen));
                screen.Update(0.011f);
                MultiParticles burst = Assert.Single(All<MultiParticles>(screen));
                Assert.Equal(15, burst.particleCount);
                Assert.Equal(15, burst.totalParticles);
                Assert.Equal(5f, burst.life);
                Assert.Equal(0.9f, burst.size);
                Assert.Equal(150f * FlashXmlScale.AtlasToFlashPointScale, burst.gravity.Y);
                float life = burst.particles[0].life;
                float size = burst.particles[0].size;
                burst.Update(0.1f);
                Assert.Equal(life - 0.1f, burst.particles[0].life, 3);
                Assert.True(burst.particles[0].size < size);
                Assert.Equal(1f, burst.particles[0].color.AlphaChannel);
                for (int i = 0; i < burst.particleCount; i++)
                {
                    Assert.Contains(burst.drawer.texCoordinates[i], new[] { burst.imageGrid.texture.quads[26], burst.imageGrid.texture.quads[27] });
                }
                Step(screen, 0.4f);
                Assert.Equal(2, All<MultiParticles>(screen).Count);
                Assert.False(screen.Stars[2].Root.updateable);
                screen.Show(result, false);
                Assert.Empty(All<MultiParticles>(screen));
                Step(screen, 7f);
                Assert.Empty(All<MultiParticles>(screen));
            });
        }

        [Fact]
        public void ResultCountAndScoreUseWhiteInsteadOfTheFontsBlackFill()
        {
            WithTimeTravel(1920, 1080, _ =>
            {
                using TimeTravelResultScreen screen = new(null);
                screen.Show(LevelResultCalculator.Calculate(20f, 3), false);
                Step(screen, 2f);
                foreach (Text text in All<Text>(screen.Overlay))
                {
                    Assert.Equal(RGBAColor.solidOpaqueRGBA, text.colorOverride);
                }
            });
        }

        [Theory]
        [InlineData(1920, 1080)]
        [InlineData(720, 1280)]
        [InlineData(2940, 960)]
        public void AnImprovedResultShowsReadableLocalizedTextOnTheAnimatedBanner(int width, int height)
        {
            WithTimeTravel(width, height, _ =>
            {
                using TimeTravelResultScreen screen = new(null);
                screen.Show(LevelResultCalculator.Calculate(20f, 3), true);
                Step(screen, 6f);
                Text text = Assert.Single(All<Text>(screen), t => t.Name == "ttResultImprovedText");
                Assert.Equal(Application.GetString("IMPROVED_RESULT"), text.GetString());
                Assert.NotEqual("IMPROVED_RESULT", text.GetString());
                Assert.True(screen.GetChildWithName("ttResultImproved").visible);
                Assert.Empty(All<RectangleElement>(text.parent));
                // Darkest ribbon pixel beneath the fitted text, sampled from native q22.
                Assert.True(Contrast(text.colorOverride.Value, RGBAColor.MakeRGBA(26f / 255f, 163f / 255f, 205f / 255f, 1f)) >= 7f);
                Assert.True(text.width * text.scaleX <= text.parent.width - 16);
                Assert.True(text.height * text.scaleY <= text.parent.height - 31);
            });
        }

        private static double Contrast(RGBAColor foreground, RGBAColor background)
        {
            static double Linear(float component)
            {
                return component <= 0.04045f ? component / 12.92 : Math.Pow((component + 0.055) / 1.055, 2.4);
            }
            static double Luminance(RGBAColor color)
            {
                return (0.2126 * Linear(color.RedColor)) + (0.7152 * Linear(color.GreenColor)) + (0.0722 * Linear(color.BlueColor));
            }
            double first = Luminance(foreground);
            double second = Luminance(background);
            return (Math.Max(first, second) + 0.05) / (Math.Min(first, second) + 0.05);
        }

        [Theory]
        [InlineData(false, 1920, 1080)]
        [InlineData(true, 1920, 1080)]
        [InlineData(false, 720, 1280)]
        [InlineData(true, 720, 1280)]
        [InlineData(false, 2940, 960)]
        [InlineData(true, 2940, 960)]
        public void TheNextChapterCapsuleAppearsOnlyWhenEnoughStarsUnlockIt(bool enoughStars, int width, int height)
        {
            WithTimeTravelGame(0, 1, (controller, _) =>
            {
                const int next = 1;
                UNLOCKEDSTATE previous = Preferences.GetUnlockedForPackLevel(next, 0);
                int slot = PackConfig.GetSaveSlot(next);
                int previousStars = Preferences.GetStarsForPackLevel(slot, 0, 0);
                int needed = PackConfig.GetUnlockStars(next);
                try
                {
                    Preferences.SetUnlockedForPackLevel(UNLOCKEDSTATE.LOCKED, next, 0);
                    int otherStars = Preferences.GetTotalStarsInBox(slot) - previousStars;
                    Preferences.SetStarsForPackLevel(slot, needed - otherStars - (enoughStars ? 0 : 1), 0, 0);
                    BoxOpenClose box = (BoxOpenClose)controller.GetView(0).GetChild(GameView.VIEW_ELEMENT_RESULTS);
                    TimeTravelResultScreen screen = box.TimeTravelResult;
                    LevelResult result = LevelResultCalculator.Calculate(20f, 2);
                    screen.Show(result, false);
                    Step(screen, 1f);
                    BaseElement capsule = screen.GetChildWithName("ttResultNewChapter");
                    Assert.NotNull(capsule);
                    Assert.False(capsule.visible);
                    Assert.Equal(UNLOCKEDSTATE.LOCKED, Preferences.GetUnlockedForPackLevel(next, 0));
                    Step(screen, 7f);
                    Assert.Equal(enoughStars, capsule.visible);
                    Assert.Equal(enoughStars ? UNLOCKEDSTATE.JUSTUNLOCKED : UNLOCKEDSTATE.LOCKED, Preferences.GetUnlockedForPackLevel(next, 0));
                    if (enoughStars)
                    {
                        Text text = Assert.Single(All<Text>(capsule));
                        Assert.Equal(Application.GetString("NEW_CHAPTER_AVAILABLE"), text.GetString());
                        Assert.NotEqual("NEW_CHAPTER_AVAILABLE", text.GetString());
                        Assert.Empty(All<RectangleElement>(capsule));
                        // Darkest capsule pixel beneath the fitted text, sampled from native q2.
                        Assert.True(Contrast(text.colorOverride.Value, RGBAColor.MakeRGBA(156f / 255f, 238f / 255f, 234f / 255f, 1f)) >= 7f);
                        Assert.True(text.width * text.scaleX <= text.parent.width - 16);
                        Assert.True(text.height * text.scaleY <= text.parent.height - 31);
                        Assert.Equal(1, capsule.CurrentTimelineIndex);
                        screen.Show(result, false);
                        Step(screen, 8f);
                        Assert.False(screen.GetChildWithName("ttResultNewChapter").visible);
                    }
                }
                finally
                {
                    Preferences.SetUnlockedForPackLevel(previous, next, 0);
                    Preferences.SetStarsForPackLevel(slot, previousStars, 0, 0);
                }
            }, width: width, height: height);
        }

        [Theory]
        [InlineData((int)MenuStyle.TimeTravel)]
        [InlineData((int)MenuStyle.Classic)]
        public void OnlyTimeTravelReplacesTheClassicWinCueWithItsResultSounds(int styleValue)
        {
            MenuStyle style = (MenuStyle)styleValue;
            WithTimeTravelGame(0, 1, (controller, _) =>
            {
                RecordingResultAudio backend = new();
                SoundMgr manager = Application.SharedSoundMgr();
                string[] cues = [Resources.Snd.Win, Resources.Snd.ResultOpenTimeTravel, Resources.Snd.ResultStar1TimeTravel, Resources.Snd.ResultStar2TimeTravel, Resources.Snd.ResultStar3TimeTravel];
                bool soundOn = Preferences.GetBooleanForKey("SOUND_ON");
                foreach (string cue in cues)
                {
                    manager.FreeSound(cue);
                }
                SoundMgr.SetBackend(backend);
                Preferences.SetBooleanForKey(true, "SOUND_ON");
                try
                {
                    controller.LevelWon(LevelResultCalculator.Calculate(20f, 3));
                    Step((BoxOpenClose)controller.GetView(0).GetChild(GameView.VIEW_ELEMENT_RESULTS), 3f);
                    if (style == MenuStyle.TimeTravel)
                    {
                        Assert.DoesNotContain(Resources.Snd.Win, backend.Played);
                        foreach (string cue in cues.Skip(1))
                        {
                            Assert.Contains(cue, backend.Played);
                        }
                    }
                    else
                    {
                        Assert.Contains(Resources.Snd.Win, backend.Played);
                        Assert.DoesNotContain(Resources.Snd.ResultOpenTimeTravel, backend.Played);
                    }
                }
                finally
                {
                    manager.StopAllSounds();
                    foreach (string cue in cues)
                    {
                        manager.FreeSound(cue);
                    }
                    SoundMgr.SetBackend(null);
                    Preferences.SetBooleanForKey(soundOn, "SOUND_ON");
                }
            }, style: style);
        }

        private sealed class RecordingResultAudio : IAudioBackend
        {
            public List<string> Played { get; } = [];
            public AudioPlaybackState MusicState => AudioPlaybackState.Stopped;
            public ISoundEffect LoadSound(string contentPath)
            {
                return new Effect(this, Path.GetFileNameWithoutExtension(contentPath));
            }
            public IMusicTrack LoadMusic(string contentPath)
            {
                throw new NotSupportedException();
            }
            public void PlayMusic(IMusicTrack track, bool repeating) { }
            public void StopMusic() { }
            public void PauseMusic() { }
            public void ResumeMusic() { }

            private sealed class Effect(RecordingResultAudio backend, string cue) : ISoundEffect
            {
                public ISoundInstance CreateInstance()
                {
                    return new Instance(backend, cue);
                }
                public void Dispose() { }
            }

            private sealed class Instance(RecordingResultAudio backend, string cue) : ISoundInstance
            {
                public bool IsLooped { get; set; }
                public float Volume { get; set; }
                public AudioPlaybackState State { get; private set; }
                public void Play()
                {
                    backend.Played.Add(cue);
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
                public void Dispose() { }
            }
        }

        [Fact]
        public void TheGameScreenSheetsAndAnimationsAreInPlace()
        {
            _ = HeadlessGame.Boot();
            foreach (string xml in new[] { TimeTravelArt.HudStarAnimationXml, TimeTravelArt.LightningAnimationXml, TimeTravelArt.SpiralAnimationXml, TimeTravelArt.ResultAnimationXml })
            {
                Assert.True(File.Exists(ContentPaths.GetAnimationXmlAbsolutePath(xml)), xml);
            }
            Assert.Equal(45, Application.GetTexture(Resources.Img.ResultScreenTimeTravel).quadRects.Length);
            Assert.Equal(18, Application.GetTexture(Resources.Img.HudTimeTravel).quadRects.Length);
        }
    }
}
