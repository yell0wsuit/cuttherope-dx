using CutTheRopeDX.Framework.Helpers;

namespace CutTheRopeDX.GameMain
{
    /// <summary>
    /// Gameplay camera policy: where the camera sits inside the range the level gives it.
    /// </summary>
    internal static class GameplayCamera
    {
        /// <summary>
        /// Returns where along an axis's scrollable range the camera should sit, as a fraction.
        /// </summary>
        /// <remarks>
        /// The camera window is never scaled to contain the level, so what the window shows is all
        /// the camera shows and the level's reach past it is the whole of the range. An axis the
        /// level does not reach past has nowhere to go, and following the tracked point would only
        /// slide a view that already contains the level; that axis holds centered instead. The test
        /// is per axis because a level can exceed the window on one and not the other.
        /// </remarks>
        /// <param name="tracked">Where the tracking has driven the camera on this axis.</param>
        /// <param name="origin">World coordinate of the level's near edge on this axis.</param>
        /// <param name="scrollable">How far the camera window can travel across the level.</param>
        /// <returns>The anchor, 0 to 1, where 0.5 is centered.</returns>
        public static float Anchor(float tracked, float origin, float scrollable)
        {
            return scrollable > 0f
                ? CTRMathHelper.FIT_TO_BOUNDARIES((tracked - origin) / scrollable, 0f, 1f)
                : 0.5f;
        }
    }
}
