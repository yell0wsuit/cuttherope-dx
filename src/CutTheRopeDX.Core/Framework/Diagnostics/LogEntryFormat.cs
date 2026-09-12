using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
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
        /// Every spelling of the account name to keep out of the log, longest first.
        /// </summary>
        /// <remarks>
        /// Read once. Logs travel: a save directory, a level path and every stack frame carry the
        /// home directory, and that names the person who sent the file.
        /// <para>
        /// More than one spelling, because nothing normalises a path on the way in. The profile
        /// directory can be named differently from the account after a rename, and Windows gives
        /// every long name an 8.3 alias that <c>GetTempPath</c> hands back on any machine whose
        /// TEMP is set that way. <c>yell0wsuit</c> arriving as <c>YELL0W~1</c> is still the
        /// person's name. Longest first so a match cannot be eaten by a shorter prefix of itself.
        /// </para>
        /// </remarks>
        private static readonly string[] UserNames = ResolveUserNames();

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
            if (text == null)
            {
                return text;
            }

            string redacted = text;
            foreach (string name in UserNames)
            {
                // Case-insensitive: a Windows path compares that way and reaches the log in
                // whatever case its source happened to use.
                redacted = redacted.Replace(name, RedactedUserName, StringComparison.OrdinalIgnoreCase);
            }

            return redacted;
        }

        /// <summary>
        /// Collects the spellings of this process's account name worth hiding.
        /// </summary>
        /// <returns>The names, longest first; empty where there is none worth matching.</returns>
        /// <remarks>
        /// The browser has no account to speak of, and asking can throw there rather than answer.
        /// </remarks>
        private static string[] ResolveUserNames()
        {
            List<string> names = [];
            try
            {
                Add(Environment.UserName);
                Add(LeafOf(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)));
            }
            catch (PlatformNotSupportedException)
            {
                return [];
            }

            // The alias keeps the trailing digit, which distinguishes colliding names and says
            // nothing on its own once the six characters in front of it are gone.
            foreach (string name in names.ToArray())
            {
                if (name.Length > 8)
                {
                    Add(name[..6] + "~");
                }
            }

            names.Sort(static (left, right) => right.Length.CompareTo(left.Length));
            return [.. names];

            void Add(string name)
            {
                if (!string.IsNullOrWhiteSpace(name)
                    && name.Length >= ShortestRedactableName
                    && !names.Contains(name, StringComparer.OrdinalIgnoreCase))
                {
                    names.Add(name);
                }
            }
        }

        /// <summary>The last segment of a directory path, with any trailing separator ignored.</summary>
        /// <param name="path">The directory path.</param>
        /// <returns>The segment, or null when there is none.</returns>
        private static string LeafOf(string path)
        {
            return string.IsNullOrEmpty(path)
                ? null
                : new DirectoryInfo(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)).Name;
        }
    }
}
