using System;
using System.Collections.Generic;

using CutTheRopeDX.Framework.Core;
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
