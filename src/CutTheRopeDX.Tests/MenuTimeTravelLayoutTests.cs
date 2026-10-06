using CutTheRopeDX.Framework;
using CutTheRopeDX.Framework.Core;
using CutTheRopeDX.Framework.Platform;
using CutTheRopeDX.Framework.Visual;
using CutTheRopeDX.GameMain;

using Xunit;

namespace CutTheRopeDX.Tests
{
    /// <summary>
    /// Pins the Time Travel menu views' element geometry at every surface size, and checks that
    /// each view's backdrop covers the screen at every shape.
    /// </summary>
    public sealed class MenuTimeTravelLayoutTests
    {
        [Theory]
        [MemberData(nameof(Cases))]
        public void TimeTravelMenuGeometryIsPinned(string surfaceName, int width, int height, int viewId, string viewName)
        {
            MenuTimeTravelTests.WithTimeTravel(width, height, controller =>
            {
                View view = controller.GetView(viewId);
                Assert.NotNull(view);
                controller.ShowView(viewId);
                controller.Update(0.016f);

                MenuTimeTravelTests.ResolveDrawPositions(view);
                Rectangle visible = ScreenPresentation.Instance.Snapshot.VisibleBounds;
                Texture2D backdropTexture = Application.GetTexture(
                    viewId == MenuController.VIEW_MAIN_MENU ? Resources.BackgroundImg.MenuTimeTravelMainBgr : Resources.BackgroundImg.MenuTimeTravelBgr);
                Image backdrop = MenuTimeTravelTests.All<Image>(view).Find(i => i.texture == backdropTexture);
                Assert.NotNull(backdrop);
                // The backdrop scales about its bottom center, so coverage is read off its scaled
                // size the way the classic backdrop coverage test reads it.
                Assert.True(backdrop.scaleX * backdrop.width >= visible.w - 0.5f, $"backdrop {backdrop.scaleX * backdrop.width} wide on {visible.w}");
                Assert.True(backdrop.scaleY * backdrop.height >= visible.h - 0.5f, $"backdrop {backdrop.scaleY * backdrop.height} tall on {visible.h}");

                LayoutBaseline.Assert($"Menu.TimeTravel.{viewName}.{surfaceName}", ElementGeometryWalker.Describe(view));
            });
        }

        public static TheoryData<string, int, int, int, string> Cases()
        {
            TheoryData<string, int, int, int, string> data = [];
            foreach (LayoutSurface surface in LayoutSurfaces.All)
            {
                data.Add(surface.Name, surface.Width, surface.Height, MenuController.VIEW_MAIN_MENU, "MainMenu");
                data.Add(surface.Name, surface.Width, surface.Height, MenuController.VIEW_OPTIONS, "Options");
                data.Add(surface.Name, surface.Width, surface.Height, MenuController.VIEW_LANGUAGE_SELECT, "LanguageSelect");
                data.Add(surface.Name, surface.Width, surface.Height, MenuController.VIEW_ABOUT, "About");
                data.Add(surface.Name, surface.Width, surface.Height, MenuController.VIEW_RESET, "Reset");
            }
            return data;
        }
    }
}
