using System;

using SkiaSharp;

namespace CutTheRopeDX.Rendering.Skia.Tests
{
    /// <summary>
    /// A CPU-backed stand-in for a host surface, so the shared renderer's pixel output can be
    /// asserted without a GPU context. <see cref="Context"/> is null, which the renderer treats
    /// as "no GPU resources to create".
    /// </summary>
    internal sealed class FakeSkiaSurface : ISkiaSurface, IDisposable
    {
        private const int Extent = 128;

        private readonly SKSurface _surface = SKSurface.Create(new SKImageInfo(Extent, Extent));

        public SKCanvas Canvas => _surface.Canvas;

        public GRContext Context => null;

        public int Width => Extent;

        public int Height => Extent;

        /// <summary>Number of times the renderer submitted a completed frame.</summary>
        public int Submissions { get; private set; }

        public void Flush()
        {
            Submissions++;
            Canvas.Flush();
        }

        /// <summary>Snapshots what has been drawn so far.</summary>
        public SKBitmap Pixels()
        {
            using SKImage image = _surface.Snapshot();
            return SKBitmap.FromImage(image);
        }

        public void Dispose()
        {
            _surface.Dispose();
        }
    }
}
