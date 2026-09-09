using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;

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

        /// <summary>Name of the log, before any per-process fallback.</summary>
        private const string LogFileName = "ctrdx.log";

        /// <summary>
        /// Reads <c>--log-level</c>.
        /// </summary>
        /// <param name="args">Raw process arguments.</param>
        /// <returns>The requested level, or <see langword="null"/> when the switch is absent.</returns>
        /// <exception cref="ArgumentException">The value is not a level name.</exception>
        public static LogLevel? ParseLevel(string[] args)
        {
            int index = Array.IndexOf(args, LevelSwitch);
            if (index < 0 || index + 1 >= args.Length)
            {
                return null;
            }

            string value = args[index + 1];
            return value.ToLowerInvariant() switch
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
        /// Builds the factory.
        /// </summary>
        /// <param name="saveDirectory">Directory the save data lives in; the log goes beside it.</param>
        /// <param name="requested">The level from <see cref="ParseLevel"/>, if any.</param>
        /// <returns>The factory, which the caller owns and must dispose.</returns>
        /// <remarks>
        /// Without a switch the file keeps everything from <see cref="LogLevel.Information"/> up
        /// while the console shows only warnings and worse, so a scripted run's stdout stays
        /// readable. A switch overrides both: someone who asks for trace wants to see it.
        /// </remarks>
        public static ILoggerFactory Create(string saveDirectory, LogLevel? requested)
        {
            LogLevel fileLevel = requested ?? LogLevel.Information;
            LogLevel consoleLevel = requested ?? LogLevel.Warning;
            string path = Path.Combine(saveDirectory, "logs", LogFileName);
            bool canWriteFile = TryCreateLogDirectory(Path.GetDirectoryName(path));
            int fallbackAttempted = 0;

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
                                    error.UseNewLogFileName(Path.Combine(
                                        Path.GetDirectoryName(path), $"ctrdx-{Environment.ProcessId}.log"));
                                }
                            };
                        });
                    }

                    // Error and worse keep going to stderr, which is where the sites this replaces
                    // already wrote. The default sends every level to stdout.
                    _ = builder.AddConsole(options => options.LogToStandardErrorThreshold = LogLevel.Error);
                });
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
        /// <returns>The line, plus the exception on lines of its own when there is one.</returns>
        /// <remarks>
        /// The default formatter runs the message straight into the exception, which is unreadable
        /// exactly when it matters most.
        /// </remarks>
        public static string Compose(LogLevel level, string category, string message, Exception exception)
        {
            StringBuilder line = new();
            _ = line.Append(DateTime.Now.ToString("O", CultureInfo.InvariantCulture))
                .Append('\t')
                .Append(level)
                .Append('\t')
                .Append(category)
                .Append('\t')
                .Append(message);

            if (exception != null)
            {
                _ = line.AppendLine().Append(exception);
            }

            return line.ToString();
        }
    }
}
