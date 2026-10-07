using static CutTheRopeDX.Framework.Helpers.MathHelper;

namespace CutTheRopeDX.GameMain
{
    /// <summary>
    /// The sound a menu or pause button makes when it is pressed: the classic tap, or in the Time
    /// Travel menus one of its two taps at random.
    /// </summary>
    internal static class TapSounds
    {
        /// <summary>Picks the tap sound for the next press.</summary>
        /// <returns>The sound resource name.</returns>
        public static string Next()
        {
            return !MenuTheme.IsTimeTravel
                ? Resources.Snd.Tap
                : RND_RANGE(0, 1) == 0 ? Resources.Snd.TapTimeTravel1 : Resources.Snd.TapTimeTravel2;
        }
    }
}
