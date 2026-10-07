using System;

using CutTheRopeDX.Framework.Core;
using CutTheRopeDX.Framework.Visual;
using CutTheRopeDX.GameMain;

using Xunit;

namespace CutTheRopeDX.Tests
{
    /// <summary>
    /// Covers the Halloween dressing of the classic menus: webs and bats over the backdrop, and
    /// the witch hat on the logo.
    /// </summary>
    public sealed class HalloweenMenuTests
    {
        [Fact]
        public void MainMenuGetsItsWebBatsAndTheHat()
        {
            // A web, three bats, and the hat's brim and crown.
            Assert.Equal(6, DecorationsOn(MenuController.VIEW_MAIN_MENU, SeasonalDate.Halloween));
        }

        [Fact]
        public void InnerMenusGetTheirOwnWebsAndBats()
        {
            Assert.Equal(5, DecorationsOn(MenuController.VIEW_OPTIONS, SeasonalDate.Halloween));
        }

        [Fact]
        public void NoDecorationsOutsideHalloween()
        {
            Assert.Equal(0, DecorationsOn(MenuController.VIEW_MAIN_MENU, SeasonalDate.NoEvent));
            Assert.Equal(0, DecorationsOn(MenuController.VIEW_OPTIONS, SeasonalDate.NoEvent));
        }

        [Fact]
        public void HalloweenSwapsInItsBackdrop()
        {
            Assert.True(UsesTexture(MenuController.VIEW_MAIN_MENU, SeasonalDate.Halloween, Resources.Img.MenuBgrHalloween));
            Assert.False(UsesTexture(MenuController.VIEW_MAIN_MENU, SeasonalDate.NoEvent, Resources.Img.MenuBgrHalloween));
        }

        private static int DecorationsOn(int viewId, DateTime date)
        {
            return CountTexture(viewId, date, Resources.Img.MenuBgrHalloweenDecorations);
        }

        private static bool UsesTexture(int viewId, DateTime date, string resource)
        {
            return CountTexture(viewId, date, resource) > 0;
        }

        private static int CountTexture(int viewId, DateTime date, string resource)
        {
            int count = 0;
            SeasonalDate.With(date, () =>
            {
                _ = HeadlessGame.Boot();
                MenuStyle previousStyle = MenuTheme.Current;
                MenuTheme.Current = MenuStyle.Classic;
                MenuController controller = new(Application.SharedRootController());
                try
                {
                    Texture2D texture = Application.GetTexture(resource);
                    count = Count(controller.GetView(viewId), texture);
                }
                finally
                {
                    controller.Dispose();
                    MenuTheme.Current = previousStyle;
                }
            });
            return count;
        }

        private static int Count(BaseElement element, Texture2D texture)
        {
            int count = element is Image image && image.texture == texture ? 1 : 0;
            for (int i = 0, seen = 0; seen < element.ChildsCount(); i++)
            {
                BaseElement child = element.GetChild(i);
                if (child != null)
                {
                    count += Count(child, texture);
                    seen++;
                }
            }
            return count;
        }
    }
}
