using CutTheRope.ios;

namespace CutTheRope.iframework.visual
{
    internal class FormattedString : NSObject
    {
        public virtual FormattedString InitWithStringAndWidth(NSString str, float w)
        {
            if (base.Init() != null)
            {
                string_ = (NSString)NSRET(str);
                width = w;
            }
            return this;
        }

        public override void Dealloc()
        {
            string_ = null;
            base.Dealloc();
        }

        public NSString string_;

        public float width;
    }
}
