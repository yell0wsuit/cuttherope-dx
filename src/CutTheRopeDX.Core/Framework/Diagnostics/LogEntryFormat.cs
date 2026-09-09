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
