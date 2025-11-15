using System;

using CutTheRope.iframework;

namespace CutTheRope.ios
{
    internal class NSObject : FrameworkTypes, IDisposable
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

        public virtual void Release()
        {
        }

        public virtual void Retain()
        {
        }

        public static void Free(object o)
        {
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~NSObject()
        {
            Dispose(false);
        }

        protected virtual void Dispose(bool disposing)
        {
        }
    }
}
