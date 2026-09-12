using System;
using System.Globalization;
using System.IO;
using System.Threading;

using CutTheRopeDX.Framework.Diagnostics;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;

using NReco.Logging.File;

namespace CutTheRopeDX.Desktop
{
    /// <summary>
    /// Builds the desktop logging pipeline: a rolling file beside the save data, and a console
    /// that stays quiet unless something is wrong.
    /// </summary>
    internal static class LoggingSetup
    {
        /// <summary>Command line switch that sets the minimum level for both sinks.</summary>
        public const string LevelSwitch = "--log-level";

        /// <summary>Size at which the log rolls.</summary>
        private const long FileSizeLimitBytes = 1024 * 1024;

        /// <summary>How many log files are kept, the live one included.</summary>
        private const int MaxRollingFiles = 4;

        /// <summary>Prefix every log file name starts with.</summary>
        private const string LogFilePrefix = "ctrdx";

        /// <summary>Which build this is, as the banner reports it.</summary>
#if DEBUG
        private const string Configuration = "Debug";
#else
        private const string Configuration = "Release";
#endif

        /// <summary>
        /// How many log files are kept in the directory, this run's own included.
        /// </summary>
        /// <remarks>
        /// A file name of its own per run means nothing ever overwrites itself, so something has
        /// to bound the directory instead. Ten files at <see cref="FileSizeLimitBytes"/> apiece is
        /// the worst case.
        /// </remarks>
        private const int MaxRetainedLogFiles = 10;

        /// <summary>
        /// Reads <c>--log-level</c>.
        /// </summary>
        /// <param name="args">Raw process arguments.</param>
        /// <returns>The requested level, or <see langword="null"/> when the switch is absent.</returns>
        /// <exception cref="ArgumentException">The value is not a level name.</exception>
        public static LogLevel? ParseLevel(string[] args)
        {
            string value = null;
            int index = Array.IndexOf(args, LevelSwitch);
            if (index >= 0 && index + 1 < args.Length)
            {
                value = args[index + 1];
            }
            else
            {
                // Both spellings, because a switch that takes a value is written either way and
                // the joined one used to fall through to the default without saying anything.
                string joined = Array.Find(args, argument =>
                    argument.StartsWith(LevelSwitch + "=", StringComparison.Ordinal));
                value = joined?[(LevelSwitch.Length + 1)..];
            }

            return string.IsNullOrEmpty(value)
                ? null
                : value.ToLowerInvariant() switch
                {
                    "trace" => LogLevel.Trace,
                    "debug" => LogLevel.Debug,
                    "info" => LogLevel.Information,
                    "warn" => LogLevel.Warning,
                    "error" => LogLevel.Error,
                    _ => throw new ArgumentException(
                        $"Unknown {LevelSwitch} '{value}'. Expected trace, debug, info, warn or error."),
                };
        }

        /// <summary>
        /// Names the directory a save directory's logs are written to.
        /// </summary>
        /// <param name="saveDirectory">Directory the save data lives in.</param>
        /// <returns>The log directory, which need not exist yet.</returns>
        public static string DirectoryFor(string saveDirectory)
        {
            return Path.Combine(saveDirectory, "logs");
        }

        /// <summary>
        /// Builds the name of one run's log.
        /// </summary>
        /// <param name="stamp">When the run started.</param>
        /// <param name="fallback">Whether this is the second name tried after the first would not open.</param>
        /// <returns>The file name, with no directory part.</returns>
        /// <remarks>
        /// Each run writes its own file, so the log a player sends carries that session alone and
        /// starting the game again never overwrites the evidence being reported. The fallback adds
        /// the process id, which is what separates two runs that started in the same second.
        /// </remarks>
        public static string LogFileName(DateTime stamp, bool fallback)
        {
            string name = LogFilePrefix + "-" + stamp.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
            return fallback ? $"{name}-{Environment.ProcessId}.log" : name + ".log";
        }

        /// <summary>
        /// Builds the factory, timestamping this run's log with the current time.
        /// </summary>
        /// <param name="saveDirectory">Directory the save data lives in; the log goes beside it.</param>
        /// <param name="requested">The level from <see cref="ParseLevel"/>, if any.</param>
        /// <returns>The factory, which the caller owns and must dispose.</returns>
        public static ILoggerFactory Create(string saveDirectory, LogLevel? requested)
        {
            return Create(saveDirectory, requested, DateTime.Now);
        }

        /// <summary>
        /// Builds the factory.
        /// </summary>
        /// <param name="saveDirectory">Directory the save data lives in; the log goes beside it.</param>
        /// <param name="requested">The level from <see cref="ParseLevel"/>, if any.</param>
        /// <param name="stamp">The time this run's log file is named after.</param>
        /// <returns>The factory, which the caller owns and must dispose.</returns>
        /// <remarks>
        /// Without a switch both sinks keep everything from <see cref="LogLevel.Information"/> up,
        /// so what a player sees in a terminal is what they send in the file. A switch overrides
        /// both: someone who asks for trace wants to see it, and someone who asks for warnings
        /// wants the rest gone.
        /// </remarks>
        public static ILoggerFactory Create(string saveDirectory, LogLevel? requested, DateTime stamp)
        {
            LogLevel fileLevel = requested ?? LogLevel.Information;
            LogLevel consoleLevel = requested ?? LogLevel.Information;
            string directory = DirectoryFor(saveDirectory);
            string path = Path.Combine(directory, LogFileName(stamp, fallback: false));
            bool canWriteFile = TryCreateLogDirectory(directory);
            int fallbackAttempted = 0;

            if (canWriteFile)
            {
                PruneOldLogs(directory);
                WriteHeader(path);
            }

            try
            {
                return BuildFactory(canWriteFile);
            }
            catch (IOException)
            {
                return BuildFactory(false);
            }
            catch (UnauthorizedAccessException)
            {
                return BuildFactory(false);
            }

            // NReco suppresses the primary open failure, but opening its proposed fallback
            // can still throw. Construct the file provider before the console provider so a
            // failed file constructor can fall back without opening another console writer.
            ILoggerFactory BuildFactory(bool includeFile)
            {
                return LoggerFactory.Create(builder =>
                {
                    _ = builder.SetMinimumLevel(fileLevel < consoleLevel ? fileLevel : consoleLevel);
                    _ = builder.AddFilter<FileLoggerProvider>(null, fileLevel);
                    _ = builder.AddFilter<ConsoleLoggerProvider>(null, consoleLevel);
                    if (includeFile)
                    {
                        _ = builder.AddFile(path, options =>
                        {
                            options.Append = true;
                            options.FileSizeLimitBytes = FileSizeLimitBytes;
                            options.MaxRollingFiles = MaxRollingFiles;
                            // Keep normal launches on the primary name even when a previous
                            // process left a newer fallback file beside it.
                            options.RollingFilesConvention = FileLoggerOptions.FileRollingConvention.AscendingStableBase;
                            options.FormatLogEntry = FormatEntry;

                            // The provider opens the file as it is built and propagates the failure,
                            // and this runs before the crash handlers exist. Without this the game
                            // fails to start over a log it could not open, showing nothing at all on
                            // Windows. One fallback, then console only; it never retries and never
                            // throws.
                            options.HandleFileError = error =>
                            {
                                if (Interlocked.Exchange(ref fallbackAttempted, 1) == 0)
                                {
                                    string fallbackPath =
                                        Path.Combine(directory, LogFileName(stamp, fallback: true));
                                    WriteHeader(fallbackPath);
                                    error.UseNewLogFileName(fallbackPath);
                                }
                            };
                        });
                    }

                    // Error and worse keep going to stderr, which is where the sites this replaces
                    // already wrote. The default sends every level to stdout.
                    //
                    // The formatter is the file's, so the console does not print a path the file
                    // was careful to redact: a bug report is usually a paste of the terminal.
                    _ = builder.AddConsole(options =>
                    {
                        options.LogToStandardErrorThreshold = LogLevel.Error;
                        options.FormatterName = RedactingConsoleFormatter.FormatterName;
                    });

                    // Registered straight into the container rather than through
                    // AddConsoleFormatter, which binds an options type out of configuration and
                    // so is annotated as unsafe to trim and to compile ahead of time. The release
                    // publishes with NativeAOT, and this formatter has nothing to bind.
                    _ = builder.Services.AddSingleton<ConsoleFormatter, RedactingConsoleFormatter>();
                });
            }
        }

        /// <summary>
        /// Names the build a log came from, and the machine that ran it.
        /// </summary>
        /// <returns>The banner, with no trailing newline.</returns>
        /// <remarks>
        /// Deliberately not a log entry: this is the first thing anyone reads on a report, and a
        /// timestamp, level and category in front of each line would only get in the way of it.
        /// </remarks>
        public static string ComposeHeader()
        {
            return string.Join(
                Environment.NewLine,
                SdlDesktopHost.CtrDXProductName,
                Configuration + " version",
                "Version: " + SdlDesktopHost.Version,
                "OS: " + DeviceReport.OperatingSystem,
                "CPU: " + DeviceReport.Processor,
                "RAM: " + DeviceReport.Memory());
        }

        /// <summary>
        /// Puts the banner at the top of a run's log, before the provider opens it.
        /// </summary>
        /// <param name="path">The log file about to be written.</param>
        /// <remarks>
        /// A log that cannot be headed is still worth having, so a failure here is dropped: the
        /// provider is about to report the same problem through its own fallback.
        /// </remarks>
        private static void WriteHeader(string path)
        {
            try
            {
                File.AppendAllText(path, ComposeHeader() + Environment.NewLine);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        /// <summary>
        /// Deletes the oldest logs, keeping room for the run that is about to start one.
        /// </summary>
        /// <param name="directory">The log directory.</param>
        /// <remarks>
        /// A file that will not go - one another run still holds open, say - is left alone. Losing
        /// the pruning is not worth failing a startup over.
        /// </remarks>
        private static void PruneOldLogs(string directory)
        {
            try
            {
                FileInfo[] existing = new DirectoryInfo(directory).GetFiles(LogFilePrefix + "-*.log");
                Array.Sort(existing, static (a, b) => b.LastWriteTimeUtc.CompareTo(a.LastWriteTimeUtc));
                for (int i = MaxRetainedLogFiles - 1; i < existing.Length; i++)
                {
                    try
                    {
                        existing[i].Delete();
                    }
                    catch (IOException)
                    {
                    }
                    catch (UnauthorizedAccessException)
                    {
                    }
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        private static bool TryCreateLogDirectory(string directory)
        {
            try
            {
                _ = Directory.CreateDirectory(directory);
                return true;
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
        }

        /// <summary>
        /// Renders one entry for the file provider.
        /// </summary>
        /// <param name="entry">The entry to render.</param>
        /// <returns>The formatted line.</returns>
        /// <remarks>
        /// This only unpacks the entry. <see cref="Compose"/> holds the formatting, because
        /// <c>LogMessage</c> is a struct of readonly fields with no public constructor and so
        /// cannot be built by a test.
        /// </remarks>
        public static string FormatEntry(LogMessage entry)
        {
            return Compose(entry.LogLevel, entry.LogName, entry.Message, entry.Exception);
        }

        /// <summary>
        /// Formats one entry.
        /// </summary>
        /// <param name="level">Severity.</param>
        /// <param name="category">Category name.</param>
        /// <param name="message">The rendered message.</param>
        /// <param name="exception">The attached exception, if any.</param>
        /// <returns>The line, in the shape every host writes.</returns>
        public static string Compose(LogLevel level, string category, string message, Exception exception)
        {
            return LogEntryFormat.Compose(level, category, message, exception);
        }
    }
}
