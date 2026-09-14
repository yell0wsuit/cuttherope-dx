using System;
using System.IO;

using CutTheRopeDX.Framework.Diagnostics;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Console;

namespace CutTheRopeDX.Desktop
{
    /// <summary>
    /// Writes a console entry in the same shape, and with the same account name taken out of it,
    /// as the one that goes to the file.
    /// </summary>
    /// <remarks>
    /// The console is not the lesser half of the pair. Reporting a problem usually means pasting
    /// what the terminal said, so an entry that leaks a home directory there leaks it exactly as
    /// far as the file would have. The default formatter is what the console had, which left the
    /// name in every path it printed, and the save directory is reported as a full path.
    /// <para>
    /// The level is bold and colored the way the default formatter colored it. Only on a terminal,
    /// though: <c>NO_COLOR</c> turns it off, and so does a redirected stream, so output piped to a
    /// file stays plain text.
    /// </para>
    /// </remarks>
    internal sealed class RedactingConsoleFormatter : ConsoleFormatter
    {
        /// <summary>The name the console options select this by.</summary>
        public const string FormatterName = "ctrdx";

        /// <summary>Lowest level the console writes to standard error rather than standard output.</summary>
        public const LogLevel StandardErrorThreshold = LogLevel.Error;

        private const string Escape = "\u001b[";

        private static readonly bool ColorDisabled =
            !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("NO_COLOR"));

        private readonly Func<LogLevel, bool> colorFor;

        /// <summary>Creates the formatter under its registered name, coloring when on a terminal.</summary>
        public RedactingConsoleFormatter() : this(ShouldColor)
        {
        }

        /// <summary>Creates the formatter under its registered name.</summary>
        /// <param name="colorFor">Whether an entry at a level is written in color.</param>
        internal RedactingConsoleFormatter(Func<LogLevel, bool> colorFor) : base(FormatterName)
        {
            this.colorFor = colorFor;
        }

        /// <inheritdoc />
        public override void Write<TState>(
            in LogEntry<TState> logEntry, IExternalScopeProvider scopeProvider, TextWriter textWriter)
        {
            string message = logEntry.Formatter is null
                ? logEntry.State?.ToString()
                : logEntry.Formatter(logEntry.State, logEntry.Exception);

            string line = LogEntryFormat.Compose(
                logEntry.LogLevel, logEntry.Category, message, logEntry.Exception);
            if (colorFor(logEntry.LogLevel))
            {
                line = ColorLevel(line, logEntry.LogLevel);
            }

            textWriter.WriteLine(line);
        }

        /// <summary>Wraps the level token of a composed entry in bold and its color.</summary>
        /// <param name="line">The composed entry.</param>
        /// <param name="level">The entry's level.</param>
        /// <returns>The entry with its <c>[Level]</c> token colored.</returns>
        private static string ColorLevel(string line, LogLevel level)
        {
            string token = "[" + level + "]";
            int at = line.IndexOf(token, StringComparison.Ordinal);
            if (at < 0)
            {
                return line;
            }

            string color = level switch
            {
                // https://stackoverflow.com/questions/4842424/list-of-ansi-color-escape-sequences
                LogLevel.Trace or LogLevel.Debug => Escape + "37m",
                LogLevel.Information => Escape + "32m",
                LogLevel.Warning => Escape + "33m",
                LogLevel.Error => Escape + "41m" + Escape + "30m",
                LogLevel.Critical => Escape + "41m" + Escape + "37m",
                LogLevel.None or _ => Escape + "39m",
            };

            // Bold, then the color; the reset undoes all three so the message keeps the terminal's own.
            return line[..at] + Escape + "1m" + color + token + Escape + "39m" + Escape + "49m" + Escape + "22m"
                + line[(at + token.Length)..];
        }

        /// <summary>Whether an entry at a level goes to a terminal that should get color.</summary>
        /// <param name="level">The entry's level, which picks the stream it is written to.</param>
        /// <returns><see langword="true"/> when the entry should be colored.</returns>
        private static bool ShouldColor(LogLevel level)
        {
            return !ColorDisabled
                && (level >= StandardErrorThreshold ? !Console.IsErrorRedirected : !Console.IsOutputRedirected);
        }
    }
}
