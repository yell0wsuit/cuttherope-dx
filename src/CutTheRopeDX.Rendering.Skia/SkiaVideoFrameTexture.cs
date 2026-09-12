using System;
using System.Runtime.InteropServices;

using CutTheRopeDX.Framework.Platform;

using SkiaSharp;

namespace CutTheRopeDX.Rendering.Skia
{
    /// <summary>
    /// A movie frame held in one Skia bitmap that every decoded frame overwrites in place.
    /// </summary>
    /// <remarks>
    /// The buffer is allocated once for the movie's dimensions rather than per frame, so playback
    /// costs a single copy of the decoded pixels and no allocation at all. Frames are opaque, which
    /// is stated in the color type so Skia never has to reason about premultiplication.
    /// <para>
    /// The pixels live in a pinned managed array that the bitmap is pointed at, so the frame is
    /// copied once, into memory this handle owns for its whole life. Pointing the bitmap at the
    /// decoder's own buffer instead would leave Skia reading it while the decode thread refills it.
    /// </para>
    /// </remarks>
    internal sealed class SkiaVideoFrameTexture : IVideoFrameTexture
    {
        private const int BytesPerPixel = 4;

        private readonly byte[] frame;
        private GCHandle pin;

        /// <summary>Allocates the frame surface for one movie's dimensions.</summary>
        /// <param name="width">Frame width in pixels.</param>
        /// <param name="height">Frame height in pixels.</param>
        internal SkiaVideoFrameTexture(int width, int height)
        {
            Width = width;
            Height = height;
            frame = new byte[width * height * BytesPerPixel];
            pin = GCHandle.Alloc(frame, GCHandleType.Pinned);
            SKBitmap surface = new();
            if (!surface.InstallPixels(
                new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Opaque),
                pin.AddrOfPinnedObject()))
            {
                surface.Dispose();
                pin.Free();
                throw new InvalidOperationException($"Could not create a {width}x{height} video frame surface.");
            }

            Bitmap = surface;
        }

        /// <inheritdoc />
        public int Width { get; }

        /// <inheritdoc />
        public int Height { get; }

        /// <summary>The bitmap the host draws the current frame from.</summary>
        internal SKBitmap Bitmap { get; private set; }

        /// <inheritdoc />
        public void Update(ReadOnlySpan<byte> pixels)
        {
            ObjectDisposedException.ThrowIf(Bitmap == null, this);
            int expected = Width * Height * BytesPerPixel;
            if (pixels.Length != expected)
            {
                throw new ArgumentException(
                    $"A {Width}x{Height} frame needs exactly {expected} bytes, not {pixels.Length}.",
                    nameof(pixels));
            }

            pixels.CopyTo(frame);
            Bitmap.NotifyPixelsChanged();
        }

        /// <inheritdoc />
        public void Dispose()
        {
            if (Bitmap == null)
            {
                return;
            }

            // The bitmap reads the pinned array, so it stops pointing at it before the pin goes.
            Bitmap.Dispose();
            Bitmap = null;
            pin.Free();
        }
    }
}
