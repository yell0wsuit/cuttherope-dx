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

        /// <summary>Height of the iOS title, in logical units: where the logo part rests after its intro.</summary>
        private const float TimeTravelLogoTop = 2.15f;

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

            _ = scene.AddChild(CreateTimeTravelGlow());

            Button play = new Button().InitWithUpElementDownElementandID(
                Image.FromResource(Resources.Img.MenuMainTimeTravel, TimeTravelArt.MainPlayUp),
                Image.FromResource(Resources.Img.MenuMainTimeTravel, TimeTravelArt.MainPlayDown),
                MenuButtonId.Play);
            play.delegateButtonDelegate = this;
            play.SetName("ttPlay");
            PlaceInScene(play, Resources.Img.MenuMainTimeTravel, TimeTravelArt.MainPlayUp, TimeTravelArt.MenuMainOriginY);
            _ = scene.AddChild(play);

            AddTimeTravelCapsules(scene);

            // The title is the DX logo, top-centered where the iOS title rests, carrying the candy
            // that opens candy selection as it does in the classic menu.
            TimeTravelLogo logo = TimeTravelLogo.Create(new Random(), this);
            logo.SetName("ttLogo");
            logo.x = (TimeTravelSceneGroup.Width - logo.width) / 2f;
            logo.y = TimeTravelLogoTop * FlashXmlScale.AtlasToFlashPointScale;
            _ = scene.AddChild(logo);

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
        /// <param name="scene">Scene the capsules go in.</param>
        private void AddTimeTravelCapsules(TimeTravelSceneGroup scene)
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
        /// One state of a Time Travel audio toggle. The icon and the cross keep the places the iOS
        /// button canvas gives them on the plate.
        /// </summary>
        /// <param name="icon">Icon quad.</param>
        /// <param name="crossed">Whether the sound is switched off.</param>
        /// <param name="pressed">Whether to draw the pressed plate.</param>
        /// <returns>The plate with its icon.</returns>
        private static Image CreateTimeTravelAudioElement(int icon, bool crossed, bool pressed)
        {
            string sheet = Resources.Img.MenuButtonSmallTimeTravel;
            int plateQuad = pressed ? TimeTravelArt.AudioPlateDown : TimeTravelArt.AudioPlateUp;
            Image plate = Image.FromResource(sheet, plateQuad);
            Image glyph = Image.FromResource(sheet, icon);
            glyph.anchor = glyph.parentAnchor = 9;
            Image.SetElementPositionWithRelativeQuadOffset(glyph, sheet, plateQuad, icon);
            _ = plate.AddChild(glyph);
            if (crossed)
            {
                glyph.color = RGBAColor.MakeRGBA(0.5f, 0.5f, 0.5f, 0.5f);
                Image cross = Image.FromResource(sheet, TimeTravelArt.AudioCross);
                cross.anchor = cross.parentAnchor = 9;
                Image.SetElementPositionWithRelativeQuadOffset(cross, sheet, plateQuad, TimeTravelArt.AudioCross);
                _ = plate.AddChild(cross);
            }
            return plate;
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
