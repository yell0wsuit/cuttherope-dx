using System;
using System.IO;

using CutTheRopeDX.Framework.Platform;

using SDL3;

using SkiaSharp;

namespace CutTheRopeDX.Desktop.Platform
{
    /// <summary>Owns the two native cursor variants, at a fixed size with a top-left hotspot.</summary>
    /// <remarks>
    /// The size does not follow the window: a cursor is pointing hardware, and shrinking it with
    /// the art left it unreadable in small windows. The art is authored at twice the base size,
    /// so it rides along as the high-density image and SDL picks per display — Retina reps on
    /// macOS, the content scale on Windows and Wayland — instead of a thin dark outline being
    /// resampled away.
    /// </remarks>
    internal sealed class SdlCursorService(Action releaseButtons) : ICursorService, IDisposable
    {
        /// <summary>Window units per art pixel at a display scale of 1.</summary>
        public const double BaseScale = 0.5;

        private SKBitmap normal, active;
        private nint normalCursor, activeCursor;
        private bool enabled;
        public void Load(string normalPath, string activePath)
        {
            SKBitmap next = SKBitmap.Decode(normalPath) ?? throw new InvalidDataException(normalPath);
            SKBitmap nextActive;
            try { nextActive = SKBitmap.Decode(activePath) ?? throw new InvalidDataException(activePath); }
            catch { next.Dispose(); throw; }
            DisposeCursors(); normal?.Dispose(); active?.Dispose(); normal = next; active = nextActive;
        }
        public static (int Width, int Height) GetBaseSize(int width, int height)
        {
            return (Math.Max(1, (int)Math.Round(width * BaseScale)), Math.Max(1, (int)Math.Round(height * BaseScale)));
        }
        public void Update(bool pressed)
        {
            if (!enabled || normal == null)
            {
                return;
            }

            if (normalCursor == 0)
            {
                // Read when the cursor is created, so it has to be in place first.
                _ = SDL.SetHint(SDL.Hints.MouseDPIScaleCursors, "1");
                nint next = Create(normal);
                nint nextActive;
                try { nextActive = Create(active); }
                catch { SDL.DestroyCursor(next); throw; }
                normalCursor = next; activeCursor = nextActive;
            }
            if (!SDL.SetCursor(pressed ? activeCursor : normalCursor))
            {
                throw new InvalidOperationException(SDL.GetError());
            }

            _ = SDL.ShowCursor();
        }
        private static nint Create(SKBitmap bitmap)
        {
            (int width, int height) = GetBaseSize(bitmap.Width, bitmap.Height);
            nint baseImage = ToSurface(bitmap, width, height);
            try
            {
                nint fullImage = ToSurface(bitmap, bitmap.Width, bitmap.Height);
                try
                {
                    if (!SDL.AddSurfaceAlternateImage(baseImage, fullImage))
                    {
                        throw new InvalidOperationException(SDL.GetError());
                    }
                }
                finally { SDL.DestroySurface(fullImage); }

                nint cursor = SDL.CreateColorCursor(baseImage, 0, 0);
                return cursor != 0 ? cursor : throw new InvalidOperationException(SDL.GetError());
            }
            finally { SDL.DestroySurface(baseImage); }
        }
        /// <summary>Resamples into straight-alpha pixels in a surface SDL owns.</summary>
        /// <remarks>
        /// Windows builds its cursor lazily from the surface on first show, so the pixels must
        /// outlive the Skia bitmap they were drawn into; a duplicate is SDL's to keep.
        /// </remarks>
        private static nint ToSurface(SKBitmap bitmap, int width, int height)
        {
            using SKBitmap scaled = new(new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Unpremul));
            using (SKCanvas canvas = new(scaled))
            {
                using SKImage image = SKImage.FromBitmap(bitmap);
                canvas.DrawImage(image, new SKRect(0, 0, width, height), new SKSamplingOptions(SKCubicResampler.Mitchell));
            }

            nint view = SDL.CreateSurfaceFrom(width, height, SDL.PixelFormat.ARGB8888, scaled.GetPixels(), scaled.RowBytes);
            if (view == 0)
            {
                throw new InvalidOperationException(SDL.GetError());
            }

            try
            {
                nint owned = SDL.DuplicateSurface(view);
                return owned != 0 ? owned : throw new InvalidOperationException(SDL.GetError());
            }
            finally { SDL.DestroySurface(view); }
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
