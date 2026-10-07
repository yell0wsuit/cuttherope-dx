using System;

using CutTheRopeDX.GameMain;

namespace CutTheRopeDX.Tests
{
    /// <summary>
    /// Runs a test body on a fixed date, so seasonal dressing is decided by the test rather than
    /// by the day the suite runs.
    /// </summary>
    internal static class SeasonalDate
    {
        /// <summary>A date outside every seasonal event.</summary>
        public static readonly DateTime NoEvent = new(2026, 6, 15);

        /// <summary>A date in the Halloween event.</summary>
        public static readonly DateTime Halloween = new(2026, 10, 15);

        /// <summary>Runs <paramref name="body"/> with the events judged on <paramref name="date"/>.</summary>
        /// <param name="date">Date to judge the events on.</param>
        /// <param name="body">Test body.</param>
        public static void With(DateTime date, Action body)
        {
            DateTime? previous = SpecialEvents.PinnedDate;
            SpecialEvents.PinnedDate = date;
            try
            {
                body();
            }
            finally
            {
                SpecialEvents.PinnedDate = previous;
            }
        }
    }
}
