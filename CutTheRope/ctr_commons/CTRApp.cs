using CutTheRope.iframework.core;
using CutTheRope.ios;
using System;

namespace CutTheRope.ctr_commons
{
    internal class CTRApp : Application
    {
        public override void dealloc()
        {
            throw new NotImplementedException();
        }

        public virtual void ApplicationWillTerminate(UIApplication application)
        {
            sharedPreferences().savePreferences();
        }

        public virtual void ApplicationDidReceiveMemoryWarning(UIApplication application)
        {
            throw new NotImplementedException();
        }

        public virtual void ChallengeStartedWithGameConfig(NSString gameConfig)
        {
            throw new NotImplementedException();
        }

        public virtual void ApplicationWillResignActive(UIApplication application)
        {
            sharedPreferences().savePreferences();
            if (root != null && !root.isSuspended())
            {
                root.suspend();
            }
        }

        public virtual void ApplicationDidBecomeActive(UIApplication application)
        {
            if (root != null && root.isSuspended())
            {
                root.resume();
            }
        }
    }
}
