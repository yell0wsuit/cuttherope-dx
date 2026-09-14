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
        private long preparedPixelBytes;
        private long peakPreparedPixelBytes;

        /// <summary>
        /// How many decodes to run at once: one fewer than the machine's cores, and never more than
        /// two, so decoding leaves room for the game and other background work on weaker devices.
        /// </summary>
        public static int DefaultConcurrency => Math.Clamp(Environment.ProcessorCount - 1, 1, 2);

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
                SKImage image = entry.Decode.GetAwaiter().GetResult();
                ReleasePreparedBytes(image);
                return image;
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
        /// Reports the most decoded pixel memory held for images not yet taken since the last call,
        /// and starts the next reading from what is held now.
        /// </summary>
        /// <remarks>
        /// Counts decoded pixels only: not the decoder's scratch memory, the encoded file bytes, or
        /// the GPU textures the images become.
        /// </remarks>
        /// <returns>Peak decoded pixel bytes awaiting upload.</returns>
        public long TakePeakPreparedPixelBytes()
        {
            return Interlocked.Exchange(ref peakPreparedPixelBytes, Interlocked.Read(ref preparedPixelBytes));
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
                SKImage image = decode(path);
                HoldPreparedBytes(image);
                return image;
            }
            finally
            {
                _ = slots.Release();
            }
        }

        private void Abandon(PendingDecode entry)
        {
            entry.Cancellation.Cancel();
            _ = entry.Decode.ContinueWith(
                finished =>
                {
                    if (finished.IsCompletedSuccessfully && finished.Result != null)
                    {
                        ReleasePreparedBytes(finished.Result);
                        finished.Result.Dispose();
                    }

                    entry.Cancellation.Dispose();
                },
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }

        private void HoldPreparedBytes(SKImage image)
        {
            if (image == null)
            {
                return;
            }

            long held = Interlocked.Add(ref preparedPixelBytes, image.Info.BytesSize64);
            long peak = Interlocked.Read(ref peakPreparedPixelBytes);
            while (held > peak)
            {
                long seen = Interlocked.CompareExchange(ref peakPreparedPixelBytes, held, peak);
                if (seen == peak)
                {
                    break;
                }

                peak = seen;
            }
        }

        private void ReleasePreparedBytes(SKImage image)
        {
            if (image != null)
            {
                _ = Interlocked.Add(ref preparedPixelBytes, -image.Info.BytesSize64);
            }
        }

        /// <summary>A decode in progress and the means to call it off before it starts.</summary>
        private readonly record struct PendingDecode(Task<SKImage> Decode, CancellationTokenSource Cancellation);
    }
}
