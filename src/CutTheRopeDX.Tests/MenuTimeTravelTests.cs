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

                foreach (string child in new[] { "ttPlay", "ttOptions", "ttLogo" })
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
        public void PlayLandsWhereTheIosSceneDrawsIt(string name, int width, int height)
        {
            _ = name;
            WithTimeTravel(width, height, controller =>
            {
                View view = controller.GetView(MenuController.VIEW_MAIN_MENU);
                controller.ShowView(MenuController.VIEW_MAIN_MENU);
                controller.Update(0.016f);
                ResolveDrawPositions(view);

                // iOS q19 spans (264, 922) to (907, 1513) canvas pixels: centered on (292.75, 608.75) logical.
                Vector expected = new TimeTravelScreen(ScreenPresentation.Instance.Snapshot.VisibleBounds).ToDesign(292.75f, 608.75f);
                Rectangle box = DrawnBox(view.GetChildWithName("ttPlay"));
                // The packs round offsets to whole asset pixels, so within one design unit; a doubled
                // or missing atlas offset would be off by hundreds.
                Assert.InRange(box.x + (box.w / 2f) - expected.X, -1f, 1f);
                Assert.InRange(box.y + (box.h / 2f) - expected.Y, -1f, 1f);
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
        public void CreditsCarryTheTimeTravelPiecesAndBorders()
        {
            WithTimeTravel(2560, 1440, controller =>
            {
                View about = controller.GetView(MenuController.VIEW_ABOUT);
                foreach (string piece in new[] { "ttLogo", "ttZeptoLab", "ttWindowTopWide", "ttWindowTopThin", "ttWindowBottomWide", "ttWindowBottomThin" })
                {
                    Assert.NotNull(about.GetChildWithName(piece));
                }

                Assert.Equal(-1f, about.GetChildWithName("ttWindowTopWide").scaleY);
                Assert.Equal(1f, about.GetChildWithName("ttWindowBottomWide").scaleY);
                Assert.Null(All<Button>(about.GetChildWithName("ttLogo")).Find(b => b.buttonID == MenuButtonId.CandySelect));
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
