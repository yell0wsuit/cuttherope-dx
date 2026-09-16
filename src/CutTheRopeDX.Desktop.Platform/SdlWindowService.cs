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
        /// <summary>The client width the window has, or returns to, when it is not fullscreen.</summary>
        public int WindowedWidth { get; private set; }

        /// <summary>The client height the window has, or returns to, when it is not fullscreen.</summary>
        public int WindowedHeight { get; private set; }
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
            FitWindowedSizeToFrame();
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
            Check(SDL.SetWindowFullscreen(window, !fullscreen)); Check(SDL.SyncWindow(window));
            if (fullscreen)
            {
                FitWindowedSizeToFrame();
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
        /// <param name="decoration">The window frame's extent on this axis, title bar included.</param>
        /// <returns>The size to ask SDL for.</returns>
        /// <remarks>
        /// Pure, and separate from the window, because this is the part worth testing: it decides
        /// what a preference file carrying a size from another machine - or a hand-edited one -
        /// turns into here. SDL sizes the client area and the frame is drawn outside it, so the
        /// frame is taken from the display first; a client as tall as the display would otherwise
        /// push the title bar off the top, where it can be neither dragged nor used to resize.
        /// The display bound is applied last so a screen smaller than the minimum wins over it,
        /// there being no use in asking for a window that cannot be shown.
        /// </remarks>
        internal static int ClampWindowSide(int requested, int usable, int minimum, int decoration)
        {
            int available = Math.Max(1, usable - decoration);
            int wanted = requested > 0 ? requested : usable - 100;
            return Math.Min(available, Math.Clamp(wanted, minimum, MaximumSide));
        }

        /// <summary>Fits a client size to the window's display, less the window frame.</summary>
        /// <remarks>
        /// A backend that cannot report the frame gets zeroed sizes from SDL, which leaves the
        /// display bound as it was. X11 is one until the window is first shown, since it learns the
        /// frame from the window manager on mapping, which is why the fit is taken again later.
        /// </remarks>
        private (int Width, int Height) FitToDisplay(int width, int height)
        {
            Check(SDL.GetDisplayUsableBounds(SDL.GetDisplayForWindow(window), out SDL.Rect bounds));
            _ = SDL.GetWindowBordersSize(window, out int top, out int left, out int bottom, out int right);
            return (ClampWindowSide(width, bounds.W, MinimumWidth, left + right),
                ClampWindowSide(height, bounds.H, MinimumHeight, top + bottom));
        }

        /// <summary>
        /// Fits the windowed size again once the frame can be measured, and puts a window that no
        /// longer fits back on the display.
        /// </summary>
        /// <remarks>
        /// Taken once the window is shown and again on leaving fullscreen, the two points at which a
        /// window whose frame was unmeasurable when it was sized is back with a frame. A window
        /// already at the fitted size is left where it is, so this does not move anything that
        /// fits; it is recentered only when the frame made it smaller, as a shrink keeps the top
        /// left corner that was placed for the larger size.
        /// </remarks>
        private void FitWindowedSizeToFrame()
        {
            const SDL.WindowFlags Unsized = SDL.WindowFlags.Fullscreen | SDL.WindowFlags.Maximized | SDL.WindowFlags.Minimized;
            if (WindowedWidth <= 0 || (SDL.GetWindowFlags(window) & Unsized) != 0)
            {
                return;
            }

            (int width, int height) = FitToDisplay(WindowedWidth, WindowedHeight);
            bool shrunk = width != WindowedWidth || height != WindowedHeight;
            WindowedWidth = width; WindowedHeight = height;
            Check(SDL.GetWindowSize(window, out int currentWidth, out int currentHeight));
            if (width == currentWidth && height == currentHeight)
            {
                return;
            }

            Check(SDL.SetWindowSize(window, width, height)); Check(SDL.SyncWindow(window));
            if (shrunk)
            {
                Center();
            }
        }

        public void ApplyWindowSize(int width, int height)
        {
            (width, height) = FitToDisplay(width, height);
            WindowedWidth = width; WindowedHeight = height;
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
            RefreshSurface(width, height, pixelWidth, pixelHeight, SDL.GetWindowFlags(window));
        }

        /// <summary>Updates the surface and remembered window size from an SDL window snapshot.</summary>
        internal void RefreshSurface(int width, int height, int pixelWidth, int pixelHeight, SDL.WindowFlags flags)
        {
            if (width <= 0 || height <= 0 || pixelWidth <= 0 || pixelHeight <= 0)
            {
                return;
            }

            WindowWidth = width; WindowHeight = height; PixelWidth = pixelWidth; PixelHeight = pixelHeight;
            // Debug, not information: a window being dragged to a new size reaches here on every
            // step of the drag.
            ILogger surfaceLogger = Log.For(LogCategories.SdlHost);
            SdlWindowServiceLog.SurfaceChanged(surfaceLogger, width, height, pixelWidth, pixelHeight);
            // A maximized client is the work area less the frame; kept as the windowed size, it
            // would reopen unmaximized with its title bar above the top of the display.
            if ((flags & (SDL.WindowFlags.Fullscreen | SDL.WindowFlags.Minimized | SDL.WindowFlags.Maximized)) == 0
                && (WindowedWidth != width || WindowedHeight != height))
            {
                WindowedWidth = width; WindowedHeight = height;
                // Queue the resized dimensions for the host's periodic save, rather than
                // keeping them only in memory until a fullscreen toggle or clean shutdown.
                SavePreferences(fullscreen: false);
            }
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
            SavePreferences(IsFullScreen);
        }
        private void SavePreferences(bool fullscreen)
        {
            Preferences.SetIntForKey(WindowedWidth, "PREFS_WINDOW_WIDTH", false);
            Preferences.SetIntForKey(WindowedHeight, "PREFS_WINDOW_HEIGHT", false);
            Preferences.SetBooleanForKey(fullscreen, "PREFS_WINDOW_FULLSCREEN", true);
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
