using System;

namespace CutTheRopeDX.Framework.Diagnostics
{
    /// <summary>
    /// Reads the two memory figures worth putting in a log.
    /// </summary>
    /// <remarks>
    /// Neither is exact, and that is the point: what a report needs is whether a level costs ten
    /// megabytes or four hundred, which both of these answer without the cost of a real profile.
    /// </remarks>
    internal static class MemoryReport
    {
        private const long BytesPerMegabyte = 1024 * 1024;

        /// <summary>
        /// Gets what the garbage collector believes is currently allocated, in megabytes.
        /// </summary>
        /// <remarks>Read without forcing a collection, so it is an upper bound on live data.</remarks>
        public static long ManagedMegabytes => GC.GetTotalMemory(false) / BytesPerMegabyte;

        /// <summary>
        /// Gets the process working set in megabytes, or zero where the platform will not say.
        /// </summary>
        /// <remarks>
        /// This counts the native side - textures, audio buffers, the driver's own allocations -
        /// which is most of what a game holds and none of what the managed figure sees.
        /// </remarks>
        public static long WorkingSetMegabytes
        {
            get
            {
                try
                {
                    return Environment.WorkingSet / BytesPerMegabyte;
                }
                catch (PlatformNotSupportedException)
                {
                    return 0;
                }
            }
        }
    }
}
