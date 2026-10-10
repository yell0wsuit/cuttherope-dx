using System;
using System.Collections.Generic;
using System.Globalization;

using CutTheRopeDX.Framework;
using CutTheRopeDX.Framework.Core;
using CutTheRopeDX.Framework.Media;
using CutTheRopeDX.Framework.Visual;

namespace CutTheRopeDX.GameMain
{
    /// <content>
    /// The Time Travel pack pages and level picker (iOS HD 1.5.0 <c>LevelSelectController</c>):
    /// one page per pack, its icon turning in place as the pages scroll, over a tinted gradient
    /// and turning rays. The level grid keeps the classic layout on the pack page's backdrop.
    /// </content>
    internal sealed partial class MenuController
    {
        /// <summary>Scale the rays are drawn at against the full screen height (iOS 1.4286).</summary>
        private const float TimeTravelRaysHeightScale = 1.4286f;

        /// <summary>Seconds per turn of the rays.</summary>
        private const float TimeTravelRaysPeriod = 10f;

        /// <summary>How far the rays swing when the page changes, in degrees (iOS 150).</summary>
        private const float TimeTravelRaysSwing = 150f;

        /// <summary>Seconds the rays swing for.</summary>
        private const float TimeTravelRaysSwingTime = 1f;

        /// <summary>How much the rays swell midway through their swing (iOS 1.1).</summary>
        private const float TimeTravelRaysSwell = 1.1f;

        /// <summary>How much a page background outgrows the screen (iOS 1.04).</summary>
        private const float TimeTravelBackdropBleed = 1.04f;

        /// <summary>
        /// Where the pack icons rest, centered, in iOS canvas pixels: the icon sheet's first quad
        /// moved by the bobbing part's rest position in <c>fx_pack_selection_icon</c>.
        /// </summary>
        private static readonly Vector TimeTravelIconCenter = new(628.4f, 998.5f);

        /// <summary>How high the icons bob, in iOS canvas pixels (logical 20.5).</summary>
        private const float TimeTravelIconBob = 41f;

        /// <summary>Seconds the icons take to rise (iOS 2.3).</summary>
        private const float TimeTravelIconRise = 2.3f;

        /// <summary>Seconds the icons take to settle back (iOS 2.1667).</summary>
        private const float TimeTravelIconFall = 2.166667f;

        /// <summary>Seconds the icons rest between bobs (iOS 0.0333).</summary>
        private const float TimeTravelIconRest = 0.033333f;

        /// <summary>How high the coming-soon gadgets bob, in iOS canvas pixels (logical 9.5).</summary>
        private const float TimeTravelGadgetBob = 19f;

        /// <summary>Gap between page bullets, in iOS canvas pixels: from bullet to current bullet.</summary>
        private static float TimeTravelBulletGap => TimeTravelArt.PackMarkers.BulletCurrent.x - TimeTravelArt.PackMarkers.Bullet.x - TimeTravelArt.PackMarkers.Bullet.w;

        /// <summary>
        /// Width the page bullets are fitted to, in iOS canvas pixels: half the iOS screen, the
        /// share of its full width iOS gave them on a phone.
        /// </summary>
        private const float TimeTravelBulletsWidth = TimeTravelScreen.SceneWidth;

        /// <summary>Speed a newly opened pack fills at, in iOS logical units per second (iOS 200).</summary>
        private const float TimeTravelUnlockSpeed = 200f;

        /// <summary>Seconds the padlock's price fades out for when the pack opens (iOS 0.1).</summary>
        private const float TimeTravelPriceFade = 0.1f;

        /// <summary>Opacity of the part of a locked icon its stars do not yet reach (iOS half transparent).</summary>
        private const float TimeTravelLockedVeil = 0.5f;

        /// <summary>Atlas pixels shared by mirrored halves so filtering cannot open their seam.</summary>
        private const float TimeTravelSeamOverlap = 2f;

        /// <summary>Seconds before a pressed level's burst ends that the screen starts to whiten.</summary>
        private const float TimeTravelLevelFlash = 0.1f;

        /// <summary>The pack pages, by page.</summary>
        private readonly List<TimeTravelPackPage> timeTravelPages = [];

        /// <summary>The pack pages' backgrounds, by page.</summary>
        private readonly List<Image> timeTravelPageBackdrops = [];

        /// <summary>What swings the pack pages' rays when the page changes.</summary>
        private BaseElement timeTravelRaysSwing;

        /// <summary>The pack scene, which holds unlock particles independently of the icons.</summary>
        private TimeTravelSceneGroup timeTravelPackScene;

        /// <summary>The pack pages' rays.</summary>
        private Image timeTravelRays;

        /// <summary>The pack pages' title.</summary>
        private Text timeTravelPackTitle;

        /// <summary>The pack pages' page bullets.</summary>
        private TimeTravelPageBullets timeTravelBullets;

        /// <summary>Width of one pack page.</summary>
        private float timeTravelPageWidth;

        /// <summary>The page the pack pages last settled their title, bullets and arrows on.</summary>
        private int timeTravelShownPage = -1;

        /// <summary>The level picker's background, or <see langword="null"/> outside Time Travel.</summary>
        private Image levelsTimeTravelBackdrop;

        /// <summary>The level picker's rays scene.</summary>
        private TimeTravelSceneGroup timeTravelLevelsScene;

        /// <summary>The level picker's rays.</summary>
        private Image timeTravelLevelsRays;

        /// <summary>The level picker's buttons, by level.</summary>
        private readonly List<BaseElement> timeTravelLevelButtons = [];

        /// <summary>One pack page.</summary>
        private sealed class TimeTravelPackPage
        {
            /// <summary>Gets or sets the turning element holding the page's icon.</summary>
            public BaseElement Icon { get; set; }

            /// <summary>Gets or sets the veiled top of a locked icon.</summary>
            public CroppedImage Veil { get; set; }

            /// <summary>Gets or sets the opaque bottom of a locked icon.</summary>
            public CroppedImage Filled { get; set; }

            /// <summary>Gets or sets the line where a locked icon's stars reach.</summary>
            public Image Line { get; set; }

            /// <summary>Gets or sets a locked icon's padlock.</summary>
            public TimeTravelFlashStage Lock { get; set; }

            /// <summary>Gets or sets the padlock's star and price.</summary>
            public BaseElement Price { get; set; }

            /// <summary>Gets or sets how much of a locked icon is drawn opaque, from its bottom.</summary>
            public float Opaque { get; set; }

            /// <summary>Gets or sets whether the icon is filling because its pack has just opened.</summary>
            public bool Opening { get; set; }

            /// <summary>Gets or sets whether the pack has opened and waits to be shown filling.</summary>
            public bool JustUnlocked { get; set; }
        }

        /// <summary>
        /// Applies the pager's fade to every piece at draw time. BaseElement colors replace
        /// renderer colors, so a masked image or Flash stage can otherwise reset the fade for
        /// the pieces after it. Keep the animation's own colors intact between draws.
        /// </summary>
        private sealed class TimeTravelPackIcon : BaseElement
        {
            private readonly List<(BaseElement Element, RGBAColor Color)> drawColors = [];

            public override void Draw()
            {
                try
                {
                    FadeChildren(this, color.AlphaChannel);
                    base.Draw();
                }
                finally
                {
                    foreach ((BaseElement element, RGBAColor saved) in drawColors)
                    {
                        element.color = saved;
                    }
                    drawColors.Clear();
                }
            }

            private void FadeChildren(BaseElement parent, float alpha)
            {
                foreach (BaseElement child in parent.GetChilds().Values)
                {
                    if (child == null)
                    {
                        continue;
                    }
                    RGBAColor local = child.color;
                    // Text combines its own color with the inherited renderer color.
                    if (child is not Text)
                    {
                        drawColors.Add((child, local));
                        child.color = RGBAColor.MakeRGBA(local.RedColor, local.GreenColor, local.BlueColor, local.AlphaChannel * alpha);
                    }
                    FadeChildren(child, child.passColorToChilds ? alpha * local.AlphaChannel : alpha);
                }
            }
        }

        /// <summary>Builds the Time Travel pack pages.</summary>
        private void CreateTimeTravelPackSelect()
        {
            MenuView menuView = new();
            Rectangle visible = VisibleBounds;
            TimeTravelScreen screen = new(visible);
            BaseElement root = new() { width = (int)visible.w, height = (int)visible.h };
            int comingSoon = PackConfig.GetComingSoonPackIndex();
            int packCount = Preferences.GetPacksCount();
            int pageCount = packCount + (comingSoon >= 0 ? 1 : 0);
            timeTravelPages.Clear();
            timeTravelPageBackdrops.Clear();
            timeTravelShownPage = -1;
            timeTravelPageWidth = visible.w;

            for (int i = 0; i < pageCount; i++)
            {
                Image backdrop = CreateTimeTravelPageBackdrop(TimeTravelArt.PackPage(i, i >= packCount));
                backdrop.SetName("ttPageBackdrop");
                StretchTimeTravelBackdrop(backdrop, visible);
                timeTravelPageBackdrops.Add(backdrop);
                _ = root.AddChild(backdrop);
            }

            // The pages scroll under the scene but draw nothing: they only carry each page's touch
            // area, while the icons turn in place above them.
            BaseElement pageRow = new() { width = (int)MathF.Ceiling(pageCount * timeTravelPageWidth), height = (int)visible.h };
            packContainer = new ScrollableContainer().InitWithWidthHeightContainer(visible.w, visible.h, pageRow);
            packContainer.minAutoScrollToSpointLength = RTD(5);
            packContainer.shouldBounceHorizontally = true;
            packContainer.resetScrollOnShow = false;
            packContainer.dontHandleTouchDownsHandledByChilds = true;
            packContainer.dontHandleTouchMovesHandledByChilds = true;
            packContainer.dontHandleTouchUpsHandledByChilds = true;
            packContainer.TurnScrollPointsOnWithCapacity(pageCount);
            packContainer.delegateScrollableContainerProtocol = this;
            _ = root.AddChild(packContainer);

            TimeTravelSceneGroup scene = new();
            timeTravelPackScene = scene;
            timeTravelRays = AddTimeTravelRays(scene, screen, out timeTravelRaysSwing);

            BaseElement bob = CreateTimeTravelIconBob();
            _ = scene.AddChild(bob);
            for (int i = 0; i < pageCount; i++)
            {
                bool isComingSoon = i >= packCount;
                TimeTravelPackPage page = isComingSoon ? CreateTimeTravelComingSoonPage() : CreateTimeTravelPackPage(i);
                page.Icon.SetName("ttPackIcon");
                timeTravelPages.Add(page);

                // iOS hangs the coming-soon page from the view, not from the bobbing icons.
                _ = isComingSoon ? scene.AddChild(page.Icon) : bob.AddChild(page.Icon);
                boxes[i] = page.Icon;

                TouchBaseElement touch = new()
                {
                    delegateValue = this,
                    bid = isComingSoon ? new MenuButtonId(-1) : MenuButtonId.ForPack(i),
                };
                Rectangle area = TimeTravelIconArea(screen, isComingSoon ? 0 : i);
                touch.anchor = touch.parentAnchor = 9;
                touch.x = (i * timeTravelPageWidth) + area.x;
                touch.y = area.y;
                touch.width = (int)MathF.Round(area.w);
                touch.height = (int)MathF.Round(area.h);
                _ = pageRow.AddChild(touch);
                _ = packContainer.AddScrollPointAtXY(i * timeTravelPageWidth, 0f);
            }

            Text title = new Text().InitWithFont(Application.GetFont(Resources.Fnt.BigFont));
            title.SetName("ttPackTitle");
            title.SetAlignment(2);
            title.anchor = 18;
            title.parentAnchor = 9;
            CenterAtCanvas(title, TimeTravelArt.PackMarkers.Title);
            timeTravelPackTitle = title;
            _ = scene.AddChild(title);

            TimeTravelPageBullets bullets = new(pageCount, TimeTravelBulletGap * TimeTravelArt.CanvasToAsset);
            bullets.SetName("ttPageBullets");
            float bulletsScale = TimeTravelBulletsWidth * TimeTravelArt.CanvasToAsset / bullets.width;
            bullets.scaleX = bullets.scaleY = bulletsScale;
            bullets.anchor = bullets.parentAnchor = 9;
            Rectangle current = TimeTravelArt.PackMarkers.BulletCurrent;
            bullets.x = ((TimeTravelArt.PackMarkers.Title.x + (TimeTravelArt.PackMarkers.Title.w / 2f)) * TimeTravelArt.CanvasToAsset) - (bullets.width / 2f);
            bullets.y = ((current.y + (current.h / 2f)) * TimeTravelArt.CanvasToAsset) - (bullets.height / 2f);
            timeTravelBullets = bullets;
            _ = scene.AddChild(bullets);
            scene.Attach(bullets, TimeTravelAttach.Bottom);

            prevb = CreateTimeTravelPageArrow(MenuButtonId.PreviousPack, mirrored: true);
            prevb.SetName("ttPrevPage");
            prevb.x = TimeTravelArt.PackMarkers.LeftArrow.x * TimeTravelArt.CanvasToAsset;
            prevb.y = TimeTravelArt.PackMarkers.LeftArrow.y * TimeTravelArt.CanvasToAsset;
            _ = scene.AddChild(prevb);
            scene.Attach(prevb, TimeTravelAttach.Left);
            nextb = CreateTimeTravelPageArrow(MenuButtonId.NextPack, mirrored: false);
            nextb.SetName("ttNextPage");
            Rectangle right = TimeTravelArt.PackMarkers.RightArrow;
            nextb.x = ((right.x + right.w) * TimeTravelArt.CanvasToAsset) - nextb.width;
            nextb.y = right.y * TimeTravelArt.CanvasToAsset;
            _ = scene.AddChild(nextb);
            scene.Attach(nextb, TimeTravelAttach.Right);

            _ = root.AddChild(scene);
            scene.Layout(screen);

            HBox starTotal = CreateTimeTravelTextWithStar(
                Preferences.GetTotalStars().ToString(CultureInfo.InvariantCulture) + "/" + TimeTravelMaxStars().ToString(CultureInfo.InvariantCulture));
            starTotal.SetName("text");
            starTotal.anchor = starTotal.parentAnchor = 12;
            PlaceStarTotal(starTotal);
            _ = root.AddChild(starTotal);

            _ = menuView.AddChild(root);
            packSelectBuiltFor = visible;
            Button back = CreateBackButtonWithDelegateID(this, MenuButtonId.BackFromPackSelect);
            back.SetName("backb");
            _ = menuView.AddChild(back);
            AttachSnowfallOverlay(menuView);
            AddViewwithID(menuView, VIEW_PACK_SELECT);

            int lastPack = Math.Clamp(Preferences.GetLastBox(), 0, pageCount - 1);
            Application.SharedRootController().Box = Preferences.GetLastGamePack();
            packContainer.PlaceToScrollPoint(lastPack);
            ScrollableContainerchangedTargetScrollPoint(packContainer, lastPack);
            UpdateTimeTravelPackSelect(0f);
        }

        /// <summary>Adds the rays, swinging inside their turn, to a scene.</summary>
        /// <param name="scene">Scene to add them to.</param>
        /// <param name="screen">The screen model they are scaled for.</param>
        /// <param name="swing">What swings them when the page changes.</param>
        /// <returns>The rays.</returns>
        private static Image AddTimeTravelRays(TimeTravelSceneGroup scene, TimeTravelScreen screen, out BaseElement swing)
        {
            Rectangle box = TimeTravelArt.PackMarkers.Rays;
            BaseElement holder = new()
            {
                width = (int)MathF.Round(box.w * TimeTravelArt.CanvasToAsset),
                height = (int)MathF.Round(box.h * TimeTravelArt.CanvasToAsset),
            };
            holder.SetName("ttRaysSwing");
            holder.anchor = holder.parentAnchor = 9;
            holder.x = box.x * TimeTravelArt.CanvasToAsset;
            holder.y = box.y * TimeTravelArt.CanvasToAsset;

            BaseElement turn = new() { width = holder.width, height = holder.height };
            turn.anchor = turn.parentAnchor = 18;
            Timeline spin = new Timeline().InitWithMaxKeyFramesOnTrack(2);
            spin.AddKeyFrame(KeyFrame.MakeRotation(0, KeyFrame.TransitionType.FRAME_TRANSITION_LINEAR, 0));
            spin.AddKeyFrame(KeyFrame.MakeRotation(360, KeyFrame.TransitionType.FRAME_TRANSITION_LINEAR, TimeTravelRaysPeriod));
            spin.SetTimelineLoopType(Timeline.LoopType.TIMELINE_REPLAY);
            _ = turn.AddTimeline(spin);
            turn.PlayTimeline(0);
            _ = holder.AddChild(turn);

            Image rays = Image.FromResource(Resources.Img.MenuPackSelectionTimeTravel, TimeTravelArt.PackRays);
            rays.SetName("ttRays");
            rays.anchor = rays.parentAnchor = 18;
            rays.hasColor = true;
            rays.color = TimeTravelArt.PageColor(0);
            ScaleTimeTravelRays(rays, screen);
            _ = turn.AddChild(rays);
            _ = scene.AddChild(holder);
            swing = holder;
            return rays;
        }

        /// <summary>
        /// Scales the rays to the full screen height, as iOS sizes them, and further on a screen
        /// wider than that leaves them, so they always span its full width.
        /// </summary>
        /// <param name="rays">The rays.</param>
        /// <param name="screen">The screen model.</param>
        internal static void ScaleTimeTravelRays(Image rays, TimeTravelScreen screen)
        {
            rays.scaleX = rays.scaleY = TimeTravelRaysScale(screen);
        }

        /// <summary>The rays' scale for a screen: iOS's height rule, or wider to span its full width.</summary>
        /// <param name="screen">The screen model.</param>
        /// <returns>The scale, against the rays' own size.</returns>
        internal static float TimeTravelRaysScale(TimeTravelScreen screen)
        {
            float byHeight = TimeTravelRaysHeightScale * screen.FullHeight / TimeTravelScreen.SceneHeight;
            float raysWidth = TimeTravelArt.PackMarkers.Rays.w / 2f;
            return MathF.Max(byHeight, screen.FullWidth / raysWidth);
        }

        /// <summary>Creates a Time Travel menu background from its page quad.</summary>
        private static Image CreateTimeTravelPageBackdrop(int page)
        {
            Image backdrop = Image.FromResource(Resources.Img.MenuBgrsTimeTravel, Math.Clamp(page, 0, TimeTravelArt.PageCount - 1));
            backdrop.anchor = backdrop.parentAnchor = 18;
            return backdrop;
        }

        /// <summary>
        /// Stretches a page background over the screen, a little past every edge so its soft rim
        /// stays off it (iOS full-screen stretch at 1.04).
        /// </summary>
        /// <param name="backdrop">The background.</param>
        /// <param name="visible">The logical region the viewport exposes.</param>
        private static void StretchTimeTravelBackdrop(Image backdrop, Rectangle visible)
        {
            backdrop.scaleX = visible.w / backdrop.width * TimeTravelBackdropBleed;
            backdrop.scaleY = visible.h / backdrop.height * TimeTravelBackdropBleed;
        }

        /// <summary>The element the icons hang from, bobbing as iOS bobs them.</summary>
        /// <returns>The element, sized to the first icon and centered where the icons rest.</returns>
        private static BaseElement CreateTimeTravelIconBob()
        {
            Vector size = Image.GetQuadSize(Resources.Img.MenuPackSelectionIconsTimeTravel, 0);
            BaseElement bob = new()
            {
                width = (int)MathF.Round(size.X),
                height = (int)MathF.Round(size.Y),
            };
            bob.SetName("ttIconBob");
            bob.anchor = bob.parentAnchor = 9;
            float x = (TimeTravelIconCenter.X * TimeTravelArt.CanvasToAsset) - (bob.width / 2f);
            float y = (TimeTravelIconCenter.Y * TimeTravelArt.CanvasToAsset) - (bob.height / 2f);
            bob.x = x;
            bob.y = y;
            float top = y - (TimeTravelIconBob * TimeTravelArt.CanvasToAsset);
            Timeline timeline = new Timeline().InitWithMaxKeyFramesOnTrack(4);
            timeline.AddKeyFrame(KeyFrame.MakePos(x, y, KeyFrame.TransitionType.FRAME_TRANSITION_FLASH_EASE_IN_OUT, 0));
            timeline.AddKeyFrame(KeyFrame.MakePos(x, top, KeyFrame.TransitionType.FRAME_TRANSITION_FLASH_EASE_IN_OUT, TimeTravelIconRise));
            timeline.AddKeyFrame(KeyFrame.MakePos(x, y, KeyFrame.TransitionType.FRAME_TRANSITION_FLASH_EASE_IN_OUT, TimeTravelIconFall));
            timeline.AddKeyFrame(KeyFrame.MakePos(x, y, KeyFrame.TransitionType.FRAME_TRANSITION_LINEAR, TimeTravelIconRest));
            timeline.SetTimelineLoopType(Timeline.LoopType.TIMELINE_REPLAY);
            _ = bob.AddTimeline(timeline);
            bob.PlayTimeline(0);
            return bob;
        }

        /// <summary>
        /// Where a pack's icon is drawn at rest: the touch area of its page (iOS adds one
        /// <c>TouchElement</c> per page over the icon).
        /// </summary>
        /// <param name="screen">The screen model.</param>
        /// <param name="pack">Pack whose icon to measure.</param>
        /// <returns>The area, in logical units.</returns>
        private static Rectangle TimeTravelIconArea(TimeTravelScreen screen, int pack)
        {
            (string sheet, int quad) = TimeTravelArt.PackIcon(pack);
            Vector size = Image.GetQuadSize(sheet, quad);
            float logicalWidth = size.X / FlashXmlScale.AtlasToFlashPointScale;
            float logicalHeight = size.Y / FlashXmlScale.AtlasToFlashPointScale;
            float centerX = TimeTravelIconCenter.X / 2f;
            float centerY = TimeTravelIconCenter.Y / 2f;
            Vector topLeft = screen.ToDesign(centerX - (logicalWidth / 2f), centerY - (logicalHeight / 2f));
            return new Rectangle(topLeft.X, topLeft.Y, logicalWidth * screen.Scale, logicalHeight * screen.Scale);
        }

        /// <summary>
        /// One pack's page (iOS <c>LevelSelectController::createPack</c>): its icon, veiled above
        /// the stars it still needs and padlocked while it is locked, and badged once every star
        /// in it is won.
        /// </summary>
        /// <param name="n">Pack index.</param>
        /// <returns>The page.</returns>
        private static TimeTravelPackPage CreateTimeTravelPackPage(int n)
        {
            int stars = Preferences.GetTotalStarsInBox(PackConfig.GetSaveSlot(n));
            int needed = PackConfig.GetUnlockStars(n);
            if (n > 0 && Preferences.GetUnlockedForPackLevel(n, 0) == UNLOCKEDSTATE.LOCKED && stars >= needed)
            {
                Preferences.SetUnlockedForPackLevel(UNLOCKEDSTATE.JUSTUNLOCKED, n, 0);
            }
            UNLOCKEDSTATE state = Preferences.GetUnlockedForPackLevel(n, 0);
            (string sheet, int quad) = TimeTravelArt.PackIcon(n);
            Vector size = Image.GetQuadSize(sheet, quad);
            BaseElement icon = new TimeTravelPackIcon()
            {
                width = (int)MathF.Round(size.X),
                height = (int)MathF.Round(size.Y),
            };
            icon.anchor = icon.parentAnchor = 18;
            TimeTravelPackPage page = new() { Icon = icon };

            if (state == UNLOCKEDSTATE.UNLOCKED)
            {
                Image art = Image.FromResource(sheet, quad);
                art.anchor = art.parentAnchor = 9;
                _ = icon.AddChild(art);
            }
            else
            {
                float opaque = TimeTravelPager.LockedOpaqueHeight(
                    Math.Min(stars, needed), needed, size.Y, TimeTravelShortestIcon());
                page.Opaque = opaque;
                page.JustUnlocked = state == UNLOCKEDSTATE.JUSTUNLOCKED;
                AddTimeTravelLock(page, n, sheet, quad);
            }

            if (Preferences.IsPackPerfect(n))
            {
                Image badge = Image.FromResource(Resources.Img.MenuPackSelectionIconsTimeTravel, TimeTravelArt.PerfectBadge);
                badge.SetName("ttPerfect");
                badge.anchor = badge.parentAnchor = 9;
                Vector badgeOffset = Image.GetQuadOffset(Resources.Img.MenuPackSelectionIconsTimeTravel, TimeTravelArt.PerfectBadge);
                Vector iconOffset = Image.GetQuadOffset(sheet, quad);
                badge.x = badgeOffset.X - iconOffset.X;
                badge.y = badgeOffset.Y - iconOffset.Y;
                _ = icon.AddChild(badge);
            }
            string labelKey = PackConfig.GetBoxLabelText(n);
            if (!string.IsNullOrEmpty(labelKey))
            {
                Image badge = Image.FromResource(Resources.Img.MenuButtonsTimeTravel, TimeTravelArt.HardestBadge);
                badge.SetName("ttPackLabel");
                badge.anchor = badge.parentAnchor = 12;
                badge.x = -badge.width * 0.5f;
                Text label = new Text().InitWithFont(Application.GetFont(Resources.Fnt.BigFont));
                label.SetName("ttPackLabelText");
                // iOS q11 is a text guide, omitted from the packed DX sheet: 316 x 118,
                // centered (-4, 34.5) from the flame's center on the original canvas.
                float textWidth = 316f * TimeTravelArt.CanvasToAsset * 1.15f;
                label.SetAlignment(2);
                label.SetStringandWidth(Application.GetString(labelKey).ToUpperInvariant(), textWidth / 0.53f);
                label.scaleX = label.scaleY = MathF.Min(0.53f, 118f * TimeTravelArt.CanvasToAsset / MathF.Max(1, label.height));
                label.anchor = label.parentAnchor = 18;
                label.x = -4f * TimeTravelArt.CanvasToAsset;
                label.y = 34.5f * TimeTravelArt.CanvasToAsset;
                _ = badge.AddChild(label);
                _ = icon.AddChild(badge);
            }
            return page;
        }

        /// <summary>
        /// Veils, lines and padlocks a pack that is not open (iOS <c>createPack</c> and
        /// <c>createLock</c>).
        /// </summary>
        /// <param name="page">The pack's page.</param>
        /// <param name="n">Pack index.</param>
        /// <param name="sheet">Icon sheet.</param>
        /// <param name="quad">Icon quad.</param>
        private static void AddTimeTravelLock(TimeTravelPackPage page, int n, string sheet, int quad)
        {
            BaseElement icon = page.Icon;
            CroppedImage filled = CroppedImage.Create(sheet, quad);
            filled.anchor = filled.parentAnchor = 9;
            _ = icon.AddChild(filled);
            CroppedImage veil = CroppedImage.Create(sheet, quad);
            veil.anchor = veil.parentAnchor = 9;
            veil.color = RGBAColor.MakeRGBA(1f, 1f, 1f, TimeTravelLockedVeil);
            _ = icon.AddChild(veil);
            page.Filled = filled;
            page.Veil = veil;

            // The line is drawn as its left half and that half mirrored, meeting on the icon's middle.
            Image line = Image.FromResource(Resources.Img.MenuPackSelectionTimeTravel, TimeTravelArt.PackProgressLine);
            line.SetName("ttProgressLine");
            line.anchor = 20;
            line.parentAnchor = 10;
            line.rotationCenterX = line.width / 2f;
            Image mirror = Image.FromResource(Resources.Img.MenuPackSelectionTimeTravel, TimeTravelArt.PackProgressLine);
            mirror.anchor = 9;
            mirror.parentAnchor = 12;
            mirror.scaleX = -1f;
            // Negative scale pivots around an integer half-width; account for odd sizes too.
            mirror.x = mirror.width - (2 * (mirror.width >> 1)) - TimeTravelSeamOverlap;
            _ = line.AddChild(mirror);
            line.passTransformationsToChilds = true;
            Timeline vanish = new Timeline().InitWithMaxKeyFramesOnTrack(2);
            vanish.AddKeyFrame(KeyFrame.MakeScale(1, 1, KeyFrame.TransitionType.FRAME_TRANSITION_LINEAR, 0));
            vanish.AddKeyFrame(KeyFrame.MakeScale(0, 0, KeyFrame.TransitionType.FRAME_TRANSITION_LINEAR, TimeTravelPriceFade));
            _ = line.AddTimeline(vanish);
            _ = icon.AddChild(line);
            page.Line = line;
            SetTimeTravelOpaque(page, page.Opaque);

            // The padlock's stage origin sits on the icon's middle.
            TimeTravelFlashStage padlock = TimeTravelFlashStage.Create(TimeTravelArt.PackLockAnimationXml, Resources.Img.MenuPackSelectionTimeTravel);
            padlock.Root.SetName("ttLock");
            padlock.Place(FlashXmlScale.AtlasToFlashPointScale, 0f, 0f, icon.width / 2f, icon.height / 2f);
            _ = icon.AddChild(padlock.Root);

            // Posed at its first frame and held there until the pack opens.
            padlock.Play(0);
            CloseTimeTravelLockSeam(padlock);
            padlock.Root.updateable = false;
            page.Lock = padlock;

            page.Price = CreateTimeTravelPrice(icon, padlock.Part("lock1half"), PackConfig.GetUnlockStars(n));
            _ = icon.AddChild(page.Price);
        }

        /// <summary>Places the mirror hinge on the trimmed quad, with a small overlap.</summary>
        /// <param name="padlock">The lock whose Flash timeline has just set its pose.</param>
        private static void CloseTimeTravelLockSeam(TimeTravelFlashStage padlock)
        {
            Image half = padlock.Part("lock2half");
            float width = FlashXmlScale.NormalizeAtlasValue(half.texture.quadRects[half.quadToDraw].w);
            float overlap = FlashXmlScale.NormalizeAtlasValue(TimeTravelSeamOverlap);
            half.rotationCenterX = width - (half.width >> 1) - (overlap / 2f);
        }

        /// <summary>
        /// The padlock's star and price, which fade away when the pack opens. iOS sets the star's
        /// right edge and the price's left edge on two marker points of the padlock, and slides
        /// both left by however much the price is wider than three digits. The number shares
        /// the star's vertical center.
        /// </summary>
        /// <param name="icon">The icon they sit on.</param>
        /// <param name="lockHalf">The posed left half, whose center is the price's scale pivot.</param>
        /// <param name="price">Stars that open the pack.</param>
        /// <returns>The star and price, in an element covering the icon.</returns>
        private static BaseElement CreateTimeTravelPrice(BaseElement icon, Image lockHalf, int price)
        {
            BaseElement group = new() { width = icon.width, height = icon.height };
            group.SetName("ttPrice");
            group.anchor = group.parentAnchor = 9;
            Rectangle half = TimeTravelArt.PackMarkers.LockHalf;
            Rectangle starMarker = TimeTravelArt.PackMarkers.LockStarMarker;
            Rectangle priceMarker = TimeTravelArt.PackMarkers.LockPriceMarker;

            // The padlock's first part rests with its top left at (-123.05, -149.65) logical units
            // from the icon's middle; the markers are measured from that corner.
            float lockLeft = (icon.width / 2f) - (123.05f * FlashXmlScale.AtlasToFlashPointScale);
            float lockTop = (icon.height / 2f) - (149.65f * FlashXmlScale.AtlasToFlashPointScale);
            float c = TimeTravelArt.CanvasToAsset;

            // iOS scales this group to 90% as a child of the left lock half. Keep the
            // icon-sized fade group, but use that half's center rather than the icon's.
            group.scaleX = group.scaleY = 0.9f;
            group.rotationCenterX = lockLeft + (lockHalf.width * FlashXmlScale.AtlasToFlashPointScale / 2f) - (group.width >> 1);
            group.rotationCenterY = lockTop + (lockHalf.height * FlashXmlScale.AtlasToFlashPointScale / 2f) - (group.height >> 1);

            Text text = new Text().InitWithFont(Application.GetFont(Resources.Fnt.BigFont));
            text.SetString(price.ToString(CultureInfo.InvariantCulture));
            Text threeDigits = new Text().InitWithFont(Application.GetFont(Resources.Fnt.BigFont));
            threeDigits.SetString("000");
            float slide = MathF.Max(0f, text.width - threeDigits.width);
            text.anchor = 17;
            text.parentAnchor = 9;
            text.x = lockLeft + ((priceMarker.x + (priceMarker.w / 2f) - half.x) * c) - slide;
            _ = group.AddChild(text);

            Image star = Image.FromResource(Resources.Img.MenuPackSelectionTimeTravel, TimeTravelArt.LockStar);
            star.anchor = 20;
            star.parentAnchor = 9;
            star.x = lockLeft + ((starMarker.x + (starMarker.w / 2f) - half.x) * c) - slide;
            star.y = lockTop + ((starMarker.y + (starMarker.h / 2f) - half.y) * c);
            _ = group.AddChild(star);
            // Center anchors use integer half-heights; compensate when just one height is odd.
            text.y = star.y + (((star.height & 1) - (text.height & 1)) / 2f);

            Timeline fade = new Timeline().InitWithMaxKeyFramesOnTrack(2);
            fade.AddKeyFrame(KeyFrame.MakeColor(RGBAColor.solidOpaqueRGBA, KeyFrame.TransitionType.FRAME_TRANSITION_LINEAR, 0));
            fade.AddKeyFrame(KeyFrame.MakeColor(RGBAColor.transparentRGBA, KeyFrame.TransitionType.FRAME_TRANSITION_LINEAR, TimeTravelPriceFade));
            _ = group.AddTimeline(fade);
            return group;
        }

        /// <summary>Redraws a locked icon's veil and line for how much of it is opaque.</summary>
        /// <param name="page">The pack's page.</param>
        /// <param name="opaque">Opaque height from the bottom, in the icon's units.</param>
        private static void SetTimeTravelOpaque(TimeTravelPackPage page, float opaque)
        {
            float height = page.Icon.height;
            float clamped = Math.Clamp(opaque, 0f, height);
            page.Opaque = clamped;
            page.Filled.CropTop = height - clamped;
            page.Veil.CropBottom = clamped;
            page.Line.y = height - clamped;
        }

        /// <summary>The shortest pack icon's height, which every locked icon fills against.</summary>
        /// <returns>The height in asset pixels.</returns>
        private static float TimeTravelShortestIcon()
        {
            float shortest = float.MaxValue;
            for (int i = 0; i < Math.Max(TimeTravelArt.PackIcons, Preferences.GetPacksCount()); i++)
            {
                (string sheet, int quad) = TimeTravelArt.PackIcon(i);
                shortest = MathF.Min(shortest, Image.GetQuadSize(sheet, quad).Y);
            }
            return shortest;
        }

        /// <summary>
        /// The page after the last pack (iOS <c>createComingSoon</c>): the three gadgets bobbing
        /// where the iOS canvas puts them, and the coming-soon line in the box beneath them.
        /// </summary>
        /// <returns>The page.</returns>
        private static TimeTravelPackPage CreateTimeTravelComingSoonPage()
        {
            // Covers the whole scene, so its art lands where the canvas draws it.
            BaseElement icon = new TimeTravelPackIcon()
            {
                width = TimeTravelSceneGroup.Width,
                height = TimeTravelSceneGroup.Height,
            };
            icon.anchor = icon.parentAnchor = 9;
            float c = TimeTravelArt.CanvasToAsset;

            for (int i = 0; i < TimeTravelArt.ComingSoonGadgets; i++)
            {
                Rectangle box = TimeTravelArt.PackMarkers.ComingSoonGadgets[i];
                Image gadget = Image.FromResource(Resources.Img.MenuPackSelectionTimeTravel, TimeTravelArt.ComingSoonFirstGadget + i);
                gadget.anchor = gadget.parentAnchor = 9;
                float x = box.x * c;
                float y = box.y * c;
                gadget.x = x;
                gadget.y = y;
                Timeline bob = new Timeline().InitWithMaxKeyFramesOnTrack(4);
                bob.AddKeyFrame(KeyFrame.MakePos(x, y, KeyFrame.TransitionType.FRAME_TRANSITION_FLASH_EASE_IN_OUT, 0));
                bob.AddKeyFrame(KeyFrame.MakePos(x, y - (TimeTravelGadgetBob * c), KeyFrame.TransitionType.FRAME_TRANSITION_FLASH_EASE_IN_OUT, TimeTravelIconRise));
                bob.AddKeyFrame(KeyFrame.MakePos(x, y, KeyFrame.TransitionType.FRAME_TRANSITION_FLASH_EASE_IN_OUT, TimeTravelIconFall));
                bob.AddKeyFrame(KeyFrame.MakePos(x, y, KeyFrame.TransitionType.FRAME_TRANSITION_LINEAR, TimeTravelIconRest));
                bob.SetTimelineLoopType(Timeline.LoopType.TIMELINE_REPLAY);
                _ = gadget.AddTimeline(bob);
                gadget.PlayTimeline(0);
                _ = icon.AddChild(gadget);
            }

            Rectangle textBox = TimeTravelArt.PackMarkers.ComingSoonText;
            Text text = new Text().InitWithFont(Application.GetFont(Resources.Fnt.BigFont));
            text.SetName("ttComingSoonText");
            text.SetAlignment(2);
            text.SetStringandWidth(Application.GetString("BOX_SOON_LABEL"), textBox.w * c);
            text.anchor = 18;
            text.parentAnchor = 9;
            CenterAtCanvas(text, textBox);
            _ = icon.AddChild(text);
            return new TimeTravelPackPage { Icon = icon };
        }

        /// <summary>Places an element's anchor on the middle of an iOS canvas rect.</summary>
        /// <param name="element">Element to place, anchored by its middle.</param>
        /// <param name="box">The rect, in canvas pixels.</param>
        private static void CenterAtCanvas(BaseElement element, Rectangle box)
        {
            element.x = (box.x + (box.w / 2f)) * TimeTravelArt.CanvasToAsset;
            element.y = (box.y + (box.h / 2f)) * TimeTravelArt.CanvasToAsset;
        }

        /// <summary>
        /// One of the page arrows (iOS <c>createArrowForView</c>): the left one is the right one
        /// mirrored. Each fades in and out as there is a page its way.
        /// </summary>
        /// <param name="id">Button identifier.</param>
        /// <param name="mirrored">Whether to point it left.</param>
        /// <returns>The button.</returns>
        private Button CreateTimeTravelPageArrow(ButtonId id, bool mirrored)
        {
            Image up = Image.FromResource(Resources.Img.MenuButtonsTimeTravel, TimeTravelArt.PageArrowUp);
            Image down = Image.FromResource(Resources.Img.MenuButtonsTimeTravel, TimeTravelArt.PageArrowDown);
            if (mirrored)
            {
                up.scaleX = down.scaleX = -1f;
            }

            // The pressed arrow is cut smaller; both share the canvas, so it is placed by its offset.
            up.anchor = up.parentAnchor = down.anchor = down.parentAnchor = 9;
            Vector upOffset = Image.GetQuadOffset(Resources.Img.MenuButtonsTimeTravel, TimeTravelArt.PageArrowUp);
            Vector downOffset = Image.GetQuadOffset(Resources.Img.MenuButtonsTimeTravel, TimeTravelArt.PageArrowDown);
            down.x = mirrored ? up.width - down.width - (downOffset.X - upOffset.X) : downOffset.X - upOffset.X;
            down.y = downOffset.Y - upOffset.Y;
            Button button = new Button().InitWithUpElementDownElementandID(up, down, id);
            button.delegateButtonDelegate = this;
            button.anchor = button.parentAnchor = 9;
            button.SetTouchIncreaseLeftRightTopBottom(20f, 20f, 20f, 20f);
            Timeline show = new Timeline().InitWithMaxKeyFramesOnTrack(2);
            show.AddKeyFrame(KeyFrame.MakeColor(RGBAColor.transparentRGBA, KeyFrame.TransitionType.FRAME_TRANSITION_FLASH_EASE_IN, 0));
            show.AddKeyFrame(KeyFrame.MakeColor(RGBAColor.solidOpaqueRGBA, KeyFrame.TransitionType.FRAME_TRANSITION_FLASH_EASE_IN, 0.5f));
            _ = button.AddTimeline(show);
            Timeline hide = new Timeline().InitWithMaxKeyFramesOnTrack(2);
            hide.AddKeyFrame(KeyFrame.MakeColor(RGBAColor.solidOpaqueRGBA, KeyFrame.TransitionType.FRAME_TRANSITION_FLASH_EASE_IN, 0));
            hide.AddKeyFrame(KeyFrame.MakeColor(RGBAColor.transparentRGBA, KeyFrame.TransitionType.FRAME_TRANSITION_FLASH_EASE_IN, 0.5f));
            _ = button.AddTimeline(hide);
            return button;
        }

        /// <summary>Fades a page arrow in or out, letting it be pressed only while it shows.</summary>
        /// <param name="arrow">The arrow.</param>
        /// <param name="shown">Whether there is a page its way.</param>
        /// <param name="animate">Whether to fade rather than switch at once.</param>
        private static void ShowTimeTravelArrow(Button arrow, bool shown, bool animate)
        {
            if (arrow == null || arrow.touchable == shown)
            {
                return;
            }
            arrow.touchable = shown;
            if (animate)
            {
                arrow.PlayTimeline(shown ? 0 : 1);
            }
            else
            {
                arrow.color = shown ? RGBAColor.solidOpaqueRGBA : RGBAColor.transparentRGBA;
            }
        }

        /// <summary>
        /// Draws the pack pages for where they are scrolled to (iOS
        /// <c>LevelSelectController::update</c>): the backgrounds cross-fade and the rays blend
        /// both pages' tints and turn once per page, while each icon near the screen turns and
        /// fades in place.
        /// </summary>
        /// <param name="delta">Seconds since the last frame.</param>
        private void UpdateTimeTravelPackSelect(float delta)
        {
            int count = timeTravelPages.Count;
            if (packContainer == null || count == 0)
            {
                return;
            }

            float position = TimeTravelPager.Position(packContainer.GetScroll().X, timeTravelPageWidth, count);
            int from = (int)MathF.Floor(position);
            int to = Math.Min(from + 1, count - 1);
            float fraction = position - from;

            for (int i = 0; i < timeTravelPageBackdrops.Count; i++)
            {
                float alpha = i == from ? 1f - fraction : i == to ? fraction : 0f;
                Image backdrop = timeTravelPageBackdrops[i];
                backdrop.visible = alpha > 0f;
                backdrop.color = RGBAColor.MakeRGBA(1f, 1f, 1f, alpha);
            }

            int packCount = Preferences.GetPacksCount();
            if (timeTravelRays != null)
            {
                timeTravelRays.color = TimeTravelPager.Blend(
                    TimeTravelArt.PageColor(TimeTravelArt.PackPage(from, from >= packCount)),
                    TimeTravelArt.PageColor(TimeTravelArt.PackPage(to, to >= packCount)),
                    fraction);
                timeTravelRays.rotation = TimeTravelPager.RaysTurn(fraction);
            }

            for (int i = 0; i < count; i++)
            {
                TimeTravelPackPage page = timeTravelPages[i];
                float distance = position - i;
                bool near = MathF.Abs(distance) < 1f;
                page.Icon.visible = near;
                if (!near)
                {
                    continue;
                }
                (float rotation, float alpha) = TimeTravelPager.IconPose(distance);
                page.Icon.rotation = rotation;
                page.Icon.color = RGBAColor.MakeRGBA(1f, 1f, 1f, alpha);
                if (page.Opening)
                {
                    StepTimeTravelOpening(page, delta);
                }
                if (page.Lock != null)
                {
                    // Flash actions reset the hinge every frame; align it after the pose updates.
                    CloseTimeTravelLockSeam(page.Lock);
                }
            }

            // iOS uses roundf, whose midpoint rule differs from .NET's default rounding.
            int shown = (int)MathF.Round(position, MidpointRounding.AwayFromZero);
            if (shown != timeTravelShownPage)
            {
                ShowTimeTravelPage(timeTravelShownPage, shown);
            }
        }

        /// <summary>
        /// Settles the title, bullets and arrows on a page, and swings the rays the way the
        /// pages moved (iOS <c>changePack</c> and <c>updateState</c>).
        /// </summary>
        /// <param name="from">Page shown before, or -1 when the pages are first drawn.</param>
        /// <param name="to">Page shown now.</param>
        private void ShowTimeTravelPage(int from, int to)
        {
            bool first = from < 0;
            timeTravelShownPage = to;
            int packCount = Preferences.GetPacksCount();
            if (timeTravelPackTitle != null)
            {
                string title = to < packCount ? PackConfig.GetPackTitle(to, withNumber: false) : string.Empty;
                Rectangle box = TimeTravelArt.PackMarkers.Title;
                timeTravelPackTitle.SetStringandWidth(title, box.w * 1.5f * TimeTravelArt.CanvasToAsset);
            }
            timeTravelBullets?.SetCurrent(to);
            ShowTimeTravelArrow(prevb, to > 0, !first);
            ShowTimeTravelArrow(nextb, to < timeTravelPages.Count - 1, !first);
            if (first || timeTravelRaysSwing == null)
            {
                return;
            }

            float start = timeTravelRaysSwing.rotation;
            float swing = from >= to ? -TimeTravelRaysSwing : TimeTravelRaysSwing;
            Timeline timeline = new Timeline().InitWithMaxKeyFramesOnTrack(3);
            timeline.AddKeyFrame(KeyFrame.MakeRotation(start, KeyFrame.TransitionType.FRAME_TRANSITION_FLASH_EASE_IN_OUT, 0));
            timeline.AddKeyFrame(KeyFrame.MakeRotation(start + swing, KeyFrame.TransitionType.FRAME_TRANSITION_FLASH_EASE_IN_OUT, TimeTravelRaysSwingTime));
            timeline.AddKeyFrame(KeyFrame.MakeScale(1, 1, KeyFrame.TransitionType.FRAME_TRANSITION_LINEAR, 0));
            timeline.AddKeyFrame(KeyFrame.MakeScale(TimeTravelRaysSwell, TimeTravelRaysSwell, KeyFrame.TransitionType.FRAME_TRANSITION_LINEAR, TimeTravelRaysSwingTime / 2f));
            timeline.AddKeyFrame(KeyFrame.MakeScale(1, 1, KeyFrame.TransitionType.FRAME_TRANSITION_LINEAR, TimeTravelRaysSwingTime / 2f));
            timeTravelRaysSwing.AddTimelinewithID(timeline, 0);
            timeTravelRaysSwing.PlayTimeline(0);
        }

        /// <summary>
        /// Settles the pages on a pack: a pack opened since the pages were built fills from its
        /// line to its top, and then its padlock splits apart (iOS <c>update</c>'s unlock).
        /// </summary>
        /// <param name="i">Page settled on.</param>
        private void ReachTimeTravelPage(int i)
        {
            if (i < 0 || i >= timeTravelPages.Count)
            {
                return;
            }
            TimeTravelPackPage page = timeTravelPages[i];
            if (page.JustUnlocked && !page.Opening && Preferences.GetUnlockedForPackLevel(i, 0) == UNLOCKEDSTATE.JUSTUNLOCKED)
            {
                Preferences.SetUnlockedForPackLevel(UNLOCKEDSTATE.UNLOCKED, i, 0);
                page.Opening = true;
            }
        }

        /// <summary>Fills an opening pack one frame further, and opens its padlock once it is full.</summary>
        /// <param name="page">The opening pack's page.</param>
        /// <param name="delta">Seconds since the last frame.</param>
        private void StepTimeTravelOpening(TimeTravelPackPage page, float delta)
        {
            float target = page.Icon.height;
            float next = page.Opaque + (TimeTravelUnlockSpeed * FlashXmlScale.AtlasToFlashPointScale * delta);
            if (next < target)
            {
                SetTimeTravelOpaque(page, next);
                return;
            }
            SetTimeTravelOpaque(page, target);
            page.Opening = false;
            page.JustUnlocked = false;
            page.Line.PlayTimeline(0);
            page.Price.PlayTimeline(0);
            page.Lock.Root.updateable = true;
            page.Lock.Play(0);
            TimeTravelLockParticles burst = new TimeTravelLockParticles().Init();
            burst.x = timeTravelPackScene.width / 2f;
            burst.y = timeTravelPackScene.height / 2f;
            _ = timeTravelPackScene.AddChild(burst);
        }

        /// <summary>Most stars the packs hold, three a level.</summary>
        /// <returns>The star count.</returns>
        private static int TimeTravelMaxStars()
        {
            int total = 0;
            for (int i = 0; i < Preferences.GetPacksCount(); i++)
            {
                total += Preferences.GetLevelsInPackCount(i) * 3;
            }
            return total;
        }

        /// <summary>A star count as the Time Travel menus show it: the count, then the star.</summary>
        /// <param name="t">Count to show.</param>
        /// <returns>The label row.</returns>
        internal static HBox CreateTimeTravelTextWithStar(string t)
        {
            Image star = Image.FromResource(Resources.Img.MenuPackSelectionTimeTravel, TimeTravelArt.CounterStar);
            HBox hbox = new HBox().InitWithOffsetAlignHeight(0, 16, star.height);
            Text text = new Text().InitWithFont(Application.GetFont(Resources.Fnt.BigFont));
            text.SetString(t);
            text.scaleX = text.scaleY = 0.7f;
            text.rotationCenterX = -text.width / 2;
            text.width = (int)(text.width * 0.7f);
            _ = hbox.AddChild(text);
            _ = hbox.AddChild(star);
            return hbox;
        }

        /// <summary>
        /// The level picker's backdrop: the pack page's background and its tinted rays, turning
        /// (iOS <c>createPackView</c>).
        /// </summary>
        /// <param name="menuView">The level picker's view.</param>
        private void CreateTimeTravelLevelsBackdrop(MenuView menuView)
        {
            bool comingSoon = pack >= Preferences.GetPacksCount();
            int page = TimeTravelArt.PackPage(pack, comingSoon);
            Image backdrop = CreateTimeTravelPageBackdrop(page);
            backdrop.SetName("levelsBack");
            StretchTimeTravelBackdrop(backdrop, VisibleBounds);
            levelsTimeTravelBackdrop = backdrop;
            _ = menuView.AddChild(backdrop);

            TimeTravelSceneGroup scene = new();
            TimeTravelScreen screen = new(VisibleBounds);
            timeTravelLevelsRays = AddTimeTravelRays(scene, screen, out _);
            timeTravelLevelsRays.color = TimeTravelArt.PageColor(page);
            timeTravelLevelsScene = scene;
            scene.Layout(screen);
            _ = menuView.AddChild(scene);
        }

        /// <summary>
        /// One level's button on the Time Travel plates (iOS <c>LevelSelectButton</c>): its number
        /// and stars on the open plate, the last one played highlighted, the padlocked plate while
        /// it is locked. Sized to the classic button so the classic grid holds.
        /// </summary>
        /// <param name="l">Level index.</param>
        /// <param name="p">Pack index.</param>
        /// <returns>The button.</returns>
        private TouchBaseElement CreateTimeTravelLevelButton(int l, int p)
        {
            bool locked = Preferences.GetUnlockedForPackLevel(p, l) == UNLOCKEDSTATE.LOCKED;
            RootController root = Application.SharedRootController();
            bool lastPlayed = !locked && root.Pack == p && root.Level == l;
            int quad = locked ? TimeTravelArt.LevelLocked : lastPlayed ? TimeTravelArt.LevelLastPlayed : TimeTravelArt.LevelOpen;
            string sheet = Resources.Img.MenuLevelsTimeTravel;
            float plateScale = TimeTravelPlates.HeightMatching(sheet, TimeTravelArt.LevelOpen, Resources.Img.MenuLevelUi, 0);
            Image plate = Image.FromResource(sheet, quad);
            plate.anchor = plate.parentAnchor = 18;
            plate.scaleX = plate.scaleY = plateScale;
            if (!locked)
            {
                Rectangle plateBox = TimeTravelArt.LevelMarkers.Plate;
                Rectangle starsBox = TimeTravelArt.LevelMarkers.Stars;
                Rectangle numberBox = TimeTravelArt.LevelMarkers.Number;
                float c = TimeTravelArt.CanvasToAsset;
                int stars = Math.Clamp(Preferences.GetStarsForPackLevel(p, l), 0, 3);
                Image starStrip = Image.FromResource(sheet, TimeTravelArt.LevelStars + stars);
                starStrip.anchor = starStrip.parentAnchor = 9;
                starStrip.x = (starsBox.x - plateBox.x) * c;
                starStrip.y = (starsBox.y - plateBox.y) * c;
                _ = plate.AddChild(starStrip);
                Text number = new Text().InitWithFont(Application.GetFont(Resources.Fnt.BigFont));
                number.SetString((l + 1).ToString(CultureInfo.InvariantCulture));
                number.anchor = 18;
                number.parentAnchor = 9;
                number.x = (numberBox.x + (numberBox.w / 2f) - plateBox.x) * c;
                number.y = (numberBox.y + (numberBox.h / 2f) - plateBox.y) * c;
                _ = plate.AddChild(number);
            }

            TouchBaseElement button = new()
            {
                bbc = MakeRectangle(5f, 0f, -10f, 0f),
                delegateValue = this,
                bid = locked ? new MenuButtonId(-1) : MenuButtonId.ForLevel(l),
                width = (int)MathF.Round(plate.width * plateScale),
                height = (int)MathF.Round(plate.height * plateScale),
            };
            _ = button.AddChild(plate);
            while (timeTravelLevelButtons.Count <= l)
            {
                timeTravelLevelButtons.Add(null);
            }
            timeTravelLevelButtons[l] = button;
            return button;
        }

        /// <summary>
        /// Plays a pressed level's burst over its button and whitens the screen as it ends, then
        /// starts the level (iOS <c>LevelSelectButton::processTouchDown</c>).
        /// </summary>
        /// <param name="l">Level pressed.</param>
        private void PlayTimeTravelLevelBurst(int l)
        {
            SoundMgr.PlaySound(Resources.Snd.LevelIconTimeTravel);
            View view = ActiveView();
            BaseElement button = l < timeTravelLevelButtons.Count ? timeTravelLevelButtons[l] : null;
            BaseElement space = (BaseElement)levelsGroup ?? view;
            if (button == null || view == null)
            {
                TimelineFinished(null);
                return;
            }

            // The burst is drawn in the grid's own space, so it grows with the grid; its stage is
            // in iOS logical units, as wide as the plate's logical width is in that space.
            BaseElement.CalculateTopLeft(space);
            float plateLogical = TimeTravelArt.LevelMarkers.Plate.w / 2f;
            float scale = button.width / plateLogical;
            Rectangle plateBox = TimeTravelArt.LevelMarkers.Plate;
            Rectangle numberBox = TimeTravelArt.LevelMarkers.Number;
            float centerX = button.drawX + (button.width / 2f) + ((numberBox.x + (numberBox.w / 2f) - plateBox.x - (plateBox.w / 2f)) / 2f * scale);
            float centerY = button.drawY + (button.height / 2f) + ((numberBox.y + (numberBox.h / 2f) - plateBox.y - (plateBox.h / 2f)) / 2f * scale);
            TimeTravelFlashStage burst = TimeTravelFlashStage.Create(TimeTravelArt.LevelBurstAnimationXml, Resources.Img.MenuLevelsTimeTravel);
            burst.Root.SetName("ttLevelBurst");
            burst.Place(scale, 0f, 0f, centerX - space.drawX, centerY - space.drawY);
            _ = space.AddChild(burst.Root);
            Timeline played = burst.RootTimeline(0);
            float length = played?.Duration ?? 0f;
            played?.delegateTimelineDelegate = this;

            Rectangle visible = VisibleBounds;
            RectangleElement flash = new()
            {
                width = (int)MathF.Ceiling(visible.w),
                height = (int)MathF.Ceiling(visible.h),
                color = RGBAColor.transparentRGBA,
            };
            flash.SetName("ttLevelFlash");
            flash.anchor = flash.parentAnchor = 9;
            Timeline whiten = new Timeline().InitWithMaxKeyFramesOnTrack(3);
            whiten.AddKeyFrame(KeyFrame.MakeColor(RGBAColor.transparentRGBA, KeyFrame.TransitionType.FRAME_TRANSITION_LINEAR, 0));
            whiten.AddKeyFrame(KeyFrame.MakeColor(RGBAColor.transparentRGBA, KeyFrame.TransitionType.FRAME_TRANSITION_LINEAR, MathF.Max(0f, length - TimeTravelLevelFlash)));
            whiten.AddKeyFrame(KeyFrame.MakeColor(RGBAColor.solidOpaqueRGBA, KeyFrame.TransitionType.FRAME_TRANSITION_LINEAR, TimeTravelLevelFlash));
            _ = flash.AddTimeline(whiten);
            flash.PlayTimeline(0);
            _ = view.AddChild(flash);
            burst.Play(0);
            if (played == null)
            {
                TimelineFinished(null);
            }
        }

        /// <summary>Re-places the level picker's Time Travel backdrop for the current viewport.</summary>
        /// <param name="visible">The logical region the viewport exposes.</param>
        private void LayOutTimeTravelLevelsBackdrop(Rectangle visible)
        {
            if (levelsTimeTravelBackdrop != null)
            {
                StretchTimeTravelBackdrop(levelsTimeTravelBackdrop, visible);
            }
            if (timeTravelLevelsScene != null)
            {
                TimeTravelScreen screen = new(visible);
                timeTravelLevelsScene.Layout(screen);
                ScaleTimeTravelRays(timeTravelLevelsRays, screen);
            }
        }
    }
}
