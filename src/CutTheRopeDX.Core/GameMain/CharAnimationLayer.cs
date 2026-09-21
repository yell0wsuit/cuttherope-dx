using CutTheRopeDX.Framework;
using CutTheRopeDX.Framework.Visual;

namespace CutTheRopeDX.GameMain
{
    /// <summary>
    /// One selectable Om Nom character animation layer, handling the timeline-switch actions
    /// emitted by the animation graph.
    /// </summary>
    internal sealed class CharAnimationLayer : Animation
    {
        /// <inheritdoc />
        public override bool HandleAction(ActionData a)
        {
            if (a.actionName == "ACTION_PLAY_TIMELINE")
            {
                if (a.actionParam == 1)
                {
                    parent.color = RGBAColor.transparentRGBA;
                }
                PlayTimeline(a.actionSubParam);
                return true;
            }
            return base.HandleAction(a);
        }
    }
}
