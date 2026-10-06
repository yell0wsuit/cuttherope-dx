using System;
using System.Collections.Generic;

using CutTheRopeDX.Commons;
using CutTheRopeDX.Framework;
using CutTheRopeDX.Framework.Core;
using CutTheRopeDX.Framework.Platform;
using CutTheRopeDX.Framework.Visual;
using CutTheRopeDX.GameMain;

using Xunit;

namespace CutTheRopeDX.Tests
{
    /// <summary>
    /// Covers the Time Travel menus selected with <c>--menu timetravel</c>. The style is
    /// process-wide, which the serial suite makes safe to switch for one test at a time.
    /// </summary>
    public sealed class MenuTimeTravelTests
    {
        private sealed class NoDelegate : IButtonDelegation
        {
            public void OnButtonPressed(ButtonId buttonId)
            {
            }
        }

        private static readonly string[] MainMenuPieces = ["ttPlay", "ttOptions", "ttLogo"];

        private static readonly string[] CreditsBorderPieces = ["ttWindowTopWide", "ttWindowTopThin", "ttWindowBottomWide", "ttWindowBottomThin"];

        [Fact]
        public void TimeTravelSheetsLoadWithTheirFrames()
        {
            WithTimeTravel(2560, 1440, _ =>
            {
                Assert.Equal(20, Application.GetTexture(Resources.Img.MenuMainTimeTravel).quadRects.Length);
                Assert.Equal(17, Application.GetTexture(Resources.Img.MenuButtonSmallTimeTravel).quadRects.Length);
                Assert.Equal(3, Application.GetTexture(Resources.Img.LogoClockTimeTravel).quadRects.Length);
                Assert.Equal(2560, Application.GetTexture(Resources.BackgroundImg.MenuTimeTravelMainBgr)._realWidth);
            });
        }

        [Theory]
        [MemberData(nameof(LayoutSurfaces.Theory), MemberType = typeof(LayoutSurfaces))]
        public void MainMenuHasPlayOptionsAndTheLogoInsideTheScreen(string name, int width, int height)
        {
            _ = name;
            WithTimeTravel(width, height, controller =>
            {
                View view = controller.GetView(MenuController.VIEW_MAIN_MENU);
                controller.ShowView(MenuController.VIEW_MAIN_MENU);
                controller.Update(0.016f);
                ResolveDrawPositions(view);
                Rectangle visible = ScreenPresentation.Instance.Snapshot.VisibleBounds;

                foreach (string child in MainMenuPieces)
                {
                    BaseElement element = view.GetChildWithName(child);
                    Assert.NotNull(element);
                    Rectangle box = DrawnBox(element);
                    Assert.True(box.x >= visible.x - 0.5f && box.y >= visible.y - 0.5f, child + " left/top");
                    Assert.True(box.x + box.w <= visible.x + visible.w + 0.5f && box.y + box.h <= visible.y + visible.h + 0.5f, child + " right/bottom");
                }
                Assert.Equal(2, view.GetChildWithName("ttGlow").blendingMode);
            });
        }

        [Theory]
        [MemberData(nameof(LayoutSurfaces.Theory), MemberType = typeof(LayoutSurfaces))]
        public void PlayLandsWhereTheIosSceneDrawsItLoweredWithTheStack(string name, int width, int height)
        {
            _ = name;
            WithTimeTravel(width, height, controller =>
            {
                View view = controller.GetView(MenuController.VIEW_MAIN_MENU);
                controller.ShowView(MenuController.VIEW_MAIN_MENU);
                controller.Update(0.016f);
                ResolveDrawPositions(view);

                // iOS q19 spans (264, 922) to (907, 1513) canvas pixels: centered on (292.75, 608.75) logical.
                Vector expected = new TimeTravelScreen(ScreenPresentation.Instance.Snapshot.VisibleBounds)
                    .ToDesign(292.75f, 608.75f + MenuController.TimeTravelStackDrop);
                Rectangle box = DrawnBox(view.GetChildWithName("ttPlay"));
                // The packs round offsets to whole asset pixels, so within one design unit; a doubled
                // or missing atlas offset would be off by hundreds.
                Assert.InRange(box.x + (box.w / 2f) - expected.X, -1f, 1f);
                Assert.InRange(box.y + (box.h / 2f) - expected.Y, -1f, 1f);
            });
        }

        [Theory]
        [InlineData("Portrait", 720, 1280)]
        [InlineData("TallPortrait", 400, 1280)]
        [InlineData("Native", 2560, 1440)]
        public void TouchesLandOnTheButtonsAsDrawn(string name, int width, int height)
        {
            _ = name;
            WithTimeTravel(width, height, controller =>
            {
                View view = controller.GetView(MenuController.VIEW_MAIN_MENU);
                controller.ShowView(MenuController.VIEW_MAIN_MENU);
                controller.Update(0.016f);
                ResolveDrawPositions(view);
                Button play = (Button)view.GetChildWithName("ttPlay");
                Button options = (Button)view.GetChildWithName("ttOptions");

                Rectangle optionsBox = DrawnBox(options);
                Assert.True(view.OnTouchDownXY(optionsBox.x + (optionsBox.w / 2f), optionsBox.y + (optionsBox.h / 2f)));
                Assert.Equal(Button.BUTTON_STATE.BUTTON_DOWN, options.state);
                Assert.Equal(Button.BUTTON_STATE.BUTTON_UP, play.state);
                _ = view.OnTouchUpXY(-1000f, -1000f);

                // Just above Play's drawn bottom edge, which an unmapped hit test hands to Options.
                Rectangle playBox = DrawnBox(play);
                Assert.True(view.OnTouchDownXY(playBox.x + (playBox.w / 2f), playBox.y + (playBox.h * 0.9f)));
                Assert.Equal(Button.BUTTON_STATE.BUTTON_DOWN, play.state);
                Assert.Equal(Button.BUTTON_STATE.BUTTON_UP, options.state);
                _ = view.OnTouchUpXY(-1000f, -1000f);
            });
        }

        [Theory]
        [InlineData(2560, 1440)]
        [InlineData(1280, 720)]
        public void AtTheDesignShapeTheLogoTopMatchesTheDxMenus(int width, int height)
        {
            WithTimeTravel(width, height, controller =>
            {
                View view = ShowMainMenu(controller);

                // The DX main menu draws its logo 55 design pixels into its design box, which
                // fills the screen at this shape.
                Assert.InRange(DrawnBox(view.GetChildWithName("ttLogo")).y, 55f - 0.5f, 55f + 0.5f);
            });
        }

        [Theory]
        [MemberData(nameof(LayoutSurfaces.Theory), MemberType = typeof(LayoutSurfaces))]
        public void TheLoweredStackStaysOnScreen(string name, int width, int height)
        {
            _ = name;
            WithTimeTravel(width, height, controller =>
            {
                View view = ShowMainMenu(controller);
                Rectangle visible = ScreenPresentation.Instance.Snapshot.VisibleBounds;
                Rectangle options = DrawnBox(view.GetChildWithName("ttOptions"));

                Assert.True(options.y + options.h <= visible.y + visible.h, "the capsule row runs off the bottom");
            });
        }

        [Theory]
        [MemberData(nameof(LayoutSurfaces.Theory), MemberType = typeof(LayoutSurfaces))]
        public void TheLogoSitsInTheTimeTravelTitleBox(string name, int width, int height)
        {
            _ = name;
            WithTimeTravel(width, height, controller =>
            {
                View view = ShowMainMenu(controller);
                TimeTravelScreen screen = new(ScreenPresentation.Instance.Snapshot.VisibleBounds);
                Vector origin = screen.ToDesign(0f, 0f);
                Rectangle logo = DrawnBox(view.GetChildWithName("ttLogo"));
                Rectangle play = DrawnBox(view.GetChildWithName("ttPlay"));
                float scale = MenuController.TimeTravelLogoScale;

                // In the iOS scene's logical units.
                float top = (logo.y - origin.Y) / screen.Scale;
                float center = (logo.x + (logo.w / 2f) - origin.X) / screen.Scale;
                float logicalWidth = logo.w / screen.Scale;
                float expectedTop = MenuController.TimeTravelLogoTop + MenuController.TimeTravelStackDrop;
                Assert.InRange(top, expectedTop - 0.5f, expectedTop + 0.5f);
                Assert.InRange(center, (TimeTravelScreen.SceneWidth / 2f) - 0.5f, (TimeTravelScreen.SceneWidth / 2f) + 0.5f);
                float artWidth = view.GetChildWithName("ttLogo").width * scale / FlashXmlScale.AtlasToFlashPointScale;
                Assert.InRange(logicalWidth, artWidth - 0.5f, artWidth + 0.5f);
                Assert.True(scale < 1f);
                Assert.True(logo.y + logo.h <= play.y + 0.5f, "the logo reaches into Play");
            });
        }

        [Theory]
        [MemberData(nameof(LayoutSurfaces.Theory), MemberType = typeof(LayoutSurfaces))]
        public void TwoCapsulesSitSideBySideInsideTheScreen(string name, int width, int height)
        {
            _ = name;
            IHostApp previousHost = PlatformServices.Host;
            PlatformServices.Host = new QuitHost();
            try
            {
                WithTimeTravel(width, height, controller =>
                {
                    View view = controller.GetView(MenuController.VIEW_MAIN_MENU);
                    controller.ShowView(MenuController.VIEW_MAIN_MENU);
                    controller.Update(0.016f);
                    ResolveDrawPositions(view);
                    Rectangle visible = ScreenPresentation.Instance.Snapshot.VisibleBounds;
                    Rectangle options = DrawnBox(view.GetChildWithName("ttOptions"));
                    Rectangle quit = DrawnBox(view.GetChildWithName("ttQuit"));

                    foreach (Rectangle box in new[] { options, quit })
                    {
                        Assert.True(box.x >= visible.x - 0.5f && box.x + box.w <= visible.x + visible.w + 0.5f);
                        Assert.True(box.y >= visible.y - 0.5f && box.y + box.h <= visible.y + visible.h + 0.5f);
                    }
                    Assert.True(options.x + options.w <= quit.x, "the capsules overlap");
                    Assert.Equal(options.y, quit.y, 1);
                    float sceneCenter = new TimeTravelScreen(visible).ToDesign(TimeTravelScreen.SceneWidth / 2f, 0f).X;
                    Assert.InRange(((options.x + quit.x + quit.w) / 2f) - sceneCenter, -1f, 1f);
                });
            }
            finally
            {
                PlatformServices.Host = previousHost;
            }
        }

        [Theory]
        [MemberData(nameof(LayoutSurfaces.Theory), MemberType = typeof(LayoutSurfaces))]
        public void SubViewBackButtonsSitInTheBottomLeftCorner(string name, int width, int height)
        {
            _ = name;
            WithTimeTravel(width, height, controller =>
            {
                Rectangle visible = ScreenPresentation.Instance.Snapshot.VisibleBounds;
                foreach (int id in new[] { MenuController.VIEW_OPTIONS, MenuController.VIEW_LANGUAGE_SELECT, MenuController.VIEW_RESET, MenuController.VIEW_ABOUT })
                {
                    View view = controller.GetView(id);
                    controller.ShowView(id);
                    controller.Update(0.016f);
                    ResolveDrawPositions(view);
                    Rectangle back = DrawnBox(view.GetChildWithName("backb"));

                    Assert.True(back.x >= visible.x - 0.5f && back.y + back.h <= visible.y + visible.h + 0.5f, "inside the screen");
                    Assert.True(back.x + (back.w / 2f) < visible.x + (visible.w / 2f), "left half");
                    Assert.True(back.y + (back.h / 2f) > visible.y + (visible.h / 2f), "bottom half");
                }
            });
        }

        [Theory]
        [InlineData(2560, 1440)]
        [InlineData(400, 1280)]
        public void CreditsBordersStraddleTheWindowEdges(int width, int height)
        {
            WithTimeTravel(width, height, controller =>
            {
                View about = controller.GetView(MenuController.VIEW_ABOUT);
                ScrollableContainer window = Find<ScrollableContainer>(about);
                float half = window.height / 2f;
                foreach ((string piece, float edge) in new[]
                {
                    ("ttWindowTopWide", -half), ("ttWindowTopThin", -half),
                    ("ttWindowBottomWide", half), ("ttWindowBottomThin", half),
                })
                {
                    BaseElement strip = about.GetChildWithName(piece);
                    Assert.InRange(strip.y - edge, -strip.height / 2f, strip.height / 2f);
                }
            });
        }

        private sealed class QuitHost : IHostApp
        {
            public bool CanExit => true;

            public string LevelEditorUrl => null;

            public string CustomLevelExitLabelKey => null;

            public void Exit()
            {
            }

            public bool IsKeyPressed(KeyCode key)
            {
                return false;
            }

            public void DrawMovie()
            {
            }

            public void OpenUrl(string url)
            {
            }
        }

        private static readonly int[] SettingsViews =
        [
            MenuController.VIEW_OPTIONS, MenuController.VIEW_LANGUAGE_SELECT, MenuController.VIEW_RESET, MenuController.VIEW_ABOUT,
        ];

        [Fact]
        public void EverySettingsViewHasTheFanCorner()
        {
            WithTimeTravel(2560, 1440, controller =>
            {
                foreach (int id in SettingsViews)
                {
                    View view = controller.GetView(id);
                    Assert.NotNull(view.GetChildWithName("ttFanPlate"));
                    Assert.NotNull(view.GetChildWithName("ttFan"));
                    Assert.NotNull(view.GetChildWithName("ttFanHub"));
                }
                Assert.Null(controller.GetView(MenuController.VIEW_MAIN_MENU).GetChildWithName("ttFan"));
            });
        }

        [Theory]
        [MemberData(nameof(LayoutSurfaces.Theory), MemberType = typeof(LayoutSurfaces))]
        public void TheFanCornerHugsTheVisibleTopLeft(string name, int width, int height)
        {
            _ = name;
            WithTimeTravel(width, height, controller =>
            {
                Rectangle visible = ScreenPresentation.Instance.Snapshot.VisibleBounds;
                float assetScale = new TimeTravelScreen(visible).AssetScale;
                foreach (int id in SettingsViews)
                {
                    View view = controller.GetView(id);
                    controller.ShowView(id);
                    controller.Update(0.016f);
                    ResolveDrawPositions(view);
                    Rectangle plate = DrawnBox(view.GetChildWithName("ttFanPlate"));

                    // The plate keeps its atlas offset (trimmed by the packer) from a scene origin
                    // pinned to the visible corner.
                    Vector offset = Image.GetQuadOffset(Resources.Img.MenuMainTimeTravel, TimeTravelArt.FanPlate);
                    Assert.InRange(plate.x - (visible.x + (offset.X * assetScale)), -0.5f, 0.5f);
                    Assert.InRange(plate.y - (visible.y + ((offset.Y - TimeTravelArt.MenuMainOriginY) * assetScale)), -0.5f, 0.5f);
                }
            });
        }

        [Fact]
        public void TheFanSpinsUnderAStillHub()
        {
            WithTimeTravel(2560, 1440, controller =>
            {
                View view = controller.GetView(MenuController.VIEW_OPTIONS);
                controller.ShowView(MenuController.VIEW_OPTIONS);
                BaseElement fan = view.GetChildWithName("ttFan");
                BaseElement hub = view.GetChildWithName("ttFanHub");
                fan.Update(0.25f);

                Assert.InRange(fan.rotation, 89f, 91f);
                Assert.Equal(0f, hub.rotation);
                Assert.False(fan.passTransformationsToChilds);
            });
        }

        [Fact]
        public void MainMenuPlacementSurvivesAResize()
        {
            WithTimeTravel(2560, 1440, controller =>
            {
                controller.ShowView(MenuController.VIEW_MAIN_MENU);
                GameLifecycle.OnSurfaceChanged(720, 1280);
                controller.RelayoutTree(ScreenPresentation.Instance.Snapshot);
                View resized = controller.GetView(MenuController.VIEW_MAIN_MENU);
                ResolveDrawPositions(resized);
                Rectangle afterResize = DrawnBox(resized.GetChildWithName("ttPlay"));

                MenuController fresh = new(Application.SharedRootController());
                try
                {
                    View built = fresh.GetView(MenuController.VIEW_MAIN_MENU);
                    fresh.RelayoutTree(ScreenPresentation.Instance.Snapshot);
                    ResolveDrawPositions(built);
                    Rectangle freshBox = DrawnBox(built.GetChildWithName("ttPlay"));
                    Assert.Equal(freshBox.x, afterResize.x, 1);
                    Assert.Equal(freshBox.y, afterResize.y, 1);
                    Assert.Equal(freshBox.w, afterResize.w, 1);
                }
                finally
                {
                    fresh.Dispose();
                }
            });
        }

        [Fact]
        public void OptionsUseTimeTravelPlatesAndAudioToggles()
        {
            WithTimeTravel(2560, 1440, controller =>
            {
                View options = controller.GetView(MenuController.VIEW_OPTIONS);
                Texture2D big = Application.GetTexture(Resources.Img.MenuButtonBigTimeTravel);
                Texture2D small = Application.GetTexture(Resources.Img.MenuButtonSmallTimeTravel);

                Assert.Equal(3, All<Image>(options).FindAll(i => i.texture == big && i.quadToDraw == TimeTravelArt.LongPlateUp).Count);
                Assert.True(All<Image>(options).FindAll(i => i.texture == small && i.quadToDraw == TimeTravelArt.AudioPlateUp).Count >= 2);
                Texture2D shaft = Application.GetTexture(Resources.Img.MenuBgrShadow);
                Assert.Null(All<Image>(options).Find(i => i.texture == shaft));
            });
        }

        [Fact]
        public void EveryTimeTravelSubViewHasTheTimeTravelBackButton()
        {
            WithTimeTravel(2560, 1440, controller =>
            {
                Texture2D buttons = Application.GetTexture(Resources.Img.MenuButtonsTimeTravel);
                foreach (int id in new[] { MenuController.VIEW_OPTIONS, MenuController.VIEW_LANGUAGE_SELECT, MenuController.VIEW_RESET, MenuController.VIEW_ABOUT })
                {
                    BaseElement back = controller.GetView(id).GetChildWithName("backb");
                    Assert.NotNull(back);
                    Assert.NotNull(All<Image>(back).Find(i => i.texture == buttons && i.quadToDraw == TimeTravelArt.BackArrow));
                }
            });
        }

        [Fact]
        public void LanguageButtonsUseShortCapsules()
        {
            WithTimeTravel(2560, 1440, controller =>
            {
                Texture2D small = Application.GetTexture(Resources.Img.MenuButtonSmallTimeTravel);
                List<Image> plates = All<Image>(controller.GetView(MenuController.VIEW_LANGUAGE_SELECT))
                    .FindAll(i => i.texture == small && (i.quadToDraw == TimeTravelArt.ShortCapsuleUp || i.quadToDraw == TimeTravelArt.ShortCapsuleDown));

                Assert.Equal(LanguageHelper.UiLanguageCodes.Count * 2, plates.Count);
            });
        }

        [Fact]
        public void OptionsKeepTimeTravelArtAfterALanguageSwitch()
        {
            WithTimeTravel(2560, 1440, controller =>
            {
                IReadOnlyList<string> codes = LanguageHelper.UiLanguageCodes;
                int original = Math.Max(0, IndexOf(codes, Preferences.GetStringForKey("PREFS_LOCALE")));
                int other = original == 0 ? 1 : 0;
                try
                {
                    controller.ShowView(MenuController.VIEW_OPTIONS);
                    controller.OnButtonPressed(MenuButtonId.ForLanguage(other));
                    controller.Update(0.016f);

                    Texture2D big = Application.GetTexture(Resources.Img.MenuButtonBigTimeTravel);
                    Assert.NotEmpty(All<Image>(controller.GetView(MenuController.VIEW_OPTIONS)).FindAll(i => i.texture == big));
                    Assert.NotNull(controller.GetView(MenuController.VIEW_MAIN_MENU).GetChildWithName("ttPlay"));
                }
                finally
                {
                    controller.OnButtonPressed(MenuButtonId.ForLanguage(original));
                }
            });
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void ShortPillsStretchToTheDxButtonLength(bool selected)
        {
            WithTimeTravel(2560, 1440, _ =>
            {
                Button button = MenuController.CreateShortButtonWithTextIDDelegate("Replay", MenuButtonId.Options, new NoDelegate(), selected);
                float dxWidth = Image.GetQuadSize(Resources.Img.MenuButtons, 3).X;

                Assert.InRange(button.width, dxWidth - 1f, dxWidth + 1f);
                for (int i = 0; i < 2; i++)
                {
                    BaseElement plate = button.GetChild(i);
                    Assert.InRange(plate.width, dxWidth - 1f, dxWidth + 1f);
                    SlicedImage art = Find<SlicedImage>(plate);
                    Assert.NotNull(art);
                    Assert.InRange(art.width * art.scaleX, dxWidth - 1f, dxWidth + 1f);
                }
            });
        }

        [Fact]
        public void ALabelTooLongForAShortPillShrinksInsideIt()
        {
            WithTimeTravel(2560, 1440, _ =>
            {
                Button button = MenuController.CreateShortButtonWithTextIDDelegate(new string('W', 400), MenuButtonId.Options, new NoDelegate());
                float dxWidth = Image.GetQuadSize(Resources.Img.MenuButtons, 3).X;
                Text label = Find<Text>(button.GetChild(0));

                Assert.InRange(button.width, dxWidth - 1f, dxWidth + 1f);
                Assert.True(label.scaleX < 1f);
                float scale = TimeTravelPlates.HeightMatching(
                    Resources.Img.MenuButtonSmallTimeTravel, TimeTravelArt.ShortCapsuleUp, Resources.Img.MenuButtons, 3);
                float padding = TimeTravelPlates.SidePadding(Resources.Img.MenuButtonSmallTimeTravel, TimeTravelArt.ShortCapsuleUp, scale);
                Assert.True(label.width * label.scaleX <= button.width - padding + 0.5f);
            });
        }

        [Theory]
        [MemberData(nameof(LayoutSurfaces.Theory), MemberType = typeof(LayoutSurfaces))]
        public void MainMenuCapsulesAreAsLongAsTheLanguageButtons(string name, int width, int height)
        {
            _ = name;
            WithTimeTravel(width, height, controller =>
            {
                View view = ShowMainMenu(controller);
                float expected = Image.GetQuadSize(Resources.Img.MenuButtons, 3).X * ContentFit.Scale;

                Assert.InRange(DrawnBox(view.GetChildWithName("ttOptions")).w, expected - 1.5f, expected + 1.5f);
                if (view.GetChildWithName("ttQuit") is { } second)
                {
                    Assert.InRange(DrawnBox(second).w, expected - 1.5f, expected + 1.5f);
                }
            });
        }

        [Theory]
        [InlineData(2560, 1440)]
        [InlineData(720, 1280)]
        public void ACapsuleDrawsAsLongAsALanguageButtonBesideIt(int width, int height)
        {
            WithTimeTravel(width, height, controller =>
            {
                View language = controller.GetView(MenuController.VIEW_LANGUAGE_SELECT);
                controller.ShowView(MenuController.VIEW_LANGUAGE_SELECT);
                controller.Update(0.016f);
                ResolveDrawPositions(language);
                float languageWidth = DrawnBox(All<Button>(language).Find(b => b.buttonID == MenuButtonId.ForLanguage(0))).w;

                View view = ShowMainMenu(controller);

                Assert.InRange(DrawnBox(view.GetChildWithName("ttOptions")).w, languageWidth - 1.5f, languageWidth + 1.5f);
            });
        }

        [Fact]
        public void APairOfCapsulesNarrowsOnlyWhereTheRowWouldLeaveTheScreen()
        {
            WithTimeTravel(2560, 1440, _ =>
            {
                float a = FlashXmlScale.AtlasToFlashPointScale;
                float dx = Image.GetQuadSize(Resources.Img.MenuButtons, 3).X;

                Rectangle native = new(0f, 0f, 2560f, 1440f);
                float nativeWidth = MenuController.TimeTravelCapsuleWidth(2, native, 1f);
                Assert.Equal(dx / new TimeTravelScreen(native).AssetScale, nativeWidth, 1);

                // The tallest supported portrait: two language-length capsules would overrun it.
                Rectangle tall = new(0f, 0f, 1440f, 1440f / ViewportLayout.MinAspect);
                float contentScale = ContentFit.ScaleForAspect(ViewportLayout.MinAspect);
                float assetScale = new TimeTravelScreen(tall).AssetScale;
                float tallWidth = MenuController.TimeTravelCapsuleWidth(2, tall, contentScale);
                float row = (2f * tallWidth) + (MenuController.TimeTravelCapsuleGap * a);
                float room = (tall.w / assetScale) - (2f * MenuController.TimeTravelCapsuleRowMargin * a);
                Assert.True(tallWidth < dx * contentScale / assetScale);
                Assert.Equal(room, row, 1);
            });
        }

        [Fact]
        public void TheCapsulesFollowTheWindowIntoAnotherShape()
        {
            WithTimeTravel(2560, 1440, controller =>
            {
                GameLifecycle.OnSurfaceChanged(720, 1280);
                View view = ShowMainMenu(controller);
                float expected = Image.GetQuadSize(Resources.Img.MenuButtons, 3).X * ContentFit.Scale;

                Assert.True(ContentFit.Scale > 1f);
                Assert.InRange(DrawnBox(view.GetChildWithName("ttOptions")).w, expected - 1.5f, expected + 1.5f);
            });
        }

        [Fact]
        public void ResizingAPillRefitsItsLabel()
        {
            WithTimeTravel(2560, 1440, _ =>
            {
                string small = Resources.Img.MenuButtonSmallTimeTravel;
                Button button = TimeTravelPlates.CreatePillButton(
                    small, TimeTravelArt.CapsuleUp, TimeTravelArt.CapsuleDown, new string('W', 60), MenuButtonId.Options, new NoDelegate(), 1f, 1000f);
                Assert.Equal(1f, Find<Text>(button.GetChild(0)).scaleX);

                // Wider than the art, but too narrow for the label at full size.
                TimeTravelPlates.ResizePillButton(button, small, TimeTravelArt.CapsuleUp, TimeTravelArt.CapsuleDown, 1f, 400f);

                Assert.Equal(400f, button.width, 1);
                for (int i = 0; i < 2; i++)
                {
                    Assert.Equal(400f, button.GetChild(i).width, 1);
                    Text label = Find<Text>(button.GetChild(i));
                    Assert.True(label.scaleX < 1f);
                    Assert.True(label.width * label.scaleX <= 400f - TimeTravelPlates.SidePadding(small, TimeTravelArt.CapsuleUp, 1f) + 0.5f);
                }

                TimeTravelPlates.ResizePillButton(button, small, TimeTravelArt.CapsuleUp, TimeTravelArt.CapsuleDown, 1f, 1000f);

                Assert.Equal(1f, Find<Text>(button.GetChild(0)).scaleX);
            });
        }

        private static View ShowMainMenu(MenuController controller)
        {
            View view = controller.GetView(MenuController.VIEW_MAIN_MENU);
            controller.ShowView(MenuController.VIEW_MAIN_MENU);
            controller.Update(0.016f);
            ResolveDrawPositions(view);
            return view;
        }

        [Fact]
        public void LongLabelsShrinkToFitTheirPlate()
        {
            WithTimeTravel(2560, 1440, _ =>
            {
                BaseElement plate = TimeTravelPlates.LabeledPlate(
                    Resources.Img.MenuButtonSmallTimeTravel, TimeTravelArt.CapsuleUp, new string('W', 400), 1f);
                Text label = Find<Text>(plate);

                Assert.True(label.width * label.scaleX <= (plate.width * TimeTravelPlates.LabelWidthShare) + 0.5f);
                Assert.True(label.scaleX < 1f);
                Assert.Equal(label.scaleX, label.scaleY);
            });
        }

        [Fact]
        public void ShortLabelsKeepTheirSize()
        {
            WithTimeTravel(2560, 1440, _ =>
            {
                Text label = Find<Text>(TimeTravelPlates.LabeledPlate(
                    Resources.Img.MenuButtonSmallTimeTravel, TimeTravelArt.CapsuleUp, "OK", 1f));

                Assert.Equal(1f, label.scaleX);
            });
        }

        [Fact]
        public void ResetYesIsAThreeSecondHoldButton()
        {
            WithTimeTravel(2560, 1440, controller =>
            {
                TimedButton yes = Find<TimedButton>(controller.GetView(MenuController.VIEW_RESET));

                Assert.NotNull(yes);
                Assert.Equal(3f, yes.HoldDuration);
                Assert.Equal((ButtonId)MenuButtonId.ConfirmResetYes, yes.buttonID);
                Assert.Equal(0.9f, yes.GetChild(1).GetChild(0).scaleX / yes.GetChild(0).GetChild(0).scaleX, 3);
            });
        }

        [Fact]
        public void CreditsKeepTheDxContentInsideTheTimeTravelFrame()
        {
            WithTimeTravel(2560, 1440, controller =>
            {
                View about = controller.GetView(MenuController.VIEW_ABOUT);
                foreach (string piece in CreditsBorderPieces)
                {
                    Assert.NotNull(about.GetChildWithName(piece));
                }
                Assert.Equal(-1f, about.GetChildWithName("ttWindowTopWide").scaleY);
                Assert.Equal(1f, about.GetChildWithName("ttWindowBottomWide").scaleY);

                // The scrolling content is DX's own: its logos, and its links drawn as text.
                Assert.Null(Find<TimeTravelLogo>(about));
                Assert.Null(about.GetChildWithName("ttZeptoLab"));
                Texture2D dxLogo = Application.GetTexture(Resources.Img.CutTheRopeDXLogo);
                Texture2D menuLogo = Application.GetTexture(Resources.Img.MenuLogo);
                Assert.NotNull(All<Image>(about).Find(i => i.texture == dxLogo));
                Assert.NotNull(All<Image>(about).Find(i => i.texture == menuLogo && i.quadToDraw == 1));
                Button link = All<Button>(about).Find(b => b.buttonID == MenuButtonId.FanworkProjectWebsite);
                _ = Assert.IsType<Text>(link.GetChild(0));
                Texture2D big = Application.GetTexture(Resources.Img.MenuButtonBigTimeTravel);
                Assert.Null(All<Image>(about).Find(i => i.texture == big));
            });
        }

        [Fact]
        public void CreditsKeepTheirPiecesWhenRebuiltForANewScale()
        {
            WithTimeTravel(2560, 1440, controller =>
            {
                controller.ShowView(MenuController.VIEW_ABOUT);
                GameLifecycle.OnSurfaceChanged(400, 1280);
                controller.RelayoutTree(ScreenPresentation.Instance.Snapshot);

                Assert.NotNull(controller.GetView(MenuController.VIEW_ABOUT).GetChildWithName("ttWindowBottomThin"));
            });
        }

        [Fact]
        public void CreditsScrollAtTimeTravelSpeed()
        {
            WithTimeTravel(2560, 1440, controller =>
            {
                controller.OnButtonPressed(MenuButtonId.ShowCredits);
                AboutView about = (AboutView)typeof(MenuController)
                    .GetField("aboutView", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                    .GetValue(controller);
                float before = about.ScrollOffset.Y;

                Assert.True(about.UpdateAutoScroll(0.5f));

                float expected = 0.5f * 30f * new TimeTravelScreen(ScreenPresentation.Instance.Snapshot.VisibleBounds).Scale;
                Assert.Equal(before + expected, about.ScrollOffset.Y, 2);
            });
        }

        private static int IndexOf(IReadOnlyList<string> list, string value)
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] == value)
                {
                    return i;
                }
            }
            return -1;
        }

        internal static void ResolveDrawPositions(BaseElement element)
        {
            BaseElement.CalculateTopLeft(element);
            foreach (BaseElement child in element.GetChilds().Values)
            {
                if (child != null)
                {
                    ResolveDrawPositions(child);
                }
            }
        }

        /// <summary>The element's drawn rectangle, with its own and every ancestor's scale about their centers applied.</summary>
        internal static Rectangle DrawnBox(BaseElement element)
        {
            float w = element.width * element.scaleX;
            float h = element.height * element.scaleY;
            float x = element.drawX + ((element.width - w) / 2f);
            float y = element.drawY + ((element.height - h) / 2f);
            for (BaseElement p = element.parent; p != null; p = p.parent)
            {
                float cx = p.drawX + (p.width >> 1);
                float cy = p.drawY + (p.height >> 1);
                x = cx + ((x - cx) * p.scaleX);
                y = cy + ((y - cy) * p.scaleY);
                w *= p.scaleX;
                h *= p.scaleY;
            }
            return new Rectangle(x, y, w, h);
        }

        internal static void WithTimeTravel(int width, int height, Action<MenuController> body)
        {
            _ = HeadlessGame.Boot();
            MenuStyle previous = MenuTheme.Current;
            MenuTheme.Current = MenuStyle.TimeTravel;
            try
            {
                LayoutSurfaces.WithSurface(width, height, () =>
                {
                    MenuController controller = new(Application.SharedRootController());
                    try
                    {
                        body(controller);
                    }
                    finally
                    {
                        controller.Dispose();
                    }
                });
            }
            finally
            {
                MenuTheme.Current = previous;
            }
        }

        internal static List<T> All<T>(BaseElement root) where T : BaseElement
        {
            List<T> found = [];
            Walk(root, found);
            return found;
        }

        internal static T Find<T>(BaseElement root) where T : BaseElement
        {
            List<T> found = All<T>(root);
            return found.Count > 0 ? found[0] : null;
        }

        private static void Walk<T>(BaseElement element, List<T> found) where T : BaseElement
        {
            if (element is T match)
            {
                found.Add(match);
            }
            foreach (BaseElement child in element.GetChilds().Values)
            {
                if (child != null)
                {
                    Walk(child, found);
                }
            }
        }
    }
}
