using System;

using CutTheRopeDX.Framework;
using CutTheRopeDX.Framework.Visual;

namespace CutTheRopeDX.GameMain
{
    /// <summary>
    /// Ghost-transformed bouncer variant with ambient supporting cloud visuals.
    /// </summary>
    internal sealed class GhostBouncer : Bouncer, IGhostApparition
    {
        /// <inheritdoc />
        BaseElement IGhostApparition.Element => this;

        /// <inheritdoc />
        public override Bouncer InitWithPosXYWidthAndAngle(float px, float py, int width, float angle)
        {
            if (base.InitWithPosXYWidthAndAngle(px, py, width, angle) != null)
            {
                backCloud2 = Image_createWithResIDQuad(Resources.Img.ObjGhost, 4);
                float radius = MathF.Sqrt(9000);
                backCloud2.x = x + (radius * MathF.Cos(float.DegreesToRadians(170 + angle)));
                backCloud2.y = y + (radius * MathF.Sin(float.DegreesToRadians(170 + angle)));
                backCloud2.anchor = 18;
                backCloud2.visible = false;
                _ = AddChild(backCloud2);
                Timeline timeline = new Timeline().InitWithMaxKeyFramesOnTrack(5);
                timeline.SetTimelineLoopType(Timeline.LoopType.TIMELINE_REPLAY);
                timeline.AddKeyFrame(KeyFrame.MakeScale(0.7f, 0.7f, KeyFrame.TransitionType.FRAME_TRANSITION_IMMEDIATE, 0));
                timeline.AddKeyFrame(KeyFrame.MakeScale(0.55f, 0.55f, KeyFrame.TransitionType.FRAME_TRANSITION_EASE_IN, 0.35f));
                timeline.AddKeyFrame(KeyFrame.MakeScale(0.4f, 0.4f, KeyFrame.TransitionType.FRAME_TRANSITION_EASE_OUT, 0.35f));
                timeline.AddKeyFrame(KeyFrame.MakeScale(0.55f, 0.55f, KeyFrame.TransitionType.FRAME_TRANSITION_EASE_IN, 0.35f));
                timeline.AddKeyFrame(KeyFrame.MakeScale(0.7f, 0.7f, KeyFrame.TransitionType.FRAME_TRANSITION_EASE_OUT, 0.35f));
                timeline.AddKeyFrame(KeyFrame.MakePos((int)(backCloud2.x + 1f), (int)(backCloud2.y + 1f), KeyFrame.TransitionType.FRAME_TRANSITION_IMMEDIATE, 0));
                timeline.AddKeyFrame(KeyFrame.MakePos((int)backCloud2.x, (int)backCloud2.y, KeyFrame.TransitionType.FRAME_TRANSITION_EASE_IN, 0.35f));
                timeline.AddKeyFrame(KeyFrame.MakePos((int)(backCloud2.x - 1f), (int)(backCloud2.y - 1f), KeyFrame.TransitionType.FRAME_TRANSITION_EASE_OUT, 0.35f));
                timeline.AddKeyFrame(KeyFrame.MakePos((int)backCloud2.x, (int)backCloud2.y, KeyFrame.TransitionType.FRAME_TRANSITION_EASE_IN, 0.35f));
                timeline.AddKeyFrame(KeyFrame.MakePos((int)(backCloud2.x + 1f), (int)(backCloud2.y + 1f), KeyFrame.TransitionType.FRAME_TRANSITION_EASE_OUT, 0.35f));
                backCloud2.AddTimelinewithID(timeline, 0);
                backCloud2.PlayTimeline(0);

                backCloud = Image_createWithResIDQuad(Resources.Img.ObjGhost, 4);
                float radius2 = MathF.Sqrt(9000);
                backCloud.x = x + (radius2 * MathF.Cos(float.DegreesToRadians(10 + angle)));
                backCloud.y = y + (radius2 * MathF.Sin(float.DegreesToRadians(10 + angle)));
                backCloud.anchor = 18;
                backCloud.visible = false;
                _ = AddChild(backCloud);
                Timeline backCloudTimeline = new Timeline().InitWithMaxKeyFramesOnTrack(5);
                backCloudTimeline.SetTimelineLoopType(Timeline.LoopType.TIMELINE_REPLAY);
                backCloudTimeline.AddKeyFrame(KeyFrame.MakeScale(0.9f, 0.9f, KeyFrame.TransitionType.FRAME_TRANSITION_IMMEDIATE, 0));
                backCloudTimeline.AddKeyFrame(KeyFrame.MakeScale(0.8f, 0.8f, KeyFrame.TransitionType.FRAME_TRANSITION_EASE_IN, 0.39f));
                backCloudTimeline.AddKeyFrame(KeyFrame.MakeScale(0.7f, 0.7f, KeyFrame.TransitionType.FRAME_TRANSITION_EASE_OUT, 0.39f));
                backCloudTimeline.AddKeyFrame(KeyFrame.MakeScale(0.8f, 0.8f, KeyFrame.TransitionType.FRAME_TRANSITION_EASE_IN, 0.39f));
                backCloudTimeline.AddKeyFrame(KeyFrame.MakeScale(0.9f, 0.9f, KeyFrame.TransitionType.FRAME_TRANSITION_EASE_OUT, 0.39f));
                backCloudTimeline.AddKeyFrame(KeyFrame.MakePos((int)(backCloud.x + 1f), (int)(backCloud.y + 1f), KeyFrame.TransitionType.FRAME_TRANSITION_IMMEDIATE, 0));
                backCloudTimeline.AddKeyFrame(KeyFrame.MakePos((int)backCloud.x, (int)backCloud.y, KeyFrame.TransitionType.FRAME_TRANSITION_EASE_IN, 0.39f));
                backCloudTimeline.AddKeyFrame(KeyFrame.MakePos((int)(backCloud.x - 1f), (int)(backCloud.y - 1f), KeyFrame.TransitionType.FRAME_TRANSITION_EASE_OUT, 0.39f));
                backCloudTimeline.AddKeyFrame(KeyFrame.MakePos((int)backCloud.x, (int)backCloud.y, KeyFrame.TransitionType.FRAME_TRANSITION_EASE_IN, 0.39f));
                backCloudTimeline.AddKeyFrame(KeyFrame.MakePos((int)(backCloud.x + 1f), (int)(backCloud.y + 1f), KeyFrame.TransitionType.FRAME_TRANSITION_EASE_OUT, 0.39f));
                backCloud.AddTimelinewithID(backCloudTimeline, 0);
                backCloud.PlayTimeline(0);

                Image frontCloud = Image_createWithResIDQuad(Resources.Img.ObjGhost, 3);
                frontCloud.x = x + 60f;
                frontCloud.y = y + 55f;
                frontCloud.anchor = 18;
                //frontCloud.DoRestoreCutTransparency();
                _ = AddChild(frontCloud);
                Timeline frontCloudTimeline = new Timeline().InitWithMaxKeyFramesOnTrack(5);
                frontCloudTimeline.SetTimelineLoopType(Timeline.LoopType.TIMELINE_REPLAY);
                frontCloudTimeline.AddKeyFrame(KeyFrame.MakeScale(1.1f, 1.1f, KeyFrame.TransitionType.FRAME_TRANSITION_IMMEDIATE, 0));
                frontCloudTimeline.AddKeyFrame(KeyFrame.MakeScale(1f, 1f, KeyFrame.TransitionType.FRAME_TRANSITION_EASE_IN, 0.45f));
                frontCloudTimeline.AddKeyFrame(KeyFrame.MakeScale(0.9f, 0.9f, KeyFrame.TransitionType.FRAME_TRANSITION_EASE_OUT, 0.45f));
                frontCloudTimeline.AddKeyFrame(KeyFrame.MakeScale(1f, 1f, KeyFrame.TransitionType.FRAME_TRANSITION_EASE_IN, 0.45f));
                frontCloudTimeline.AddKeyFrame(KeyFrame.MakeScale(1.1f, 1.1f, KeyFrame.TransitionType.FRAME_TRANSITION_EASE_OUT, 0.45f));
                frontCloudTimeline.AddKeyFrame(KeyFrame.MakePos((int)(frontCloud.x + 1f), (int)(frontCloud.y + 1f), KeyFrame.TransitionType.FRAME_TRANSITION_IMMEDIATE, 0));
                frontCloudTimeline.AddKeyFrame(KeyFrame.MakePos((int)frontCloud.x, (int)frontCloud.y, KeyFrame.TransitionType.FRAME_TRANSITION_EASE_IN, 0.45f));
                frontCloudTimeline.AddKeyFrame(KeyFrame.MakePos((int)(frontCloud.x - 1f), (int)(frontCloud.y - 1f), KeyFrame.TransitionType.FRAME_TRANSITION_EASE_OUT, 0.45f));
                frontCloudTimeline.AddKeyFrame(KeyFrame.MakePos((int)frontCloud.x, (int)frontCloud.y, KeyFrame.TransitionType.FRAME_TRANSITION_EASE_IN, 0.45f));
                frontCloudTimeline.AddKeyFrame(KeyFrame.MakePos((int)(frontCloud.x + 1f), (int)(frontCloud.y + 1f), KeyFrame.TransitionType.FRAME_TRANSITION_EASE_OUT, 0.45f));
                frontCloud.AddTimelinewithID(frontCloudTimeline, 0);
                frontCloud.PlayTimeline(0);

                Image bigCloud = Image_createWithResIDQuad(Resources.Img.ObjGhost, 2);
                bigCloud.x = x - 50f;
                bigCloud.y = y + 55f;
                bigCloud.anchor = 18;
                //bigCloud.DoRestoreCutTransparency();
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
            }
            return this;
        }

        /// <inheritdoc />
        public override void PlayTimeline(int timelineIndex)
        {
            if (CurrentTimelineIndex == 11)
            {
                return;
            }
            if (timelineIndex != 11 && CurrentTimelineIndex == 10 && GetCurrentTimeline().state != Timeline.TimelineState.TIMELINE_STOPPED)
            {
                color = RGBAColor.solidOpaqueRGBA;
            }
            base.PlayTimeline(timelineIndex);
        }

        /// <inheritdoc />
        public override void Draw()
        {
            backCloud.Draw();
            backCloud2.Draw();
            base.Draw();
        }

        /// <inheritdoc />
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                backCloud = null;
                backCloud2 = null;
            }
            base.Dispose(disposing);
        }

        /// <summary>Primary background ghost cloud element.</summary>
        public Image backCloud;

        /// <summary>Secondary background ghost cloud element.</summary>
        public Image backCloud2;
    }
}
