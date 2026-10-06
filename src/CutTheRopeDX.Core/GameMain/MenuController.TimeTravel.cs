using System;

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
        private const float TimeTravelCapsuleGap = 20f;

        /// <summary>Height of the iOS title, in logical units: where the logo part rests after its intro.</summary>
        private const float TimeTravelLogoTop = 2.15f;

        /// <summary>The main menu's Time Travel scene.</summary>
        private TimeTravelSceneGroup timeTravelMain;

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
            Button options = CreateTimeTravelCapsule(Application.GetString("OPTIONS"), MenuButtonId.Options);
            options.SetName("ttOptions");
            Button second = null;
            if (PlatformServices.Host?.CanExit == true)
            {
                second = CreateTimeTravelCapsule(Application.GetString("QUIT_BUTTON"), MenuButtonId.ShowQuitPopup);
            }
            else if (!string.IsNullOrEmpty(PlatformServices.Host?.LevelEditorUrl))
            {
                second = CreateTimeTravelCapsule(Application.GetString("LEVEL_EDITOR_BUTTON"), MenuButtonId.LevelEditor);
            }

            float a = FlashXmlScale.AtlasToFlashPointScale;
            float rowY = TimeTravelCapsuleRowY * a;
            float center = TimeTravelScreen.SceneWidth / 2f * a;
            float half = second == null ? 0f : ((options.width / 2f) + (TimeTravelCapsuleGap * a / 2f));
            CenterAt(options, center - half, rowY);
            _ = scene.AddChild(options);
            if (second != null)
            {
                second.SetName("ttQuit");
                CenterAt(second, center + half, rowY);
                _ = scene.AddChild(second);
            }
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
        /// <returns>The button.</returns>
        private Button CreateTimeTravelCapsule(string text, ButtonId id)
        {
            return TimeTravelPlates.CreateTextButton(
                Resources.Img.MenuButtonSmallTimeTravel, TimeTravelArt.CapsuleUp, TimeTravelArt.CapsuleDown, text, id, this, plateScale: 1f);
        }

        /// <summary>Re-places every Time Travel scene for the current viewport.</summary>
        /// <param name="visible">The logical region the viewport exposes.</param>
        private void LayOutTimeTravelScenes(Rectangle visible)
        {
            timeTravelMain?.Layout(new TimeTravelScreen(visible));
        }
    }
}
