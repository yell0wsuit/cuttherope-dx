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
