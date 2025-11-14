using CutTheRope.ios;

namespace CutTheRope.iframework.visual
{
    internal sealed class FormattedString : NSObject
    {
        public FormattedString InitWithStringAndWidth(string str, float w)
        {
            if (Init() != null)
            {
                string_ = (string)NSRET(str);
                width = w;
            }
            return this;
        }

        public override void Dealloc()
        {
            string_ = null;
            base.Dealloc();
        }

        public string string_;

        public float width;
    }
}
