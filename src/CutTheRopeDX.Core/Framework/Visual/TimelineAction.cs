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
            TimelineAction action2 = new()
            {
                actionTarget = target
            };
            action2.data.actionName = action;
            action2.data.actionParam = p;
            action2.data.actionSubParam = sp;
            action2.data.actionParamFloat = p;
            action2.data.actionSubParamFloat = sp;
            return action2;
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
            TimelineAction action2 = new()
            {
                actionTarget = target
            };
            action2.data.actionName = action;
            action2.data.actionParam = (int)MathF.Round(p);
            action2.data.actionSubParam = (int)MathF.Round(sp);
            action2.data.actionParamFloat = p;
            action2.data.actionSubParamFloat = sp;
            return action2;
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
