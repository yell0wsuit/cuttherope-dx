using System;

namespace CutTheRopeDX.Framework.Visual
{
    /// <summary>
    /// Interpolation curves in the four-argument form the animation timings are authored
    /// against: elapsed time, start value, change in value, duration.
    /// </summary>
    internal static class Easing
    {
        /// <summary>Decelerating exponential ease.</summary>
        /// <param name="t">Elapsed time.</param>
        /// <param name="b">Start value.</param>
        /// <param name="c">Change in value.</param>
        /// <param name="d">Duration.</param>
        /// <returns>The eased value.</returns>
        public static float OutExpo(float t, float b, float c, float d)
        {
            return t == d ? b + c : (c * (-MathF.Pow(2f, -10f * t / d) + 1f)) + b;
        }

        /// <summary>Exponential ease that accelerates, then decelerates.</summary>
        /// <param name="t">Elapsed time.</param>
        /// <param name="b">Start value.</param>
        /// <param name="c">Change in value.</param>
        /// <param name="d">Duration.</param>
        /// <returns>The eased value.</returns>
        public static float InOutExpo(float t, float b, float c, float d)
        {
            if (t == 0f)
            {
                return b;
            }
            if (t == d)
            {
                return b + c;
            }
            t /= d / 2f;
            if (t < 1f)
            {
                return (c / 2f * MathF.Pow(2f, 10f * (t - 1f))) + b;
            }
            t -= 1f;
            return (c / 2f * (-MathF.Pow(2f, -10f * t) + 2f)) + b;
        }

        /// <summary>
        /// Cubic ease that overshoots its target and settles back onto it. Named
        /// <c>easeOutBounce</c> in the web source, though the formula is back easing.
        /// </summary>
        /// <param name="t">Elapsed time.</param>
        /// <param name="b">Start value.</param>
        /// <param name="c">Change in value.</param>
        /// <param name="d">Duration.</param>
        /// <param name="s">Overshoot amount.</param>
        /// <returns>The eased value.</returns>
        public static float OutBack(float t, float b, float c, float d, float s)
        {
            t = (t / d) - 1f;
            return (c * ((t * t * (((s + 1f) * t) + s)) + 1f)) + b;
        }

        /// <summary>
        /// Back easing that overshoots at both ends. Named <c>easeInOutBounce</c> in the web
        /// source, though the formula is back easing.
        /// </summary>
        /// <param name="t">Elapsed time.</param>
        /// <param name="b">Start value.</param>
        /// <param name="c">Change in value.</param>
        /// <param name="d">Duration.</param>
        /// <param name="s">Overshoot amount, scaled by 1.525 internally.</param>
        /// <returns>The eased value.</returns>
        public static float InOutBack(float t, float b, float c, float d, float s)
        {
            t /= d / 2f;
            s *= 1.525f;
            if (t < 1f)
            {
                return (c / 2f * (t * t * (((s + 1f) * t) - s))) + b;
            }
            t -= 2f;
            return (c / 2f * ((t * t * (((s + 1f) * t) + s)) + 2f)) + b;
        }
    }
}
