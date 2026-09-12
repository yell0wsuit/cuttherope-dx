using System;
using System.Collections.Generic;

namespace CutTheRopeDX.Rendering.Skia
{
    /// <summary>
    /// Which generation of graphics device the live Skia resources belong to, and what a
    /// replacement device has to rebuild.
    /// </summary>
    /// <remarks>
    /// Every GPU-resident resource belongs to the context that uploaded it, so a device that goes
    /// away takes all of them with it. Drawing one afterwards reads freed native memory, which is
    /// why each resource carries the generation it was made in and is refused once that generation
    /// is retired: the check is cheap, and the alternative is a crash somewhere inside Skia with
    /// nothing left to point at the cause.
    /// <para>
    /// Resources are separated by whether they can be made again. A file asset is described by its
    /// content path and is reloaded from disk, so it survives any number of losses. A capture of a
    /// frame that has already gone exists nowhere else and is simply dropped; the code that reads
    /// those captures already copes with their absence, because a transition can begin before one
    /// has been taken.
    /// </para>
    /// </remarks>
    public sealed class SkiaResourceRegistry
    {
        /// <summary>
        /// The stamp for a resource no device owns. It stays valid across every loss, and is what
        /// a resource carries when nothing is tracking generations at all.
        /// </summary>
        public const int DeviceIndependent = 0;

        private readonly HashSet<string> durable = [];

        /// <summary>The generation live resources belong to. The first device owns generation 1.</summary>
        public int Generation { get; private set; } = 1;

        /// <summary>Content paths a replacement device would have to load again.</summary>
        public IReadOnlyCollection<string> DurableDescriptions => durable;

        /// <summary>How many captures would be lost with the current device.</summary>
        public int TransientCount { get; private set; }

        /// <summary>Records a resource that a file asset can rebuild.</summary>
        /// <param name="description">Content path the resource was loaded from.</param>
        /// <returns>The generation to stamp the resource with.</returns>
        public int TrackDurable(string description)
        {
            ArgumentException.ThrowIfNullOrEmpty(description);
            _ = durable.Add(description);
            return Generation;
        }

        /// <summary>Records a capture of the frame, which nothing can rebuild.</summary>
        /// <returns>The generation to stamp the resource with.</returns>
        public int TrackTransient()
        {
            TransientCount++;
            return Generation;
        }

        /// <summary>Drops a file asset that was freed before any loss.</summary>
        /// <param name="description">Content path the resource was loaded from.</param>
        public void Forget(string description)
        {
            _ = durable.Remove(description);
        }

        /// <summary>Drops a capture that was released before any loss.</summary>
        public void ForgetTransient()
        {
            if (TransientCount > 0)
            {
                TransientCount--;
            }
        }

        /// <summary>Whether a resource stamped with <paramref name="generation"/> is still drawable.</summary>
        /// <param name="generation">The stamp the resource was created with.</param>
        public bool IsCurrent(int generation)
        {
            return generation == DeviceIndependent || generation == Generation;
        }

        /// <summary>
        /// Retires the current generation and reports the file assets the next device needs.
        /// </summary>
        /// <returns>Content paths to load again, in no particular order.</returns>
        public IReadOnlyList<string> Invalidate()
        {
            string[] rebuild = [.. durable];
            durable.Clear();
            TransientCount = 0;
            Generation++;
            return rebuild;
        }
    }
}
