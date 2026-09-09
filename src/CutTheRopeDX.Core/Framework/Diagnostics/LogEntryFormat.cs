using System;
using System.Globalization;
using System.Text;

using Microsoft.Extensions.Logging;

namespace CutTheRopeDX.Framework.Diagnostics
{
    /// <summary>
    /// The one shape a written log entry takes, whichever host wrote it.
    /// </summary>
    /// <remarks>
    /// Desktop writes a file and the browser writes a database, but a report from either lands in
    /// front of the same person, so they are read more easily for being identical.
    /// </remarks>
    internal static class LogEntryFormat
    {
        /// <summary>What a redacted account name is replaced with.</summary>
        public const string RedactedUserName = "[redactedUsername]";

        /// <summary>
        /// Shortest account name worth redacting.
        /// </summary>
        /// <remarks>
        /// A one or two letter name occurs inside ordinary words, and replacing every one of them
        /// would corrupt the text it was meant to protect.
        /// </remarks>
        private const int ShortestRedactableName = 3;

        /// <summary>
        /// The account name to keep out of the log, or null when there is none worth hiding.
        /// </summary>
        /// <remarks>
        /// Read once. Logs travel: a save directory, a level path and every stack frame carry the
        /// home directory, and that names the person who sent the file.
        /// </remarks>
        private static readonly string UserName = ResolveUserName();

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
                .Append(" [")
                .Append(level)
                .Append("] ")
                .Append(category)
                .Append(' ')
                .Append(message);

            if (exception != null)
            {
                _ = line.AppendLine().Append(exception);
            }

            return Redact(line.ToString());
        }

        /// <summary>
        /// Takes the account name out of a line.
        /// </summary>
        /// <param name="text">The composed entry.</param>
        /// <returns>The entry with every occurrence of the account name replaced.</returns>
        /// <remarks>
        /// Done on the whole entry rather than on the message alone, because an attached exception
        /// carries the same paths through its stack frames.
        /// </remarks>
        public static string Redact(string text)
        {
            return UserName == null || text == null
                ? text
                : text.Replace(UserName, RedactedUserName, StringComparison.Ordinal);
        }

        /// <summary>
        /// Reads the account name this process runs as.
        /// </summary>
        /// <returns>The name, or null where there is none or it is too short to match safely.</returns>
        /// <remarks>
        /// The browser has no account to speak of, and asking can throw there rather than answer.
        /// </remarks>
        private static string ResolveUserName()
        {
            try
            {
                string name = Environment.UserName;
                return string.IsNullOrWhiteSpace(name) || name.Length < ShortestRedactableName
                    ? null
                    : name;
            }
            catch (PlatformNotSupportedException)
            {
                return null;
            }
        }
    }
}
