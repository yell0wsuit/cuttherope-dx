using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;

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
    /// <summary>The desktop host: SDL3 window/input/audio composed with the SkiaSharp renderer.</summary>
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
        private GraphicsRecoveryCoordinator recovery;
        private RendererMemory rendererMemory;
        private readonly SkiaResourceRegistry registry = new();
        private readonly SdlHostLoop loop = new();
        private readonly Stopwatch clock = new();
        private TimeSpan nextSave = TimeSpan.FromSeconds(1);
        private int frameCount;
        private int frameLimit;
        private string screenshot;
        private bool drewMovie;
        private bool moviePressArmed;
        private readonly Queue<(int Frame, float X, float Y, bool? Down)> taps = new();
        private readonly Queue<int> scheduledLosses = new();
        private int recoveriesWithoutAFrame;

        /// <summary>
        /// The running version, exactly as the assembly records it. Read once: the attribute does
        /// not change while the process runs.
        /// </summary>
        internal static readonly string Version = ResolveVersion();

        /// <summary>The Cut the Rope: DX name shown in the title.</summary>
        internal const string CtrDXProductName = "Cut The Rope: DX";

        /// <summary>
        /// How many devices may be built without one of them drawing anything before the run is
        /// given up as unrecoverable.
        /// </summary>
        /// <remarks>
        /// Bringing a device up resizes the window, and a resize is itself something that can
        /// report a lost device, so a device that comes up but cannot be resized would replace
        /// itself forever without ever presenting. A device that draws even one frame resets this,
        /// so an unlucky burst of real losses is not mistaken for that.
        /// </remarks>
        private const int MaximumFramelessRecoveries = 4;

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

            // A movie replaces the scene, so the frame goes straight to the surface and the
            // render target is not presented over it afterwards.
            drewMovie = true;
            device.Canvas.Clear(SKColors.Black);
            MovieMgr movies = Application.SharedMovieMgr();
            if (!movies.IsTextureReady() || movies.GetTexture() is not SkiaVideoFrameTexture frame)
            {
                return;
            }

            // The press that started the cutscene is still down when its first frame is drawn, so
            // acting on it waits for the pointer to come up once. Without that, choosing Play both
            // opens the intro and dismisses it, and the movie is never seen.
            if (!input.PrimaryPressed)
            {
                moviePressArmed = true;
            }
            else
            {
                switch (GraphicsRecovery.PressAction(moviePressArmed, movies.IsPaused()))
                {
                    case MoviePressAction.Resume:
                        movies.Resume();
                        // The pointer is still down, and the cutscene is now playing, so the very
                        // next frame would read the same press as a skip. It has to come up again.
                        moviePressArmed = false;
                        break;
                    case MoviePressAction.Skip:
                        movies.Stop();
                        return;
                    case MoviePressAction.Ignore:
                    default:
                        break;
                }
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
            ScheduleLosses(args);
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
            recovery = new(platform, forced);

            // A driver that faults while starting up takes the process with it, so the renderer
            // being tried is written down first and the note torn up once one has drawn a frame.
            rendererMemory = new(Path.Combine(Preferences.SaveDirectory, "renderer.txt"));
            selection = BackendSelector.Attempt(
                rendererMemory.Filter(BackendSelector.PreferenceOrder(platform, forced)),
                (kind, lifetime) => { rendererMemory.BeginAttempt(kind); return CreateDevice(kind, lifetime); },
                ValidateDevice);
            rendererMemory.RecordSuccess();
            SdlGraphicsDevice device = selection.Device;
            _ = SDL.SetWindowTitle(device.Window, TitleFor(selection.Kind));
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
            input = new()
            {
                Touch = touch => CtrRenderer.Java_com_zeptolab_ctr_CtrRenderer_nativeTouchProcess([touch]),
                MouseMoved = position => Application.SharedRootController().MouseMoved(CtrRenderer.TransformX(position.X), CtrRenderer.TransformY(position.Y)),
                Wheel = delta => Application.SharedRootController().HandleMouseWheel(delta),
                Back = () => { Application.SharedMovieMgr().Stop(); _ = CtrRenderer.Java_com_zeptolab_ctr_CtrRenderer_nativeBackPressed(); },
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
            };
            AttachWindow(device);
            window.Initialize(Preferences.GetIntForKey("PREFS_WINDOW_WIDTH"), Preferences.GetIntForKey("PREFS_WINDOW_HEIGHT"), Preferences.GetBooleanForKey("PREFS_WINDOW_FULLSCREEN"));
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
            PlatformServices.Cursor = cursor;
            PlatformServices.Updates = new DesktopUpdateService();
            PlatformServices.FileWatchers = new DesktopFileWatcherFactory();
            PlatformServices.RichPresence = new RPCHelpers();
            PlatformServices.VideoPlayerFactory = DesktopVideoPlayerFactory.Create;
            render = new(device, registry);
            PlatformServices.Render = render;
            assets = new(PlatformServices.Content, device.Context, registry);
            _ = SDL.GetWindowSize(device.Window, out int width, out int height);
            _ = SDL.GetWindowSizeInPixels(device.Window, out int pixels, out int ignored);
            CtrBootstrap.Initialize(assets, audio, width, height, LanguageHelper.FromSystemCulture(), (float)pixels / width);
            window.RefreshSurface();
            clock.Start();
            loop.Reset(clock.Elapsed);
            while (!exiting)
            {
                while (!exiting && SDL.PollEvent(out SDL.Event evt))
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
            if (scheduledLosses.Count > 0 && scheduledLosses.Peek() <= frameCount)
            {
                _ = scheduledLosses.Dequeue();
                RecoverDevice(new GraphicsDeviceLostException($"Loss injected at frame {frameCount}."));
                return;
            }

            GuardDevice(DrawFrame);
        }

        /// <summary>Runs work that touches the device, recovering if it reports a loss.</summary>
        /// <param name="work">The device work to attempt.</param>
        /// <remarks>
        /// Drawing is not the only thing that talks to the device. Resizing rebuilds the swapchain
        /// and waits for the device to go idle, which is one of the first places a driver reports a
        /// reset, and it runs from event handling rather than from the frame. A loss reaching this
        /// from anywhere has to end in recovery, not in an unhandled exception out of the run loop.
        /// </remarks>
        private void GuardDevice(Action work)
        {
            try
            {
                work();
            }
            catch (GraphicsDeviceLostException lost)
            {
                RecoverDevice(lost);
            }
        }

        /// <summary>Produces and presents one frame.</summary>
        private void DrawFrame()
        {
            SdlGraphicsDevice device = selection.Device;
            if (!device.AcquireFrame())
            {
                return;
            }

            device.Canvas.Clear(SKColors.Black);
            drewMovie = false;
            Renderer.BeginFrame();
            try { CtrRenderer.OnDrawFrame(); }
            finally { Renderer.EndFrame(); }

            // The native cursor stands in for the sprite the old host drew here, so it is
            // refreshed at the same point in the frame. Core only enables and disables the
            // service, so tracking the presentation scale and the pressed variant is the host's.
            cursor.Update(ScreenPresentation.Instance.Snapshot.Scale, window.DevicePixelRatio, input.PrimaryPressed);
            if (!drewMovie)
            {
                // Away from a movie the latch rests disarmed, so the next cutscene starts out
                // immune to whatever press opened it.
                moviePressArmed = false;
                Renderer.CopyFromRenderTargetToScreen();
            }

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
            recoveriesWithoutAFrame = 0;
            if (frameLimit > 0 && frameCount >= frameLimit)
            {
                Exit();
            }
        }

        /// <summary>Points the window service and the window-bound input at a device's window.</summary>
        /// <param name="device">The device whose window is now the game's.</param>
        /// <remarks>
        /// A replacement device brings a new window with it: each backend needs its own window
        /// flags, so the old one cannot be handed over. Everything addressed to a window handle is
        /// therefore rebuilt here rather than captured once at boot.
        /// </remarks>
        private void AttachWindow(SdlGraphicsDevice device)
        {
            window = new(device.Window);
            PlatformServices.Window = window;
            input.WindowId = SDL.GetWindowID(device.Window);
            input.WindowSize = () => (window.WindowWidth, window.WindowHeight);
            input.MapPosition = (x, y) => window.MapWindowToView(x, y);
            input.ToggleFullscreen = () => window.ToggleFullScreen();
            input.Resized = () => GuardDevice(() =>
            {
                selection.Device.Resize();
                window.RefreshSurface();
            });
        }

        /// <summary>
        /// Replaces a lost graphics device and puts the game back on the screen, or ends the run
        /// when nothing can draw any more.
        /// </summary>
        /// <param name="lost">What revealed the loss.</param>
        /// <remarks>
        /// Order is the whole of this. Everything the failing device owns is released while it is
        /// still alive, because Skia frees its resources through the context that made them; then
        /// the generation is retired, so any handle that escaped the sweep is refused rather than
        /// sampled; only then is a replacement built and the assets that are still needed loaded
        /// through it.
        /// </remarks>
        private void RecoverDevice(GraphicsDeviceLostException lost)
        {
            if (exiting)
            {
                return;
            }

            Console.Error.WriteLine($"[sdl] device lost: {lost.Message}");
            recoveriesWithoutAFrame++;
            if (recoveriesWithoutAFrame > MaximumFramelessRecoveries)
            {
                Abandon($"The graphics device was replaced {MaximumFramelessRecoveries} times "
                    + "without any of them drawing a frame.");
                return;
            }

            GraphicsRecoveryPlan plan = GraphicsRecovery.Begin();

            // A press that was down when the device went is not a press the player is still
            // making by the time one comes back.
            input.ClearInput();
            render.DiscardDeviceResources();
            assets.DiscardDeviceResources();
            _ = registry.Invalidate();

            int width = window.WindowWidth;
            int height = window.WindowHeight;
            bool fullscreen = window.IsFullScreen;
            window.SavePreferences();

            SdlGraphicsDevice device;
            try
            {
                selection = recovery.Recover(selection, CreateDevice, ValidateDevice);
                device = selection.Device;
            }
            catch (GraphicsRecoveryFailedException failure)
            {
                Abandon(failure.Message);
                return;
            }

            _ = SDL.SetWindowTitle(device.Window, TitleFor(selection.Kind));
            AttachWindow(device);
            window.Initialize(width, height, fullscreen);
            render.Rebind(device);
            assets.Rebind(device.Context);
            GraphicsRecoveryReport report = GraphicsRecovery.Complete(plan);

            // Building a device takes real time, and the loop would otherwise treat all of it as
            // gameplay owed and replay it as one batch of catch-up updates. Nothing happened
            // during it that the game should live through, so the accounting starts again here.
            loop.Reset(clock.Elapsed);
            Console.WriteLine(
                $"[sdl] recovered on {selection.Kind} at frame {frameCount}: "
                + $"{report.ReloadedAssets} assets reloaded, {report.DroppedCaptures} captures dropped");
        }

        /// <summary>Shuts the game down when the graphics device cannot be got back.</summary>
        /// <param name="reason">What was tried, for the log and the dialog.</param>
        /// <remarks>
        /// Nothing here touches the graphics device. The message box is asked for with no parent
        /// window, because the only window the game had belonged to the device that has gone, and
        /// drawing recovery UI through dead resources is how a recoverable failure turns into a
        /// crash on the way out.
        /// </remarks>
        private void Abandon(string reason)
        {
            if (exiting)
            {
                return;
            }

            Console.Error.WriteLine($"[sdl] {reason}");
            audio?.Dispose();
            audio = null;
            window?.SavePreferences();
            Preferences.RequestSave();
            // Forced: the host is shutting down after a fatal error, so a save still backing off
            // from an earlier failure would never be retried.
            Preferences.Update(force: true);
            window = null;
            PlatformServices.Window = null;
            _ = SDL.ShowSimpleMessageBox(
                SDL.MessageBoxFlags.Error,
                "Cut the Rope: DX",
                $"{reason}\n\nThe game cannot continue and has to be closed. Your progress is unaffected.",
                0);
            Exit();
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
        /// Reads the <c>--sdl-lose-device FRAME</c> options and schedules a device loss at each.
        /// </summary>
        /// <param name="args">The command line.</param>
        /// <remarks>
        /// Injection stands in for the losses a driver reset, a GPU switch or a display change
        /// produce, and it exercises the same teardown and rebuild those take. It does not
        /// reproduce them: real resets can also strip a capability or change the drawable format,
        /// and only running on the hardware shows that.
        /// </remarks>
        private void ScheduleLosses(string[] args)
        {
            List<int> frames = [];
            for (int index = 0; index + 1 < args.Length; index++)
            {
                if (args[index] != "--sdl-lose-device")
                {
                    continue;
                }

                if (!int.TryParse(args[index + 1], out int frame) || frame < 0)
                {
                    throw new ArgumentException($"Could not read the loss frame '{args[index + 1]}'.");
                }

                frames.Add(frame);
            }

            frames.Sort();
            foreach (int frame in frames)
            {
                scheduledLosses.Enqueue(frame);
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

        /// <summary>
        /// Builds the window title: the CTRDX product name, the running version, and the renderer drawing it.
        /// </summary>
        /// <remarks>
        /// The renderer is not settled at startup: a device loss can bring the game back on a
        /// different one, so the title is rebuilt whenever a device is, rather than being fixed
        /// once. <see cref="GraphicsBackendKind"/> already spells the names the way they are
        /// written down - Metal, OpenGL, Vulkan - so it is used as it stands.
        /// </remarks>
        /// <param name="renderer">The renderer currently drawing.</param>
        /// <returns>The title to give the window.</returns>
        internal static string TitleFor(GraphicsBackendKind renderer)
        {
            return $"{CtrDXProductName} v{Version} | {renderer}";
        }

        /// <summary>
        /// Reads the assembly's informational version.
        /// </summary>
        /// <remarks>
        /// Reported as recorded, including the "+&lt;hash&gt;" a revision-stamped development build
        /// appends: naming the exact commit is the point of showing a version on an unreleased
        /// build. A release sets no suffix, so it reads as a plain four-part version.
        /// </remarks>
        /// <returns>The version to show, or "Unknown" when the assembly carries none.</returns>
        private static string ResolveVersion()
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            return assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                ?? assembly.GetName().Version?.ToString()
                ?? "Unknown";
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
                // Forced: the process is going away, so a save still backing off from an earlier
                // failure would never be retried.
                Preferences.Update(force: true);
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
