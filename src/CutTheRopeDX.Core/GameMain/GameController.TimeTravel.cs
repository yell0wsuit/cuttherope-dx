using System.Collections.Generic;

using CutTheRopeDX.Framework;
using CutTheRopeDX.Framework.Core;
using CutTheRopeDX.Framework.Visual;

namespace CutTheRopeDX.GameMain
{
    /// <content>
    /// The Time Travel game screen's chrome (iOS HD 1.5.0 <c>GameController</c>): the pause pill
    /// and restart disc in the top right corner, and a pause menu of round icon buttons over a
    /// dimmed level, dropping in from above. Every icon is a picture, so nothing here is localized.
    /// </content>
    internal sealed partial class GameController
    {
        /// <summary>Seconds the pause menu fades in or out over (iOS 0.2).</summary>
        private const float TimeTravelPauseFade = 0.2f;

        /// <summary>How far above its place the pause menu drops in from, in iOS logical units.</summary>
        private const float TimeTravelPauseDrop = -200f;

        /// <summary>Where the pause menu settles, in iOS logical units: a little above its markers.</summary>
        private const float TimeTravelPauseRest = -12f;

        /// <summary>How small the pause menu's second row of buttons is drawn (iOS 0.7).</summary>
        private const float TimeTravelPauseSmallScale = 0.7f;

        /// <summary>
        /// Room between the pause menu's small buttons, in iOS canvas pixels: the step between the
        /// iOS sound and skip markers.
        /// </summary>
        private const float TimeTravelPauseSmallStep = 270f;

        /// <summary>How dark the pause menu dims the level (iOS half black).</summary>
        private static readonly RGBAColor TimeTravelPauseDim = RGBAColor.MakeRGBA(0f, 0f, 0f, 0.5f);

        /// <summary>The pause menu's dim, or <see langword="null"/> outside Time Travel.</summary>
        private RectangleElement timeTravelPauseDim;

        /// <summary>The pause menu's buttons, in the iOS scene.</summary>
        private TimeTravelSceneGroup timeTravelPauseScene;

        /// <summary>What drops the pause menu's buttons in and fades them.</summary>
        private BaseElement timeTravelPauseContent;

        /// <summary>
        /// Builds one of the HUD's corner buttons: the art at rest and its glowing pressed state,
        /// which the sheet draws larger, kept where the canvas puts it against the rest state.
        /// </summary>
        /// <param name="upQuad">Quad at rest.</param>
        /// <param name="downQuad">Quad pressed.</param>
        /// <param name="id">Button id.</param>
        /// <param name="d">Delegate that receives the press.</param>
        /// <returns>The button, anchored to the top right.</returns>
        private static Button CreateTimeTravelHudButton(int upQuad, int downQuad, GameControllerButtonId id, IButtonDelegation d)
        {
            string sheet = Resources.Img.HudTimeTravel;
            Image up = Image.FromResource(sheet, upQuad);
            up.anchor = up.parentAnchor = 9;
            BaseElement down = new() { width = up.width, height = up.height };
            down.anchor = down.parentAnchor = 9;
            Image glow = Image.FromResource(sheet, downQuad);
            glow.anchor = glow.parentAnchor = 9;
            Vector upOffset = Image.GetQuadOffset(sheet, upQuad);
            Vector downOffset = Image.GetQuadOffset(sheet, downQuad);
            glow.x = downOffset.X - upOffset.X;
            glow.y = downOffset.Y - upOffset.Y;
            _ = down.AddChild(glow);
            Button button = new Button().InitWithUpElementDownElementandID(up, down, id);
            button.delegateButtonDelegate = d;
            button.anchor = button.parentAnchor = 12;
            return button;
        }

        /// <summary>
        /// Puts a HUD corner button where the iOS canvas does, pinned to the visible top right as
        /// iOS pinned it, at the scene's scale.
        /// </summary>
        /// <param name="button">The button.</param>
        /// <param name="quad">Its quad at rest, whose canvas place it takes.</param>
        /// <param name="screen">The screen model.</param>
        private static void PlaceTimeTravelHudButton(Button button, int quad, TimeTravelScreen screen)
        {
            Vector offset = Image.GetQuadOffset(Resources.Img.HudTimeTravel, quad);
            float fromRight = offset.X + button.width - TimeTravelSceneGroup.Width;
            PlaceCornerAnchoredHudButton(button, fromRight, offset.Y, screen.AssetScale);
        }

        /// <summary>
        /// One of the round icon buttons the pause and result screens are made of: the back
        /// button's plate with an icon set where iOS sets every one of them, a little above the
        /// plate's middle.
        /// </summary>
        /// <param name="iconSheet">Sheet the icon is in.</param>
        /// <param name="iconQuad">The icon.</param>
        /// <param name="id">Button id.</param>
        /// <param name="d">Delegate that receives the press.</param>
        /// <returns>The button, centered on its place.</returns>
        internal static Button CreateTimeTravelRoundButton(string iconSheet, int iconQuad, ButtonId id, IButtonDelegation d)
        {
            BaseElement up = CreateTimeTravelRoundFace(TimeTravelArt.BackPlateUp, iconSheet, iconQuad);
            BaseElement down = CreateTimeTravelRoundFace(TimeTravelArt.BackPlateDown, iconSheet, iconQuad);
            Button button = new Button().InitWithUpElementDownElementandID(up, down, id);
            button.delegateButtonDelegate = d;
            button.anchor = 18;
            button.parentAnchor = 9;
            return button;
        }

        /// <summary>One state of a round icon button.</summary>
        /// <param name="plateQuad">The plate's quad.</param>
        /// <param name="iconSheet">Sheet the icon is in.</param>
        /// <param name="iconQuad">The icon.</param>
        /// <returns>The plate with its icon.</returns>
        private static Image CreateTimeTravelRoundFace(int plateQuad, string iconSheet, int iconQuad)
        {
            Image plate = Image.FromResource(Resources.Img.MenuButtonsTimeTravel, plateQuad);
            plate.anchor = plate.parentAnchor = 9;
            Image icon = Image.FromResource(iconSheet, iconQuad);
            icon.anchor = icon.parentAnchor = 18;
            Vector lift = TimeTravelRoundIconLift;
            icon.x = lift.X;
            icon.y = lift.Y;
            _ = plate.AddChild(icon);
            return plate;
        }

        /// <summary>
        /// How far a round button's icon sits from its plate's middle, in asset pixels: the iOS
        /// replay icon's middle against its marker's, which iOS applies to every round icon.
        /// </summary>
        private static Vector TimeTravelRoundIconLift
        {
            get
            {
                Rectangle icon = TimeTravelArt.ResultMarkers.RestartIcon;
                Rectangle marker = TimeTravelArt.ResultMarkers.Restart;
                return new Vector(
                    (icon.x + (icon.w / 2f) - marker.x - (marker.w / 2f)) * TimeTravelArt.CanvasToAsset,
                    (icon.y + (icon.h / 2f) - marker.y - (marker.h / 2f)) * TimeTravelArt.CanvasToAsset);
            }
        }

        /// <summary>
        /// Builds the Time Travel pause menu: the dim over the level, and the buttons on the iOS
        /// markers. The first row is the level grid, replay and resume; the second, drawn smaller,
        /// is sound, music and skip. Both rows are centered together in the viewport.
        /// </summary>
        /// <param name="gameView">The game view the menu is added to.</param>
        private void CreateTimeTravelPauseMenu(GameView gameView)
        {
            Rectangle visible = VisibleBounds;
            timeTravelPauseDim = new RectangleElement
            {
                width = (int)System.MathF.Ceiling(visible.w),
                height = (int)System.MathF.Ceiling(visible.h),
                color = RGBAColor.transparentRGBA,
            };
            timeTravelPauseDim.SetName("ttPauseDim");
            timeTravelPauseDim.anchor = timeTravelPauseDim.parentAnchor = 9;

            timeTravelPauseScene = new TimeTravelSceneGroup();
            timeTravelPauseScene.SetName("ttPauseScene");
            timeTravelPauseContent = new BaseElement
            {
                width = TimeTravelSceneGroup.Width,
                height = TimeTravelSceneGroup.Height,
            };
            timeTravelPauseContent.anchor = timeTravelPauseContent.parentAnchor = 9;
            _ = timeTravelPauseScene.AddChild(timeTravelPauseContent);

            bool custom = CustomLevelSession.IsActive;
            List<BaseElement> big = [];
            if (!custom)
            {
                big.Add(CreateTimeTravelRoundButton(Resources.Img.ResultScreenTimeTravel, TimeTravelArt.ResultMenuIcon, GameControllerButtonId.LevelSelect, this));
            }
            big.Add(CreateTimeTravelRoundButton(Resources.Img.ResultScreenTimeTravel, TimeTravelArt.ResultRestartIcon, GameControllerButtonId.PauseRestart, this));
            big.Add(CreateTimeTravelRoundButton(Resources.Img.MenuButtonSmallTimeTravel, TimeTravelArt.ResumeIcon, GameControllerButtonId.Continue, this));

            ToggleButton soundToggle = MenuController.CreateAudioButtonWithQuadDelegateIDiconOffset(2, this, GameControllerButtonId.ToggleSound, round: true);
            ToggleButton musicToggle = MenuController.CreateAudioButtonWithQuadDelegateIDiconOffset(3, this, GameControllerButtonId.ToggleMusic, round: true);
            if (!Preferences.GetBooleanForKey("SOUND_ON"))
            {
                soundToggle.Toggle();
            }
            if (!Preferences.GetBooleanForKey("MUSIC_ON"))
            {
                musicToggle.Toggle();
            }
            soundToggle.anchor = musicToggle.anchor = 18;
            soundToggle.parentAnchor = musicToggle.parentAnchor = 9;
            List<BaseElement> small =
            [
                soundToggle,
                musicToggle,
            ];
            if (!custom)
            {
                small.Add(CreateTimeTravelRoundButton(Resources.Img.MenuButtonSmallTimeTravel, TimeTravelArt.SkipIcon, GameControllerButtonId.SkipLevel, this));
            }

            Rectangle left = TimeTravelArt.HudMarkers.PauseLevelSelect;
            Rectangle right = TimeTravelArt.HudMarkers.PauseResume;
            float bigStep = (right.x - left.x) / 2f;
            float bigY = left.y + (left.h / 2f);
            float smallY = TimeTravelArt.HudMarkers.PauseSound.y + (TimeTravelArt.HudMarkers.PauseSound.h / 2f);
            PlaceTimeTravelPauseRow(big, bigStep, bigY, 1f);
            PlaceTimeTravelPauseRow(small, TimeTravelPauseSmallStep, smallY, TimeTravelPauseSmallScale);

            // Center the combined visible bounds, including the drop animation's resting offset.
            float top = big[0].y - (big[0].height / 2f);
            float bottom = small[0].y + (small[0].height * TimeTravelPauseSmallScale / 2f);
            float shift = (TimeTravelSceneGroup.Height / 2f) - ((top + bottom) / 2f)
                - (TimeTravelPauseRest * FlashXmlScale.AtlasToFlashPointScale);
            foreach (BaseElement button in big)
            {
                button.y += shift;
            }
            foreach (BaseElement button in small)
            {
                button.y += shift;
            }

            AddTimeTravelPauseTimelines();
            timeTravelPauseDim.SetEnabled(false);
            timeTravelPauseScene.SetEnabled(false);
            timeTravelPauseScene.Layout(new TimeTravelScreen(visible));
            _ = gameView.AddChildwithID(timeTravelPauseDim, GameView.VIEW_ELEMENT_PAUSE_MENU);
        }

        /// <summary>Centers a row of pause buttons across the iOS canvas.</summary>
        /// <param name="row">The buttons, left to right.</param>
        /// <param name="step">Room from one button's middle to the next, in iOS canvas pixels.</param>
        /// <param name="y">The row's middle, in iOS canvas pixels.</param>
        /// <param name="scale">How large the buttons are drawn.</param>
        private void PlaceTimeTravelPauseRow(List<BaseElement> row, float step, float y, float scale)
        {
            float middle = TimeTravelSceneGroup.Width / (2f * TimeTravelArt.CanvasToAsset);
            for (int i = 0; i < row.Count; i++)
            {
                BaseElement button = row[i];
                float x = middle + ((i - ((row.Count - 1) / 2f)) * step);
                button.x = x * TimeTravelArt.CanvasToAsset;
                button.y = y * TimeTravelArt.CanvasToAsset;
                button.scaleX = button.scaleY = scale;
                _ = timeTravelPauseContent.AddChild(button);
            }
        }

        /// <summary>
        /// Gives the pause menu its iOS timings: timeline 0 fades the dim and the buttons in while
        /// the buttons drop in with a small bounce; timeline 1 fades them out and lifts the buttons
        /// away, then puts the menu away.
        /// </summary>
        private void AddTimeTravelPauseTimelines()
        {
            float unit = FlashXmlScale.AtlasToFlashPointScale;
            Timeline dimIn = new Timeline().InitWithMaxKeyFramesOnTrack(2);
            dimIn.AddKeyFrame(KeyFrame.MakeColor(RGBAColor.transparentRGBA, KeyFrame.TransitionType.FRAME_TRANSITION_LINEAR, 0f));
            dimIn.AddKeyFrame(KeyFrame.MakeColor(TimeTravelPauseDim, KeyFrame.TransitionType.FRAME_TRANSITION_LINEAR, TimeTravelPauseFade));
            timeTravelPauseDim.AddTimelinewithID(dimIn, 0);
            Timeline dimOut = new Timeline().InitWithMaxKeyFramesOnTrack(2);
            dimOut.AddKeyFrame(KeyFrame.MakeColor(TimeTravelPauseDim, KeyFrame.TransitionType.FRAME_TRANSITION_LINEAR, 0f));
            dimOut.AddKeyFrame(KeyFrame.MakeColor(RGBAColor.transparentRGBA, KeyFrame.TransitionType.FRAME_TRANSITION_LINEAR, TimeTravelPauseFade));
            dimOut.AddKeyFrame(KeyFrame.MakeAction([], 0f));
            dimOut.AddKeyFrame(KeyFrame.MakeAction(
            [
                TimelineAction.CreateAction(timeTravelPauseDim, BaseElement.ACTION_SET_VISIBLE, 0, 0),
                TimelineAction.CreateAction(timeTravelPauseScene, BaseElement.ACTION_SET_VISIBLE, 0, 0),
                TimelineAction.CreateAction(timeTravelPauseScene, BaseElement.ACTION_SET_UPDATEABLE, 0, 0),
            ], TimeTravelPauseFade));
            timeTravelPauseDim.AddTimelinewithID(dimOut, 1);

            Timeline drop = new Timeline().InitWithMaxKeyFramesOnTrack(5);
            drop.AddKeyFrame(KeyFrame.MakeColor(RGBAColor.transparentRGBA, KeyFrame.TransitionType.FRAME_TRANSITION_LINEAR, 0f));
            drop.AddKeyFrame(KeyFrame.MakeColor(RGBAColor.solidOpaqueRGBA, KeyFrame.TransitionType.FRAME_TRANSITION_LINEAR, TimeTravelPauseFade));
            drop.AddKeyFrame(KeyFrame.MakePos(0f, TimeTravelPauseDrop * unit, KeyFrame.TransitionType.FRAME_TRANSITION_LINEAR, 0f));
            drop.AddKeyFrame(KeyFrame.MakePos(0f, TimeTravelPauseDrop * unit, KeyFrame.TransitionType.FRAME_TRANSITION_LINEAR, 0.1f));
            drop.AddKeyFrame(KeyFrame.MakePos(0f, 0f, KeyFrame.TransitionType.FRAME_TRANSITION_EASE_OUT, 0.15f));
            drop.AddKeyFrame(KeyFrame.MakePos(0f, -18f * unit, KeyFrame.TransitionType.FRAME_TRANSITION_EASE_OUT, 0.1f));
            drop.AddKeyFrame(KeyFrame.MakePos(0f, TimeTravelPauseRest * unit, KeyFrame.TransitionType.FRAME_TRANSITION_LINEAR, 0.05f));
            timeTravelPauseContent.AddTimelinewithID(drop, 0);

            Timeline lift = new Timeline().InitWithMaxKeyFramesOnTrack(4);
            lift.AddKeyFrame(KeyFrame.MakeColor(RGBAColor.solidOpaqueRGBA, KeyFrame.TransitionType.FRAME_TRANSITION_LINEAR, 0f));
            lift.AddKeyFrame(KeyFrame.MakeColor(RGBAColor.transparentRGBA, KeyFrame.TransitionType.FRAME_TRANSITION_LINEAR, TimeTravelPauseFade));
            lift.AddKeyFrame(KeyFrame.MakePos(0f, TimeTravelPauseRest * unit, KeyFrame.TransitionType.FRAME_TRANSITION_LINEAR, 0f));
            lift.AddKeyFrame(KeyFrame.MakePos(0f, -210f * unit, KeyFrame.TransitionType.FRAME_TRANSITION_LINEAR, 0.3f));
            lift.AddKeyFrame(KeyFrame.MakePos(0f, -192f * unit, KeyFrame.TransitionType.FRAME_TRANSITION_EASE_OUT, 0.1f));
            lift.AddKeyFrame(KeyFrame.MakePos(0f, TimeTravelPauseDrop * unit, KeyFrame.TransitionType.FRAME_TRANSITION_LINEAR, 0.05f));
            timeTravelPauseContent.AddTimelinewithID(lift, 1);
        }

        /// <summary>Opens or closes the Time Travel pause menu with its iOS animation.</summary>
        /// <param name="show">Whether to open it.</param>
        private void ShowTimeTravelPauseMenu(bool show)
        {
            if (show)
            {
                timeTravelPauseDim.SetEnabled(true);
                timeTravelPauseScene.SetEnabled(true);
                timeTravelPauseDim.PlayTimeline(0);
                timeTravelPauseContent.PlayTimeline(0);
                return;
            }

            // Stops taking touches at once, and fades out over the level before it goes away.
            timeTravelPauseScene.touchable = false;
            timeTravelPauseDim.touchable = false;
            if (!timeTravelPauseDim.visible)
            {
                return;
            }
            timeTravelPauseDim.PlayTimeline(1);
            timeTravelPauseContent.PlayTimeline(1);
        }

        /// <summary>Lays the Time Travel HUD and pause menu out for the current viewport.</summary>
        /// <param name="view">The game view.</param>
        /// <param name="visible">The logical region the viewport exposes.</param>
        private void LayOutTimeTravelChrome(View view, Rectangle visible)
        {
            TimeTravelScreen screen = new(visible);
            PlaceTimeTravelHudButton((Button)view.GetChild(GameView.VIEW_ELEMENT_PAUSE_BUTTON), TimeTravelArt.HudPauseUp, screen);
            PlaceTimeTravelHudButton((Button)view.GetChild(GameView.VIEW_ELEMENT_RESTART_BUTTON), TimeTravelArt.HudRestartUp, screen);
            timeTravelPauseScene?.Layout(screen);
            if (timeTravelPauseDim != null)
            {
                timeTravelPauseDim.width = (int)System.MathF.Ceiling(visible.w);
                timeTravelPauseDim.height = (int)System.MathF.Ceiling(visible.h);
            }
        }
    }
}
