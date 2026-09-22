using System.Collections.Generic;

using CutTheRopeDX.Framework;
using CutTheRopeDX.Framework.Helpers;
using CutTheRopeDX.Framework.Visual;

namespace CutTheRopeDX.GameMain
{
    /// <summary>
    /// Om Nom character animation container that switches between the base texture and named child animations.
    /// </summary>
    internal sealed class CharAnimations : GameObject
    {
        /// <summary>
        /// Adds a named child Om Nom character animation image to the container.
        /// </summary>
        /// <param name="resourceName">Texture resource name for the child animation.</param>
        public void AddImage(string resourceName)
        {
            animations ??= [];
            animationNameToIndex ??= [];

            CharAnimationLayer layer = InitializeFromResource(new CharAnimationLayer(), resourceName);
            // Use the same anchor as the base animation (18) for proper centering
            layer.parentAnchor = layer.anchor = anchor;
            layer.DoRestoreCutTransparency();

            int index = nextAnimationIndex++;
            animations.Add(layer);
            animationNameToIndex[resourceName] = index;
            _ = AddChild(layer);
            layer.SetEnabled(false);
        }

        /// <inheritdoc />
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (animations != null)
                {
                    foreach (CharAnimationLayer animation in animations)
                    {
                        animation?.Dispose();
                    }
                    animations.Clear();
                    animations = null;
                    nextAnimationIndex = 0;
                }
                animationNameToIndex?.Clear();
                animationNameToIndex = null;
            }
            base.Dispose(disposing);
        }

        /// <summary>
        /// Adds a frame animation to the base animation or a named child animation.
        /// </summary>
        /// <param name="resourceName">Texture resource name that identifies the target animation.</param>
        /// <param name="aid">Timeline ID to assign to the animation.</param>
        /// <param name="d">Delay between animation frames, in seconds.</param>
        /// <param name="l">Loop behavior for the timeline.</param>
        /// <param name="s">First frame index in the animation range.</param>
        /// <param name="e">Last frame index in the animation range.</param>
        public void AddAnimationWithIDDelayLoopFirstLast(string resourceName, int aid, float d, Timeline.LoopType l, int s, int e)
        {
            if (resourceName == Resources.Img.CharAnimations)
            {
                AddAnimationWithIDDelayLoopFirstLast(aid, d, l, s, e);
            }
            else if (animationNameToIndex != null && animationNameToIndex.TryGetValue(resourceName, out int index))
            {
                animations[index].AddAnimationWithIDDelayLoopFirstLast(aid, d, l, s, e);
            }
        }

        /// <summary>
        /// Gets the base animation or a named child animation by texture resource name.
        /// </summary>
        /// <param name="resourceName">Texture resource name that identifies the animation.</param>
        /// <returns>The matching animation, or <see langword="null"/> when no matching child animation exists.</returns>
        public Animation GetAnimation(string resourceName)
        {
            return resourceName == Resources.Img.CharAnimations
                ? this
                : animationNameToIndex != null && animationNameToIndex.TryGetValue(resourceName, out int index)
                ? animations[index]
                : null;
        }

        /// <summary>
        /// Adds an action keyframe that switches from one animation to another after a delay.
        /// </summary>
        /// <param name="resourceName2">Texture resource name for the destination animation.</param>
        /// <param name="a2">Timeline ID to play on the destination animation.</param>
        /// <param name="resourceName1">Texture resource name for the source animation.</param>
        /// <param name="a1">Timeline ID on the source animation that receives the switch action.</param>
        /// <param name="d">Delay before switching animations, in seconds.</param>
        public void SwitchToAnimationatEndOfAnimationDelay(string resourceName2, int a2, string resourceName1, int a1, float d)
        {
            Animation currentAnimation = GetAnimation(resourceName1);
            Animation nextAnimation = GetAnimation(resourceName2);
            Timeline timeline = currentAnimation.GetTimeline(a1);
            List<TimelineAction> dynamicArray = [];
            // Check if resourceName1 refers to the base currentAnimation (CharAnimations)
            bool isBaseAnimation = resourceName1 == Resources.Img.CharAnimations;
            dynamicArray.Add(TimelineAction.CreateAction(nextAnimation, "ACTION_PLAY_TIMELINE", isBaseAnimation ? 1 : 0, a2));
            if (currentAnimation != nextAnimation)
            {
                dynamicArray.Add(TimelineAction.CreateAction(nextAnimation, "ACTION_SET_UPDATEABLE", 1, 1));
                dynamicArray.Add(TimelineAction.CreateAction(nextAnimation, "ACTION_SET_VISIBLE", 1, 1));
                dynamicArray.Add(TimelineAction.CreateAction(nextAnimation, "ACTION_SET_TOUCHABLE", 1, 1));
                dynamicArray.Add(TimelineAction.CreateAction(currentAnimation, "ACTION_SET_UPDATEABLE", 0, 0));
                dynamicArray.Add(TimelineAction.CreateAction(currentAnimation, "ACTION_SET_VISIBLE", 0, 0));
                dynamicArray.Add(TimelineAction.CreateAction(currentAnimation, "ACTION_SET_TOUCHABLE", 0, 0));
            }
            timeline.AddKeyFrame(KeyFrame.MakeAction(dynamicArray, d));
        }

        /// <summary>
        /// Plays a timeline on the base currentAnimation or a named child currentAnimation.
        /// </summary>
        /// <param name="resourceName">Texture resource name that identifies the currentAnimation to play.</param>
        /// <param name="t">Timeline ID to play.</param>
        public void PlayAnimationtimeline(string resourceName, int t)
        {
            if (GetCurrentTimeline() != null)
            {
                StopCurrentTimeline();
            }
            foreach (CharAnimationLayer anim in animations)
            {
                anim.SetEnabled(false);
            }
            Animation currentAnimation = GetAnimation(resourceName);
            currentAnimation.SetEnabled(true);
            color = currentAnimation == this ? RGBAColor.solidOpaqueRGBA : RGBAColor.transparentRGBA;
            currentAnimation.PlayTimeline(t);
        }

        /// <inheritdoc />
        public override void PlayTimeline(int t)
        {
            foreach (CharAnimationLayer obj in animations)
            {
                obj.SetEnabled(false);
            }
            color = RGBAColor.solidOpaqueRGBA;
            base.PlayTimeline(t);
        }

        /// <summary>
        /// Animation layers managed by this character animation container.
        /// </summary>
        private List<CharAnimationLayer> animations;

        /// <summary>
        /// Maps animation names to their layer indexes.
        /// </summary>
        private Dictionary<string, int> animationNameToIndex;

        /// <summary>
        /// Next available animation layer index.
        /// </summary>
        private int nextAnimationIndex;
    }
}
