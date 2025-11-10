using CutTheRope.ios;

namespace CutTheRope.iframework.sfe
{
    internal class Constraint : NSObject
    {
        public ConstraintedPoint cp;

        public float restLength;

        public CONSTRAINT type;

        public enum CONSTRAINT
        {
            DISTANCE,
            NOT_MORE_THAN,
            NOT_LESS_THAN
        }
    }
}
