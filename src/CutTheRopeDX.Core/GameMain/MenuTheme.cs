using System;

namespace CutTheRopeDX.GameMain
{
    /// <summary>
    /// Which game's menus are drawn.
    /// </summary>
    internal enum MenuStyle
    {
        /// <summary>The Cut the Rope DX menus.</summary>
        Classic,

        /// <summary>The Cut the Rope: Experiments menus, laid out from the iOS HD build.</summary>
        Experiments,

        /// <summary>The Cut the Rope: Time Travel menus, laid out from the iOS HD build.</summary>
        TimeTravel,
    }

    /// <summary>
    /// The menu style chosen for this process. Set once at startup, before any controller is built,
    /// and read by every scene that has an Experiments variant.
    /// </summary>
    internal static class MenuTheme
    {
        /// <summary>Gets or sets the active menu style.</summary>
        public static MenuStyle Current { get; set; } = MenuStyle.Classic;

        /// <summary>Gets whether the Experiments menus are active.</summary>
        public static bool IsExperiments => Current == MenuStyle.Experiments;

        /// <summary>Gets whether the Time Travel menus are active.</summary>
        public static bool IsTimeTravel => Current == MenuStyle.TimeTravel;

        /// <summary>
        /// Picks between a classic value and its Experiments counterpart.
        /// </summary>
        /// <typeparam name="T">Value type.</typeparam>
        /// <param name="classic">Value used by the classic menus.</param>
        /// <param name="experiments">Value used by the Experiments menus.</param>
        /// <returns><paramref name="experiments"/> when the Experiments menus are active; otherwise <paramref name="classic"/>.</returns>
        public static T Select<T>(T classic, T experiments)
        {
            return IsExperiments ? experiments : classic;
        }

        /// <summary>
        /// Reads a menu style name as given on the command line or in the page URL.
        /// </summary>
        /// <param name="value">"classic", "experiments" or "timetravel", in any case.</param>
        /// <param name="style">The named style, or <see cref="MenuStyle.Classic"/> when <paramref name="value"/> names none.</param>
        /// <returns>Whether <paramref name="value"/> names a style.</returns>
        public static bool TryParse(string value, out MenuStyle style)
        {
            style = MenuStyle.Classic;
            if (string.Equals(value, "experiments", StringComparison.OrdinalIgnoreCase))
            {
                style = MenuStyle.Experiments;
                return true;
            }
            if (string.Equals(value, "timetravel", StringComparison.OrdinalIgnoreCase))
            {
                style = MenuStyle.TimeTravel;
                return true;
            }
            return string.Equals(value, "classic", StringComparison.OrdinalIgnoreCase);
        }
    }
}
