using CutTheRopeDX.Framework;
using CutTheRopeDX.Framework.Core;
using CutTheRopeDX.Framework.Platform;
using CutTheRopeDX.Framework.Visual;

namespace CutTheRopeDX.GameMain
{
    /// <summary>
    /// Ghost-transformed grab variant with ambient cloud visuals and a tinted grab radius.
    /// </summary>
    internal sealed class GhostGrab : Grab, IGhostApparition
    {
        /// <inheritdoc />
        BaseElement IGhostApparition.Element => this;

        /// <summary>
        /// Initializes the ghost grab at a level position and creates its supporting cloud visuals.
        /// </summary>
        /// <param name="px">World-space X position.</param>
        /// <param name="py">World-space Y position.</param>
        /// <returns>The initialized ghost grab.</returns>
        public GhostGrab InitWithPosition(float px, float py)
        {
            x = px;
            y = py;
            Image smallCloud = FromResource(Resources.Img.ObjGhost, 5);
            smallCloud.x = x - 60f;
            smallCloud.y = y + 2f;
            smallCloud.anchor = 18;
            // smallCloud.DoRestoreCutTransparency();
            _ = AddChild(smallCloud);
            Timeline smallCloudTimeline = new Timeline().InitWithMaxKeyFramesOnTrack(5);
            smallCloudTimeline.SetTimelineLoopType(Timeline.LoopType.TIMELINE_REPLAY);
            smallCloudTimeline.AddKeyFrame(KeyFrame.MakeScale(0.43f, 0.43f, KeyFrame.TransitionType.FRAME_TRANSITION_IMMEDIATE, 0));
            smallCloudTimeline.AddKeyFrame(KeyFrame.MakeScale(0.465f, 0.465f, KeyFrame.TransitionType.FRAME_TRANSITION_EASE_IN, 0.65f));
            smallCloudTimeline.AddKeyFrame(KeyFrame.MakeScale(0.5f, 0.5f, KeyFrame.TransitionType.FRAME_TRANSITION_EASE_OUT, 0.65f));
            smallCloudTimeline.AddKeyFrame(KeyFrame.MakeScale(0.465f, 0.465f, KeyFrame.TransitionType.FRAME_TRANSITION_EASE_IN, 0.65f));
            smallCloudTimeline.AddKeyFrame(KeyFrame.MakeScale(0.43f, 0.43f, KeyFrame.TransitionType.FRAME_TRANSITION_EASE_OUT, 0.65f));
            smallCloudTimeline.AddKeyFrame(KeyFrame.MakePos((int)(smallCloud.x - 1f), (int)(smallCloud.y + 1f), KeyFrame.TransitionType.FRAME_TRANSITION_IMMEDIATE, 0));
            smallCloudTimeline.AddKeyFrame(KeyFrame.MakePos((int)smallCloud.x, (int)smallCloud.y, KeyFrame.TransitionType.FRAME_TRANSITION_EASE_IN, 0.65f));
            smallCloudTimeline.AddKeyFrame(KeyFrame.MakePos((int)(smallCloud.x + 1f), (int)(smallCloud.y - 1f), KeyFrame.TransitionType.FRAME_TRANSITION_EASE_OUT, 0.65f));
            smallCloudTimeline.AddKeyFrame(KeyFrame.MakePos((int)smallCloud.x, (int)smallCloud.y, KeyFrame.TransitionType.FRAME_TRANSITION_EASE_IN, 0.65f));
            smallCloudTimeline.AddKeyFrame(KeyFrame.MakePos((int)(smallCloud.x - 1f), (int)(smallCloud.y + 1f), KeyFrame.TransitionType.FRAME_TRANSITION_EASE_OUT, 0.65f));
            smallCloud.AddTimelinewithID(smallCloudTimeline, 0);
            smallCloud.PlayTimeline(0);

            Image midCloud = FromResource(Resources.Img.ObjGhost, 4);
            midCloud.x = x + 58f;
            midCloud.y = y + 18f;
            midCloud.anchor = 18;
            // midCloud.DoRestoreCutTransparency();
            _ = AddChild(midCloud);
            Timeline midCloudTimeline = new Timeline().InitWithMaxKeyFramesOnTrack(5);
            midCloudTimeline.SetTimelineLoopType(Timeline.LoopType.TIMELINE_REPLAY);
            midCloudTimeline.AddKeyFrame(KeyFrame.MakeScale(0.9f, 0.9f, KeyFrame.TransitionType.FRAME_TRANSITION_IMMEDIATE, 0));
            midCloudTimeline.AddKeyFrame(KeyFrame.MakeScale(0.8f, 0.8f, KeyFrame.TransitionType.FRAME_TRANSITION_EASE_IN, 0.45f));
            midCloudTimeline.AddKeyFrame(KeyFrame.MakeScale(0.7f, 0.7f, KeyFrame.TransitionType.FRAME_TRANSITION_EASE_OUT, 0.45f));
            midCloudTimeline.AddKeyFrame(KeyFrame.MakeScale(0.8f, 0.8f, KeyFrame.TransitionType.FRAME_TRANSITION_EASE_IN, 0.45f));
            midCloudTimeline.AddKeyFrame(KeyFrame.MakeScale(0.9f, 0.9f, KeyFrame.TransitionType.FRAME_TRANSITION_EASE_OUT, 0.45f));
            midCloudTimeline.AddKeyFrame(KeyFrame.MakePos((int)(midCloud.x + 1f), (int)(midCloud.y + 1f), KeyFrame.TransitionType.FRAME_TRANSITION_IMMEDIATE, 0));
            midCloudTimeline.AddKeyFrame(KeyFrame.MakePos((int)midCloud.x, (int)midCloud.y, KeyFrame.TransitionType.FRAME_TRANSITION_EASE_IN, 0.45f));
            midCloudTimeline.AddKeyFrame(KeyFrame.MakePos((int)(midCloud.x - 1f), (int)(midCloud.y - 1f), KeyFrame.TransitionType.FRAME_TRANSITION_EASE_OUT, 0.45f));
            midCloudTimeline.AddKeyFrame(KeyFrame.MakePos((int)midCloud.x, (int)midCloud.y, KeyFrame.TransitionType.FRAME_TRANSITION_EASE_IN, 0.45f));
            midCloudTimeline.AddKeyFrame(KeyFrame.MakePos((int)(midCloud.x + 1f), (int)(midCloud.y + 1f), KeyFrame.TransitionType.FRAME_TRANSITION_EASE_OUT, 0.45f));
            midCloud.AddTimelinewithID(midCloudTimeline, 0);
            midCloud.PlayTimeline(0);

            Image bigCloud = FromResource(Resources.Img.ObjGhost, 2);
            bigCloud.x = x - 15f;
            bigCloud.y = y + 45f;
            bigCloud.anchor = 18;
            // bigCloud.DoRestoreCutTransparency();
            _ = AddChild(bigCloud);
            Timeline bigCloudTimeline = new Timeline().InitWithMaxKeyFramesOnTrack(5);
            bigCloudTimeline.SetTimelineLoopType(Timeline.LoopType.TIMELINE_REPLAY);
            bigCloudTimeline.AddKeyFrame(KeyFrame.MakeScale(1.1f, 1.1f, KeyFrame.TransitionType.FRAME_TRANSITION_IMMEDIATE, 0));
            bigCloudTimeline.AddKeyFrame(KeyFrame.MakeScale(1f, 1f, KeyFrame.TransitionType.FRAME_TRANSITION_EASE_IN, 0.5f));
            bigCloudTimeline.AddKeyFrame(KeyFrame.MakeScale(0.9f, 0.9f, KeyFrame.TransitionType.FRAME_TRANSITION_EASE_OUT, 0.5f));
            bigCloudTimeline.AddKeyFrame(KeyFrame.MakeScale(1f, 1f, KeyFrame.TransitionType.FRAME_TRANSITION_EASE_IN, 0.5f));
            bigCloudTimeline.AddKeyFrame(KeyFrame.MakeScale(1.1f, 1.1f, KeyFrame.TransitionType.FRAME_TRANSITION_EASE_OUT, 0.5f));
            bigCloudTimeline.AddKeyFrame(KeyFrame.MakePos((int)(bigCloud.x - 1f), (int)(bigCloud.y + 1f), KeyFrame.TransitionType.FRAME_TRANSITION_IMMEDIATE, 0));
            bigCloudTimeline.AddKeyFrame(KeyFrame.MakePos((int)bigCloud.x, (int)bigCloud.y, KeyFrame.TransitionType.FRAME_TRANSITION_EASE_IN, 0.5f));
            bigCloudTimeline.AddKeyFrame(KeyFrame.MakePos((int)(bigCloud.x + 1f), (int)(bigCloud.y - 1f), KeyFrame.TransitionType.FRAME_TRANSITION_EASE_OUT, 0.5f));
            bigCloudTimeline.AddKeyFrame(KeyFrame.MakePos((int)bigCloud.x, (int)bigCloud.y, KeyFrame.TransitionType.FRAME_TRANSITION_EASE_IN, 0.5f));
            bigCloudTimeline.AddKeyFrame(KeyFrame.MakePos((int)(bigCloud.x - 1f), (int)(bigCloud.y + 1f), KeyFrame.TransitionType.FRAME_TRANSITION_EASE_OUT, 0.5f));
            bigCloud.AddTimelinewithID(bigCloudTimeline, 0);
            bigCloud.PlayTimeline(0);
            return this;
        }

        /// <inheritdoc />
        public override void DrawBack()
        {
        }

        /// <inheritdoc />
        public override void Draw()
        {
            if (!visible)
            {
                return;
            }
            PreDraw();
            back.color = color;
            Renderer.SetBlendFunc(BlendingFactor.GLONE, BlendingFactor.GLONEMINUSSRCALPHA);
            back.Draw();
            Renderer.SetBlendFunc(BlendingFactor.GLSRCALPHA, BlendingFactor.GLONEMINUSSRCALPHA);
            Renderer.Disable(Renderer.GL_TEXTURE_2D);
            if (RadiusSource?.ShouldDrawCircle == true)
            {
                RootController rootController = Application.SharedRootController();
                int pack = rootController.Pack;
                RGBAColor? ghostGrabOverride = PackConfig.GetGhostGrabColor(pack);
                RGBAColor grabColor = ghostGrabOverride.HasValue
                    ? RGBAColor.MakeRGBA(ghostGrabOverride.Value.RedColor, ghostGrabOverride.Value.GreenColor, ghostGrabOverride.Value.BlueColor, RadiusSource.RadiusAlpha * color.AlphaChannel)
                    : RGBAColor.MakeRGBA(0.2f, 0.5f, 0.9f, RadiusSource.RadiusAlpha * color.AlphaChannel);
                DrawGrabCircle(this, grabColor);
            }
            Renderer.SetColor(Color.White);
            Renderer.Enable(Renderer.GL_TEXTURE_2D);
            Renderer.Disable(Renderer.GL_TEXTURE_2D);
            Rope?.Draw();
            Renderer.SetColor(Color.White);
            Renderer.Enable(Renderer.GL_TEXTURE_2D);
            Renderer.SetBlendFunc(BlendingFactor.GLONE, BlendingFactor.GLONEMINUSSRCALPHA);
            front.color = color;
            front.Draw();
            PostDraw();
        }
    }
}
