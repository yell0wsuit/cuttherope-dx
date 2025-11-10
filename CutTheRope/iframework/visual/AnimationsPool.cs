using System.Collections.Generic;

namespace CutTheRope.iframework.visual
{
    internal class AnimationsPool : BaseElement, ITimelineDelegate
    {
        public AnimationsPool()
        {
            _ = Init();
        }

        public virtual void TimelinereachedKeyFramewithIndex(Timeline t, KeyFrame k, int i)
        {
        }

        public virtual void TimelineFinished(Timeline t)
        {
            if (GetChildId(t.element) != -1)
            {
                removeList.Add(t.element);
            }
        }

        public override void Update(float delta)
        {
            int count = removeList.Count;
            for (int i = 0; i < count; i++)
            {
                RemoveChild(removeList[i]);
            }
            removeList.Clear();
            base.Update(delta);
        }

        public override void Draw()
        {
            base.Draw();
        }

        public virtual void ParticlesFinished(Particles p)
        {
            if (GetChildId(p) != -1)
            {
                removeList.Add(p);
            }
        }

        public override void Dealloc()
        {
            removeList.Clear();
            removeList = null;
            base.Dealloc();
        }

        private List<BaseElement> removeList = [];
    }
}
