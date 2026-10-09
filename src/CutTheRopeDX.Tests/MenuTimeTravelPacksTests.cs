using System.Collections.Generic;

using CutTheRopeDX.Framework;
using CutTheRopeDX.Framework.Core;
using CutTheRopeDX.Framework.Platform;
using CutTheRopeDX.Framework.Visual;
using CutTheRopeDX.GameMain;

using Xunit;

using static CutTheRopeDX.Tests.MenuTimeTravelTests;

namespace CutTheRopeDX.Tests
{
    /// <summary>
    /// Covers the Time Travel pack pages, level picker and loading screen: what they draw at a
    /// scroll position, where they put it at every screen shape, and how a load plays out.
    /// </summary>
    public sealed class MenuTimeTravelPacksTests
    {
        private static readonly string[] PagePieces = ["ttPackIcon", "ttPageBullets", "ttPrevPage", "ttNextPage", "ttPackTitle"];

        [Fact]
        public void AnIconFacesFrontOnItsOwnPageAndHasTurnedAwayOnePageOff()
        {
            Assert.Equal((0f, 1f), TimeTravelPager.IconPose(0f));
            Assert.Equal((180f, 0f), TimeTravelPager.IconPose(1f));
            Assert.Equal((0f, 0f), TimeTravelPager.IconPose(-1f));

            // Coming in, it appears two thirds of a page off, turned half round the other way.
            (float entering, float hidden) = TimeTravelPager.IconPose(-2f / 3f);
            Assert.Equal(-180f, entering, 3);
            Assert.Equal(0f, hidden, 3);

            // Halfway to the next page the icon has turned 135 degrees and is a quarter visible.
            (float rotation, float alpha) = TimeTravelPager.IconPose(0.5f);
            Assert.Equal(135f, rotation, 3);
            Assert.Equal(0.25f, alpha, 3);
        }

        [Fact]
        public void TheRaysTurnOnceAPageEasedAtBothEnds()
        {
            Assert.Equal(0f, TimeTravelPager.RaysTurn(0f));
            Assert.Equal(180f, TimeTravelPager.RaysTurn(0.5f), 3);
            Assert.Equal(360f, TimeTravelPager.RaysTurn(1f), 3);
            Assert.True(TimeTravelPager.RaysTurn(0.1f) < 36f);
        }

        [Fact]
        public void ALockedIconFillsWithItsStarsBetweenTheIosBounds()
        {
            Assert.Equal(15f + 0f, TimeTravelPager.LockedOpaqueHeight(0, 50, 100f, 100f), 3);
            Assert.Equal(85f, TimeTravelPager.LockedOpaqueHeight(50, 50, 100f, 100f), 3);
            Assert.Equal(50f, TimeTravelPager.LockedOpaqueHeight(25, 50, 100f, 100f), 3);

            // A taller icon is filled against the shortest one and centered on its own height.
            Assert.Equal(60f, TimeTravelPager.LockedOpaqueHeight(25, 50, 120f, 100f), 3);
        }

        [Fact]
        public void ThePositionStaysWithinThePages()
        {
            Assert.Equal(0f, TimeTravelPager.Position(-50f, 100f, 5));
            Assert.Equal(2.5f, TimeTravelPager.Position(250f, 100f, 5), 3);
            Assert.Equal(4f, TimeTravelPager.Position(900f, 100f, 5));
        }

        [Fact]
        public void PacksTakeTheTwelveIconsInTurnOnTheirIosPages()
        {
            Assert.Equal((Resources.Img.MenuPackSelectionIconsTimeTravel, 0), TimeTravelArt.PackIcon(0));
            Assert.Equal((Resources.Img.MenuPackSelectionIconsTimeTravel, 5), TimeTravelArt.PackIcon(5));
            Assert.Equal((Resources.Img.MenuPackSelectionIcons1TimeTravel, 0), TimeTravelArt.PackIcon(6));
            Assert.Equal((Resources.Img.MenuPackSelectionIcons2TimeTravel, 1), TimeTravelArt.PackIcon(11));
            Assert.Equal((Resources.Img.MenuPackSelectionIconsTimeTravel, 0), TimeTravelArt.PackIcon(12));
            Assert.Equal(1, TimeTravelArt.PackPage(0, comingSoon: false));
            Assert.Equal(1, TimeTravelArt.PackPage(12, comingSoon: false));
            Assert.Equal(TimeTravelArt.ComingSoonPage, TimeTravelArt.PackPage(3, comingSoon: true));
        }

        [Fact]
        public void TheSheetsLoadWithTheirFramesAndTheAnimationsDrawOnlyQuadsTheyHave()
        {
            WithTimeTravel(2560, 1440, _ =>
            {
                Assert.Equal(34, Application.GetTexture(Resources.Img.MenuPackSelectionTimeTravel).quadRects.Length);
                Assert.Equal(9, Application.GetTexture(Resources.Img.MenuPackSelectionIconsTimeTravel).quadRects.Length);
                Assert.Equal(4, Application.GetTexture(Resources.Img.MenuPackSelectionIcons1TimeTravel).quadRects.Length);
                Assert.Equal(2, Application.GetTexture(Resources.Img.MenuPackSelectionIcons2TimeTravel).quadRects.Length);
                Assert.Equal(15, Application.GetTexture(Resources.Img.MenuBgrsTimeTravel).quadRects.Length);
                Assert.Equal(18, Application.GetTexture(Resources.Img.MenuLevelsTimeTravel).quadRects.Length);
                Assert.Equal(33, Application.GetTexture(Resources.Img.MenuLoadingTimeTravel).quadRects.Length);

                foreach ((string xml, string sheet, int parts) in new[]
                {
                    (TimeTravelArt.LoadingAnimationXml, Resources.Img.MenuLoadingTimeTravel, 34),
                    (TimeTravelArt.LevelBurstAnimationXml, Resources.Img.MenuLevelsTimeTravel, 9),
                    (TimeTravelArt.PackLockAnimationXml, Resources.Img.MenuPackSelectionTimeTravel, 2),
                })
                {
                    TimeTravelFlashStage stage = TimeTravelFlashStage.Create(xml, sheet);
                    Assert.Equal(parts, stage.Parts.Count);
                    int quads = Application.GetTexture(sheet).quadRects.Length;
                    foreach (Image part in stage.Parts)
                    {
                        Assert.InRange(part.quadToDraw, 0, quads - 1);
                    }
                }
            });
        }

        [Theory]
        [MemberData(nameof(LayoutSurfaces.Theory), MemberType = typeof(LayoutSurfaces))]
        public void AtRestOneIconShowsOnItsPageOverItsOwnBackground(string name, int width, int height)
        {
            _ = name;
            WithTimeTravel(width, height, controller =>
            {
                View view = ShowPacks(controller);
                Rectangle visible = ScreenPresentation.Instance.Snapshot.VisibleBounds;

                List<BaseElement> icons = Named(view, "ttPackIcon");
                Assert.Equal(Preferences.GetPacksCount() + (PackConfig.GetComingSoonPackIndex() >= 0 ? 1 : 0), icons.Count);
                _ = Assert.Single(icons, icon => icon.visible);
                Assert.True(icons[0].visible);
                Assert.Equal(0f, icons[0].rotation);

                List<BaseElement> backdrops = Named(view, "ttPageBackdrop");
                BaseElement shown = Assert.Single(backdrops, b => b.visible);
                Rectangle box = DrawnBox(shown);
                Assert.True(box.x <= visible.x && box.y <= visible.y, name + " backdrop top left");
                Assert.True(box.x + box.w >= visible.x + visible.w && box.y + box.h >= visible.y + visible.h, name + " backdrop bottom right");

                // The first page has nowhere to go left.
                Assert.False(((Button)view.GetChildWithName("ttPrevPage")).touchable);
                Assert.True(((Button)view.GetChildWithName("ttNextPage")).touchable);
                Assert.False(string.IsNullOrEmpty(((Text)view.GetChildWithName("ttPackTitle")).GetString()));

                foreach (string piece in PagePieces)
                {
                    Rectangle drawn = DrawnBox(view.GetChildWithName(piece));
                    Assert.True(drawn.x >= visible.x - 0.5f && drawn.y >= visible.y - 0.5f, name + " " + piece + " left/top");
                    Assert.True(drawn.x + drawn.w <= visible.x + visible.w + 0.5f && drawn.y + drawn.h <= visible.y + visible.h + 0.5f, name + " " + piece + " right/bottom");
                }
            });
        }

        [Theory]
        [MemberData(nameof(LayoutSurfaces.Theory), MemberType = typeof(LayoutSurfaces))]
        public void TheRaysSpanTheFullWidthAndKeepTheIosHeightRule(string name, int width, int height)
        {
            _ = name;
            WithTimeTravel(width, height, controller =>
            {
                View view = ShowPacks(controller);
                Rectangle visible = ScreenPresentation.Instance.Snapshot.VisibleBounds;
                TimeTravelScreen screen = new(visible);
                BaseElement rays = view.GetChildWithName("ttRays");
                Rectangle box = DrawnBox(rays);
                Assert.True(box.x <= visible.x + 0.5f && box.x + box.w >= visible.x + visible.w - 0.5f, name + " rays width");
                Assert.True(rays.scaleX >= (1.4286f * screen.FullHeight / TimeTravelScreen.SceneHeight) - 0.0001f, name + " rays height rule");
            });
        }

        [Fact]
        public void HalfwayBetweenPagesBothIconsAndBackgroundsShareTheScreen()
        {
            WithTimeTravel(2560, 1440, controller =>
            {
                View view = ShowPacks(controller);
                ScrollableContainer pages = Find<ScrollableContainer>(view);
                Rectangle visible = ScreenPresentation.Instance.Snapshot.VisibleBounds;
                pages.SetScroll(new Vector(visible.w / 2f, 0f));
                controller.Update(0f);

                List<BaseElement> icons = Named(view, "ttPackIcon");
                Assert.True(icons[0].visible && icons[1].visible);
                Assert.Equal(135f, icons[0].rotation, 2);
                Assert.Equal(-135f, icons[1].rotation, 2);
                List<BaseElement> backdrops = Named(view, "ttPageBackdrop");
                Assert.Equal(0.5f, backdrops[0].color.AlphaChannel, 3);
                Assert.Equal(0.5f, backdrops[1].color.AlphaChannel, 3);
                Assert.Equal(180f, view.GetChildWithName("ttRays").rotation, 2);
            });
        }

        [Theory]
        [InlineData(0.25f, 1f)]
        [InlineData(0.5f, 1f)]
        [InlineData(0.75f, 1f)]
        [InlineData(0.5f, 0.4f)]
        public void SwipingLockedPacksFadesTheLockPriceAndBothProgressLineHalves(float fraction, float unlockAlpha)
        {
            WithTimeTravel(2560, 1440, controller =>
            {
                int pack = Preferences.GetPacksCount() - 1;
                UNLOCKEDSTATE previous = Preferences.GetUnlockedForPackLevel(pack, 0);
                IRenderBackend previousRender = PlatformServices.Render;
                RecordingRenderBackend renderer = new();
                try
                {
                    Preferences.SetUnlockedForPackLevel(UNLOCKEDSTATE.LOCKED, pack, 0);
                    View view = ShowPacks(controller);
                    ScrollableContainer pages = Find<ScrollableContainer>(view);
                    pages.SetScroll(new Vector((pack - fraction) * ScreenPresentation.Instance.Snapshot.VisibleBounds.w, 0f));
                    controller.Update(0f);
                    BaseElement icon = Named(view, "ttPackIcon")[pack];
                    BaseElement price = icon.GetChildWithName("ttPrice");
                    price.color = RGBAColor.MakeRGBA(1f, 1f, 1f, unlockAlpha);
                    FlashXmlStageRoot padlock = Assert.IsType<FlashXmlStageRoot>(icon.GetChildWithName("ttLock"));
                    foreach (Image half in All<Image>(padlock))
                    {
                        if (half != padlock)
                        {
                            half.color = RGBAColor.MakeRGBA(1f, 1f, 1f, unlockAlpha);
                        }
                    }
                    List<(DrawColorProbe Probe, float Alpha)> probes = [];
                    foreach (BaseElement image in All<BaseElement>(icon).FindAll(element => element is Image or Text))
                    {
                        if (image is FlashXmlStageRoot)
                        {
                            continue;
                        }
                        DrawColorProbe probe = new(renderer);
                        _ = image.AddChild(probe);
                        float localAlpha = image is CroppedImage cropped && cropped.CropBottom > 0f ? 0.5f : image.color.AlphaChannel;
                        probes.Add((probe, localAlpha * (image.parent == price ? unlockAlpha : 1f)));
                    }
                    Assert.NotNull(icon.GetChildWithName("ttLock"));
                    PlatformServices.Render = renderer;
                    icon.Draw();
                    foreach ((DrawColorProbe probe, float alpha) in probes)
                    {
                        Assert.InRange(probe.Alpha, (alpha * icon.color.AlphaChannel) - 0.005f, (alpha * icon.color.AlphaChannel) + 0.005f);
                    }
                    // Drawing must not alter the held lock pose or accumulate fading between frames.
                    icon.Draw();
                    foreach ((DrawColorProbe probe, float alpha) in probes)
                    {
                        Assert.InRange(probe.Alpha, (alpha * icon.color.AlphaChannel) - 0.005f, (alpha * icon.color.AlphaChannel) + 0.005f);
                    }
                }
                finally
                {
                    PlatformServices.Render = previousRender;
                    Preferences.SetUnlockedForPackLevel(previous, pack, 0);
                }
            });
        }

        private sealed class DrawColorProbe(RecordingRenderBackend renderer) : BaseElement
        {
            public float Alpha { get; private set; }

            public override void Draw()
            {
                Alpha = renderer.LastTexturedDrawColor.A / 255f;
            }
        }

        [Theory]
        [InlineData(2560, 1440)]
        [InlineData(720, 1280)]
        public void UnlockingAPackEmitsThirtyShrinkingParticlesAfterTheIosDelay(int width, int height)
        {
            WithTimeTravel(width, height, controller =>
            {
                int pack = Preferences.GetPacksCount() - 1;
                UNLOCKEDSTATE previous = Preferences.GetUnlockedForPackLevel(pack, 0);
                int lastBox = Preferences.GetLastBox();
                try
                {
                    Preferences.SetUnlockedForPackLevel(UNLOCKEDSTATE.JUSTUNLOCKED, pack, 0);
                    controller.DeleteView(MenuController.VIEW_PACK_SELECT);
                    controller.CreatePackSelect();
                    View view = ShowPacks(controller);
                    ScrollableContainer pages = Find<ScrollableContainer>(view);
                    pages.PlaceToScrollPoint(pack);
                    BaseElement icon = Named(view, "ttPackIcon")[pack];
                    CroppedImage filled = All<CroppedImage>(icon)[0];
                    float fillTime = filled.CropTop / (200f * FlashXmlScale.AtlasToFlashPointScale);
                    controller.Update(fillTime + 0.001f);
                    Assert.True(icon.GetChildWithName("ttLock").updateable);

                    MultiParticles burst = Assert.Single(All<MultiParticles>(view), p => p.Name == "ttLockParticles");
                    _ = Assert.IsType<TimeTravelSceneGroup>(burst.parent);
                    Assert.Equal(0, burst.particleCount);
                    controller.Update(0.19f);
                    Assert.Equal(0, burst.particleCount);
                    controller.Update(0.011f);
                    Assert.Equal(30, burst.particleCount);
                    Assert.Equal(UNLOCKEDSTATE.UNLOCKED, Preferences.GetUnlockedForPackLevel(pack, 0));
                    Assert.Equal(burst.parent.width / 2f, burst.x);
                    Assert.Equal(burst.parent.height / 2f, burst.y);

                    Quad2D expected = Application.GetTexture(Resources.Img.MenuPackSelectionTimeTravel).quads[24];
                    foreach (Quad2D quad in burst.drawer.texCoordinates)
                    {
                        Assert.Equal(expected, quad);
                    }
                    float size = burst.particles[0].size;
                    float alpha = burst.particles[0].color.AlphaChannel;
                    float life = burst.particles[0].life;
                    float elapsed = burst.elapsed;
                    controller.Update(0.1f);
                    Assert.Equal(life - 0.1f, burst.particles[0].life, 4);
                    Assert.Equal(elapsed + 0.1f, burst.elapsed, 4);
                    Assert.True(burst.particles[0].size < size);
                    Assert.True(burst.particles[0].color.AlphaChannel < alpha);
                    Assert.NotEqual(0f, burst.particles[0].angle);

                    // The burst survives swiping away, cleans itself up, and never repeats.
                    pages.PlaceToScrollPoint(pack - 1);
                    for (int i = 0; i < 100; i++)
                    {
                        controller.Update(0.1f);
                    }
                    Assert.Empty(All<MultiParticles>(view).FindAll(p => p.Name == "ttLockParticles"));
                    pages.PlaceToScrollPoint(pack);
                    controller.Update(0.3f);
                    Assert.Empty(All<MultiParticles>(view).FindAll(p => p.Name == "ttLockParticles"));
                }
                finally
                {
                    Preferences.SetUnlockedForPackLevel(previous, pack, 0);
                    Preferences.SetLastBox(lastBox);
                }
            });
        }

        [Fact]
        public void SettlingOnTheNextPageMovesTheBulletAndShowsTheBackArrow()
        {
            WithTimeTravel(2560, 1440, controller =>
            {
                View view = ShowPacks(controller);
                ScrollableContainer pages = Find<ScrollableContainer>(view);
                pages.PlaceToScrollPoint(1);
                controller.Update(0f);

                Assert.Equal(1, ((TimeTravelPageBullets)view.GetChildWithName("ttPageBullets")).Current);
                Assert.True(((Button)view.GetChildWithName("ttPrevPage")).touchable);
                Assert.Equal(PackConfig.GetPackTitle(1, withNumber: false), ((Text)view.GetChildWithName("ttPackTitle")).GetString());
            });
        }

        [Theory]
        [InlineData(0.5f, 1)]
        [InlineData(1.5f, 2)]
        [InlineData(2.5f, 3)]
        public void ThePageIndicatorRoundsMidpointsLikeIos(float position, int expected)
        {
            WithTimeTravel(2560, 1440, controller =>
            {
                View view = ShowPacks(controller);
                Find<ScrollableContainer>(view).SetScroll(new Vector(position * ScreenPresentation.Instance.Snapshot.VisibleBounds.w, 0f));
                controller.Update(0f);
                Assert.Equal(expected, ((TimeTravelPageBullets)view.GetChildWithName("ttPageBullets")).Current);
                Assert.Equal(PackConfig.GetPackTitle(expected, withNumber: false), ((Text)view.GetChildWithName("ttPackTitle")).GetString());
            });
        }

        [Theory]
        [InlineData(2560, 1440)]
        [InlineData(720, 1280)]
        [InlineData(2940, 960)]
        public void TheLockStarAndPriceUseTheIosScaleAroundTheLockHalf(int width, int height)
        {
            WithTimeTravel(width, height, controller =>
            {
                View view = ShowPacks(controller);
                BaseElement icon = Named(view, "ttPackIcon")[Preferences.GetPacksCount() - 1];
                BaseElement price = icon.GetChildWithName("ttPrice");
                Assert.Equal(0.9f, price.scaleX);
                Assert.Equal(0.9f, price.scaleY);
                Assert.True(price.passTransformationsToChilds);
                Text number = Assert.Single(All<Text>(price));
                Image star = Assert.Single(All<Image>(price));
                ResolveDrawPositions(view);
                Rectangle numberBox = DrawnBox(number);
                Rectangle starBox = DrawnBox(star);
                Assert.InRange(numberBox.y + (numberBox.h / 2f) - (starBox.y + (starBox.h / 2f)), -0.5f, 0.5f);
                FlashXmlStageRoot padlock = Assert.IsType<FlashXmlStageRoot>(icon.GetChildWithName("ttLock"));
                FlashXmlImage half = All<FlashXmlImage>(padlock)[0];
                float units = FlashXmlScale.AtlasToFlashPointScale;
                float expectedX = icon.drawX + (icon.width / 2f) - (123.05f * units) + (half.width * units / 2f);
                float expectedY = icon.drawY + (icon.height / 2f) - (149.65f * units) + (half.height * units / 2f);
                Assert.Equal(expectedX, price.drawX + (price.width >> 1) + price.rotationCenterX, 3);
                Assert.Equal(expectedY, price.drawY + (price.height >> 1) + price.rotationCenterY, 3);
            });
        }

        [Fact]
        public void TheProgressLineShrinksAroundTheMeetingPointOfItsHalves()
        {
            WithTimeTravel(2560, 1440, controller =>
            {
                int pack = Preferences.GetPacksCount() - 1;
                UNLOCKEDSTATE previous = Preferences.GetUnlockedForPackLevel(pack, 0);
                try
                {
                    Preferences.SetUnlockedForPackLevel(UNLOCKEDSTATE.LOCKED, pack, 0);
                    View view = ShowPacks(controller);
                    BaseElement icon = Named(view, "ttPackIcon")[pack];
                    Image line = Assert.IsType<Image>(icon.GetChildWithName("ttProgressLine"));
                    line.PlayTimeline(0);
                    line.Update(0.05f);
                    ResolveDrawPositions(view);
                    Assert.Equal(0.5f, line.scaleX, 3);
                    float pivot = line.drawX + (line.width >> 1) + line.rotationCenterX;
                    Assert.InRange(pivot - (icon.drawX + (icon.width / 2f)), -1f, 1f);
                }
                finally
                {
                    Preferences.SetUnlockedForPackLevel(previous, pack, 0);
                }
            });
        }

        [Theory]
        [InlineData(2560, 1440)]
        [InlineData(720, 1280)]
        [InlineData(2940, 960)]
        public void TheTrimmedLockAndProgressLineOverlapAtTheirMirroredSeams(int width, int height)
        {
            WithTimeTravel(width, height, controller =>
            {
                View view = ShowPacks(controller);
                int pack = Preferences.GetPacksCount() - 1;
                Find<ScrollableContainer>(view).PlaceToScrollPoint(pack);
                controller.Update(0f);
                BaseElement icon = Named(view, "ttPackIcon")[pack];
                FlashXmlStageRoot padlock = Assert.IsType<FlashXmlStageRoot>(icon.GetChildWithName("ttLock"));
                List<FlashXmlImage> halves = All<FlashXmlImage>(padlock);
                Assert.Equal(2, halves.Count);
                Image left = halves[0];
                Image right = halves[1];

                // The draw uses fractional quad sizes; the element's integer size is only its pivot basis.
                for (int frame = 0; frame < 2; frame++)
                {
                    ResolveDrawPositions(view);
                    float quadWidth = left.texture.quadRects[left.quadToDraw].w / FlashXmlScale.AtlasToFlashPointScale;
                    float leftPivot = left.drawX + (left.width >> 1) + left.rotationCenterX;
                    float leftEdge = leftPivot + ((left.drawX + quadWidth - leftPivot) * left.scaleX);
                    float rightPivot = right.drawX + (right.width >> 1) + right.rotationCenterX;
                    float rightEdge = rightPivot - ((right.drawX + quadWidth - rightPivot) * right.scaleX);
                    float overlap = (leftEdge - rightEdge) * FlashXmlScale.AtlasToFlashPointScale;
                    Assert.True(overlap >= 1.9f * left.scaleX, "lock overlap: " + overlap);
                    padlock.updateable = true;
                    controller.Update(0.1f);
                }

                Image line = Assert.IsType<Image>(icon.GetChildWithName("ttProgressLine"));
                Image mirror = Assert.IsType<Image>(line.GetChild(0));
                ResolveDrawPositions(view);
                float mirrorPivot = mirror.drawX + (mirror.width >> 1);
                float mirrorLeft = (2f * mirrorPivot) - (mirror.drawX + mirror.width);
                Assert.InRange(line.drawX + line.width - mirrorLeft, 1.9f, 2.1f);
            });
        }

        [Theory]
        [InlineData("Native", 2560, 1440)]
        [InlineData("Portrait", 720, 1280)]
        public void PressingTheIconOpensItsLevels(string name, int width, int height)
        {
            _ = name;
            WithTimeTravel(width, height, controller =>
            {
                View view = ShowPacks(controller);
                ResolveDrawPositions(view);
                Rectangle icon = DrawnBox(view.GetChildWithName("ttPackIcon"));
                // The pages hand a touch to the icon's area once it is let go as a tap.
                float x = icon.x + (icon.w / 2f);
                float y = icon.y + (icon.h / 2f);
                _ = view.OnTouchDownXY(x, y);
                _ = view.OnTouchUpXY(x, y);
                Assert.Equal(MenuController.VIEW_LEVEL_SELECT, controller.activeViewID);
                Assert.NotNull(controller.GetView(MenuController.VIEW_LEVEL_SELECT).GetChildWithName("ttRays"));
            });
        }

        [Theory]
        [MemberData(nameof(LayoutSurfaces.Theory), MemberType = typeof(LayoutSurfaces))]
        public void TheLevelGridUsesTheTimeTravelPlatesInsideTheScreen(string name, int width, int height)
        {
            _ = name;
            WithTimeTravel(width, height, controller =>
            {
                View view = ShowLevels(controller);
                Rectangle visible = ScreenPresentation.Instance.Snapshot.VisibleBounds;
                Texture2D plates = Application.GetTexture(Resources.Img.MenuLevelsTimeTravel);
                List<Image> drawn = All<Image>(view).FindAll(image => image.texture == plates && image.quadToDraw <= TimeTravelArt.LevelOpen);
                Assert.Equal(Preferences.GetLevelsInPackCount(0), drawn.Count);
                foreach (Image plate in drawn)
                {
                    Rectangle box = DrawnBox(plate);
                    Assert.True(box.x >= visible.x - 0.5f && box.y >= visible.y - 0.5f, name + " plate left/top");
                    Assert.True(box.x + box.w <= visible.x + visible.w + 0.5f && box.y + box.h <= visible.y + visible.h + 0.5f, name + " plate right/bottom");
                }

                Rectangle back = DrawnBox(view.GetChildWithName("levelsBack"));
                Assert.True(back.x <= visible.x && back.x + back.w >= visible.x + visible.w, name + " backdrop width");
                Assert.True(back.y <= visible.y && back.y + back.h >= visible.y + visible.h, name + " backdrop height");
            });
        }

        [Fact]
        public void PressingALevelPlaysTheBurstOverItAndWhitensTheScreen()
        {
            WithTimeTravel(2560, 1440, controller =>
            {
                View view = ShowLevels(controller);
                controller.OnButtonPressed(MenuButtonId.ForLevel(0));

                FlashXmlStageRoot burst = Assert.IsType<FlashXmlStageRoot>(view.GetChildWithName("ttLevelBurst"));
                RectangleElement flash = Assert.IsType<RectangleElement>(view.GetChildWithName("ttLevelFlash"));
                Assert.Same(controller, burst.GetTimeline(0).delegateTimelineDelegate);
                ResolveDrawPositions(view);

                // The burst's stage origin sits on the pressed plate.
                Image plate = All<Image>(view).Find(image => image.texture == Application.GetTexture(Resources.Img.MenuLevelsTimeTravel));
                Rectangle plateBox = DrawnBox(plate);
                Vector origin = StageOrigin(burst);
                Assert.InRange(origin.X, plateBox.x, plateBox.x + plateBox.w);
                Assert.InRange(origin.Y, plateBox.y, plateBox.y + plateBox.h);

                float length = burst.GetTimeline(0).Duration;
                flash.Update(length - 0.15f);
                Assert.Equal(0f, flash.color.AlphaChannel, 3);
                flash.Update(0.15f);
                Assert.Equal(1f, flash.color.AlphaChannel, 3);
            });
        }

        [Fact]
        public void ALoadWindsTheClocksWithItsProgressAndEndsWithTheirExit()
        {
            WithTimeTravel(2560, 1440, _ =>
            {
                LoadingController loading = new(Application.SharedRootController());
                try
                {
                    LoadingView view = (LoadingView)loading.GetView(0);
                    float percent = 0f;
                    view.TimeTravelPercent = () => percent;
                    view.Show();
                    Assert.Equal(LoadingView.TimeTravelPhase.Intro, view.TimeTravelState);

                    view.Update(0.5f);
                    Assert.Equal(LoadingView.TimeTravelPhase.Progress, view.TimeTravelState);
                    float length = view.TimeTravelClock.GetTimeline(1).Duration;

                    percent = 50f;
                    view.Update(0.016f);
                    Assert.Equal(length / 2f, view.TimeTravelClock.GetTimeline(1).time, 3);

                    // Nothing more loaded, so the clocks hold however long the frame.
                    view.Update(1f);
                    Assert.Equal(length / 2f, view.TimeTravelClock.GetTimeline(1).time, 3);

                    percent = 100f;
                    view.Update(0.016f);
                    Assert.Equal(LoadingView.TimeTravelPhase.Outro, view.TimeTravelState);

                    view.Update(0.5f);
                    Assert.Equal(LoadingView.TimeTravelPhase.Done, view.TimeTravelState);
                    Assert.True(view.IsAnimationComplete());
                }
                finally
                {
                    loading.Dispose();
                }
            });
        }

        [Theory]
        [MemberData(nameof(LayoutSurfaces.Theory), MemberType = typeof(LayoutSurfaces))]
        public void TheLoadingClocksAreCenteredOnWhiteCoveringTheScreen(string name, int width, int height)
        {
            _ = name;
            WithTimeTravel(width, height, _ =>
            {
                LoadingController loading = new(Application.SharedRootController());
                try
                {
                    LoadingView view = (LoadingView)loading.GetView(0);
                    view.LayOutTimeTravel(ScreenPresentation.Instance.Snapshot.VisibleBounds);
                    ResolveDrawPositions(view);
                    Rectangle visible = ScreenPresentation.Instance.Snapshot.VisibleBounds;
                    Rectangle white = DrawnBox(view.GetChildWithName("ttLoadingWhite"));
                    Assert.True(white.x <= visible.x && white.y <= visible.y);
                    Assert.True(white.x + white.w >= visible.x + visible.w && white.y + white.h >= visible.y + visible.h);
                    Rectangle clock = DrawnBox(view.TimeTravelClock);
                    Assert.InRange(clock.x + (clock.w / 2f) - (visible.w / 2f), -1f, 1f);
                    Assert.InRange(clock.y + (clock.h / 2f) - (visible.h / 2f), -1f, 1f);
                    TimeTravelScreen screen = new(visible);
                    Assert.InRange(clock.w - (800f * screen.Scale), -1f, 1f);
                }
                finally
                {
                    loading.Dispose();
                }
            });
        }

        [Fact]
        public void APhoneShapedScreenGetsNoExtraClocks()
        {
            TimeTravelScreen screen = new(new Rectangle(0f, 0f, 640f, 960f));
            Rectangle shown = TimeTravelClockFill.ShownRegion(screen);
            List<FillPlacement> placed = TimeTravelClockFill.Plan(shown, [], [50f, 60f], new System.Random(1));
            Assert.Empty(placed);
            Assert.All(TimeTravelClockFill.CutClocks, cut => Assert.False(TimeTravelClockFill.ShowsCut(cut, shown)));
        }

        [Fact]
        public void AWideScreenPacksClocksThatNeverTouchEachOtherOrTheIosScreen()
        {
            TimeTravelScreen screen = new(new Rectangle(0f, 0f, 2560f, 1080f));
            Rectangle shown = TimeTravelClockFill.ShownRegion(screen);
            Rectangle ios = TimeTravelClockFill.IosScreen;
            Circle hero = new(400f, 593.5f, 120f);
            float[] radii = [88f, 75f, 70f, 63f, 51f, 46f, 55f, 73f];
            List<FillPlacement> placed = TimeTravelClockFill.Plan(shown, [hero], radii, new System.Random(7));

            Assert.True(placed.Count >= 4, "only " + placed.Count + " clocks");
            List<Circle> circles = [hero];
            foreach (FillPlacement p in placed)
            {
                Assert.False(p.X >= ios.x && p.X <= ios.x + ios.w && p.Y >= ios.y && p.Y <= ios.y + ios.h);
                Assert.InRange(p.Scale, TimeTravelClockFill.MinScale, TimeTravelClockFill.MaxScale);
                Assert.InRange(p.Tilt, -TimeTravelClockFill.MaxTilt, TimeTravelClockFill.MaxTilt);
                float r = radii[p.Clock] * p.Scale;
                float bleed = r * TimeTravelClockFill.Bleed;
                Assert.True(p.X - r >= shown.x - bleed - 0.01f && p.X + r <= shown.x + shown.w + bleed + 0.01f);
                Assert.True(p.Y - r >= shown.y - bleed - 0.01f && p.Y + r <= shown.y + shown.h + bleed + 0.01f);
                foreach (Circle other in circles)
                {
                    float dx = p.X - other.X;
                    float dy = p.Y - other.Y;
                    Assert.True(System.MathF.Sqrt((dx * dx) + (dy * dy)) >= ((r + other.Radius) * (1f + (TimeTravelClockFill.Spacing / 2f))) - 0.01f);
                }
                circles.Add(new Circle(p.X, p.Y, r));
            }
        }

        [Fact]
        public void AnEdgeClockIsHiddenOnlyOnceItsCutWouldShow()
        {
            CutClock right = new("clock_09", CutSide.Right, 829.4f);
            Assert.False(TimeTravelClockFill.ShowsCut(right, TimeTravelClockFill.ShownRegion(new TimeTravelScreen(new Rectangle(0f, 0f, 768f, 1024f)))));
            Assert.True(TimeTravelClockFill.ShowsCut(right, TimeTravelClockFill.ShownRegion(new TimeTravelScreen(new Rectangle(0f, 0f, 1024f, 768f)))));
            CutClock bottom = new("clock_12", CutSide.Bottom, 1259.2f);
            Assert.False(TimeTravelClockFill.ShowsCut(bottom, TimeTravelClockFill.ShownRegion(new TimeTravelScreen(new Rectangle(0f, 0f, 1920f, 1080f)))));
            Assert.True(TimeTravelClockFill.ShowsCut(bottom, TimeTravelClockFill.ShownRegion(new TimeTravelScreen(new Rectangle(0f, 0f, 400f, 1280f)))));
        }

        [Theory]
        [MemberData(nameof(LayoutSurfaces.Theory), MemberType = typeof(LayoutSurfaces))]
        public void TheLoadingScreenFillsItsRoomWithClocksThatTickWithTheLoad(string name, int width, int height)
        {
            WithTimeTravel(width, height, _ =>
            {
                LoadingController loading = new(Application.SharedRootController());
                try
                {
                    LoadingView view = (LoadingView)loading.GetView(0);
                    view.TimeTravelRandom = new System.Random(3);
                    float percent = 0f;
                    view.TimeTravelPercent = () => percent;
                    view.LayOutTimeTravel(ScreenPresentation.Instance.Snapshot.VisibleBounds);
                    view.Show();
                    Rectangle visible = ScreenPresentation.Instance.Snapshot.VisibleBounds;
                    TimeTravelScreen screen = new(visible);
                    bool roomy = screen.FullWidth > 900f || screen.FullHeight > 1400f;
                    Assert.True(!roomy || view.TimeTravelFills.Count > 0, name + " has no extra clocks");

                    view.Update(0.5f);
                    percent = 40f;
                    view.Update(0.016f);
                    foreach (TimeTravelFlashStage fill in view.TimeTravelFills)
                    {
                        Assert.Equal(3, fill.Parts.Count);
                        Image hand = fill.Parts[1];
                        Assert.Equal(1, hand.CurrentTimelineIndex);
                        Assert.Equal(view.TimeTravelClock.GetTimeline(1).time, hand.GetCurrentTimeline().time, 3);
                    }

                    // An edge clock cut where the screen now shows is kept hidden after every step.
                    Rectangle shown = TimeTravelClockFill.ShownRegion(screen);
                    foreach (CutClock cut in TimeTravelClockFill.CutClocks)
                    {
                        Image part = All<Image>(view.TimeTravelClock).Find(image => image.quadToDraw == CutQuad(cut.Part) && image.parent == view.TimeTravelClock);
                        if (TimeTravelClockFill.ShowsCut(cut, shown))
                        {
                            Assert.False(part.visible, name + " " + cut.Part);
                        }
                    }
                }
                finally
                {
                    loading.Dispose();
                }
            });
        }

        private static int CutQuad(string part)
        {
            return part switch
            {
                "clock_09" => 12,
                "clock_11" => 6,
                "clock_12" => 11,
                _ => 10,
            };
        }

        private static View ShowPacks(MenuController controller)
        {
            controller.ShowView(MenuController.VIEW_PACK_SELECT);
            controller.Update(0f);
            View view = controller.GetView(MenuController.VIEW_PACK_SELECT);
            ResolveDrawPositions(view);
            return view;
        }

        private static View ShowLevels(MenuController controller)
        {
            controller.PreLevelSelect();
            controller.ShowView(MenuController.VIEW_LEVEL_SELECT);
            controller.Update(0f);
            View view = controller.GetView(MenuController.VIEW_LEVEL_SELECT);
            ResolveDrawPositions(view);
            return view;
        }

        private static List<BaseElement> Named(BaseElement root, string name)
        {
            return All<BaseElement>(root).FindAll(element => element.Name == name);
        }

        private static Vector StageOrigin(BaseElement stage)
        {
            Rectangle box = DrawnBox(stage);
            float scale = box.w / stage.width;
            float halfWidth = stage.width >> 1;
            float halfHeight = stage.height >> 1;
            return new Vector(
                box.x + (box.w / 2f) - (halfWidth * scale),
                box.y + (box.h / 2f) - (halfHeight * scale));
        }
    }
}
