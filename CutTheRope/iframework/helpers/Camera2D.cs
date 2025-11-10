using CutTheRope.desktop;
using CutTheRope.iframework.core;
using CutTheRope.ios;

namespace CutTheRope.iframework.helpers
{
    internal class Camera2D : NSObject
    {
        public virtual Camera2D InitWithSpeedandType(float s, CAMERATYPE t)
        {
            if (base.Init() != null)
            {
                speed = s;
                type = t;
            }
            return this;
        }

        public virtual void MoveToXYImmediate(float x, float y, bool immediate)
        {
            target.x = x;
            target.y = y;
            if (immediate)
            {
                pos = target;
                return;
            }
            if (type == CAMERATYPE.CAMERASPEEDDELAY)
            {
                offset = VectMult(VectSub(target, pos), speed);
                return;
            }
            if (type == CAMERATYPE.CAMERASPEEDPIXELS)
            {
                offset = VectMult(VectNormalize(VectSub(target, pos)), speed);
            }
        }

        public virtual void Update(float delta)
        {
            if (!VectEqual(pos, target))
            {
                pos = VectAdd(pos, VectMult(offset, delta));
                pos = Vect(Round(pos.x), Round(pos.y));
                if (!SameSign(offset.x, target.x - pos.x) || !SameSign(offset.y, target.y - pos.y))
                {
                    pos = target;
                }
            }
        }

        public virtual void ApplyCameraTransformation()
        {
            OpenGL.GlTranslatef((double)(0f - pos.x), (double)(0f - pos.y), 0.0);
        }

        public virtual void CancelCameraTransformation()
        {
            OpenGL.GlTranslatef(pos.x, pos.y, 0.0);
        }

        public CAMERATYPE type;

        public float speed;

        public Vector pos;

        public Vector target;

        public Vector offset;
    }
}
