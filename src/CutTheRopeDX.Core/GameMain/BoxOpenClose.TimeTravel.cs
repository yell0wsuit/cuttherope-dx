using CutTheRopeDX.Framework;
using CutTheRopeDX.Framework.Visual;

namespace CutTheRopeDX.GameMain
{
    /// <content>
    /// The Time Travel level transition: no box, but the white the loading screen ends on fading
    /// off the level as it opens, and the level fading to white as it is left (iOS HD
    /// <c>GameController</c>'s full-screen rectangle). A won level shows the Time Travel result
    /// screen in place of the classic result box.
    /// </content>
    internal sealed partial class BoxOpenClose
    {
        /// <summary>Seconds the white takes to fade off or on (iOS 0.25).</summary>
        internal const float TimeTravelFlashSeconds = 0.25f;

        /// <summary>The Time Travel result screen, or <see langword="null"/> outside Time Travel.</summary>
        internal TimeTravelResultScreen TimeTravelResult { get; private set; }

        /// <summary>Builds the Time Travel result screen over the transitions.</summary>
        /// <param name="b">Delegate that receives its buttons' presses.</param>
        private void CreateTimeTravelResult(IButtonDelegation b)
        {
            TimeTravelResult = new TimeTravelResultScreen(b)
            {
                Shut = () => delegateboxClosed?.Invoke(),
            };
            _ = AddChildwithID(TimeTravelResult, 2);
        }

        /// <summary>Fades the white off the level as it opens, or onto it as it is left.</summary>
        /// <param name="open"><see langword="true"/> to fade it off; <see langword="false"/> to fade it on.</param>
        private void ShowTimeTravelFlash(bool open)
        {
            openCloseAnims.scaleX = openCloseAnims.scaleY = 1f;
            openCloseAnims.translateX = openCloseAnims.translateY = 0f;
            Rectangle visible = VisibleBounds;
            RectangleElement white = new()
            {
                width = (int)System.MathF.Ceiling(visible.w),
                height = (int)System.MathF.Ceiling(visible.h),
                color = open ? RGBAColor.solidOpaqueRGBA : RGBAColor.transparentRGBA,
            };
            white.SetName("ttLevelFlash");
            white.anchor = white.parentAnchor = 9;
            Timeline timeline = new Timeline().InitWithMaxKeyFramesOnTrack(2);
            timeline.AddKeyFrame(KeyFrame.MakeColor(open ? RGBAColor.solidOpaqueRGBA : RGBAColor.transparentRGBA, KeyFrame.TransitionType.FRAME_TRANSITION_LINEAR, 0f));
            timeline.AddKeyFrame(KeyFrame.MakeColor(open ? RGBAColor.transparentRGBA : RGBAColor.solidOpaqueRGBA, KeyFrame.TransitionType.FRAME_TRANSITION_LINEAR, TimeTravelFlashSeconds));
            timeline.delegateTimelineDelegate = this;
            white.AddTimelinewithID(timeline, 0);
            white.PlayTimeline(0);
            _ = openCloseAnims.AddChild(white);
        }

        /// <summary>Keeps the white over the whole viewport, unscaled.</summary>
        /// <param name="visible">The logical region the viewport exposes.</param>
        private void FitTimeTravelFlash(Rectangle visible)
        {
            openCloseAnims.scaleX = openCloseAnims.scaleY = 1f;
            openCloseAnims.translateX = openCloseAnims.translateY = 0f;
            for (int i = 0; i < openCloseAnims.ChildsCount(); i++)
            {
                BaseElement white = openCloseAnims.GetChild(i);
                white.width = (int)System.MathF.Ceiling(visible.w);
                white.height = (int)System.MathF.Ceiling(visible.h);
            }
        }
    }
}
