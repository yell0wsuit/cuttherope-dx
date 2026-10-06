using System;
using System.Collections.Generic;

using CutTheRopeDX.Framework;
using CutTheRopeDX.Framework.Core;
using CutTheRopeDX.Framework.Platform;
using CutTheRopeDX.Framework.Visual;

namespace CutTheRopeDX.GameMain
{
    /// <content>
    /// The Cut the Rope: Time Travel menu scenes, selected with <c>--menu timetravel</c>.
    /// </content>
    /// <remarks>
    /// The main menu keeps the iOS HD 1.5.0 composition (<c>MenuController::initWithParent</c>)
    /// inside a <see cref="TimeTravelSceneGroup"/>, positions in iOS canvas pixels times
    /// <see cref="TimeTravelArt.CanvasToAsset"/>. Its sub-views keep the classic structure and only
    /// swap their art.
    /// </remarks>
    internal sealed partial class MenuController
    {
        /// <summary>Where the glow turns about: the iOS position-only quad 1 of its sheet, in canvas pixels.</summary>
        private static readonly Vector TimeTravelGlowPivot = new(629f, 1201f);

        /// <summary>Scale of the glow (iOS <c>0x402CCCCD</c>).</summary>
        private const float TimeTravelGlowScale = 2.7f;

        /// <summary>Seconds per glow turn.</summary>
        private const float TimeTravelGlowPeriod = 20f;

        /// <summary>Height of the round-button row the capsules sit on: the iOS slots' average, in logical units.</summary>
        private const float TimeTravelCapsuleRowY = 803f;

        /// <summary>Gap between the two capsules, in logical units.</summary>
        internal const float TimeTravelCapsuleGap = 20f;

        /// <summary>
        /// Room the main menu's capsule row keeps from each side of the screen when its capsules
        /// would otherwise run past it, in iOS logical units.
        /// </summary>
        internal const float TimeTravelCapsuleRowMargin = 24f;

        /// <summary>
        /// Where the iOS title's art begins, in logical units: its logo part rests at 2.15 and is
        /// clear for its first 18. The DX logo's art is opaque to its top edge, so it is placed by
        /// this line rather than by the part's position.
        /// </summary>
        internal const float TimeTravelLogoTop = 20.15f;

        /// <summary>
        /// Scale the DX logo is drawn at: the iOS title's drawn width (483 logical units) over the
        /// DX logo's (508), so the logo fills the box the iOS title fills.
        /// </summary>
        internal const float TimeTravelLogoScale = 0.95f;

        /// <summary>
        /// How far the main menu's stack (logo, Play and its glow, the capsule row) sits below the
        /// iOS scene's, in logical units: enough that at the design shape, where the DX menu's
        /// design box fills the screen, the logo's top lands on the DX logo's.
        /// </summary>
        internal const float TimeTravelStackDrop =
            (LogoTop / (ViewportLayout.DesignHeight / TimeTravelScreen.SceneHeight)) - TimeTravelLogoTop;

        /// <summary>
        /// Gap between the settings' music and sound capsules, in iOS logical units: the room the
        /// iOS layout markers leave between them.
        /// </summary>
        internal const float TimeTravelAudioPillGap = 14f;

        /// <summary>How much higher the iOS settings set the sound capsule than the music one, in logical units.</summary>
        internal const float TimeTravelAudioPillStagger = 4f;

        /// <summary>The main menu's Time Travel scene.</summary>
        private TimeTravelSceneGroup timeTravelMain;

        /// <summary>Seconds per turn of the settings views' corner fan.</summary>
        private const float TimeTravelFanPeriod = 1f;

        /// <summary>Smallest scale of the fan hub's pulse (iOS <c>0x3F733333</c>).</summary>
        private const float TimeTravelFanHubPulse = 0.95f;

        /// <summary>The corner fan of each settings view, by view, so a layout pass can re-pin it.</summary>
        private readonly Dictionary<int, TimeTravelSceneGroup> timeTravelFans = [];

        /// <summary>The main menu's capsules, left to right, restretched on every layout pass.</summary>
        private readonly List<Button> timeTravelCapsules = [];

        /// <summary>Builds the Time Travel main menu.</summary>
        private void CreateTimeTravelMainMenu()
        {
            MenuView menuView = new();
            BaseElement background = CreateBackgroundWithLogowithShadow(false, false, VIEW_MAIN_MENU);
            TimeTravelSceneGroup scene = new();
            timeTravelMain = scene;

            Image band = Image.FromResource(Resources.Img.MenuMainTimeTravel, TimeTravelArt.MainTopBand);
            band.SetName("ttBand");
            PlaceInScene(band, Resources.Img.MenuMainTimeTravel, TimeTravelArt.MainTopBand, TimeTravelArt.MenuMainOriginY);
            _ = scene.AddChild(band);
            scene.Attach(band, TimeTravelAttach.Top | TimeTravelAttach.ScaleToFullX);

            // Everything under the band moves down together, so their iOS spacing holds.
            BaseElement stack = new()
            {
                width = TimeTravelSceneGroup.Width,
                height = TimeTravelSceneGroup.Height,
                y = TimeTravelStackDrop * FlashXmlScale.AtlasToFlashPointScale,
            };
            stack.anchor = stack.parentAnchor = 9;
            stack.SetName("ttStack");
            _ = scene.AddChild(stack);

            _ = stack.AddChild(CreateTimeTravelGlow());

            Button play = new Button().InitWithUpElementDownElementandID(
                Image.FromResource(Resources.Img.MenuMainTimeTravel, TimeTravelArt.MainPlayUp),
                Image.FromResource(Resources.Img.MenuMainTimeTravel, TimeTravelArt.MainPlayDown),
                MenuButtonId.Play);
            play.delegateButtonDelegate = this;
            play.SetName("ttPlay");
            PlaceInScene(play, Resources.Img.MenuMainTimeTravel, TimeTravelArt.MainPlayUp, TimeTravelArt.MenuMainOriginY);
            _ = stack.AddChild(play);

            AddTimeTravelCapsules(stack);

            // The title is the DX logo, top-centered where the iOS title rests, carrying the candy
            // that opens candy selection as it does in the classic menu.
            TimeTravelLogo logo = TimeTravelLogo.Create(new Random(), this);
            logo.SetName("ttLogo");
            logo.scaleX = logo.scaleY = TimeTravelLogoScale;

            // Scaled about its center like every element, so its top is solved for the scale.
            logo.x = (TimeTravelSceneGroup.Width - logo.width) / 2f;
            logo.y = (TimeTravelLogoTop * FlashXmlScale.AtlasToFlashPointScale) - ((logo.height >> 1) * (1f - TimeTravelLogoScale));
            _ = stack.AddChild(logo);

            _ = background.AddChild(scene);
            _ = menuView.AddChild(background);
            scene.Layout(new TimeTravelScreen(VisibleBounds));
            AttachSnowfallOverlay(menuView);
            AddViewwithID(menuView, VIEW_MAIN_MENU);
        }

        /// <summary>Places an atlas element at its own offset, measured from the iOS canvas origin.</summary>
        /// <param name="element">Element to place.</param>
        /// <param name="resource">Sheet the quad is in.</param>
        /// <param name="quad">Quad whose offset to use.</param>
        /// <param name="originY">Where the iOS canvas origin sits in the sheet, in asset pixels.</param>
        private static void PlaceInScene(BaseElement element, string resource, int quad, float originY)
        {
            Vector offset = Image.GetQuadOffset(resource, quad);
            element.anchor = element.parentAnchor = 9;
            element.x = offset.X;
            element.y = offset.Y - originY;
        }

        /// <summary>The additive light that turns behind Play.</summary>
        /// <returns>The turning element holding the glow.</returns>
        private static BaseElement CreateTimeTravelGlow()
        {
            Image glow = Image.FromResource(Resources.Img.MenuMainAniTimeTravel, TimeTravelArt.Glow);
            glow.SetName("ttGlow");
            glow.blendingMode = 2;
            glow.anchor = glow.parentAnchor = 18;
            glow.scaleX = glow.scaleY = TimeTravelGlowScale;

            BaseElement turner = new() { width = glow.width, height = glow.height };
            PlaceInScene(turner, Resources.Img.MenuMainAniTimeTravel, TimeTravelArt.Glow, 0f);
            float pivotX = TimeTravelGlowPivot.X * TimeTravelArt.CanvasToAsset;
            float pivotY = TimeTravelGlowPivot.Y * TimeTravelArt.CanvasToAsset;
            turner.rotationCenterX = pivotX - (turner.x + (turner.width / 2f));
            turner.rotationCenterY = pivotY - (turner.y + (turner.height / 2f));
            Timeline spin = new Timeline().InitWithMaxKeyFramesOnTrack(2);
            spin.AddKeyFrame(KeyFrame.MakeRotation(0, KeyFrame.TransitionType.FRAME_TRANSITION_LINEAR, 0));
            spin.AddKeyFrame(KeyFrame.MakeRotation(360, KeyFrame.TransitionType.FRAME_TRANSITION_LINEAR, TimeTravelGlowPeriod));
            spin.SetTimelineLoopType(Timeline.LoopType.TIMELINE_REPLAY);
            _ = turner.AddTimeline(spin);
            turner.PlayTimeline(0);
            _ = turner.AddChild(glow);
            return turner;
        }

        /// <summary>Options and, where the host allows it, Quit or the level editor, as capsules under Play.</summary>
        /// <param name="scene">Scene layer the capsules go in.</param>
        private void AddTimeTravelCapsules(BaseElement scene)
        {
            (string Label, ButtonId Id) options = (Application.GetString("OPTIONS"), MenuButtonId.Options);
            List<(string Label, ButtonId Id)> specs = [options];
            if (PlatformServices.Host?.CanExit == true)
            {
                specs.Add((Application.GetString("QUIT_BUTTON"), MenuButtonId.ShowQuitPopup));
            }
            else if (!string.IsNullOrEmpty(PlatformServices.Host?.LevelEditorUrl))
            {
                specs.Add((Application.GetString("LEVEL_EDITOR_BUTTON"), MenuButtonId.LevelEditor));
            }

            timeTravelCapsules.Clear();
            float width = TimeTravelCapsuleWidth(specs.Count, VisibleBounds, FittedScale);
            for (int i = 0; i < specs.Count; i++)
            {
                Button capsule = CreateTimeTravelCapsule(specs[i].Label, specs[i].Id, width);
                capsule.SetName(i == 0 ? "ttOptions" : "ttQuit");
                timeTravelCapsules.Add(capsule);
                _ = scene.AddChild(capsule);
            }
            LayOutTimeTravelCapsules(VisibleBounds);
        }

        /// <summary>
        /// Stretches the main menu's capsules to the current length and centers their row under
        /// Play. Rerun on every layout pass, since the length follows the window's shape.
        /// </summary>
        /// <param name="visible">The visible bounds being laid out for.</param>
        private void LayOutTimeTravelCapsules(Rectangle visible)
        {
            int count = timeTravelCapsules.Count;
            if (count == 0)
            {
                return;
            }
            float width = TimeTravelCapsuleWidth(count, visible, FittedScale);
            float a = FlashXmlScale.AtlasToFlashPointScale;
            float pitch = width + (TimeTravelCapsuleGap * a);
            float first = (TimeTravelScreen.SceneWidth / 2f * a) - (pitch * (count - 1) / 2f);
            for (int i = 0; i < count; i++)
            {
                Button capsule = timeTravelCapsules[i];
                TimeTravelPlates.ResizePillButton(
                    capsule, Resources.Img.MenuButtonSmallTimeTravel, TimeTravelArt.CapsuleUp, TimeTravelArt.CapsuleDown, plateScale: 1f, width);
                CenterAt(capsule, first + (i * pitch), TimeTravelCapsuleRowY * a);
            }
        }

        /// <summary>
        /// The main menu capsules' width in the scene: drawn as long as a language button, narrowed
        /// only where a row of them would run past the screen's side margins.
        /// </summary>
        /// <param name="count">Capsules in the row.</param>
        /// <param name="visible">The visible bounds being laid out for.</param>
        /// <param name="contentScale">Scale the classic menus' content is drawn at.</param>
        /// <returns>The width, in scene asset pixels.</returns>
        internal static float TimeTravelCapsuleWidth(int count, Rectangle visible, float contentScale)
        {
            float drawnPerScene = new TimeTravelScreen(visible).AssetScale;
            float language = Image.GetQuadSize(Resources.Img.MenuButtons, LanguageButtonQuad).X * contentScale / drawnPerScene;
            float a = FlashXmlScale.AtlasToFlashPointScale;
            float room = (visible.w / drawnPerScene) - (2f * TimeTravelCapsuleRowMargin * a) - ((count - 1) * TimeTravelCapsuleGap * a);
            return MathF.Min(language, room / count);
        }

        /// <summary>Places an element by its center.</summary>
        /// <param name="element">Element to place.</param>
        /// <param name="cx">Center X.</param>
        /// <param name="cy">Center Y.</param>
        private static void CenterAt(BaseElement element, float cx, float cy)
        {
            element.anchor = element.parentAnchor = 9;
            element.x = cx - (element.width / 2f);
            element.y = cy - (element.height / 2f);
        }

        /// <summary>A Time Travel capsule button with a DX label.</summary>
        /// <param name="text">Label.</param>
        /// <param name="id">Button identifier.</param>
        /// <param name="width">Width the capsule is stretched to.</param>
        /// <returns>The button.</returns>
        private Button CreateTimeTravelCapsule(string text, ButtonId id, float width)
        {
            return TimeTravelPlates.CreatePillButton(
                Resources.Img.MenuButtonSmallTimeTravel, TimeTravelArt.CapsuleUp, TimeTravelArt.CapsuleDown, text, id, this, plateScale: 1f, width);
        }

        /// <summary>
        /// One state of a Time Travel settings audio toggle: the icon on the capsule plate, drawn
        /// at the long buttons' scale so the settings keep the iOS proportions. The icon and the
        /// cross keep the places the iOS button canvas gives them, and the pressed capsule sits
        /// where that canvas puts it, a little below the one at rest.
        /// </summary>
        /// <param name="icon">Icon quad.</param>
        /// <param name="crossed">Whether the sound is switched off.</param>
        /// <param name="pressed">Whether to draw the pressed plate.</param>
        /// <returns>The plate in a container sized to the capsule at rest.</returns>
        private static BaseElement CreateTimeTravelAudioPill(int icon, bool crossed, bool pressed)
        {
            string sheet = Resources.Img.MenuButtonSmallTimeTravel;
            int plateQuad = pressed ? TimeTravelArt.CapsuleDown : TimeTravelArt.CapsuleUp;
            float scale = TimeTravelAudioPillScale;
            Image plate = Image.FromResource(sheet, plateQuad);
            plate.anchor = plate.parentAnchor = 9;
            plate.scaleX = plate.scaleY = scale;

            // Scaled about its center like every element, so the drift that puts on its top left
            // comes back out of the canvas offset it is placed by.
            Vector shift = Vect(
                Image.GetQuadOffset(sheet, plateQuad).X - Image.GetQuadOffset(sheet, TimeTravelArt.CapsuleUp).X,
                Image.GetQuadOffset(sheet, plateQuad).Y - Image.GetQuadOffset(sheet, TimeTravelArt.CapsuleUp).Y);
            plate.x = (shift.X * scale) - ((plate.width >> 1) * (1f - scale));
            plate.y = (shift.Y * scale) - ((plate.height >> 1) * (1f - scale));
            Image glyph = AddTimeTravelAudioGlyphs(plate, plateQuad, icon, crossed);
            Image.SetElementPositionWithRelativeQuadOffset(glyph, sheet, plateQuad, icon);

            Vector rest = Image.GetQuadSize(sheet, TimeTravelArt.CapsuleUp);
            BaseElement container = new()
            {
                width = (int)MathF.Round(rest.X * scale),
                height = (int)MathF.Round(rest.Y * scale),
            };
            _ = container.AddChild(plate);
            return container;
        }

        /// <summary>The settings audio capsules' scale: the long buttons', so they keep the iOS proportions.</summary>
        private static float TimeTravelAudioPillScale => TimeTravelPlates.HeightMatching(
            Resources.Img.MenuButtonBigTimeTravel, TimeTravelArt.LongPlateUp, Resources.Img.MenuButtons, 0);

        /// <summary>
        /// The settings' audio row as the iOS layout markers set it: music, then sound, the gap
        /// between them, and sound a little higher.
        /// </summary>
        /// <param name="music">The music toggle.</param>
        /// <param name="sound">The sound toggle.</param>
        /// <returns>The row.</returns>
        private static HBox CreateTimeTravelAudioRow(ToggleButton music, ToggleButton sound)
        {
            // iOS logical units to the capsules' drawn pixels.
            float unit = FlashXmlScale.AtlasToFlashPointScale * TimeTravelAudioPillScale;
            float stagger = TimeTravelAudioPillStagger * unit;
            HBox row = new HBox().InitWithOffsetAlignHeight(TimeTravelAudioPillGap * unit, 16, music.height + stagger);
            _ = row.AddChild(music);
            _ = row.AddChild(sound);

            // Centered in the row, so the stagger splits about its middle.
            music.y = stagger / 2f;
            sound.y = -stagger / 2f;
            return row;
        }

        /// <summary>
        /// One state of a Time Travel pause-menu audio toggle, on the round plate. The iOS pause
        /// menu has a round sound button only, and does not leave its icon where the canvas draws
        /// it: it centers the icon across on <see cref="TimeTravelArt.RoundAudioIconMarker"/> and
        /// sets its top 0.6 of its height above that point, which lands the speaker's weight on the
        /// middle of the disc. That rule was never drawn for the music note, so the note is given
        /// the same result: its weight center on <see cref="TimeTravelArt.RoundAudioFaceCenter"/>.
        /// Neither icon follows the pressed plate's face down, as the speaker does not in iOS.
        /// </summary>
        /// <param name="icon">Icon quad.</param>
        /// <param name="crossed">Whether the sound is switched off.</param>
        /// <param name="pressed">Whether to draw the pressed plate.</param>
        /// <returns>The plate with its icon.</returns>
        private static Image CreateTimeTravelRoundAudioElement(int icon, bool crossed, bool pressed)
        {
            string sheet = Resources.Img.MenuButtonSmallTimeTravel;
            int plateQuad = pressed ? TimeTravelArt.AudioPlateDown : TimeTravelArt.AudioPlateUp;
            Image plate = Image.FromResource(sheet, plateQuad);
            Image glyph = AddTimeTravelAudioGlyphs(plate, plateQuad, icon, crossed);
            Vector plateOffset = Image.GetQuadOffset(sheet, plateQuad);
            if (icon != TimeTravelArt.SoundIcon)
            {
                Vector face = TimeTravelArt.RoundAudioFaceCenter;
                Vector weight = TimeTravelArt.MusicIconWeightCenter;
                glyph.x = face.X - plateOffset.X - weight.X;
                glyph.y = face.Y - plateOffset.Y - weight.Y;
                return plate;
            }
            Vector marker = TimeTravelArt.RoundAudioIconMarker;
            glyph.x = marker.X - plateOffset.X - (glyph.width * 0.5f);
            glyph.y = marker.Y - plateOffset.Y - (glyph.height * 0.6f);
            return plate;
        }

        /// <summary>
        /// Puts an audio icon on a plate, dimmed and crossed where the cross places it on the iOS
        /// canvas when the sound is switched off.
        /// </summary>
        /// <param name="plate">The plate.</param>
        /// <param name="plateQuad">The plate's quad.</param>
        /// <param name="icon">Icon quad.</param>
        /// <param name="crossed">Whether the sound is switched off.</param>
        /// <returns>The icon, for its caller to place.</returns>
        private static Image AddTimeTravelAudioGlyphs(Image plate, int plateQuad, int icon, bool crossed)
        {
            string sheet = Resources.Img.MenuButtonSmallTimeTravel;
            Image glyph = Image.FromResource(sheet, icon);
            glyph.anchor = glyph.parentAnchor = 9;
            _ = plate.AddChild(glyph);
            if (crossed)
            {
                glyph.color = RGBAColor.MakeRGBA(0.5f, 0.5f, 0.5f, 0.5f);
                Image cross = Image.FromResource(sheet, TimeTravelArt.AudioCross);
                cross.anchor = cross.parentAnchor = 9;
                Image.SetElementPositionWithRelativeQuadOffset(cross, sheet, plateQuad, TimeTravelArt.AudioCross);
                _ = plate.AddChild(cross);
            }
            return glyph;
        }

        /// <summary>The round Time Travel back button: the plate with its arrow where the iOS canvas puts it.</summary>
        /// <param name="d">Delegate that receives the press.</param>
        /// <param name="bid">Button identifier.</param>
        /// <returns>The button, anchored to the bottom left like the classic one.</returns>
        private static Button CreateTimeTravelBackButton(IButtonDelegation d, ButtonId bid)
        {
            string sheet = Resources.Img.MenuButtonsTimeTravel;
            Image up = Image.FromResource(sheet, TimeTravelArt.BackPlateUp);
            Image down = Image.FromResource(sheet, TimeTravelArt.BackPlateDown);
            foreach ((Image plate, int quad) in new[] { (up, TimeTravelArt.BackPlateUp), (down, TimeTravelArt.BackPlateDown) })
            {
                Image arrow = Image.FromResource(sheet, TimeTravelArt.BackArrow);
                arrow.anchor = arrow.parentAnchor = 9;
                Image.SetElementPositionWithRelativeQuadOffset(arrow, sheet, quad, TimeTravelArt.BackArrow);
                _ = plate.AddChild(arrow);
            }
            Button button = new Button().InitWithUpElementDownElementandID(up, down, bid);
            button.delegateButtonDelegate = d;
            button.anchor = button.parentAnchor = 33;
            return button;
        }

        /// <summary>Scale of a held reset plate (iOS <c>0x3F666666</c>).</summary>
        private const float TimeTravelHeldPlateScale = 0.9f;

        /// <summary>The reset confirmation: confirms only after a three-second hold, as on iOS.</summary>
        /// <param name="text">Label.</param>
        /// <param name="id">Button identifier.</param>
        /// <returns>The hold button.</returns>
        private TimedButton CreateTimeTravelHoldButton(string text, ButtonId id)
        {
            string sheet = Resources.Img.MenuButtonBigTimeTravel;
            float plateScale = TimeTravelPlates.HeightMatching(sheet, TimeTravelArt.LongPlateUp, Resources.Img.MenuButtons, 0);
            BaseElement up = TimeTravelPlates.LabeledPlate(sheet, TimeTravelArt.LongPlateUp, text, plateScale);
            BaseElement down = TimeTravelPlates.LabeledPlate(sheet, TimeTravelArt.LongPlateDown, text, plateScale);
            BaseElement downPlate = down.GetChild(0);
            downPlate.scaleX = downPlate.scaleY = plateScale * TimeTravelHeldPlateScale;
            TimedButton button = new();
            _ = button.InitWithUpElementDownElementandID(up, down, id);
            button.SetTouchIncreaseLeftRightTopBottom(15, 15, 15, 15);
            button.delegateButtonDelegate = this;
            return button;
        }

        /// <summary>Whether a view is one of the settings views, which carry the corner fan.</summary>
        /// <param name="viewId">View to check.</param>
        /// <returns><see langword="true"/> for options, language, credits and reset.</returns>
        private static bool IsTimeTravelSettingsView(int viewId)
        {
            return viewId is VIEW_OPTIONS or VIEW_LANGUAGE_SELECT or VIEW_ABOUT or VIEW_RESET;
        }

        /// <summary>
        /// Adds the spinning fan the iOS settings views show in their top-left corner
        /// (<c>Factory::createFanForView</c>): a plate and a fan pinned to the corner, the fan
        /// turning under a hub highlight that pulses but does not turn with it.
        /// </summary>
        /// <param name="background">Backdrop element the corner is drawn over.</param>
        /// <param name="viewId">View the corner belongs to.</param>
        private void AttachTimeTravelFanCorner(BaseElement background, int viewId)
        {
            string sheet = Resources.Img.MenuMainTimeTravel;
            TimeTravelSceneGroup corner = new();

            Image plate = Image.FromResource(sheet, TimeTravelArt.FanPlate);
            plate.SetName("ttFanPlate");
            PlaceInScene(plate, sheet, TimeTravelArt.FanPlate, TimeTravelArt.MenuMainOriginY);
            _ = corner.AddChild(plate);
            corner.Attach(plate, TimeTravelAttach.Left | TimeTravelAttach.Top);

            Image fan = Image.FromResource(sheet, TimeTravelArt.Fan);
            fan.SetName("ttFan");
            PlaceInScene(fan, sheet, TimeTravelArt.Fan, TimeTravelArt.MenuMainOriginY);
            fan.passTransformationsToChilds = false;
            Timeline spin = new Timeline().InitWithMaxKeyFramesOnTrack(2);
            spin.AddKeyFrame(KeyFrame.MakeRotation(0, KeyFrame.TransitionType.FRAME_TRANSITION_LINEAR, 0));
            spin.AddKeyFrame(KeyFrame.MakeRotation(360, KeyFrame.TransitionType.FRAME_TRANSITION_LINEAR, TimeTravelFanPeriod));
            spin.SetTimelineLoopType(Timeline.LoopType.TIMELINE_REPLAY);
            _ = fan.AddTimeline(spin);
            fan.PlayTimeline(0);
            _ = corner.AddChild(fan);
            corner.Attach(fan, TimeTravelAttach.Left | TimeTravelAttach.Top);

            Image hub = Image.FromResource(sheet, TimeTravelArt.FanHub);
            hub.SetName("ttFanHub");
            hub.anchor = hub.parentAnchor = 9;
            Image.SetElementPositionWithRelativeQuadOffset(hub, sheet, TimeTravelArt.Fan, TimeTravelArt.FanHub);
            hub.rotationCenterY = hub.height / 2f;
            Timeline pulse = new Timeline().InitWithMaxKeyFramesOnTrack(3);
            pulse.AddKeyFrame(KeyFrame.MakeScale(TimeTravelFanHubPulse, TimeTravelFanHubPulse, KeyFrame.TransitionType.FRAME_TRANSITION_LINEAR, 0));
            pulse.AddKeyFrame(KeyFrame.MakeScale(1f, 1f, KeyFrame.TransitionType.FRAME_TRANSITION_LINEAR, TimeTravelFanPeriod / 2f));
            pulse.AddKeyFrame(KeyFrame.MakeScale(TimeTravelFanHubPulse, TimeTravelFanHubPulse, KeyFrame.TransitionType.FRAME_TRANSITION_LINEAR, TimeTravelFanPeriod / 2f));
            pulse.SetTimelineLoopType(Timeline.LoopType.TIMELINE_REPLAY);
            _ = hub.AddTimeline(pulse);
            hub.PlayTimeline(0);
            _ = fan.AddChild(hub);

            _ = background.AddChild(corner);
            timeTravelFans[viewId] = corner;
            corner.Layout(new TimeTravelScreen(VisibleBounds));
        }

        /// <summary>Re-places every Time Travel scene for the current viewport.</summary>
        /// <param name="visible">The logical region the viewport exposes.</param>
        private void LayOutTimeTravelScenes(Rectangle visible)
        {
            TimeTravelScreen screen = new(visible);
            timeTravelMain?.Layout(screen);
            LayOutTimeTravelCapsules(visible);
            foreach (TimeTravelSceneGroup corner in timeTravelFans.Values)
            {
                corner.Layout(screen);
            }
        }
    }
}
