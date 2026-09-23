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
    /// Covers the Experiments menus selected with <c>--menu experiments</c>. The style is
    /// process-wide, which the serial suite makes safe to switch for one test at a time.
    /// </summary>
    public sealed class MenuExperimentsTests
    {
        [Theory]
        [MemberData(nameof(LayoutSurfaces.Theory), MemberType = typeof(LayoutSurfaces))]
        public void PackSelectHasOneBoxPerPackWithTheRestingBoxLit(string name, int width, int height)
        {
            _ = name;
            WithExperiments(width, height, controller =>
            {
                View view = controller.GetView(MenuController.VIEW_PACK_SELECT);
                List<BaseElement> containers = Named(view, "boxContainer");
                int expected = Preferences.GetPacksCount() + (PackConfig.GetComingSoonPackIndex() >= 0 ? 1 : 0);
                Assert.Equal(expected, containers.Count);

                int resting = Math.Min(Preferences.GetLastBox(), expected - 1);
                for (int i = 0; i < containers.Count; i++)
                {
                    float selected = containers[i].GetChildWithName("boxSelected").color.AlphaChannel;
                    float idle = containers[i].GetChildWithName("box").color.AlphaChannel;
                    Assert.Equal(i == resting ? 1f : 0f, selected, 3);
                    Assert.Equal(1f - selected, idle, 3);
                }
                Assert.NotNull(view.GetChildWithName("backb"));
            });
        }

        [Fact]
        public void OnlyAPerfectPackWearsTheBadge()
        {
            _ = HeadlessGame.Boot();
            const int Pack = 0;
            int slot = PackConfig.GetSaveSlot(Pack);
            int levels = Preferences.GetLevelsInPackCount(Pack);
            int[] original = new int[levels];
            for (int level = 0; level < levels; level++)
            {
                original[level] = Preferences.GetStarsForPackLevel(slot, Pack, level);
                Preferences.SetStarsForPackLevel(slot, 3, Pack, level);
            }

            try
            {
                WithExperiments(2560, 1440, controller =>
                {
                    List<BaseElement> containers = Named(controller.GetView(MenuController.VIEW_PACK_SELECT), "boxContainer");
                    for (int i = 0; i < containers.Count; i++)
                    {
                        bool perfect = i < Preferences.GetPacksCount() && Preferences.IsPackPerfect(i);
                        Assert.Equal(perfect, containers[i].GetChildWithName("perfect") != null);
                    }
                    Assert.NotNull(containers[Pack].GetChildWithName("perfect"));
                });
            }
            finally
            {
                for (int level = 0; level < levels; level++)
                {
                    Preferences.SetStarsForPackLevel(slot, original[level], Pack, level);
                }
            }
        }

        [Fact]
        public void OnlyTheLastPackIsCagedAndItsCageShowsNoPrice()
        {
            WithExperiments(2560, 1440, controller =>
            {
                List<BaseElement> containers = Named(controller.GetView(MenuController.VIEW_PACK_SELECT), "boxContainer");
                Texture2D packAtlas = Application.GetTexture(Resources.Img.MenuExpPackSelection);
                int caged = Preferences.GetPacksCount() - 1;
                for (int i = 1; i < Preferences.GetPacksCount(); i++)
                {
                    if (Preferences.GetUnlockedForPackLevel(i, 0) != UNLOCKEDSTATE.LOCKED)
                    {
                        continue;
                    }
                    Image cage = All<Image>(containers[i]).Find(image => image.texture == packAtlas && image.quadToDraw == 18);
                    Assert.Equal(i == caged, cage != null);
                    if (cage != null)
                    {
                        Assert.Empty(All<HBox>(cage));
                    }
                }
            });
        }

        [Fact]
        public void TheButterflyKeepsTheLockedCagedBoxCompany()
        {
            WithExperiments(2560, 1440, controller =>
            {
                int caged = Preferences.GetPacksCount() - 1;
                ExperimentsButterfly butterfly = Find<ExperimentsButterfly>(controller.GetView(MenuController.VIEW_PACK_SELECT));
                Assert.Equal(Preferences.GetUnlockedForPackLevel(caged, 0) == UNLOCKEDSTATE.LOCKED, butterfly != null);
                if (butterfly == null)
                {
                    return;
                }

                // Heading for the caged box sends it in to land; it perches once there. The view is
                // never scrolled here, so it crosses most of the strip at its on-screen speed.
                controller.ScrollableContainerchangedTargetScrollPoint(null, caged);
                for (int frame = 0; frame < 3000 && butterfly.Mode != ExperimentsButterfly.FlightMode.Landed; frame++)
                {
                    butterfly.Update(1f / 60f);
                }
                Assert.Equal(ExperimentsButterfly.FlightMode.Landed, butterfly.Mode);
            });
        }

        [Fact]
        public void PackSelectRebuildsForANewShape()
        {
            WithExperiments(2560, 1440, controller =>
            {
                controller.ShowView(MenuController.VIEW_PACK_SELECT);
                GameLifecycle.OnSurfaceChanged(720, 1280);
                controller.RelayoutTree(ScreenPresentation.Instance.Snapshot);

                Assert.NotEmpty(Named(controller.GetView(MenuController.VIEW_PACK_SELECT), "boxContainer"));
            });
        }

        [Fact]
        public void OptionsUseTheExperimentsAudioToggles()
        {
            WithExperiments(2560, 1440, controller =>
            {
                // Sound and music; the click-to-cut switch is a toggle too, drawn with its own art.
                Texture2D audio = Application.GetTexture(Resources.Img.MenuExpAudio);
                int audioToggles = 0;
                foreach (ToggleButton toggle in All<ToggleButton>(controller.GetView(MenuController.VIEW_OPTIONS)))
                {
                    if (Find<Image>(toggle)?.texture == audio)
                    {
                        audioToggles++;
                    }
                }
                Assert.Equal(2, audioToggles);
            });
        }

        [Theory]
        [MemberData(nameof(LayoutSurfaces.Theory), MemberType = typeof(LayoutSurfaces))]
        public void LevelSelectCoversTheScreenWithTheLoadingSheet(string name, int width, int height)
        {
            _ = name;
            WithExperiments(width, height, controller =>
            {
                controller.CreateLevelSelect();
                View view = controller.GetView(MenuController.VIEW_LEVEL_SELECT);
                BaseElement sheet = view.GetChildWithName("levelsBack");
                Image backdrop = (Image)sheet.GetChild(0);
                Assert.Same(Application.GetTexture(Resources.BackgroundImg.MenuExpLoadingBgr), backdrop.texture);

                controller.ShowView(MenuController.VIEW_LEVEL_SELECT);
                controller.RelayoutTree(ScreenPresentation.Instance.Snapshot);
                Rectangle visible = ScreenPresentation.Instance.Snapshot.VisibleBounds;
                Assert.True(sheet.width * sheet.scaleX >= visible.w - 0.5f, $"the sheet is {sheet.width * sheet.scaleX} wide on a {visible.w} viewport");
                Assert.True(sheet.height * sheet.scaleY >= visible.h - 0.5f, $"the sheet is {sheet.height * sheet.scaleY} tall on a {visible.h} viewport");
            });
        }

        [Fact]
        public void BackdropsUseTheExperimentsArt()
        {
            WithExperiments(2560, 1440, controller =>
            {
                Assert.Same(Application.GetTexture(Resources.BackgroundImg.MenuExpMainBgr), Backdrop(controller, MenuController.VIEW_MAIN_MENU).texture);
                Assert.Same(Application.GetTexture(Resources.BackgroundImg.MenuExpDefaultBgr), Backdrop(controller, MenuController.VIEW_OPTIONS).texture);
                Assert.Same(Application.GetTexture(Resources.BackgroundImg.MenuExpCampaignBgr), Backdrop(controller, MenuController.VIEW_PACK_SELECT).texture);
            });
        }

        /// <summary>
        /// Builds a menu controller with the Experiments style at a surface size, then restores
        /// the classic style whatever happens.
        /// </summary>
        /// <param name="width">Surface width.</param>
        /// <param name="height">Surface height.</param>
        /// <param name="body">Checks to run against the controller.</param>
        private static void WithExperiments(int width, int height, Action<MenuController> body)
        {
            _ = HeadlessGame.Boot();
            MenuStyle previous = MenuTheme.Current;
            MenuTheme.Current = MenuStyle.Experiments;
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

        /// <summary>Reads the painted layer of a view's backdrop.</summary>
        /// <param name="controller">Controller owning the view.</param>
        /// <param name="viewId">View whose backdrop to read.</param>
        /// <returns>The backdrop image.</returns>
        private static Image Backdrop(MenuController controller, int viewId)
        {
            return (Image)controller.GetView(viewId).GetChild(0).GetChild(0);
        }

        /// <summary>Collects every element under <paramref name="root"/> with the given name.</summary>
        /// <param name="root">Element to search.</param>
        /// <param name="name">Name to match.</param>
        /// <returns>The matches, in tree order.</returns>
        private static List<BaseElement> Named(BaseElement root, string name)
        {
            List<BaseElement> found = [];
            foreach (BaseElement child in root.GetChilds().Values)
            {
                if (child == null)
                {
                    continue;
                }
                if (child.Name == name)
                {
                    found.Add(child);
                }
                found.AddRange(Named(child, name));
            }
            return found;
        }

        /// <summary>Collects every element of a type under <paramref name="root"/>, depth first.</summary>
        /// <typeparam name="T">Element type.</typeparam>
        /// <param name="root">Element to search.</param>
        /// <returns>The matches, in tree order.</returns>
        private static List<T> All<T>(BaseElement root)
            where T : BaseElement
        {
            List<T> found = [];
            foreach (BaseElement child in root.GetChilds().Values)
            {
                if (child is T match)
                {
                    found.Add(match);
                }
                if (child != null)
                {
                    found.AddRange(All<T>(child));
                }
            }
            return found;
        }

        /// <summary>Finds the first element of a type under <paramref name="root"/>, depth first.</summary>
        /// <typeparam name="T">Element type.</typeparam>
        /// <param name="root">Element to search.</param>
        /// <returns>The first match, or <see langword="null"/>.</returns>
        private static T Find<T>(BaseElement root)
            where T : BaseElement
        {
            foreach (BaseElement child in root.GetChilds().Values)
            {
                if (child is T match)
                {
                    return match;
                }
                T nested = child == null ? null : Find<T>(child);
                if (nested != null)
                {
                    return nested;
                }
            }
            return null;
        }
    }
}
