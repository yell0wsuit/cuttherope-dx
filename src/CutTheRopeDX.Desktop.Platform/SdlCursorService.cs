using System;
using System.IO;

using CutTheRopeDX.Framework.Platform;

using SDL3;

using SkiaSharp;
namespace CutTheRopeDX.Desktop.Platform
{
    /// <summary>Owns the two native cursor variants, scaled in window units with a top-left hotspot.</summary>
    internal sealed class SdlCursorService(Action releaseButtons) : ICursorService, IDisposable
    {
        private SKBitmap normal, active;
        private nint normalCursor, activeCursor;
        private double currentScale;
        private bool enabled;
        public void Load(string normalPath, string activePath)
        {
            SKBitmap next = SKBitmap.Decode(normalPath) ?? throw new InvalidDataException(normalPath);
            SKBitmap nextActive;
            try { nextActive = SKBitmap.Decode(activePath) ?? throw new InvalidDataException(activePath); }
            catch { next.Dispose(); throw; }
            DisposeCursors(); normal?.Dispose(); active?.Dispose(); normal = next; active = nextActive; currentScale = 0;
        }
        public static (int Width, int Height) GetScaledSize(int width, int height, double scale, double dpi)
        {
            return !double.IsFinite(scale) || scale <= 0 || !double.IsFinite(dpi) || dpi <= 0
                ? throw new ArgumentOutOfRangeException(nameof(scale))
                : ((int Width, int Height))(Math.Max(1, (int)(width * scale / dpi)), Math.Max(1, (int)(height * scale / dpi)));
        }
        public void Update(double scale, bool pressed)
        {
            Update(scale, 1, pressed);
        }

        public void Update(double scale, double dpi, bool pressed)
        {
            if (!enabled || normal == null)
            {
                return;
            }

            double requested = scale / dpi;
            if (normalCursor == 0 || Math.Abs(requested - currentScale) >= 0.01)
            {
                nint next = Create(normal, scale, dpi);
                nint nextActive;
                try { nextActive = Create(active, scale, dpi); }
                catch { SDL.DestroyCursor(next); throw; }
                DisposeCursors(); normalCursor = next; activeCursor = nextActive; currentScale = requested;
            }
            if (!SDL.SetCursor(pressed ? activeCursor : normalCursor))
            {
                throw new InvalidOperationException(SDL.GetError());
            }

            _ = SDL.ShowCursor();
        }
        private static nint Create(SKBitmap bitmap, double scale, double dpi)
        {
            (int Width, int Height) = GetScaledSize(bitmap.Width, bitmap.Height, scale, dpi);
            using SKBitmap scaled = new(new SKImageInfo(Width, Height, SKColorType.Bgra8888, SKAlphaType.Unpremul));
            using (SKCanvas canvas = new(scaled))
            {
                canvas.DrawImage(SKImage.FromBitmap(bitmap), new SKRect(0, 0, Width, Height), new SKSamplingOptions(SKFilterMode.Linear));
            }

            nint surface = SDL.CreateSurfaceFrom(Width, Height, SDL.PixelFormat.ARGB8888, scaled.GetPixels(), scaled.RowBytes);
            if (surface == 0)
            {
                throw new InvalidOperationException(SDL.GetError());
            }

            try { nint cursor = SDL.CreateColorCursor(surface, 0, 0); return cursor != 0 ? cursor : throw new InvalidOperationException(SDL.GetError()); }
            finally { SDL.DestroySurface(surface); }
        }
        public void Enable(bool value)
        {
            enabled = value; _ = value ? SDL.ShowCursor() : SDL.HideCursor();
        }
        public void ReleaseButtons()
        {
            releaseButtons();
        }

        private void DisposeCursors()
        {
            if (normalCursor != 0) { _ = SDL.SetCursor(SDL.GetDefaultCursor()); SDL.DestroyCursor(normalCursor); normalCursor = 0; }
            if (activeCursor != 0) { SDL.DestroyCursor(activeCursor); activeCursor = 0; }
        }
        public void Dispose() { DisposeCursors(); normal?.Dispose(); active?.Dispose(); normal = null; active = null; }
    }
}
