using System;
using System.Globalization;

using CutTheRopeDX.Framework;
using CutTheRopeDX.Framework.Core;
using CutTheRopeDX.Framework.Visual;

namespace CutTheRopeDX.GameMain
{
    /// <content>
    /// The Cut the Rope: Experiments menu scenes, selected with <c>--menu experiments</c>.
    /// </content>
    /// <remarks>
    /// <para>
    /// Structure follows the WP7 decompile; measurements come from the iOS HD 1.7.6 build
    /// (<c>ctrexpdecompiled.c</c>), whose iPad-retina branch matches the atlases. Those atlases were
    /// resampled to <see cref="IosToAsset"/> of the iPad-retina art, so every iOS constant is
    /// multiplied by it here, and the result is in design units.
    /// </para>
    /// <para>
    /// iOS lays these scenes out on a portrait iPad. Only positions inside one piece (a box and its
    /// lock, the beam and its projector) carry over as they are; where the pieces sit on the
    /// landscape screen is composed here, around <see cref="ExpBoxCenterY"/>.
    /// </para>
    /// </remarks>
    internal sealed partial class MenuController
    {
        /// <summary>Scale from iOS iPad-retina units to the Experiments atlases' pixels.</summary>
        internal const float IosToAsset = 25f / 32f;

        /// <summary>Side of a pack element: iOS sizes the touch area and box container to 1200.</summary>
        private const float ExpPackElementSize = 1200f * IosToAsset;

        /// <summary>Gap between neighboring pack elements (iOS <c>flt_414EC</c>, negative: they overlap).</summary>
        private const float ExpPackGap = -110f * IosToAsset;

        /// <summary>Hit rectangle trim from the left of a pack element (iOS <c>flt_414F4</c>).</summary>
        private const float ExpPackHitTrim = 140f * IosToAsset;

        /// <summary>Design-space height the selected box's art is centered on.</summary>
        private const float ExpBoxCenterY = 560f;

        /// <summary>Pack title's top center in the element (iOS 740, 260; the coming-soon box uses 440).</summary>
        private static readonly Vector ExpTitlePosition = new(740f * IosToAsset, 260f * IosToAsset);

        /// <summary>Coming-soon title's top in the element.</summary>
        private const float ExpComingSoonTitleY = 440f * IosToAsset;

        /// <summary>Box the pack title is fitted into (iOS <c>scaleToFitRect:800, 520</c>).</summary>
        private static readonly Vector ExpTitleFit = new(800f * IosToAsset, 520f * IosToAsset);

        /// <summary>Required-stars label's offset from the lock's center (iOS 32, 200).</summary>
        private static readonly Vector ExpLockStarsOffset = new(32f * IosToAsset, 200f * IosToAsset);

        /// <summary>Unlock hint's offset from the element's center (iOS 160, 682).</summary>
        private static readonly Vector ExpHintOffset = new(160f * IosToAsset, 682f * IosToAsset);

        /// <summary>Width the unlock hint wraps within (iOS <c>scaleToFitRect:800, 240</c>).</summary>
        private const float ExpHintWidth = 800f * IosToAsset;

        /// <summary>Monster tint on the selected box (iOS and WP7 agree on 172, 85, 13).</summary>
        private static readonly RGBAColor ExpMonsterSelected = new(172f / 255f, 85f / 255f, 13f / 255f, 1f);

        /// <summary>Monster tint on every other box (54, 100, 169).</summary>
        private static readonly RGBAColor ExpMonsterIdle = new(54f / 255f, 100f / 255f, 169f / 255f, 1f);

        /// <summary>Pack atlas quads, in iOS numbering.</summary>
        private const int ExpQuadBox = 1;
        private const int ExpQuadBoxSelected = 2;
        private const int ExpQuadFirstMonster = 5;
        private const int ExpMonsterCount = 8;
        private const int ExpQuadStar = 13;
        private const int ExpQuadPerfect = 14;
        private const int ExpQuadBambooLock = 18;

        /// <summary>
        /// How far right of the butterfly body's own spot on the box it perches (iOS 20), in
        /// atlas pixels.
        /// </summary>
        private const float ExpButterflyPerchNudge = 20f * IosToAsset;

        /// <summary>Speed the strip scrolls to the caged box when the butterfly is tapped (iOS 1.8).</summary>
        private const float ExpButterflyScrollMultiplier = 1.8f;

        /// <summary>
        /// Whether the butterfly is still flying about this session. Every launch starts it
        /// flying, as iOS does; tapping it sends it to perch on the caged box instead.
        /// </summary>
        private static bool expButterflyFlying = true;

        /// <summary>The butterfly by the caged box, or <see langword="null"/> once that box is open.</summary>
        private ExperimentsButterfly expButterfly;

        /// <summary>Audio atlas quads: two button plates, three icons and the cross.</summary>
        private const int ExpAudioQuadSound = 3;
        private const int ExpAudioQuadMusic = 4;
        private const int ExpAudioQuadCross = 5;

        /// <summary>
        /// Picks the Experiments backdrop for a menu scene.
        /// </summary>
        /// <param name="withLogo">Whether the scene is the main menu.</param>
        /// <param name="viewId">View the backdrop belongs to.</param>
        /// <returns>Resource name of the backdrop.</returns>
        private static string ExperimentsBackdropFor(bool withLogo, int viewId)
        {
            return withLogo
                ? Resources.BackgroundImg.MenuExpMainBgr
                : viewId == VIEW_PACK_SELECT ? Resources.BackgroundImg.MenuExpCampaignBgr : Resources.BackgroundImg.MenuExpDefaultBgr;
        }

        /// <summary>
        /// Maps a classic audio icon quad to its Experiments counterpart.
        /// </summary>
        /// <param name="classicQuad">Quad in <see cref="Resources.Img.MenuOptions"/>: 2 is sound, 3 is music.</param>
        /// <returns>The matching quad in <see cref="Resources.Img.MenuExpAudio"/>.</returns>
        private static int ExperimentsAudioIcon(int classicQuad)
        {
            return classicQuad == 3 ? ExpAudioQuadMusic : ExpAudioQuadSound;
        }

        /// <summary>
        /// Creates one state of an Experiments audio toggle. The engine's
        /// <c>+[MenuController createAudioElementForQuad:withCross:pressed:]</c>.
        /// </summary>
        /// <param name="q">Icon quad.</param>
        /// <param name="crossed">Whether the option is off: the icon is dimmed and crossed out.</param>
        /// <param name="pressed">Whether to use the pressed plate.</param>
        /// <returns>The plate with its icon.</returns>
        private static Image CreateExperimentsAudioElement(int q, bool crossed, bool pressed)
        {
            int plateQuad = pressed ? 1 : 0;
            Image plate = Image.FromResource(Resources.Img.MenuExpAudio, plateQuad);
            Image icon = Image.FromResource(Resources.Img.MenuExpAudio, q);
            icon.anchor = icon.parentAnchor = 18;
            _ = plate.AddChild(icon);
            if (crossed)
            {
                icon.color = RGBAColor.MakeRGBA(0.5f, 0.5f, 0.5f, 0.5f);
                Image cross = Image.FromResource(Resources.Img.MenuExpAudio, ExpAudioQuadCross);
                cross.anchor = cross.parentAnchor = 9;
                Image.SetElementPositionWithRelativeQuadOffset(cross, Resources.Img.MenuExpAudio, plateQuad, ExpAudioQuadCross);
                _ = plate.AddChild(cross);
            }
            return plate;
        }

        /// <summary>
        /// Creates a total-stars style label with the Experiments star. The engine's
        /// <c>-[MenuController createTextWithStar:]</c>.
        /// </summary>
        /// <param name="t">Text shown before the star.</param>
        /// <returns>The label row.</returns>
        private static HBox CreateExperimentsTextWithStar(string t)
        {
            HBox hbox = new HBox().InitWithOffsetAlignHeight(0, 16, 100f * IosToAsset);
            Text text = new Text().InitWithFont(Application.GetFont(Resources.Fnt.BigFont));
            text.SetString(t);
            text.scaleX = text.scaleY = 0.7f;
            text.rotationCenterX = -text.width / 2;
            text.width = (int)(text.width * 0.7f);
            _ = hbox.AddChild(text);
            _ = hbox.AddChild(Image.FromResource(Resources.Img.MenuExpPackSelection, ExpQuadStar));
            return hbox;
        }

        /// <summary>
        /// Builds the Experiments pack selection view: a strip of boxes lit from below by a
        /// projector, with a bullet per box.
        /// </summary>
        /// <remarks>
        /// Built in logical space rather than hung from a fitted group, like the classic strip,
        /// because <see cref="ScrollableContainer"/> clips with a scissor no ancestor scale reaches.
        /// Each box carries the fitted scale itself. The scene is rebuilt whenever the viewport
        /// changes shape (<see cref="LayOutPackSelect"/>), so it is placed once, here.
        /// </remarks>
        private void CreateExperimentsPackSelect()
        {
            MenuView menuView = new();
            BaseElement baseElement = CreateBackgroundWithLogo(false, VIEW_PACK_SELECT);
            Rectangle visible = VisibleBounds;
            Rectangle fitted = FittedBox;
            float scale = FittedScale;
            float boxCenterY = fitted.y - visible.y + (ExpBoxCenterY * scale);

            int displayCount = Preferences.GetPacksCount() + (PackConfig.GetComingSoonPackIndex() >= 0 ? 1 : 0);
            float elementSize = ExpPackElementSize * scale;
            float step = (ExpPackElementSize + ExpPackGap) * scale;
            Vector artCenter = ExperimentsBoxArtCenter();
            float firstLeft = (visible.w / 2f) - (artCenter.X * scale);
            float elementCenterY = boxCenterY - ((artCenter.Y - (ExpPackElementSize / 2f)) * scale);
            BaseElement packRow = new()
            {
                width = (int)MathF.Ceiling(((displayCount - 1) * step) + visible.w),
                height = (int)visible.h,
            };
            packContainer = new ScrollableContainer().InitWithWidthHeightContainer(visible.w, visible.h, packRow);
            packContainer.minAutoScrollToSpointLength = RTD(5);
            packContainer.shouldBounceHorizontally = true;
            packContainer.resetScrollOnShow = false;
            packContainer.dontHandleTouchDownsHandledByChilds = true;
            packContainer.dontHandleTouchMovesHandledByChilds = true;
            packContainer.dontHandleTouchUpsHandledByChilds = true;
            packContainer.TurnScrollPointsOnWithCapacity(displayCount);
            packContainer.delegateScrollableContainerProtocol = this;
            for (int i = 0; i < displayCount; i++)
            {
                TouchBaseElement element = CreateExperimentsPackElement(i, elementSize, scale);
                boxes[i] = element;
                element.anchor = element.parentAnchor = 17;
                element.x = firstLeft + (i * step);
                element.y = elementCenterY - (visible.h / 2f);
                _ = packRow.AddChild(element);
                _ = packContainer.AddScrollPointAtXY(i * step, 0f);
            }
            AddExperimentsButterfly(packRow, visible, scale, elementSize);
            _ = baseElement.AddChild(packContainer);

            HBox starTotal = CreateExperimentsTextWithStar(
                Application.GetString("TOTAL_STARS").ToString().Replace("%d", "", StringComparison.Ordinal)
                + Preferences.GetTotalStars().ToString(CultureInfo.InvariantCulture));
            starTotal.SetName("text");
            starTotal.anchor = starTotal.parentAnchor = 12;
            PlaceStarTotal(starTotal);
            _ = baseElement.AddChild(starTotal);

            // iOS has no arrows; the classic ones stay for keyboard and mouse users, level with the box.
            float arrowY = boxCenterY - (visible.h / 2f);
            prevb = CreateButton2WithImageQuad1Quad2IDDelegate(Resources.Img.MenuPackUI, 6, 7, MenuButtonId.PreviousPack, this);
            prevb.parentAnchor = 17;
            prevb.anchor = 20;
            prevb.y = arrowY;
            PlacePackEdge(prevb, (prevb.width * scale) + PackArrowInset, scale, mirroredX: false, mirroredY: false);
            _ = baseElement.AddChild(prevb);
            nextb = CreateButton2WithImageQuad1Quad2IDDelegate(Resources.Img.MenuPackUI, 6, 7, MenuButtonId.NextPack, this);
            nextb.anchor = nextb.parentAnchor = 17;
            nextb.y = arrowY;
            PlacePackEdge(nextb, visible.w - (nextb.width * scale) - PackArrowInset, scale, mirroredX: true, mirroredY: false);
            _ = baseElement.AddChild(nextb);

            _ = menuView.AddChild(baseElement);
            packSelectBuiltFor = visible;
            Button button = CreateBackButtonWithDelegateID(this, MenuButtonId.BackFromPackSelect);
            button.SetName("backb");
            _ = menuView.AddChild(button);
            AttachSnowfallOverlay(menuView);
            AddViewwithID(menuView, VIEW_PACK_SELECT);
            int lastPack = Math.Min(Preferences.GetLastBox(), displayCount - 1);
            Application.SharedRootController().Box = Preferences.GetLastGamePack();
            packContainer.PlaceToScrollPoint(lastPack);
            ScrollableContainerchangedTargetScrollPoint(packContainer, lastPack);
            UpdateExperimentsPackSelect();
        }

        /// <summary>
        /// Center of the box art in a pack element's frame, which is the atlas's pre-cut frame.
        /// </summary>
        /// <returns>The art's center, in atlas pixels.</returns>
        private static Vector ExperimentsBoxArtCenter()
        {
            Vector offset = Image.GetQuadOffset(Resources.Img.MenuExpPackSelection, ExpQuadBox);
            Vector size = Image.GetQuadSize(Resources.Img.MenuExpPackSelection, ExpQuadBox);
            return new Vector(offset.X + (size.X / 2f), offset.Y + (size.Y / 2f));
        }

        /// <summary>
        /// Creates one Experiments pack box. The engine's <c>-[MenuController createPackElement:forContainer:]</c>.
        /// </summary>
        /// <param name="n">Displayed pack index; one past the last pack is the coming-soon box.</param>
        /// <param name="elementSize">Side of the element in logical units.</param>
        /// <param name="scale">Scale from design units to logical units.</param>
        /// <returns>The touchable pack element.</returns>
        private TouchBaseElement CreateExperimentsPackElement(int n, float elementSize, float scale)
        {
            int packsCount = Preferences.GetPacksCount();
            bool isComingSoon = n >= packsCount;
            int totalStars = Preferences.GetTotalStarsInBox(PackConfig.GetSaveSlot(n));
            if (n > 0 && !isComingSoon && Preferences.GetUnlockedForPackLevel(n, 0) == UNLOCKEDSTATE.LOCKED && totalStars >= PackConfig.GetUnlockStars(n))
            {
                Preferences.SetUnlockedForPackLevel(UNLOCKEDSTATE.JUSTUNLOCKED, n, 0);
            }
            UNLOCKEDSTATE unlocked = isComingSoon ? UNLOCKEDSTATE.UNLOCKED : Preferences.GetUnlockedForPackLevel(n, 0);

            TouchBaseElement element = new()
            {
                delegateValue = this,
                bid = isComingSoon ? new MenuButtonId(-1) : MenuButtonId.ForPack(n),
                width = (int)MathF.Round(elementSize),
                height = (int)MathF.Round(elementSize),
                bbc = MakeRectangle(ExpPackHitTrim * scale, 0f, -ExpPackHitTrim * scale, 0f),
            };

            // Holds the fitted scale apart from the press bounce, whose keyframes set an absolute scale.
            BaseElement scaler = new()
            {
                width = (int)ExpPackElementSize,
                height = (int)ExpPackElementSize,
                scaleX = scale,
                scaleY = scale,
            };
            scaler.anchor = scaler.parentAnchor = 18;
            _ = element.AddChild(scaler);

            BaseElement boxContainer = new()
            {
                width = (int)ExpPackElementSize,
                height = (int)ExpPackElementSize,
            };
            boxContainer.SetName("boxContainer");
            boxContainer.anchor = boxContainer.parentAnchor = 9;
            _ = scaler.AddChild(boxContainer);

            _ = boxContainer.AddChild(CreateExperimentsBoxLayer(ExpQuadBox, "box"));
            _ = boxContainer.AddChild(CreateExperimentsBoxLayer(ExpQuadBoxSelected, "boxSelected"));
            if (!isComingSoon)
            {
                Image monster = CreateExperimentsBoxLayer(ExpQuadFirstMonster + (n % ExpMonsterCount), "boxPic");
                monster.color = ExpMonsterIdle;
                monster.hasColor = true;
                _ = boxContainer.AddChild(monster);
            }

            Text title = new Text().InitWithFont(Application.GetFont(Resources.Fnt.BigFont));
            title.SetAlignment(2);
            title.SetString(isComingSoon
                ? Application.GetString("BOX_SOON_LABEL")
                : $"{n + 1}. {Application.GetString(PackConfig.GetPackName(n))}");
            float titleScale = MathF.Min(0.75f, MathF.Min(ExpTitleFit.X / title.width, ExpTitleFit.Y / title.height));
            title.scaleX = title.scaleY = titleScale;
            title.rotationCenterY = -title.height / 2f;
            title.anchor = 10;
            title.parentAnchor = 9;
            title.x = ExpTitlePosition.X;
            title.y = isComingSoon ? ExpComingSoonTitleY : ExpTitlePosition.Y;
            _ = boxContainer.AddChild(title);

            if (unlocked == UNLOCKEDSTATE.LOCKED)
            {
                int requiredStars = PackConfig.GetUnlockStars(n);
                Image lockImage = CreateExperimentsLock(n);
                _ = boxContainer.AddChild(lockImage);

                // The padlock shows its price; the bamboo cage leaves it to the hint below.
                if (!IsExperimentsCagedPack(n))
                {
                    HBox stars = CreateExperimentsTextWithStar(requiredStars.ToString(CultureInfo.InvariantCulture));
                    stars.anchor = stars.parentAnchor = 18;
                    stars.x = ExpLockStarsOffset.X;
                    stars.y = ExpLockStarsOffset.Y;
                    _ = lockImage.AddChild(stars);
                }

                Text hint = new Text().InitWithFont(Application.GetFont(Resources.Fnt.SmallFont));
                hint.SetName("hintText");
                hint.SetAlignment(2);
                hint.SetStringandWidth(
                    Application.GetString("UNLOCK_HINT").ToString().Replace("%d", requiredStars.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal),
                    ExpHintWidth);
                hint.anchor = hint.parentAnchor = 18;
                hint.x = ExpHintOffset.X;
                hint.y = ExpHintOffset.Y;
                hint.color = RGBAColor.transparentRGBA;
                _ = scaler.AddChild(hint);
            }
            else if (unlocked == UNLOCKEDSTATE.JUSTUNLOCKED)
            {
                Image lockImage = CreateExperimentsLock(n);
                lockImage.SetName("lockHideMe");
                Timeline timeline = new Timeline().InitWithMaxKeyFramesOnTrack(3);
                timeline.AddKeyFrame(KeyFrame.MakeColor(RGBAColor.solidOpaqueRGBA, KeyFrame.TransitionType.FRAME_TRANSITION_LINEAR, 0));
                timeline.AddKeyFrame(KeyFrame.MakeColor(RGBAColor.transparentRGBA, KeyFrame.TransitionType.FRAME_TRANSITION_LINEAR, 1.5f));
                timeline.AddKeyFrame(KeyFrame.MakeScale(1, 1, KeyFrame.TransitionType.FRAME_TRANSITION_LINEAR, 0));
                timeline.AddKeyFrame(KeyFrame.MakeScale(2, 2, KeyFrame.TransitionType.FRAME_TRANSITION_LINEAR, 1.5f));
                _ = lockImage.AddTimeline(timeline);
                _ = boxContainer.AddChild(lockImage);
            }

            // Every star in the pack collected: the badge sits on the box's corner, over the lock.
            if (!isComingSoon && Preferences.IsPackPerfect(n))
            {
                _ = boxContainer.AddChild(CreateExperimentsBoxLayer(ExpQuadPerfect, "perfect"));
            }

            Timeline pressBounce = new Timeline().InitWithMaxKeyFramesOnTrack(4);
            pressBounce.AddKeyFrame(KeyFrame.MakeScale(1, 1, KeyFrame.TransitionType.FRAME_TRANSITION_LINEAR, 0));
            pressBounce.AddKeyFrame(KeyFrame.MakeScale(0.95f, 1.05f, KeyFrame.TransitionType.FRAME_TRANSITION_EASE_OUT, 0.15f));
            pressBounce.AddKeyFrame(KeyFrame.MakeScale(1.05f, 0.95f, KeyFrame.TransitionType.FRAME_TRANSITION_EASE_OUT, 0.2f));
            pressBounce.AddKeyFrame(KeyFrame.MakeScale(1, 1, KeyFrame.TransitionType.FRAME_TRANSITION_EASE_OUT, 0.25f));
            _ = boxContainer.AddTimeline(pressBounce);
            return element;
        }

        /// <summary>
        /// Creates a layer of a pack box, drawn in the atlas's pre-cut frame so every layer registers.
        /// </summary>
        /// <param name="quad">Pack atlas quad.</param>
        /// <param name="name">Name the per-frame blend finds the layer by.</param>
        /// <returns>The layer.</returns>
        private static Image CreateExperimentsBoxLayer(int quad, string name)
        {
            Image layer = Image.FromResource(Resources.Img.MenuExpPackSelection, quad);
            layer.DoRestoreCutTransparency();
            layer.anchor = layer.parentAnchor = 9;
            layer.SetName(name);
            return layer;
        }

        /// <summary>
        /// Gets whether a pack is caged in bamboo rather than padlocked. iOS cages its last pack;
        /// here that is the last of the packs.
        /// </summary>
        /// <param name="n">Displayed pack index.</param>
        /// <returns><see langword="true"/> for the last pack.</returns>
        private static bool IsExperimentsCagedPack(int n)
        {
            return n == Preferences.GetPacksCount() - 1;
        }

        /// <summary>
        /// Creates the lock drawn over a closed box: bamboo on the last pack, the padlock
        /// everywhere else.
        /// </summary>
        /// <param name="n">Displayed pack index.</param>
        /// <returns>The lock, in the box's frame.</returns>
        private static Image CreateExperimentsLock(int n)
        {
            Image lockImage = IsExperimentsCagedPack(n)
                ? Image.FromResource(Resources.Img.MenuExpPackSelection, ExpQuadBambooLock)
                : Image.FromResource(Resources.Img.MenuExpLock, 0);
            lockImage.DoRestoreCutTransparency();
            lockImage.anchor = lockImage.parentAnchor = 9;
            return lockImage;
        }

        /// <summary>
        /// Blends every box by how far the strip is from resting on it: the selected plate and
        /// the monster's tint fade in, and the idle plate fades out. The WP7
        /// <c>MenuController.update</c> pack branch.
        /// </summary>
        /// <remarks>
        /// WP7 tints each monster by the nearest box seen so far in the loop rather than by its
        /// own distance, so a box's color depends on its left neighbors; each box uses its own
        /// distance here.
        /// </remarks>
        private void UpdateExperimentsPackSelect()
        {
            if (packContainer == null || packContainer.TotalScrollPoints == 0)
            {
                return;
            }

            float scroll = packContainer.GetScroll().X;
            expButterfly?.ViewLeft = scroll;
            float step = packContainer.TotalScrollPoints > 1
                ? packContainer.GetScrollPoint(1).X - packContainer.GetScrollPoint(0).X
                : 1f;
            for (int i = 0; i < packContainer.TotalScrollPoints; i++)
            {
                BaseElement element = boxes[i];
                if (element == null)
                {
                    continue;
                }

                float distance = MathF.Min(1f, MathF.Abs((scroll + packContainer.GetScrollPoint(i).X) / step));
                float closeness = 1f - distance;

                SetFade(element.GetChildWithName("box"), distance);
                SetFade(element.GetChildWithName("boxSelected"), closeness);
                SetFade(element.GetChildWithName("hintText"), closeness);
                element.GetChildWithName("boxPic")?.color = RGBAColor.MakeRGBA(
                    ExpMonsterIdle.RedColor + ((ExpMonsterSelected.RedColor - ExpMonsterIdle.RedColor) * closeness),
                    ExpMonsterIdle.GreenColor + ((ExpMonsterSelected.GreenColor - ExpMonsterIdle.GreenColor) * closeness),
                    ExpMonsterIdle.BlueColor + ((ExpMonsterSelected.BlueColor - ExpMonsterIdle.BlueColor) * closeness),
                    1f);
            }
        }

        /// <summary>
        /// Adds the butterfly while the caged box is still locked. iOS
        /// <c>-[MenuController createPackSelect]</c>: it perches on the cage at once when the
        /// picker opens on that box or has already been sent there this session, and otherwise
        /// flies in.
        /// </summary>
        /// <param name="packRow">The strip's content, which the butterfly scrolls with.</param>
        /// <param name="visible">The logical region the viewport exposes.</param>
        /// <param name="scale">Scale the strip is drawn at.</param>
        /// <param name="elementSize">Side of a pack element in logical units.</param>
        private void AddExperimentsButterfly(BaseElement packRow, Rectangle visible, float scale, float elementSize)
        {
            expButterfly = null;
            int caged = Preferences.GetPacksCount() - 1;
            if (caged < 0 || boxes[caged] == null || Preferences.GetUnlockedForPackLevel(caged, 0) != UNLOCKEDSTATE.LOCKED)
            {
                return;
            }

            // Perched where the atlas draws the body on the box: the top of the cage's left pole.
            BaseElement box = boxes[caged];
            float boxTop = (visible.h / 2f) + box.y - (elementSize / 2f);
            Vector bodyOffset = Image.GetQuadOffset(Resources.Img.MenuExpPackSelection, 19);
            Vector bodySize = Image.GetQuadSize(Resources.Img.MenuExpPackSelection, 19);
            Vector perch = new(
                box.x + ((bodyOffset.X + (bodySize.X / 2f) + ExpButterflyPerchNudge) * scale),
                boxTop + ((bodyOffset.Y + (bodySize.Y / 2f)) * scale));

            ExperimentsButterfly butterfly = new(scale)
            {
                ViewSize = new Vector(visible.w, visible.h),
            };
            butterfly.SetLandingPoint(perch);
            int lastPack = Math.Min(Preferences.GetLastBox(), Preferences.GetPacksCount());
            butterfly.ViewLeft = ExperimentsViewLeftAt(lastPack);
            butterfly.SetViewRect(butterfly.ViewLeft);
            if (lastPack == caged || !expButterflyFlying)
            {
                butterfly.SetFlyingMode(ExperimentsButterfly.FlightMode.Landed);
                butterfly.x = perch.X;
                butterfly.y = perch.Y;
            }
            else
            {
                // Starts a little off the left of the screen and flies in.
                butterfly.x = butterfly.ViewLeft - (butterfly.width * scale);
                butterfly.y = visible.h / 3f;
            }
            butterfly.Tapped = () =>
            {
                expButterflyFlying = false;
                butterfly.SetFlyingMode(ExperimentsButterfly.FlightMode.Landing);
                packContainer.MoveToScrollPointmoveMultiplier(caged, ExpButterflyScrollMultiplier);
            };
            butterfly.Gone = () => expButterfly = null;
            _ = packRow.AddChild(butterfly);
            expButterfly = butterfly;
        }

        /// <summary>
        /// Steers the butterfly as the picker heads for another box: toward the cage to land if
        /// that is where it is going, otherwise over to the new box. iOS
        /// <c>-[MenuController scrollableContainer:changedTargetScrollPoint:]</c>.
        /// </summary>
        /// <param name="i">Box the picker is heading for.</param>
        private void SteerExperimentsButterfly(int i)
        {
            if (expButterfly == null || !expButterflyFlying || i >= packContainer.TotalScrollPoints)
            {
                return;
            }

            expButterfly.SetViewRect(ExperimentsViewLeftAt(i));
            expButterfly.SetFlyingMode(IsExperimentsCagedPack(i)
                ? ExperimentsButterfly.FlightMode.Landing
                : ExperimentsButterfly.FlightMode.Transition);
        }

        /// <summary>
        /// Gets where the screen's left edge falls in the strip's content once it rests on a box.
        /// The container keeps its scroll points as negative offsets.
        /// </summary>
        /// <param name="i">Box index.</param>
        /// <returns>The left edge in content coordinates.</returns>
        private float ExperimentsViewLeftAt(int i)
        {
            return -packContainer.GetScrollPoint(i).X;
        }

        /// <summary>
        /// Sends the butterfly away when the caged box opens.
        /// </summary>
        /// <param name="i">Box whose lock just came off.</param>
        private void ReleaseExperimentsButterfly(int i)
        {
            if (expButterfly != null && IsExperimentsCagedPack(i))
            {
                expButterfly.FlyAway();
            }
        }

        /// <summary>
        /// Sets an element's opacity, in the premultiplied form the menu draws with.
        /// </summary>
        /// <param name="element">Element to fade, or <see langword="null"/>.</param>
        /// <param name="alpha">Opacity, 0 to 1.</param>
        private static void SetFade(BaseElement element, float alpha)
        {
            element?.color = RGBAColor.MakeRGBA(alpha, alpha, alpha, alpha);
        }
    }
}
