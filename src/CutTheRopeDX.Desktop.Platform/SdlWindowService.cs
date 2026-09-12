using System;
using System.Numerics;

using CutTheRopeDX.Commons;
using CutTheRopeDX.Framework;
using CutTheRopeDX.Framework.Core;
using CutTheRopeDX.Framework.Diagnostics;
using CutTheRopeDX.Framework.Platform;


using Microsoft.Extensions.Logging;

using SDL3;
namespace CutTheRopeDX.Desktop.Platform
{
    internal sealed class SdlWindowService(nint window) : IWindowService
    {
        private int windowedWidth, windowedHeight;
        public int WindowWidth { get; private set; } = 1;
        public int WindowHeight { get; private set; } = 1;
        public int PixelWidth { get; private set; } = 1;
        public int PixelHeight { get; private set; } = 1;
        public float DevicePixelRatio => (float)PixelWidth / WindowWidth;
        public bool IsFullScreen => (SDL.GetWindowFlags(window) & SDL.WindowFlags.Fullscreen) != 0;
        public void Initialize(int width, int height, bool fullscreen)
        {
            Check(SDL.SetWindowMinimumSize(window, 320, 480));
            ApplyWindowSize(width, height);
            Center();
            if (fullscreen != IsFullScreen)
            {
                ToggleFullScreen();
            }

            Show();
            RefreshSurface();
            ILogger logger = Log.For(LogCategories.SdlHost);
            SdlWindowServiceLog.Resolution(
                logger, WindowWidth, WindowHeight, PixelWidth, PixelHeight, DevicePixelRatio, IsFullScreen);
        }
        /// <summary>Reveals the window the backend created hidden, once it is sized and placed.</summary>
        public void Show()
        {
            Check(SDL.ShowWindow(window));
            _ = SDL.SyncWindow(window);
        }
        public void ToggleFullScreen()
        {
            bool fullscreen = IsFullScreen;
            if (!fullscreen) { Check(SDL.GetWindowSize(window, out windowedWidth, out windowedHeight)); }
            Check(SDL.SetWindowFullscreen(window, !fullscreen)); Check(SDL.SyncWindow(window));
            if (fullscreen && windowedWidth > 0)
            {
                Check(SDL.SetWindowSize(window, windowedWidth, windowedHeight));
            }

            RefreshSurface(); SavePreferences();
            ILogger logger = Log.For(LogCategories.SdlHost);
            SdlWindowServiceLog.FullScreenChanged(logger, IsFullScreen, WindowWidth, WindowHeight);
        }
        /// <summary>Smallest window the game is playable in, on each axis.</summary>
        internal const int MinimumWidth = 320;
        internal const int MinimumHeight = 480;

        /// <summary>Largest window asked for, whatever a saved preference says.</summary>
        internal const int MaximumSide = 4096;

        /// <summary>
        /// Fits a requested window size to a display, on one axis.
        /// </summary>
        /// <param name="requested">The saved or requested size, or zero to ask for a default.</param>
        /// <param name="usable">The display's usable extent on this axis.</param>
        /// <param name="minimum">Smallest size the game is playable at on this axis.</param>
        /// <returns>The size to ask SDL for.</returns>
        /// <remarks>
        /// Pure, and separate from the window, because this is the part worth testing: it decides
        /// what a preference file carrying a size from another machine - or a hand-edited one -
        /// turns into here. The display bound is applied last so a screen smaller than the minimum
        /// wins over it, there being no use in asking for a window that cannot be shown.
        /// </remarks>
        internal static int ClampWindowSide(int requested, int usable, int minimum)
        {
            int wanted = requested > 0 ? requested : usable - 100;
            return Math.Min(usable, Math.Clamp(wanted, minimum, MaximumSide));
        }

        public void ApplyWindowSize(int width, int height)
        {
            Check(SDL.GetDisplayUsableBounds(SDL.GetDisplayForWindow(window), out SDL.Rect bounds));
            width = ClampWindowSide(width, bounds.W, MinimumWidth);
            height = ClampWindowSide(height, bounds.H, MinimumHeight);
            windowedWidth = width; windowedHeight = height;
            if (!IsFullScreen) { Check(SDL.SetWindowSize(window, width, height)); Check(SDL.SyncWindow(window)); }
            RefreshSurface(); SavePreferences();
        }
        /// <summary>Puts the window in the middle of the display it was placed on.</summary>
        /// <remarks>
        /// The window is born at the backend's probe size and only then resized to the saved one,
        /// and a resize keeps the top-left corner fixed, so whatever placement SDL chose for the
        /// original size leaves the real window sitting off-center.
        /// </remarks>
        public void Center()
        {
            if (IsFullScreen)
            {
                return;
            }

            int centered = (int)SDL.WindowPosCenteredDisplay((int)SDL.GetDisplayForWindow(window));
            Check(SDL.SetWindowPosition(window, centered, centered));
            Check(SDL.SyncWindow(window));
        }
        public void RefreshSurface()
        {
            Check(SDL.GetWindowSize(window, out int width, out int height));
            Check(SDL.GetWindowSizeInPixels(window, out int pixelWidth, out int pixelHeight));
            if (width <= 0 || height <= 0 || pixelWidth <= 0 || pixelHeight <= 0)
            {
                return;
            }

            WindowWidth = width; WindowHeight = height; PixelWidth = pixelWidth; PixelHeight = pixelHeight;
            // Debug, not information: a window being dragged to a new size reaches here on every
            // step of the drag.
            ILogger surfaceLogger = Log.For(LogCategories.SdlHost);
            SdlWindowServiceLog.SurfaceChanged(surfaceLogger, width, height, pixelWidth, pixelHeight);
            if (!IsFullScreen && (SDL.GetWindowFlags(window) & SDL.WindowFlags.Minimized) == 0) { windowedWidth = width; windowedHeight = height; }
            CtrRenderer.OnSurfaceChanged(pixelWidth, pixelHeight, DevicePixelRatio);
        }
        public Vector2 MapWindowToView(float x, float y)
        {
            CTRRectangle viewport = ScreenPresentation.Instance.Snapshot.RenderViewport;
            return MapWindowToView(x, y, WindowWidth, WindowHeight, PixelWidth, PixelHeight, viewport.x, viewport.y);
        }
        public static Vector2 MapWindowToView(float x, float y, int windowWidth, int windowHeight, int pixelWidth, int pixelHeight, float marginX, float marginY)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(windowWidth); ArgumentOutOfRangeException.ThrowIfNegativeOrZero(windowHeight);
            return new((x * pixelWidth / windowWidth) - marginX, (y * pixelHeight / windowHeight) - marginY);
        }
        public void SavePreferences()
        {
            Preferences.SetIntForKey(windowedWidth, "PREFS_WINDOW_WIDTH", false);
            Preferences.SetIntForKey(windowedHeight, "PREFS_WINDOW_HEIGHT", false);
            Preferences.SetBooleanForKey(IsFullScreen, "PREFS_WINDOW_FULLSCREEN", true);
        }
        private static void Check(bool ok)
        {
            if (!ok)
            {
                throw new InvalidOperationException(SDL.GetError());
            }
        }
    }

    /// <summary>Log messages for the window's size and mode.</summary>
    internal static partial class SdlWindowServiceLog
    {
        [LoggerMessage(
            Level = LogLevel.Information,
            Message = "Window {Width}x{Height} ({PixelWidth}x{PixelHeight} pixels, scale {Scale:F2}), "
                + "fullscreen={FullScreen}")]
        public static partial void Resolution(
            ILogger logger,
            int width,
            int height,
            int pixelWidth,
            int pixelHeight,
            float scale,
            bool fullScreen);

        [LoggerMessage(Level = LogLevel.Information, Message = "Fullscreen {FullScreen} at {Width}x{Height}")]
        public static partial void FullScreenChanged(ILogger logger, bool fullScreen, int width, int height);

        [LoggerMessage(
            Level = LogLevel.Debug,
            Message = "Surface {Width}x{Height} ({PixelWidth}x{PixelHeight} pixels)")]
        public static partial void SurfaceChanged(
            ILogger logger, int width, int height, int pixelWidth, int pixelHeight);
    }
}
