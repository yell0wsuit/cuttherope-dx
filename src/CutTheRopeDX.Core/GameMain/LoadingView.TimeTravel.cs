using System;

using CutTheRopeDX.Framework;
using CutTheRopeDX.Framework.Core;
using CutTheRopeDX.Framework.Platform;
using CutTheRopeDX.Framework.Visual;

namespace CutTheRopeDX.GameMain
{
    /// <content>
    /// The Cut the Rope: Time Travel loading screen (iOS HD 1.5.0
    /// <c>MainAbstractLoadingController</c>): the clock animation on white. Its first timeline
    /// brings the clocks in, its second is wound forward only as far as loading has got, and once
    /// that one ends the third sends them off while the label fades.
    /// </content>
    internal sealed partial class LoadingView
    {
        /// <summary>How far below the screen's middle the label's bottom sits, in iOS logical units.</summary>
        internal const float TimeTravelLabelDrop = 200f;

        /// <summary>Seconds the label fades in or out for (iOS 0.3).</summary>
        private const float TimeTravelLabelFade = 0.3f;

        /// <summary>The clock animation's timelines, in the order they play.</summary>
        internal enum TimeTravelPhase
        {
            /// <summary>The clocks come in.</summary>
            Intro = 0,

            /// <summary>The clocks wind forward with loading.</summary>
            Progress = 1,

            /// <summary>The clocks leave.</summary>
            Outro = 2,

            /// <summary>The animation is over and the screen can hand over.</summary>
            Done = 3,
        }

        private TimeTravelFlashStage timeTravelClock;
        private RectangleElement timeTravelWhite;
        private Text timeTravelLabel;
        private float timeTravelWound;
        private float timeTravelProgressLength;

        /// <summary>Gets the timeline the clock animation is on.</summary>
        internal TimeTravelPhase TimeTravelState { get; private set; } = TimeTravelPhase.Done;

        /// <summary>Gets or sets where loading has got, in percent; the resource manager's by default.</summary>
        internal Func<float> TimeTravelPercent { get; set; } = () => Application.SharedResourceMgr().GetPercentLoaded();

        /// <summary>Gets the clock animation's stage, or <see langword="null"/> outside Time Travel.</summary>
        internal FlashXmlStageRoot TimeTravelClock => timeTravelClock?.Root;

        /// <summary>Builds the white backdrop and the clock animation, under the label.</summary>
        internal void AttachTimeTravel()
        {
            timeTravelWhite = new RectangleElement { color = RGBAColor.solidOpaqueRGBA };
            timeTravelWhite.SetName("ttLoadingWhite");
            timeTravelWhite.anchor = timeTravelWhite.parentAnchor = 9;
            _ = AddChild(timeTravelWhite);

            timeTravelClock = TimeTravelFlashStage.Create(TimeTravelArt.LoadingAnimationXml, Resources.Img.MenuLoadingTimeTravel);
            timeTravelClock.Root.SetName("ttLoadingClock");

            // Wound by hand while loading, so the view's own update must not run it as well.
            timeTravelClock.Root.updateable = false;
            _ = AddChild(timeTravelClock.Root);
            timeTravelProgressLength = timeTravelClock.RootTimeline((int)TimeTravelPhase.Progress)?.Duration ?? 0f;
            LayOutTimeTravel(VisibleBounds);
        }

        /// <summary>Gives the label its fades and hangs it under the clocks.</summary>
        /// <param name="label">The loading label.</param>
        internal void UseTimeTravelLabel(Text label)
        {
            timeTravelLabel = label;
            label.anchor = 34;
            label.parentAnchor = 18;
            label.color = RGBAColor.transparentRGBA;
            Timeline fadeIn = new Timeline().InitWithMaxKeyFramesOnTrack(2);
            fadeIn.AddKeyFrame(KeyFrame.MakeColor(RGBAColor.transparentRGBA, KeyFrame.TransitionType.FRAME_TRANSITION_LINEAR, 0));
            fadeIn.AddKeyFrame(KeyFrame.MakeColor(RGBAColor.solidOpaqueRGBA, KeyFrame.TransitionType.FRAME_TRANSITION_LINEAR, TimeTravelLabelFade));
            _ = label.AddTimeline(fadeIn);
            Timeline fadeOut = new Timeline().InitWithMaxKeyFramesOnTrack(2);
            fadeOut.AddKeyFrame(KeyFrame.MakeColor(RGBAColor.solidOpaqueRGBA, KeyFrame.TransitionType.FRAME_TRANSITION_LINEAR, 0));
            fadeOut.AddKeyFrame(KeyFrame.MakeColor(RGBAColor.transparentRGBA, KeyFrame.TransitionType.FRAME_TRANSITION_LINEAR, TimeTravelLabelFade));
            _ = label.AddTimeline(fadeOut);
        }

        /// <summary>
        /// Covers the screen with white and centers the clocks on it, at the scale the iOS screen
        /// is contained at.
        /// </summary>
        /// <param name="visible">The logical region the viewport exposes.</param>
        /// <param name="contentScale">Scale the label is drawn at.</param>
        internal void LayOutTimeTravel(Rectangle visible, float contentScale = 1f)
        {
            if (timeTravelClock == null)
            {
                return;
            }
            width = (int)MathF.Ceiling(visible.w);
            height = (int)MathF.Ceiling(visible.h);
            timeTravelWhite.width = width;
            timeTravelWhite.height = height;
            TimeTravelScreen screen = new(visible);
            FlashXmlStageRoot stage = timeTravelClock.Root;
            timeTravelClock.Place(screen.Scale, stage.width / 2f, stage.height / 2f, visible.w / 2f, visible.h / 2f);
            if (timeTravelLabel != null)
            {
                timeTravelLabel.scaleX = timeTravelLabel.scaleY = contentScale;
                timeTravelLabel.y = TimeTravelLabelDrop * screen.Scale;
            }
        }

        /// <summary>Starts the clock animation over for a new load.</summary>
        private void ResetTimeTravel()
        {
            if (timeTravelClock == null)
            {
                return;
            }
            timeTravelWound = 0f;
            PlayTimeTravel(TimeTravelPhase.Intro);
            timeTravelLabel?.PlayTimeline(0);
        }

        /// <summary>Gets whether the clocks have left and the screen can hand over.</summary>
        /// <returns><see langword="true"/> once the last timeline has ended.</returns>
        private bool IsTimeTravelComplete()
        {
            return timeTravelClock == null || TimeTravelState == TimeTravelPhase.Done;
        }

        /// <summary>Runs the clock animation one frame further.</summary>
        /// <param name="delta">Seconds since the last frame.</param>
        private void UpdateTimeTravel(float delta)
        {
            if (timeTravelClock == null)
            {
                return;
            }
            FlashXmlStageRoot stage = timeTravelClock.Root;
            switch (TimeTravelState)
            {
                case TimeTravelPhase.Intro:
                case TimeTravelPhase.Outro:
                    stage.Update(delta);
                    break;
                case TimeTravelPhase.Progress:
                    // Wound to where loading has got; reaching 100 runs the timeline to its end.
                    float percent = Math.Clamp(TimeTravelPercent(), 0f, 100f);
                    float target = percent >= 100f
                        ? timeTravelProgressLength + 0.001f
                        : timeTravelProgressLength * percent / 100f;
                    float step = target - timeTravelWound;
                    if (step > 0f)
                    {
                        timeTravelWound = target;
                        stage.Update(step);
                    }
                    break;
                case TimeTravelPhase.Done:
                default:
                    break;
            }
        }

        /// <summary>Plays one of the clock animation's timelines, moving on when it ends.</summary>
        /// <param name="phase">Timeline to play.</param>
        private void PlayTimeTravel(TimeTravelPhase phase)
        {
            TimeTravelState = phase;
            if (phase == TimeTravelPhase.Done)
            {
                return;
            }
            Timeline timeline = timeTravelClock.RootTimeline((int)phase);
            if (timeline == null)
            {
                FinishTimeTravel(phase);
                return;
            }
            timeline.OnFinished = () => FinishTimeTravel(phase);
            timeTravelClock.Play((int)phase);
        }

        /// <summary>Moves on from a clock timeline that has ended.</summary>
        /// <param name="phase">Timeline that ended.</param>
        private void FinishTimeTravel(TimeTravelPhase phase)
        {
            if (TimeTravelState != phase)
            {
                return;
            }
            if (phase == TimeTravelPhase.Progress)
            {
                timeTravelLabel?.PlayTimeline(1);
            }
            PlayTimeTravel(phase + 1);
        }

        /// <summary>Draws the white backdrop, the clocks and the label.</summary>
        private void DrawTimeTravel()
        {
            PlatformServices.Cursor?.Enable(true);
            Renderer.Enable(Renderer.GL_TEXTURE_2D);
            Renderer.Enable(Renderer.GL_BLEND);
            Renderer.SetBlendFunc(BlendingFactor.GLONE, BlendingFactor.GLONEMINUSSRCALPHA);
            PreDraw();
            PostDraw();
            Renderer.SetColor(Color.White);
            Renderer.Disable(Renderer.GL_TEXTURE_2D);
            Renderer.Disable(Renderer.GL_BLEND);
        }
    }
}
