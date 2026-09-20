using System;

namespace CutTheRopeDX.Framework.Visual
{
    /// <summary>
    /// Represents a single action that can be dispatched to a <see cref="BaseElement"/> during timeline playback.
    /// </summary>
    internal sealed class TimelineAction : FrameworkTypes
    {
        /// <summary>
        /// Initializes a new <see cref="TimelineAction"/> with empty action data.
        /// </summary>
        public TimelineAction()
        {
            data = new ActionData();
        }

        /// <summary>
        /// Creates an <paramref name="action"/> targeting <paramref name="target"/> with integer parameters.
        /// </summary>
        /// <param name="target">Element that will handle the action.</param>
        /// <param name="action">Action name.</param>
        /// <param name="p">Primary integer parameter.</param>
        /// <param name="sp">Secondary integer parameter.</param>
        /// <returns>The created action instance.</returns>
        public static TimelineAction CreateAction(BaseElement target, string action, int p, int sp)
        {
            TimelineAction timelineAction = new()
            {
                actionTarget = target
            };
            timelineAction.data.actionName = action;
            timelineAction.data.actionParam = p;
            timelineAction.data.actionSubParam = sp;
            timelineAction.data.actionParamFloat = p;
            timelineAction.data.actionSubParamFloat = sp;
            return timelineAction;
        }

        /// <summary>
        /// Creates an <paramref name="action"/> targeting <paramref name="target"/> with float parameters.
        /// </summary>
        /// <param name="target">Element that will handle the action.</param>
        /// <param name="action">Action name.</param>
        /// <param name="p">Primary float parameter.</param>
        /// <param name="sp">Secondary float parameter.</param>
        /// <returns>The created action instance.</returns>
        public static TimelineAction CreateAction(BaseElement target, string action, float p, float sp)
        {
            TimelineAction timelineAction = new()
            {
                actionTarget = target
            };
            timelineAction.data.actionName = action;
            timelineAction.data.actionParam = (int)MathF.Round(p);
            timelineAction.data.actionSubParam = (int)MathF.Round(sp);
            timelineAction.data.actionParamFloat = p;
            timelineAction.data.actionSubParamFloat = sp;
            return timelineAction;
        }

        /// <summary>
        /// Element that will receive and handle this action.
        /// </summary>
        public BaseElement actionTarget;

        /// <summary>
        /// Action name and parameters.
        /// </summary>
        public ActionData data;
    }
}
