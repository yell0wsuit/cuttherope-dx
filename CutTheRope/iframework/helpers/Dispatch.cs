using CutTheRope.ios;

namespace CutTheRope.iframework.helpers
{
    internal class DispatchClass : NSObject
    {
        public virtual DispatchClass InitWithObjectSelectorParamafterDelay(DelayedDispatcher.DispatchFunc callThisFunc, NSObject p, float d)
        {
            callThis = callThisFunc;
            param = p;
            delay = d;
            return this;
        }

        public virtual void Dispatch()
        {
            callThis?.Invoke(param);
        }

        public float delay;

        public DelayedDispatcher.DispatchFunc callThis;

        public NSObject param;
    }
}
