using System;

namespace CutTheRopeDX.GameMain
{
    /// <summary>
    /// Provides helper properties to determine whether seasonal events
    /// should be active based on the current system date.
    /// </summary>
    internal static class SpecialEvents
    {
        /// <summary>
        /// Date the events are judged by on this thread, or <see langword="null"/> for today.
        /// Tests pin it so a seasonal dressing does not depend on the day they run.
        /// </summary>
        [ThreadStatic]
        internal static DateTime? PinnedDate;

        /// <summary>Gets the date the events are judged by.</summary>
        private static DateTime Today => PinnedDate ?? DateTime.Now;

        #region Christmas event

        /// <summary>
        /// Gets a value indicating whether the current month is January.
        /// </summary>
        public static bool IsJanuary => Today.Month == 1;

        /// <summary>
        /// Gets a value indicating whether the Christmas event period is active.
        /// Includes December and January.
        /// </summary>
        public static bool IsXmas => Today.Month is 12 or 1;

        #endregion

        #region Halloween event

        /// <summary>
        /// Gets a value indicating whether the Halloween event period is active.
        /// </summary>
        public static bool IsHalloween => Today.Month is 10;

        #endregion
    }
}
