using CutTheRope.iframework;

namespace CutTheRope.ios
{
    internal class NSObject : FrameworkTypes
    {
        public static void NSREL(object obj)
        {
        }

        public static object NSRET(object obj)
        {
            return obj;
        }

        public static string NSS(string s)
        {
            return s;
        }

        public virtual void Dealloc()
        {
        }

        public virtual void Release()
        {
        }

        public virtual void Retain()
        {
        }

        public static void Free(object o)
        {
        }
    }
}
