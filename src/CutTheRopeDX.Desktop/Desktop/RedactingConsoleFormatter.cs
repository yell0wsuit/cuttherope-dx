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
    /// </remarks>
    internal sealed class RedactingConsoleFormatter : ConsoleFormatter
    {
        /// <summary>The name the console options select this by.</summary>
        public const string FormatterName = "ctrdx";

        /// <summary>Creates the formatter under its registered name.</summary>
        public RedactingConsoleFormatter() : base(FormatterName)
        {
        }

        /// <inheritdoc />
        public override void Write<TState>(
            in LogEntry<TState> logEntry, IExternalScopeProvider scopeProvider, TextWriter textWriter)
        {
            string message = logEntry.Formatter is null
                ? logEntry.State?.ToString()
                : logEntry.Formatter(logEntry.State, logEntry.Exception);

            textWriter.WriteLine(LogEntryFormat.Compose(
                logEntry.LogLevel, logEntry.Category, message, logEntry.Exception));
        }
    }
}
