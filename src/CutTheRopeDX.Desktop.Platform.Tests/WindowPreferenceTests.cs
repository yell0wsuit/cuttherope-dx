using System.Collections.Generic;

using CutTheRopeDX.Framework.Core;
using CutTheRopeDX.Framework.Platform;

using SDL3;

using Xunit;

namespace CutTheRopeDX.Desktop.Platform.Tests
{
    public sealed class WindowPreferenceTests
    {
        [Theory]
        [InlineData(SDL.WindowFlags.Maximized)]
        [InlineData(SDL.WindowFlags.Fullscreen)]
        [InlineData(SDL.WindowFlags.Minimized)]
        public void MaximizedModeIsSavedUntilTheWindowIsRestored(SDL.WindowFlags nextMode)
        {
            IPreferenceStore previousStore = PlatformServices.Preferences;
            try
            {
                PlatformServices.Preferences = new MemoryStore();
                Preferences.LoadPreferences();
                SdlWindowService window = new(0);
                window.RefreshSurface(960, 720, 1920, 1440, 0);
                window.RefreshSurface(1470, 850, 2940, 1700, SDL.WindowFlags.Maximized);
                window.RefreshSurface(1470, 850, 2940, 1700, nextMode);
                Preferences.Update(force: true);
                Preferences.LoadPreferences();

                Assert.True(Preferences.GetBooleanForKey("PREFS_WINDOW_MAXIMIZED"));
                Assert.Equal(960, Preferences.GetIntForKey("PREFS_WINDOW_WIDTH"));
                Assert.Equal(720, Preferences.GetIntForKey("PREFS_WINDOW_HEIGHT"));

                window.RefreshSurface(960, 720, 1920, 1440, 0);
                Preferences.Update(force: true);
                Preferences.LoadPreferences();
                Assert.False(Preferences.GetBooleanForKey("PREFS_WINDOW_MAXIMIZED"));
            }
            finally
            {
                PlatformServices.Preferences = previousStore;
                Preferences.LoadPreferences();
            }
        }

        [Theory]
        [InlineData(SDL.WindowFlags.Fullscreen)]
        [InlineData(SDL.WindowFlags.Maximized)]
        [InlineData(SDL.WindowFlags.Minimized)]
        [InlineData(SDL.WindowFlags.Fullscreen | SDL.WindowFlags.Maximized)]
        public void SpecialWindowModesPreserveTheLastNormalSize(SDL.WindowFlags flags)
        {
            IPreferenceStore previousStore = PlatformServices.Preferences;
            try
            {
                PlatformServices.Preferences = new MemoryStore();
                Preferences.LoadPreferences();
                SdlWindowService window = new(0);
                window.RefreshSurface(960, 720, 1920, 1440, 0);
                Preferences.Update(force: true);

                // Feed the state reported by a window manager: the dummy SDL driver cannot
                // maximize windows, and a native desktop is not needed to test this policy.
                window.RefreshSurface(1920, 1080, 3840, 2160, flags);
                Preferences.Update(force: true);
                Preferences.LoadPreferences();

                Assert.Equal(1920, window.WindowWidth);
                Assert.Equal(1080, window.WindowHeight);
                Assert.Equal(960, window.WindowedWidth);
                Assert.Equal(720, window.WindowedHeight);
                Assert.Equal(960, Preferences.GetIntForKey("PREFS_WINDOW_WIDTH"));
                Assert.Equal(720, Preferences.GetIntForKey("PREFS_WINDOW_HEIGHT"));

                // After returning to normal mode, subsequent resizes must save again.
                window.RefreshSurface(960, 720, 1920, 1440, 0);
                window.RefreshSurface(1000, 750, 2000, 1500, 0);
                Preferences.Update(force: true);
                Preferences.LoadPreferences();
                Assert.Equal(1000, window.WindowedWidth);
                Assert.Equal(750, window.WindowedHeight);
                Assert.Equal(1000, Preferences.GetIntForKey("PREFS_WINDOW_WIDTH"));
                Assert.Equal(750, Preferences.GetIntForKey("PREFS_WINDOW_HEIGHT"));
            }
            finally
            {
                PlatformServices.Preferences = previousStore;
                Preferences.LoadPreferences();
            }
        }

        [Fact]
        public void ResizingPersistsTheWindowSizeBeforeShutdown()
        {
            IPreferenceStore previousStore = PlatformServices.Preferences;
            string previousDriver = SDL.GetHint("SDL_VIDEO_DRIVER");
            nint handle = 0;
            try
            {
                // Exercise the real SDL size queries without requiring a desktop or GPU.
                Assert.True(SDL.SetHint("SDL_VIDEO_DRIVER", "dummy"));
                Assert.True(SDL.Init(SDL.InitFlags.Video), SDL.GetError());
                handle = SDL.CreateWindow("Window preference test", 800, 600, SDL.WindowFlags.Hidden | SDL.WindowFlags.Resizable);
                Assert.NotEqual(0, handle);
                PlatformServices.Preferences = new MemoryStore();
                Preferences.LoadPreferences();
                SdlWindowService window = new(handle);
                window.RefreshSurface();
                window.SavePreferences();
                Preferences.Update(force: true);

                Assert.True(SDL.SetWindowSize(handle, 960, 720), SDL.GetError());
                window.RefreshSurface();
                // The host periodically updates preferences, without explicitly requesting a save.
                Preferences.Update(force: true);
                Preferences.LoadPreferences();

                Assert.Equal(960, Preferences.GetIntForKey("PREFS_WINDOW_WIDTH"));
                Assert.Equal(720, Preferences.GetIntForKey("PREFS_WINDOW_HEIGHT"));
                Assert.False(Preferences.GetBooleanForKey("PREFS_WINDOW_FULLSCREEN"));
            }
            finally
            {
                if (handle != 0)
                {
                    SDL.DestroyWindow(handle);
                }
                SDL.Quit();
                _ = previousDriver == null
                    ? SDL.ResetHint("SDL_VIDEO_DRIVER")
                    : SDL.SetHint("SDL_VIDEO_DRIVER", previousDriver);
                PlatformServices.Preferences = previousStore;
                Preferences.LoadPreferences();
            }
        }

        private sealed class MemoryStore : IPreferenceStore
        {
            private readonly Dictionary<string, string> blobs = [];
            public string Read(string name)
            {
                return blobs.GetValueOrDefault(name);
            }
            public void Write(string name, string contents)
            {
                blobs[name] = contents;
            }
            public IEnumerable<string> EnumerateBoxSlots()
            {
                return [];
            }
        }
    }
}
