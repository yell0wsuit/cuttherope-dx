using CutTheRopeDX.Framework.Core;
using CutTheRopeDX.Framework.Visual;

namespace CutTheRopeDX.GameMain
{
    /// <summary>
    /// Ghost-transformed bubble variant with supporting cloud visuals and custom draw behavior.
    /// </summary>
    internal sealed class GhostBubble : Bubble, IGhostApparition
    {
        /// <inheritdoc />
        BaseElement IGhostApparition.Element => this;

        /// <summary>
        /// Adds looping ghost cloud timelines around the bubble.
        /// </summary>
        public void AddSupportingCloudsTimelines()
        {
            // first right cloud
            backCloud = Image_createWithResIDQuad(Resources.Img.ObjGhost, 6);
            backCloud.x = x + 85f;
            backCloud.y = y + 25f;
            backCloud.anchor = 18;
            _ = AddChild(backCloud);
            Timeline timeline = new Timeline().InitWithMaxKeyFramesOnTrack(5);
            timeline.SetTimelineLoopType(Timeline.LoopType.TIMELINE_REPLAY);
            timeline.AddKeyFrame(KeyFrame.MakeScale(0.8f, 0.8f, KeyFrame.TransitionType.FRAME_TRANSITION_IMMEDIATE, 0));
            timeline.AddKeyFrame(KeyFrame.MakeScale(0.78f, 0.78f, KeyFrame.TransitionType.FRAME_TRANSITION_EASE_IN, 0.48f));
            timeline.AddKeyFrame(KeyFrame.MakeScale(0.76f, 0.76f, KeyFrame.TransitionType.FRAME_TRANSITION_EASE_OUT, 0.48f));
            timeline.AddKeyFrame(KeyFrame.MakeScale(0.78f, 0.78f, KeyFrame.TransitionType.FRAME_TRANSITION_EASE_IN, 0.48f));
            timeline.AddKeyFrame(KeyFrame.MakeScale(0.8f, 0.8f, KeyFrame.TransitionType.FRAME_TRANSITION_EASE_OUT, 0.48f));
            timeline.AddKeyFrame(KeyFrame.MakePos((int)(backCloud.x + 1f), (int)(backCloud.y + 1f), KeyFrame.TransitionType.FRAME_TRANSITION_IMMEDIATE, 0));
            timeline.AddKeyFrame(KeyFrame.MakePos((int)backCloud.x, (int)backCloud.y, KeyFrame.TransitionType.FRAME_TRANSITION_EASE_IN, 0.48f));
            timeline.AddKeyFrame(KeyFrame.MakePos((int)(backCloud.x - 1f), (int)(backCloud.y - 1f), KeyFrame.TransitionType.FRAME_TRANSITION_EASE_OUT, 0.48f));
            timeline.AddKeyFrame(KeyFrame.MakePos((int)backCloud.x, (int)backCloud.y, KeyFrame.TransitionType.FRAME_TRANSITION_EASE_IN, 0.48f));
            timeline.AddKeyFrame(KeyFrame.MakePos((int)(backCloud.x + 1f), (int)(backCloud.y + 1f), KeyFrame.TransitionType.FRAME_TRANSITION_EASE_OUT, 0.48f));
            backCloud.AddTimelinewithID(timeline, 0);
            backCloud.PlayTimeline(0);

            backCloud2 = Image_createWithResIDQuad(Resources.Img.ObjGhost, 5);
            backCloud2.x = x + 65f;
            backCloud2.y = y + 55f;
            backCloud2.anchor = 18;
            _ = AddChild(backCloud2);
            Timeline backCloud2Timeline = new Timeline().InitWithMaxKeyFramesOnTrack(5);
            backCloud2Timeline.SetTimelineLoopType(Timeline.LoopType.TIMELINE_REPLAY);
            backCloud2Timeline.AddKeyFrame(KeyFrame.MakeScale(0.93f, 0.93f, KeyFrame.TransitionType.FRAME_TRANSITION_IMMEDIATE, 0));
            backCloud2Timeline.AddKeyFrame(KeyFrame.MakeScale(0.965f, 0.965f, KeyFrame.TransitionType.FRAME_TRANSITION_EASE_IN, 0.4f));
            backCloud2Timeline.AddKeyFrame(KeyFrame.MakeScale(1f, 1f, KeyFrame.TransitionType.FRAME_TRANSITION_EASE_OUT, 0.4f));
            backCloud2Timeline.AddKeyFrame(KeyFrame.MakeScale(0.965f, 0.965f, KeyFrame.TransitionType.FRAME_TRANSITION_EASE_IN, 0.4f));
            backCloud2Timeline.AddKeyFrame(KeyFrame.MakeScale(0.93f, 0.93f, KeyFrame.TransitionType.FRAME_TRANSITION_EASE_OUT, 0.4f));
            backCloud2Timeline.AddKeyFrame(KeyFrame.MakePos((int)(backCloud2.x + 1f), (int)(backCloud2.y + 1f), KeyFrame.TransitionType.FRAME_TRANSITION_IMMEDIATE, 0));
            backCloud2Timeline.AddKeyFrame(KeyFrame.MakePos((int)backCloud2.x, (int)backCloud2.y, KeyFrame.TransitionType.FRAME_TRANSITION_EASE_IN, 0.4f));
            backCloud2Timeline.AddKeyFrame(KeyFrame.MakePos((int)(backCloud2.x - 1f), (int)(backCloud2.y - 1f), KeyFrame.TransitionType.FRAME_TRANSITION_EASE_OUT, 0.4f));
            backCloud2Timeline.AddKeyFrame(KeyFrame.MakePos((int)backCloud2.x, (int)backCloud2.y, KeyFrame.TransitionType.FRAME_TRANSITION_EASE_IN, 0.4f));
            backCloud2Timeline.AddKeyFrame(KeyFrame.MakePos((int)(backCloud2.x + 1f), (int)(backCloud2.y + 1f), KeyFrame.TransitionType.FRAME_TRANSITION_EASE_OUT, 0.4f));
            backCloud2.AddTimelinewithID(backCloud2Timeline, 0);
            backCloud2.PlayTimeline(0);

            // first left small cloud
            backCloud3 = Image_createWithResIDQuad(Resources.Img.ObjGhost, 5);
            backCloud3.x = x - 90f;
            backCloud3.y = y + 15f;
            backCloud3.anchor = 18;
            _ = AddChild(backCloud3);
            Timeline backCloud3Timeline = new Timeline().InitWithMaxKeyFramesOnTrack(5);
            backCloud3Timeline.SetTimelineLoopType(Timeline.LoopType.TIMELINE_REPLAY);
            backCloud3Timeline.AddKeyFrame(KeyFrame.MakeScale(0.33f, 0.33f, KeyFrame.TransitionType.FRAME_TRANSITION_IMMEDIATE, 0));
            backCloud3Timeline.AddKeyFrame(KeyFrame.MakeScale(0.365f, 0.365f, KeyFrame.TransitionType.FRAME_TRANSITION_EASE_IN, 0.43f));
            backCloud3Timeline.AddKeyFrame(KeyFrame.MakeScale(0.4f, 0.4f, KeyFrame.TransitionType.FRAME_TRANSITION_EASE_OUT, 0.43f));
            backCloud3Timeline.AddKeyFrame(KeyFrame.MakeScale(0.365f, 0.365f, KeyFrame.TransitionType.FRAME_TRANSITION_EASE_IN, 0.43f));
            backCloud3Timeline.AddKeyFrame(KeyFrame.MakeScale(0.33f, 0.33f, KeyFrame.TransitionType.FRAME_TRANSITION_EASE_OUT, 0.43f));
            backCloud3Timeline.AddKeyFrame(KeyFrame.MakePos((int)(backCloud3.x + 1f), (int)(backCloud3.y + 1f), KeyFrame.TransitionType.FRAME_TRANSITION_IMMEDIATE, 0));
            backCloud3Timeline.AddKeyFrame(KeyFrame.MakePos((int)backCloud3.x, (int)backCloud3.y, KeyFrame.TransitionType.FRAME_TRANSITION_EASE_IN, 0.43f));
            backCloud3Timeline.AddKeyFrame(KeyFrame.MakePos((int)(backCloud3.x - 1f), (int)(backCloud3.y - 1f), KeyFrame.TransitionType.FRAME_TRANSITION_EASE_OUT, 0.43f));
            backCloud3Timeline.AddKeyFrame(KeyFrame.MakePos((int)backCloud3.x, (int)backCloud3.y, KeyFrame.TransitionType.FRAME_TRANSITION_EASE_IN, 0.43f));
            backCloud3Timeline.AddKeyFrame(KeyFrame.MakePos((int)(backCloud3.x + 1f), (int)(backCloud3.y + 1f), KeyFrame.TransitionType.FRAME_TRANSITION_EASE_OUT, 0.43f));
            backCloud3.AddTimelinewithID(backCloud3Timeline, 0);
            backCloud3.PlayTimeline(0);

            // second left small cloud
            Image smallCloud = Image_createWithResIDQuad(Resources.Img.ObjGhost, 6);
            smallCloud.x = x - 75f;
            smallCloud.y = y + 45f;
            smallCloud.anchor = 18;
            //smallCloud.DoRestoreCutTransparency();
            _ = AddChild(smallCloud);
            Timeline smallCloudTimeline = new Timeline().InitWithMaxKeyFramesOnTrack(5);
            smallCloudTimeline.SetTimelineLoopType(Timeline.LoopType.TIMELINE_REPLAY);
            smallCloudTimeline.AddKeyFrame(KeyFrame.MakeScale(0.6f, 0.6f, KeyFrame.TransitionType.FRAME_TRANSITION_IMMEDIATE, 0));
            smallCloudTimeline.AddKeyFrame(KeyFrame.MakeScale(0.565f, 0.565f, KeyFrame.TransitionType.FRAME_TRANSITION_EASE_IN, 0.42f));
            smallCloudTimeline.AddKeyFrame(KeyFrame.MakeScale(0.53f, 0.53f, KeyFrame.TransitionType.FRAME_TRANSITION_EASE_OUT, 0.42f));
            smallCloudTimeline.AddKeyFrame(KeyFrame.MakeScale(0.565f, 0.565f, KeyFrame.TransitionType.FRAME_TRANSITION_EASE_IN, 0.42f));
            smallCloudTimeline.AddKeyFrame(KeyFrame.MakeScale(0.6f, 0.6f, KeyFrame.TransitionType.FRAME_TRANSITION_EASE_OUT, 0.42f));
            smallCloudTimeline.AddKeyFrame(KeyFrame.MakePos((int)(smallCloud.x - 1f), (int)(smallCloud.y + 1f), KeyFrame.TransitionType.FRAME_TRANSITION_IMMEDIATE, 0));
            smallCloudTimeline.AddKeyFrame(KeyFrame.MakePos((int)smallCloud.x, (int)smallCloud.y, KeyFrame.TransitionType.FRAME_TRANSITION_EASE_IN, 0.42f));
            smallCloudTimeline.AddKeyFrame(KeyFrame.MakePos((int)(smallCloud.x + 1f), (int)(smallCloud.y - 1f), KeyFrame.TransitionType.FRAME_TRANSITION_EASE_OUT, 0.42f));
            smallCloudTimeline.AddKeyFrame(KeyFrame.MakePos((int)smallCloud.x, (int)smallCloud.y, KeyFrame.TransitionType.FRAME_TRANSITION_EASE_IN, 0.42f));
            smallCloudTimeline.AddKeyFrame(KeyFrame.MakePos((int)(smallCloud.x - 1f), (int)(smallCloud.y + 1f), KeyFrame.TransitionType.FRAME_TRANSITION_EASE_OUT, 0.42f));
            smallCloud.AddTimelinewithID(smallCloudTimeline, 0);
            smallCloud.PlayTimeline(0);

            // big cloud
            Image bigCloud = Image_createWithResIDQuad(Resources.Img.ObjGhost, 2);
            bigCloud.x = x - 20f;
            bigCloud.y = y + 75f;
            bigCloud.anchor = 18;
            //bigCloud.DoRestoreCutTransparency();
            _ = AddChild(bigCloud);
            Timeline bigCloudTimeline = new Timeline().InitWithMaxKeyFramesOnTrack(5);
            bigCloudTimeline.SetTimelineLoopType(Timeline.LoopType.TIMELINE_REPLAY);
            bigCloudTimeline.AddKeyFrame(KeyFrame.MakeScale(0.93f, 0.93f, KeyFrame.TransitionType.FRAME_TRANSITION_IMMEDIATE, 0));
            bigCloudTimeline.AddKeyFrame(KeyFrame.MakeScale(0.965f, 0.965f, KeyFrame.TransitionType.FRAME_TRANSITION_EASE_IN, 0.47f));
            bigCloudTimeline.AddKeyFrame(KeyFrame.MakeScale(1f, 1f, KeyFrame.TransitionType.FRAME_TRANSITION_EASE_OUT, 0.47f));
            bigCloudTimeline.AddKeyFrame(KeyFrame.MakeScale(0.965f, 0.965f, KeyFrame.TransitionType.FRAME_TRANSITION_EASE_IN, 0.47f));
            bigCloudTimeline.AddKeyFrame(KeyFrame.MakeScale(0.93f, 0.93f, KeyFrame.TransitionType.FRAME_TRANSITION_EASE_OUT, 0.47f));
            bigCloudTimeline.AddKeyFrame(KeyFrame.MakePos((int)(bigCloud.x + 1f), (int)(bigCloud.y - 1f), KeyFrame.TransitionType.FRAME_TRANSITION_IMMEDIATE, 0));
            bigCloudTimeline.AddKeyFrame(KeyFrame.MakePos((int)bigCloud.x, (int)bigCloud.y, KeyFrame.TransitionType.FRAME_TRANSITION_EASE_IN, 0.47f));
            bigCloudTimeline.AddKeyFrame(KeyFrame.MakePos((int)(bigCloud.x - 1f), (int)(bigCloud.y + 1f), KeyFrame.TransitionType.FRAME_TRANSITION_EASE_OUT, 0.47f));
            bigCloudTimeline.AddKeyFrame(KeyFrame.MakePos((int)bigCloud.x, (int)bigCloud.y, KeyFrame.TransitionType.FRAME_TRANSITION_EASE_IN, 0.47f));
            bigCloudTimeline.AddKeyFrame(KeyFrame.MakePos((int)(bigCloud.x + 1f), (int)(bigCloud.y - 1f), KeyFrame.TransitionType.FRAME_TRANSITION_EASE_OUT, 0.47f));
            bigCloudTimeline.AddKeyFrame(KeyFrame.MakeRotation(350, KeyFrame.TransitionType.FRAME_TRANSITION_IMMEDIATE, 0));
            bigCloudTimeline.AddKeyFrame(KeyFrame.MakeRotation(350, KeyFrame.TransitionType.FRAME_TRANSITION_IMMEDIATE, 0));
            bigCloudTimeline.AddKeyFrame(KeyFrame.MakeRotation(350, KeyFrame.TransitionType.FRAME_TRANSITION_IMMEDIATE, 0));
            bigCloud.AddTimelinewithID(bigCloudTimeline, 0);
            bigCloud.PlayTimeline(0);
            passTransformationsToChilds = true;
        }

        /// <inheritdoc />
        public override void Draw()
        {
            PreDraw();
            if (!withoutShadow)
            {
                if (quadToDraw == -1)
                {
                    DrawHelper.DrawImage(texture, drawX, drawY);
                }
                else
                {
                    DrawQuad(quadToDraw);
                }
            }
            if (!popped)
            {
                PostDraw();
                return;
            }
            RestoreColor(this);
        }

        /// <inheritdoc />
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                backCloud = null;
                backCloud2 = null;
                backCloud3 = null;
            }
            base.Dispose(disposing);
        }

        /// <summary>Primary background ghost cloud element.</summary>
        public Image backCloud;

        /// <summary>Secondary background ghost cloud element.</summary>
        public Image backCloud2;

        /// <summary>Tertiary background ghost cloud element.</summary>
        public Image backCloud3;
    }
}
