using System;

using CutTheRopeDX.Framework.Diagnostics;

using Microsoft.Extensions.Logging;

namespace CutTheRopeDX.Browser
{
    /// <summary>
    /// Keeps the web build's log where a player can send it back.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The desktop build writes a file the player can attach to a report; a page has nowhere to
    /// put one, so entries go to a per-run record in IndexedDB that the page's export button
    /// packs into a zip. Everything also reaches the developer console, which is where anyone
    /// with the page already open would look first.
    /// </para>
    /// <para>
    /// This is both the factory and the provider, written against the logging abstractions alone.
    /// The usual <c>LoggerFactory.Create</c> brings in dependency injection, options and
    /// primitives, and those assemblies abort this runtime while it is still loading its AOT
    /// images - the page never reaches its entry point at all.
    /// </para>
    /// </remarks>
    internal sealed class BrowserLogStore : ILoggerProvider, ILoggerFactory
    {
        /// <summary>
        /// Lowest severity that reaches the store.
        /// </summary>
        /// <remarks>
        /// Enforced here rather than by a filtering factory. Every entry that gets this far costs
        /// a proxied call from the game thread to the one that owns the database, so the entries
        /// nobody asked for are best dropped before they become one.
        /// </remarks>
        private const LogLevel Minimum = LogLevel.Information;

        /// <summary>
        /// Creates a logger for one category.
        /// </summary>
        /// <param name="categoryName">The category.</param>
        /// <returns>The logger.</returns>
        public ILogger CreateLogger(string categoryName)
        {
            return new BrowserLogger(categoryName);
        }

        /// <summary>
        /// Ignores an added provider.
        /// </summary>
        /// <param name="provider">Unused.</param>
        /// <remarks>
        /// This is its own factory and its own single provider. Nothing in the web build composes
        /// a second one, and pulling in the machinery that would allow it is what this class
        /// exists to avoid.
        /// </remarks>
        public void AddProvider(ILoggerProvider provider)
        {
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
                // Nothing here reads scopes, but the contract says a disposable, and a scope
                // provider added later would dereference whatever this hands back.
                return NullScope.Instance;
            }

            public bool IsEnabled(LogLevel logLevel)
            {
                return logLevel is >= Minimum and not LogLevel.None;
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

    /// <summary>A scope that records nothing and can still be disposed.</summary>
    internal sealed class NullScope : IDisposable
    {
        /// <summary>The only instance needed, since it carries no state.</summary>
        public static NullScope Instance { get; } = new();

        private NullScope()
        {
        }

        /// <inheritdoc />
        public void Dispose()
        {
        }
    }
    }
}
