using System;
using System.Runtime.Versioning;

using CutTheRopeDX.Browser;
using CutTheRopeDX.Framework;
using CutTheRopeDX.Framework.Diagnostics;
using CutTheRopeDX.Framework.Platform;
using CutTheRopeDX.Rendering.Skia;

[assembly: SupportedOSPlatform("browser")]

await GLContextInterop.ImportAsync();
await FetchInterop.ImportAsync();
await AudioInterop.ImportAsync();
await StorageInterop.ImportAsync();
await HostEventInterop.ImportAsync();
await LogInterop.ImportAsync();
await BrowserCursorService.ImportAsync();
await BrowserVideoPlayer.ImportAsync();

// Installed before anything else can fail, so a boot that never reaches the game still leaves a
// record the player can export. The banner goes first for the same reason it does on desktop:
// whoever reads the report needs to know which build produced it.
Log.Factory = new BrowserLogStore();
_ = LogInterop.Begin(BrowserBuild.ComposeHeader());

// Announced before the content bundle starts downloading, so the level transfer overlaps a ~56 MB
// load rather than following it. A normal launch returns immediately.
_ = await PlaytestSession.BeginAsync();

// Whichever thread this is, it has to be the one that owns the GL context before Skia
// exists: the released SkiaSharp archive calls GL on whichever thread it is running on
// and consults no proxy.
#if WASM_THREADS
// This thread is a worker, so the canvas has to come to it, and the move is permanent -
// the page can never draw to that element again. Everything that could rule the threaded
// runtime out was checked by runtime-mode.js before this build was even fetched.
_ = HostShim.AcquireCanvas();
int[] canvas = GLContextInterop.TransferCanvasToThread("game", HostShim.ThreadId());
if (canvas.Length != 4)
{
    throw new InvalidOperationException("Could not transfer the canvas to the game thread.");
}

int deliveryAttempts = 0;
while (HostShim.CanvasReceived() == 0 && deliveryAttempts < 200)
{
    deliveryAttempts++;
    await System.Threading.Tasks.Task.Delay(25);
}
if (HostShim.CanvasReceived() == 0)
{
    throw new InvalidOperationException("The transferred canvas never arrived.");
}
#else
// This thread is the page's own, so the canvas is already here and nothing is handed
// over. Measuring it is all the arrangement the single-threaded runtime needs.
int[] canvas = GLContextInterop.MeasureCanvas("game");
if (canvas.Length != 4)
{
    throw new InvalidOperationException("The page has no canvas to draw to.");
}

if (HostShim.AcquireCanvas() == 0)
{
    throw new InvalidOperationException("Could not take the page's canvas.");
}
#endif

if (HostShim.CreateContext(canvas[2], canvas[3]) == 0)
{
    throw new InvalidOperationException("Could not create the game thread's WebGL context.");
}

int[] size = [canvas[2], canvas[3]];
float devicePixelRatio = canvas[0] > 0 ? (float)canvas[2] / canvas[0] : 1f;
Console.WriteLine($"gl: size={size[0]}x{size[1]}");

SkiaSurface surface = new(0, size[0], size[1]);

BrowserContentStore content = new("./content/");
WebAudioBackend audio = new("./content/");
await content.LoadTier0Async("./content/tier0.json");
await content.LoadAllAssetsAsync("./content/assets.json", audio);
PlatformServices.Content = content;

BrowserHostApp host = new();
PlatformServices.Host = host;
PlatformServices.Render = new SkiaRenderBackend(surface);
PlatformServices.Preferences = new LocalStoragePreferenceStore();
PlatformServices.Cursor = new BrowserCursorService();
PlatformServices.VideoPlayerFactory = () => new BrowserVideoPlayer();

BrowserAssetPlatform assets = new(surface);

ScreenPresentation.Instance =
    new ScreenPresentation((int)ViewportLayout.DesignWidth, (int)ViewportLayout.DesignHeight);
CutTheRopeDX.CtrBootstrap.Initialize(
    assets,
    audio,
    size[0],
    size[1],
    LanguageHelper.Current,
    devicePixelRatio);

GameLoop.Surface = surface;
GameLoop.Host = host;
InputRouter.Host = host;

HostEventQueue.Initialize();
GLContextInterop.WatchCanvas("game");
GameLoop.Start();

Console.WriteLine("boot complete");
