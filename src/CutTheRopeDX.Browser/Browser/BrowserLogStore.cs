using System;

using CutTheRopeDX.Framework.Diagnostics;

using Microsoft.Extensions.Logging;

namespace CutTheRopeDX.Browser
{
    /// <summary>
    /// Keeps the web build's log where a player can send it back.
    /// </summary>
    /// <remarks>
    /// The desktop build writes a file the player can attach to a report; a page has nowhere to
    /// put one, so entries go to a per-run record in IndexedDB that the page's export button
    /// packs into a zip. Everything also reaches the developer console, which is where anyone
    /// with the page already open would look first.
    /// </remarks>
    internal sealed class BrowserLogStore : ILoggerProvider
    {
        /// <summary>
        /// Creates a logger for one category.
        /// </summary>
        /// <param name="categoryName">The category.</param>
        /// <returns>The logger.</returns>
        public ILogger CreateLogger(string categoryName)
        {
            return new BrowserLogger(categoryName);
        }

        /// <summary>Nothing to release: the store outlives the provider by design.</summary>
        public void Dispose()
        {
        }

        private sealed class BrowserLogger(string category) : ILogger
        {
            public IDisposable BeginScope<TState>(TState state)
                where TState : notnull
            {
                return null;
            }

            public bool IsEnabled(LogLevel logLevel)
            {
                return logLevel != LogLevel.None;
            }

            public void Log<TState>(
                LogLevel logLevel,
                EventId eventId,
                TState state,
                Exception exception,
                Func<TState, Exception, string> formatter)
            {
                if (!IsEnabled(logLevel))
                {
                    return;
                }

                string line = LogEntryFormat.Compose(logLevel, category, formatter(state, exception), exception);

                // A failure worth reporting is written through rather than buffered: the tab that
                // is about to be closed over it would take the batch with it.
                LogInterop.Append(line, logLevel >= LogLevel.Warning);
                if (logLevel >= LogLevel.Error)
                {
                    Console.Error.WriteLine(line);
                }
                else
                {
                    Console.WriteLine(line);
                }
            }
        }
    }
}
