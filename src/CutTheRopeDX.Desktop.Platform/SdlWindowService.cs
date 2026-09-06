using System;
using System.Numerics;

using CutTheRopeDX.Commons;
using CutTheRopeDX.Framework;
using CutTheRopeDX.Framework.Core;
using CutTheRopeDX.Framework.Platform;

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
            if (fullscreen != IsFullScreen)
            {
                ToggleFullScreen();
            }

            RefreshSurface();
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
        }
        public void ApplyWindowSize(int width, int height)
        {
            Check(SDL.GetDisplayUsableBounds(SDL.GetDisplayForWindow(window), out SDL.Rect bounds));
            width = Math.Min(bounds.W, Math.Clamp(width > 0 ? width : bounds.W - 100, 320, 4096));
            height = Math.Min(bounds.H, Math.Clamp(height > 0 ? height : bounds.H - 100, 480, 4096));
            windowedWidth = width; windowedHeight = height;
            if (!IsFullScreen) { Check(SDL.SetWindowSize(window, width, height)); Check(SDL.SyncWindow(window)); }
            RefreshSurface(); SavePreferences();
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
}
