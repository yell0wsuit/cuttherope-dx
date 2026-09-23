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
    }
}
