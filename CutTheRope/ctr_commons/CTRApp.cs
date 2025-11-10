using CutTheRope.iframework.core;
using CutTheRope.ios;
using System;

namespace CutTheRope.ctr_commons
{
    internal class CTRApp : Application
    {
        public override void Dealloc()
        {
            throw new NotImplementedException();
        }

        public virtual void ApplicationWillTerminate(UIApplication application)
        {
            SharedPreferences().savePreferences();
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
            SharedPreferences().savePreferences();
            if (root != null && !root.IsSuspended())
            {
                root.Suspend();
            }
        }

        public virtual void ApplicationDidBecomeActive(UIApplication application)
        {
            if (root != null && root.IsSuspended())
            {
                root.Resume();
            }
        }
    }
}
