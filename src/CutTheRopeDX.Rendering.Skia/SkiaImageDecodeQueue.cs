using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using SkiaSharp;

namespace CutTheRopeDX.Rendering.Skia
{
    /// <summary>
    /// Decodes images on background threads ahead of the frame that uploads them.
    /// </summary>
    /// <remarks>
    /// Loading an image is a decode followed by an upload, and the decode is nearly all of it: a
    /// full-screen PNG takes tens of milliseconds to decode and a couple to upload. Only the decode
    /// can leave the render thread, since the GPU context belongs to that thread, so this holds the
    /// decoded CPU images until the render thread comes to take them. The images are CPU memory,
    /// which a lost graphics device does not affect.
    /// </remarks>
    /// <param name="decode">Reads and decodes one image into a CPU image; may throw.</param>
    /// <param name="concurrency">How many decodes may run at once.</param>
    internal sealed class SkiaImageDecodeQueue(Func<string, SKImage> decode, int concurrency) : IDisposable
    {
        private readonly SemaphoreSlim slots = new(Math.Max(1, concurrency));
        private readonly Dictionary<string, PendingDecode> pending = new(StringComparer.Ordinal);
        private readonly Lock gate = new();

        /// <summary>How many decodes to run at once on this machine, leaving a core for the game.</summary>
        public static int DefaultConcurrency => Math.Max(1, Environment.ProcessorCount - 1);

        /// <summary>Starts decoding an image unless it is already being decoded.</summary>
        /// <param name="path">Key the image is taken by.</param>
        public void Prepare(string path)
        {
            lock (gate)
            {
                if (!pending.ContainsKey(path))
                {
                    CancellationTokenSource cancellation = new();
                    CancellationToken token = cancellation.Token;
                    pending[path] = new PendingDecode(Task.Run(() => DecodeInSlotAsync(path, token)), cancellation);
                }
            }
        }

        /// <summary>
        /// Whether taking the image would return without waiting: its decode has finished, or
        /// nothing is decoding it at all.
        /// </summary>
        /// <param name="path">Key the image was prepared by.</param>
        /// <returns><see langword="true"/> when <see cref="Take"/> would not block.</returns>
        public bool IsReady(string path)
        {
            lock (gate)
            {
                return !pending.TryGetValue(path, out PendingDecode entry) || entry.Decode.IsCompleted;
            }
        }

        /// <summary>
        /// Hands over a prepared image, waiting for its decode if it is still running.
        /// </summary>
        /// <param name="path">Key the image was prepared by.</param>
        /// <returns>
        /// The decoded image, now owned by the caller, or <see langword="null"/> when the image was
        /// never prepared or its decode failed. Either way the caller loads it the ordinary way,
        /// which also reports a failure where it always has.
        /// </returns>
        public SKImage Take(string path)
        {
            PendingDecode entry;
            lock (gate)
            {
                if (!pending.Remove(path, out entry))
                {
                    return null;
                }
            }

            try
            {
                return entry.Decode.GetAwaiter().GetResult();
            }
            catch (Exception)
            {
                return null;
            }
            finally
            {
                entry.Cancellation.Dispose();
            }
        }

        /// <summary>
        /// Drops a prepared image nobody took. A decode still waiting for a slot never runs; one
        /// already running has its image released once it finishes.
        /// </summary>
        /// <param name="path">Key the image was prepared by.</param>
        public void Discard(string path)
        {
            PendingDecode entry;
            lock (gate)
            {
                if (!pending.Remove(path, out entry))
                {
                    return;
                }
            }

            Abandon(entry);
        }

        /// <summary>
        /// Reads nothing itself: turns encoded bytes into a CPU image with its pixels decoded.
        /// </summary>
        /// <param name="bytes">Encoded PNG or WebP bytes.</param>
        /// <returns>The decoded image.</returns>
        /// <exception cref="InvalidDataException">The bytes are not an image Skia can decode.</exception>
        public static SKImage DecodeRaster(byte[] bytes)
        {
            using SKData data = SKData.CreateCopy(bytes);
            SKImage encoded = SKImage.FromEncodedData(data)
                ?? throw new InvalidDataException("Could not decode the image.");
            SKImage raster;
            try
            {
                raster = encoded.ToRasterImage(ensurePixelData: true)
                    ?? throw new InvalidDataException("Could not decode the image's pixels.");
            }
            catch
            {
                encoded.Dispose();
                throw;
            }

            // The conversion hands back the source itself when it already holds decoded pixels,
            // and SkiaSharp maps one native handle to one managed instance.
            if (!ReferenceEquals(raster, encoded))
            {
                encoded.Dispose();
            }

            return raster;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            List<PendingDecode> abandoned;
            lock (gate)
            {
                abandoned = [.. pending.Values];
                pending.Clear();
            }

            foreach (PendingDecode entry in abandoned)
            {
                Abandon(entry);
            }
        }

        /// <remarks>
        /// Waits for its slot without holding a pool thread, so decodes queued behind a busy slot
        /// cost nothing until they run, and a canceled one leaves the line without ever running.
        /// </remarks>
        private async Task<SKImage> DecodeInSlotAsync(string path, CancellationToken cancellation)
        {
            await slots.WaitAsync(cancellation).ConfigureAwait(false);
            try
            {
                cancellation.ThrowIfCancellationRequested();
                return decode(path);
            }
            finally
            {
                _ = slots.Release();
            }
        }

        private static void Abandon(PendingDecode entry)
        {
            entry.Cancellation.Cancel();
            _ = entry.Decode.ContinueWith(
                static (finished, state) =>
                {
                    if (finished.IsCompletedSuccessfully)
                    {
                        finished.Result?.Dispose();
                    }

                    ((CancellationTokenSource)state).Dispose();
                },
                entry.Cancellation,
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }

        /// <summary>A decode in progress and the means to call it off before it starts.</summary>
        private readonly record struct PendingDecode(Task<SKImage> Decode, CancellationTokenSource Cancellation);
    }
}
