using System;
using System.Collections.Generic;

using CutTheRopeDX.Framework.Core;
using CutTheRopeDX.Framework.Visual;
using CutTheRopeDX.GameMain;

using Xunit;

namespace CutTheRopeDX.Tests
{
    public sealed class SeasonalMenuThemeTests
    {
        [Theory]
        [InlineData("classic", 10)]
        [InlineData("classic", 12)]
        [InlineData("classic", 1)]
        [InlineData("experiments", 10)]
        [InlineData("experiments", 12)]
        [InlineData("experiments", 1)]
        [InlineData("timetravel", 10)]
        [InlineData("timetravel", 12)]
        [InlineData("timetravel", 1)]
        public void SeasonalDressingIsExclusiveToClassicMenus(string theme, int month)
        {
            SeasonalDate.With(new DateTime(2026, month, 15), () =>
            {
                _ = HeadlessGame.Boot();
                MenuStyle previous = MenuTheme.Current;
                Assert.True(MenuTheme.TryParse(theme, out MenuStyle style));
                MenuTheme.Current = style;
                try
                {
                    using MenuController controller = new(Application.SharedRootController());
                    bool classic = style == MenuStyle.Classic;
                    BaseElement main = controller.GetView(MenuController.VIEW_MAIN_MENU);
                    Assert.Equal(classic && month == 10, UsesTexture(main, Resources.Img.MenuBgrHalloweenDecorations));
                    Assert.Equal(classic && month != 10, UsesTexture(main, Resources.Img.MenuLogoXmasHat));
                    Assert.Equal(classic && month == 10, UsesTexture(main, Resources.Img.MenuBgrHalloween));
                    Assert.Equal(classic && month != 10, UsesTexture(main, Resources.Img.MenuBgrXmas));
                    foreach (int viewId in new[] { MenuController.VIEW_MAIN_MENU, MenuController.VIEW_OPTIONS, MenuController.VIEW_PACK_SELECT })
                    {
                        Assert.Equal(classic && month != 10, ContainsSnowfall(controller.GetView(viewId)));
                    }
                }
                finally
                {
                    MenuTheme.Current = previous;
                }
            });
        }

        private static bool UsesTexture(BaseElement root, string resource)
        {
            Texture2D texture = Application.GetTexture(resource);
            foreach (BaseElement element in Descendants(root))
            {
                if (element is Image image && image.texture == texture)
                {
                    return true;
                }
            }
            return false;
        }

        private static bool ContainsSnowfall(BaseElement root)
        {
            foreach (BaseElement element in Descendants(root))
            {
                if (element is SnowfallOverlay)
                {
                    return true;
                }
            }
            return false;
        }

        private static IEnumerable<BaseElement> Descendants(BaseElement root)
        {
            foreach (BaseElement child in root.GetChilds().Values)
            {
                if (child == null)
                {
                    continue;
                }
                yield return child;
                foreach (BaseElement descendant in Descendants(child))
                {
                    yield return descendant;
                }
            }
        }
    }
}
