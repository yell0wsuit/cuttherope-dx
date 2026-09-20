using CutTheRopeDX.Framework;
using CutTheRopeDX.Framework.Core;
using CutTheRopeDX.Framework.Visual;

namespace CutTheRopeDX.GameMain
{
    /// <summary>
    /// Om Nom character animation that handles timeline-switch actions emitted by the animation graph.
    /// </summary>
    internal sealed class CharAnimation : Animation
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
