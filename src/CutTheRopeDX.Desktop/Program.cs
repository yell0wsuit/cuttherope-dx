using System;
using System.Xml.Linq;

using CutTheRopeDX;
using CutTheRopeDX.Desktop;
using CutTheRopeDX.Framework;
using CutTheRopeDX.Framework.Core;
using CutTheRopeDX.Framework.Diagnostics;
using CutTheRopeDX.GameMain;

using Microsoft.Extensions.Logging;

CommandLineResult cli = CommandLine.Parse(args);

// Held rather than logged where it happens: the factory is built further down, after the headless
// branch has had its chance to return without one.
string handshake = null;

if (cli.IsCustomLevel)
{
    if (cli.ErrorMessage != null)
    {
        Console.Error.WriteLine(cli.ErrorMessage);
        return 1;
    }

    if (!CustomLevelFile.TryLoad(cli.LevelPath, out XElement _, out string loadError))
    {
        Console.Error.WriteLine(loadError);
        return 1;
    }

    CustomLevelSession.Activate(cli.LevelPath);

    // Tell the launcher, before the run loop blocks, that this build understood --level and loaded the
    // level. A build too old for the switch never reaches here, so the line's absence is the signal.
    handshake = PlaytestHandshake.Announce(Console.Out);
}

if (cli.IsHeadless)
{
    // A fixed-frame smoke run proving the shipped binary boots and ticks with no window.
    // Scripted scenarios are out of scope; tests drive the engine in-process instead.
    HeadlessHost.Boot(HeadlessHost.DefaultWidth, HeadlessHost.DefaultHeight, LanguageHelper.FromSystemCulture());
    for (int i = 0; i < 600; i++)
    {
        HeadlessHost.Tick(0.016f);
    }

    // Report where the run landed. Without this an "exit 0" would also be printed by a run
    // wedged on the loading screen, which is exactly the failure this smoke test must catch.
    Console.WriteLine($"[headless] ran 600 frames, active controller = {HeadlessHost.ActiveControllerName()}");
    return CustomLevelSession.IsActive && !HeadlessHost.IsInGameplay() ? 1 : 0;
}

LogLevel? requestedLevel;
try
{
    requestedLevel = LoggingSetup.ParseLevel(args);
}
catch (ArgumentException error)
{
    Console.Error.WriteLine(error.Message);
    return 1;
}

// Resolve the save directory before constructing its file sink, then replay the diagnostics
// collected during resolution now that a logger can receive them.
using ILoggerFactory loggerFactory = LoggingSetup.Create(Preferences.SaveDirectory, requestedLevel);
Log.Factory = loggerFactory;
CrashHandlers.Install(loggerFactory);

if (CustomLevelSession.IsActive)
{
    ILogger playtestLogger = Log.For(LogCategories.Playtest);
    PlaytestLog.SessionActive(playtestLogger, CustomLevelSession.LevelPath);
    PlaytestLog.Handshake(playtestLogger, handshake);
}

ILogger startupLogger = Log.For(LogCategories.Preferences);
foreach (Preferences.StartupDiagnostic diagnostic in Preferences.DrainStartupDiagnostics())
{
    StartupLog.Diagnostic(startupLogger, diagnostic.Level, diagnostic.Message);
}

using SdlDesktopHost host = new();
host.Run(args);
return 0;

/// <summary>Log messages collected before the desktop factory exists.</summary>
internal static partial class StartupLog
{
    [LoggerMessage(Message = "{Message}")]
    internal static partial void Diagnostic(ILogger logger, LogLevel level, string message);
}
