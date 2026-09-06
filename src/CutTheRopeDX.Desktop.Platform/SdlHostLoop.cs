using System;

namespace CutTheRopeDX.Desktop.Platform
{
    /// <summary>Fixed desktop updates followed by one draw, including bounded slow-frame catch-up.</summary>
    public sealed class SdlHostLoop
    {
        /// <summary>
        /// Wall-clock ticks one scheduled update stands for. The gameplay delta below is the
        /// rounded value the simulation has always been fed, so the two are deliberately not
        /// the same number.
        /// </summary>
        public const long ScheduledUpdateTicks = 166666;

        /// <summary>Milliseconds handed to gameplay for each scheduled update.</summary>
        public const float GameplayMilliseconds = 16f;

        /// <summary>
        /// The longest span a single <see cref="Advance"/> may account for. A frame slower than
        /// this drops the excess instead of spending the next frames replaying it.
        /// </summary>
        private const long MaximumCatchupTicks = TimeSpan.TicksPerSecond / 2;

        private TimeSpan last;
        private long accumulated;
        private bool initialized;
        private bool suspended;

        /// <summary>Starts accounting from <paramref name="now"/>, discarding any accumulated debt.</summary>
        /// <param name="now">The host clock's current reading.</param>
        public void Reset(TimeSpan now)
        {
            last = now;
            accumulated = 0;
            initialized = true;
        }

        /// <summary>
        /// Suspends or resumes stepping. Either direction resets accounting, so time spent
        /// unfocused is never replayed as catch-up on the way back.
        /// </summary>
        /// <param name="value">Whether the loop is suspended.</param>
        /// <param name="now">The host clock's current reading.</param>
        public void SetSuspended(bool value, TimeSpan now)
        {
            suspended = value;
            Reset(now);
        }

        /// <summary>
        /// Runs the updates <paramref name="now"/> has earned, then draws once if any ran.
        /// </summary>
        /// <param name="now">The host clock's current reading.</param>
        /// <param name="update">Advances gameplay by <see cref="GameplayMilliseconds"/>.</param>
        /// <param name="draw">Renders one frame after the batch of updates.</param>
        /// <returns>How many updates ran.</returns>
        public int Advance(TimeSpan now, Action<float> update, Action draw)
        {
            ArgumentNullException.ThrowIfNull(update);
            ArgumentNullException.ThrowIfNull(draw);

            if (!initialized)
            {
                Reset(now);
                return 0;
            }

            long elapsed = Math.Clamp((now - last).Ticks, 0, MaximumCatchupTicks);
            last = now;
            if (suspended)
            {
                accumulated = 0;
                return 0;
            }

            accumulated = Math.Min(accumulated + elapsed, MaximumCatchupTicks);
            int count = 0;
            while (accumulated >= ScheduledUpdateTicks)
            {
                accumulated -= ScheduledUpdateTicks;
                update(GameplayMilliseconds);
                count++;

                // An update can suspend the loop itself, by losing focus or quitting. Stop the
                // catch-up batch there rather than stepping a host that has already gone away.
                if (suspended)
                {
                    break;
                }
            }

            if (count > 0 && !suspended)
            {
                draw();
            }

            return count;
        }
    }
}
