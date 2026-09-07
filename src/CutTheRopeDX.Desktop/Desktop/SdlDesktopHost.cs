using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

using CutTheRopeDX.Commons;
using CutTheRopeDX.Desktop.Platform;
using CutTheRopeDX.Desktop.Platform.Audio;
using CutTheRopeDX.Desktop.Platform.Graphics;
using CutTheRopeDX.Framework;
using CutTheRopeDX.Framework.Core;
using CutTheRopeDX.Framework.Media;
using CutTheRopeDX.Framework.Platform;
using CutTheRopeDX.Helpers;
using CutTheRopeDX.Rendering.Skia;

using SDL3;

using SkiaSharp;

namespace CutTheRopeDX.Desktop
{
    /// <summary>
    /// Opt-in SDL composition, selected by <c>--sdl</c>. The legacy host remains available as the
    /// comparison path.
    /// </summary>
    internal sealed class SdlDesktopHost : IHostApp, IDisposable
    {
        private bool exiting;
        private bool initialized;
        private GraphicsSelection<SdlGraphicsDevice> selection;
        private SkiaRenderBackend render;
        private SkiaAssetPlatform assets;
        private SdlWindowService window;
        private SdlInputRouter input;
        private SdlCursorService cursor;
        private SdlGamepadService gamepads;
        private SdlAudioBackend audio;
        private readonly SdlHostLoop loop = new();
        private readonly Stopwatch clock = new();
        private TimeSpan nextSave = TimeSpan.FromSeconds(1);
        private int frameCount;
        private int frameLimit;
        private string screenshot;
        private readonly Queue<(int Frame, float X, float Y, bool? Down)> taps = new();

        public bool CanExit => true;
        public string LevelEditorUrl => null;
        public string CustomLevelExitLabelKey => "QUIT_BUTTON";
        public void Exit()
        {
            exiting = true;
        }

        public bool IsKeyPressed(KeyCode key)
        {
            return input?.IsKeyPressed(key) == true;
        }

        /// <summary>
        /// Draws the current movie frame over the whole presentation viewport, and lets a click
        /// skip the cutscene.
        /// </summary>
        /// <remarks>
        /// The frame replaces the scene rather than compositing with it, so pending quads are
        /// flushed and the surface cleared first. A press held from before the movie started
        /// cannot skip it: the router drops held presses when focus is lost, which is what stops
        /// the click that returns to the window from also ending the cutscene.
        /// </remarks>
        public void DrawMovie()
        {
            Renderer.FlushQuads();
            SdlGraphicsDevice device = selection.Device;

            // Core shows a view from inside an update, and showing the movie view draws it, so
            // this runs outside the draw phase too. There is no canvas then; the frame that
            // follows draws the same movie anyway.
            if (!device.HasFrame)
            {
                return;
            }

            device.Canvas.Clear(SKColors.Black);
            MovieMgr movies = Application.SharedMovieMgr();
            if (!movies.IsTextureReady() || movies.GetTexture() is not SkiaVideoFrameTexture frame)
            {
                return;
            }

            if (input.PrimaryPressed)
            {
                movies.Stop();
                return;
            }

            CTRRectangle viewport = ScreenPresentation.Instance.Snapshot.RenderViewport;
            device.Canvas.DrawBitmap(
                frame.Bitmap,
                SKRect.Create(viewport.x, viewport.y, viewport.w, viewport.h),
                SkiaTexture.LinearSampling,
                paint: null);
        }
        public void OpenUrl(string url)
        {
            try { _ = Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
            catch (Exception error) { Console.Error.WriteLine($"Could not open URL: {error.Message}"); }
        }

        public void Run(string[] args)
        {
            string renderer = Option(args, "--renderer") ?? "auto";
            frameLimit = int.TryParse(Option(args, "--sdl-frames"), out int frames) ? Math.Max(0, frames) : 0;
            screenshot = Option(args, "--sdl-screenshot");
            ScheduleTaps(args);
            GraphicsBackendKind? forced = renderer switch
            {
                "auto" => null,
                "metal" => GraphicsBackendKind.Metal,
                "gl" => GraphicsBackendKind.OpenGL,
                "vulkan" => GraphicsBackendKind.Vulkan,
                "angle" => GraphicsBackendKind.Angle,
                _ => throw new ArgumentException($"Unknown SDL renderer '{renderer}'."),
            };
            if (!SDL.Init(SDL.InitFlags.Video | SDL.InitFlags.Events | SDL.InitFlags.Gamepad))
            {
                throw new InvalidOperationException(SDL.GetError());
            }

            initialized = true;
            string platform = OperatingSystem.IsMacOS() ? "macos" : OperatingSystem.IsWindows() ? "windows" : "linux";
            selection = BackendSelector.Select(platform, forced, CreateDevice, ValidateDevice);
            SdlGraphicsDevice device = selection.Device;
            _ = SDL.SetWindowTitle(device.Window, "Cut The Rope: DX - SDL preview");
            string root = SkiaAssetPlatform.ResolveContentRoot(AppContext.BaseDirectory);
            PlatformServices.Content = new FileContentStore(root);

            // A machine with no usable audio device still plays the game, so this reports failure
            // rather than throwing: the graphics that already came up must not depend on it.
            audio = SdlAudioBackend.TryOpen(root);
            Console.WriteLine($"[sdl] renderer={selection.Kind}; audio {(audio == null ? "unavailable" : "on")}");
            foreach (Exception failure in selection.Failures)
            {
                Console.Error.WriteLine($"[sdl] rejected renderer: {failure.Message}");
            }

            Preferences.LoadPreferences();
            window = new(device.Window);
            window.Initialize(Preferences.GetIntForKey("PREFS_WINDOW_WIDTH"), Preferences.GetIntForKey("PREFS_WINDOW_HEIGHT"), Preferences.GetBooleanForKey("PREFS_WINDOW_FULLSCREEN"));
            input = new()
            {
                WindowId = SDL.GetWindowID(device.Window),
                WindowSize = () => (window.WindowWidth, window.WindowHeight),
                MapPosition = window.MapWindowToView,
                Touch = touch => CtrRenderer.Java_com_zeptolab_ctr_CtrRenderer_nativeTouchProcess([touch]),
                MouseMoved = position => Application.SharedRootController().MouseMoved(CtrRenderer.TransformX(position.X), CtrRenderer.TransformY(position.Y)),
                Wheel = delta => Application.SharedRootController().HandleMouseWheel(delta),
                Back = () => { Application.SharedMovieMgr().Stop(); _ = CtrRenderer.Java_com_zeptolab_ctr_CtrRenderer_nativeBackPressed(); },
                ToggleFullscreen = window.ToggleFullScreen,
                FocusChanged = focused =>
                {
                    if (focused)
                    {
                        CtrRenderer.Java_com_zeptolab_ctr_CtrRenderer_nativeResume();
                    }
                    else
                    {
                        CtrRenderer.Java_com_zeptolab_ctr_CtrRenderer_nativePause();
                    }

                    // A run working to a frame budget keeps stepping whatever the window manager
                    // does with focus. Suspending stops frames being produced, so an unfocused
                    // window would leave the budget unreachable and the run never ending.
                    loop.SetSuspended(!focused && frameLimit == 0, clock.Elapsed);
                },
                Quit = Exit,
                Resized = () => { device.Resize(); window.RefreshSurface(); },
            };
            // SDL reports gamepad buttons only for devices that have been opened, so the Back
            // handling in the router is inert until this runs.
            gamepads = new(
                static () => SDL.GetGamepads(out _) ?? [],
                static id => SDL.OpenGamepad(id),
                static handle => SDL.CloseGamepad(handle));
            gamepads.OpenConnected();
            cursor = new(input.ClearInput);
            cursor.Load(Path.Combine(root, "images/cursor.png"), Path.Combine(root, "images/cursor_active.png"));
            PlatformServices.Host = this;
            PlatformServices.Window = window;
            PlatformServices.Cursor = cursor;
            PlatformServices.Updates = new DesktopUpdateService();
            PlatformServices.FileWatchers = new DesktopFileWatcherFactory();
            PlatformServices.RichPresence = new RPCHelpers();
            PlatformServices.VideoPlayerFactory = DesktopVideoPlayerFactory.Create;
            render = new(device);
            PlatformServices.Render = render;
            assets = new(PlatformServices.Content, device.Context);
            _ = SDL.GetWindowSize(device.Window, out int width, out int height);
            _ = SDL.GetWindowSizeInPixels(device.Window, out int pixels, out int ignored);
            CtrBootstrap.Initialize(assets, audio, width, height, LanguageHelper.FromSystemCulture(), (float)pixels / width);
            window.RefreshSurface();
            clock.Start();
            loop.Reset(clock.Elapsed);
            while (!exiting)
            {
                while (SDL.PollEvent(out SDL.Event evt))
                {

                    gamepads.HandleEvent(in evt);
                    input.HandleEvent(in evt);
                }

                if (exiting)
                {
                    break;
                }

                InjectScheduledTaps();
                _ = loop.Advance(clock.Elapsed, Update, Draw);
                SDL.Delay(1);
            }
            Console.WriteLine($"[sdl] presented {frameCount} frames; active controller = {HeadlessHost.ActiveControllerName()}");
        }

        private void Update(float delta)
        {
            SoundMgr.Update(TimeSpan.FromMilliseconds(delta));
            CtrRenderer.Java_com_zeptolab_ctr_CtrRenderer_nativeTick(delta);
            input.EndUpdate();
            if (clock.Elapsed >= nextSave)
            {
                Preferences.Update();
                nextSave = clock.Elapsed + TimeSpan.FromSeconds(1);
            }
        }

        private void Draw()
        {
            SdlGraphicsDevice device = selection.Device;
            if (!device.AcquireFrame())
            {
                return;
            }

            device.Canvas.Clear(SKColors.Black);
            Renderer.BeginFrame();
            try { CtrRenderer.OnDrawFrame(); }
            finally { Renderer.EndFrame(); }

            // The native cursor stands in for the sprite the MonoGame host drew here, so it is
            // refreshed at the same point in the frame. Core only enables and disables the
            // service, so tracking the presentation scale and the pressed variant is the host's.
            cursor.Update(ScreenPresentation.Instance.Snapshot.Scale, window.DevicePixelRatio, input.PrimaryPressed);
            Renderer.CopyFromRenderTargetToScreen();
            device.Flush();
            frameCount++;
            if (frameLimit > 0 && frameCount >= frameLimit && screenshot != null)
            {
                using SKBitmap bitmap = device.ReadPixels();
                using SKImage image = SKImage.FromBitmap(bitmap);
                using SKData data = image.Encode(SKEncodedImageFormat.Png, 100);
                using FileStream file = File.Create(screenshot);
                data.SaveTo(file);
            }
            device.Present();
            if (frameLimit > 0 && frameCount >= frameLimit)
            {
                Exit();
            }
        }

        private static SdlGraphicsDevice CreateDevice(GraphicsBackendKind kind, CandidateLifetime lifetime)
        {
            // Each device declares its own Initialize, so the concrete type has to stay in scope
            // until it has run. The lifetime owns the device from construction, so a throw out of
            // Initialize still releases it.
            switch (kind)
            {
                case GraphicsBackendKind.Metal:
                    MetalDevice metal = lifetime.Own(new MetalDevice(static fault => { }));
                    metal.Initialize();
                    return metal;
                case GraphicsBackendKind.OpenGL:
                    SdlGlDevice gl = lifetime.Own(new SdlGlDevice(static fault => { }));
                    gl.Initialize();
                    return gl;
                case GraphicsBackendKind.Vulkan:
                    VulkanDevice vulkan = lifetime.Own(new VulkanDevice(static fault => { }));
                    vulkan.Initialize();
                    return vulkan;
                case GraphicsBackendKind.Angle:
                default:
                    throw new PlatformNotSupportedException($"{kind} device is not implemented.");
            }
        }

        private static void ValidateDevice(SdlGraphicsDevice device)
        {
            if (!device.AcquireFrame())
            {
                throw new InvalidOperationException("No startup drawable.");
            }

            device.Canvas.Clear(SKColors.Black);
            device.Flush();
            device.Present();
        }

        /// <summary>
        /// Reads the <c>--sdl-tap FRAME:X,Y</c> options and schedules a press and release for
        /// each. X and Y are fractions of the window, so a scripted run does not depend on its size.
        /// </summary>
        /// <param name="args">The command line.</param>
        private void ScheduleTaps(string[] args)
        {
            // Long enough for the game to see the button held before it is let go.
            const int HoldFrames = 6;
            List<(int Frame, float X, float Y, bool? Down)> scheduled = [];
            for (int index = 0; index + 1 < args.Length; index++)
            {
                if (args[index] != "--sdl-tap")
                {
                    continue;
                }

                string[] parts = args[index + 1].Split(':', ',');
                if (parts.Length != 3 ||
                    !int.TryParse(parts[0], out int frame) ||
                    !float.TryParse(parts[1], out float x) ||
                    !float.TryParse(parts[2], out float y))
                {
                    throw new ArgumentException($"Could not read the tap '{args[index + 1]}'; expected FRAME:X,Y.");
                }

                // A real click always arrives after the pointer has moved onto the target, and the
                // menu tracks that motion to decide what is under the cursor, so the move is part
                // of the tap rather than an extra.
                scheduled.Add((frame - 1, x, y, null));
                scheduled.Add((frame, x, y, true));
                scheduled.Add((frame + HoldFrames, x, y, false));
            }

            foreach ((int Frame, float X, float Y, bool? Down) entry in scheduled.OrderBy(static entry => entry.Frame))
            {
                taps.Enqueue(entry);
            }
        }

        /// <summary>
        /// Pushes any scheduled press or release the frame counter has reached.
        /// </summary>
        /// <remarks>
        /// The events go through SDL rather than straight into the router, so a scripted run
        /// exercises exactly the path a real click takes. Each is sent once: the loop spins faster
        /// than frames are drawn, so testing the counter alone would repeat every event.
        /// </remarks>
        private void InjectScheduledTaps()
        {
            while (taps.Count > 0 && taps.Peek().Frame <= frameCount)
            {
                (int _, float x, float y, bool? down) = taps.Dequeue();
                uint id = SDL.GetWindowID(selection.Device.Window);
                float px = x * window.WindowWidth;
                float py = y * window.WindowHeight;
                SDL.Event tap = default;
                if (down == null)
                {
                    tap.Type = (uint)SDL.EventType.MouseMotion;
                    tap.Motion.WindowID = id;
                    tap.Motion.Which = 0;
                    tap.Motion.X = px;
                    tap.Motion.Y = py;
                }
                else
                {
                    tap.Type = (uint)(down.Value ? SDL.EventType.MouseButtonDown : SDL.EventType.MouseButtonUp);
                    tap.Button.WindowID = id;
                    tap.Button.Which = 0;
                    tap.Button.Button = 1;
                    tap.Button.Down = down.Value;
                    tap.Button.X = px;
                    tap.Button.Y = py;
                }

                _ = SDL.PushEvent(ref tap);
            }
        }

        private static string Option(string[] args, string key)
        {
            int index = Array.IndexOf(args, key);
            return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
        }

        public void Dispose()
        {
            PlatformServices.Updates?.Cancel();
            if (window != null)
            {
                window.SavePreferences();
                Preferences.RequestSave();
                Preferences.Update();
            }
            PlatformServices.RichPresence?.Dispose();
            gamepads?.Dispose();
            cursor?.Dispose();
            audio?.Dispose();
            render?.Dispose();
            assets?.Dispose();
            selection?.Dispose();
            if (initialized) { SDL.Quit(); initialized = false; }
        }
    }
}
