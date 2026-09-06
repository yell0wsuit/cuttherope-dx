using System;
using System.Diagnostics;
using System.IO;

using CutTheRopeDX.Commons;
using CutTheRopeDX.Desktop.Platform;
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
    /// <summary>Opt-in SDL composition; Batch 2 deliberately has silent audio and skipped movies.</summary>
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
        private readonly SdlHostLoop loop = new();
        private readonly Stopwatch clock = new();
        private TimeSpan nextSave = TimeSpan.FromSeconds(1);
        private int frameCount;
        private int frameLimit;
        private string screenshot;

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

        public void DrawMovie() { }
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
            GraphicsBackendKind? forced = renderer switch
            {
                "auto" => null,
                "metal" => GraphicsBackendKind.Metal,
                "gl" => GraphicsBackendKind.OpenGL,
                "vulkan" => GraphicsBackendKind.Vulkan,
                "angle" => GraphicsBackendKind.Angle,
                _ => throw new ArgumentException($"Unknown SDL renderer '{renderer}'."),
            };
            if (!SDL.Init(SDL.InitFlags.Video | SDL.InitFlags.Events))
            {
                throw new InvalidOperationException(SDL.GetError());
            }

            initialized = true;
            string platform = OperatingSystem.IsMacOS() ? "macos" : OperatingSystem.IsWindows() ? "windows" : "linux";
            selection = BackendSelector.Select(platform, forced, CreateDevice, ValidateDevice);
            SdlGraphicsDevice device = selection.Device;
            _ = SDL.SetWindowTitle(device.Window, "Cut The Rope: DX - SDL preview (audio/video pending)");
            Console.WriteLine($"[sdl] renderer={selection.Kind}; audio silent, movies skipped (Batch 2)");
            foreach (Exception failure in selection.Failures)
            {
                Console.Error.WriteLine($"[sdl] rejected renderer: {failure.Message}");
            }

            string root = SkiaAssetPlatform.ResolveContentRoot(AppContext.BaseDirectory);
            PlatformServices.Content = new FileContentStore(root);
            Preferences.LoadPreferences();
            window = new(device.Window);
            window.Initialize(Preferences.GetIntForKey("PREFS_WINDOW_WIDTH"), Preferences.GetIntForKey("PREFS_WINDOW_HEIGHT"), Preferences.GetBooleanForKey("PREFS_WINDOW_FULLSCREEN"));
            input = new()
            {
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

                    loop.SetSuspended(!focused, clock.Elapsed);
                },
                Quit = Exit,
                Resized = () => { device.Resize(); window.RefreshSurface(); },
            };
            cursor = new(input.ClearInput);
            cursor.Load(Path.Combine(root, "images/cursor.png"), Path.Combine(root, "images/cursor_active.png"));
            PlatformServices.Host = this;
            PlatformServices.Window = window;
            PlatformServices.Cursor = cursor;
            PlatformServices.Updates = new DesktopUpdateService();
            PlatformServices.FileWatchers = new DesktopFileWatcherFactory();
            PlatformServices.RichPresence = new RPCHelpers();
            PlatformServices.VideoPlayerFactory = static () => new VideoPlayerMonoGame();
            render = new(device);
            PlatformServices.Render = render;
            assets = new(PlatformServices.Content, device.Context);
            _ = SDL.GetWindowSize(device.Window, out int width, out int height);
            _ = SDL.GetWindowSizeInPixels(device.Window, out int pixels, out int ignored);
            CtrBootstrap.Initialize(assets, null, width, height, LanguageHelper.FromSystemCulture(), (float)pixels / width);
            window.RefreshSurface();
            clock.Start();
            loop.Reset(clock.Elapsed);
            while (!exiting)
            {
                while (SDL.PollEvent(out SDL.Event evt))
                {
                    input.HandleEvent(in evt);
                }

                if (exiting)
                {
                    break;
                }

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
            cursor?.Dispose();
            render?.Dispose();
            assets?.Dispose();
            selection?.Dispose();
            if (initialized) { SDL.Quit(); initialized = false; }
        }
    }
}
