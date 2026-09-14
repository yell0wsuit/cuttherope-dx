using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

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
    /// Entries are written from a pool thread, in batches. Writing one is a JS import, and a JS
    /// import blocks its caller until the browser thread has run it: from the game thread that
    /// cost about 9 ms an entry, so a teardown that logged a few dozen lines stalled for a third
    /// of a second. Errors are the exception. They are written on the thread that logs them,
    /// after everything queued before them, so a crash report is stored before the tab that
    /// shows it can be closed.
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
        /// Enforced here rather than by a filtering factory. Every entry that gets this far is
        /// formatted on the thread that logged it and then costs a proxied call to the thread that
        /// owns the database, so the entries nobody asked for are best dropped before either.
        /// </remarks>
        private const LogLevel Minimum = LogLevel.Information;

        private static readonly ConcurrentQueue<(string Line, LogLevel Level)> Unwritten = new();
        private static readonly Lock WriteGate = new();
        private static int _drainScheduled;

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

        /// <summary>Queues a formatted entry, writing it through at once when it is an error.</summary>
        /// <param name="line">The formatted entry.</param>
        /// <param name="level">Its severity.</param>
        private static void Write(string line, LogLevel level)
        {
            Unwritten.Enqueue((line, level));
            if (level >= LogLevel.Error)
            {
                Drain();
            }
            else if (Interlocked.CompareExchange(ref _drainScheduled, 1, 0) == 0)
            {
                _ = Task.Run(Drain);
            }
        }

        /// <summary>Writes every queued entry in order, a run of same-stream entries at a time.</summary>
        private static void Drain()
        {
            lock (WriteGate)
            {
                // Cleared before emptying the queue, so an entry arriving during the write
                // schedules a drain of its own rather than waiting for one that has already looked.
                _ = Interlocked.Exchange(ref _drainScheduled, 0);

                List<string> run = [];
                bool runIsError = false;
                bool runIsUrgent = false;
                while (Unwritten.TryDequeue(out (string Line, LogLevel Level) entry))
                {
                    bool isError = entry.Level >= LogLevel.Error;
                    if (run.Count > 0 && isError != runIsError)
                    {
                        WriteRun(run, runIsError, runIsUrgent);
                        run.Clear();
                        runIsUrgent = false;
                    }

                    runIsError = isError;
                    runIsUrgent |= entry.Level >= LogLevel.Warning;
                    run.Add(entry.Line);
                }

                if (run.Count > 0)
                {
                    WriteRun(run, runIsError, runIsUrgent);
                }
            }
        }

        /// <summary>Writes consecutive entries bound for the same console stream as one call each.</summary>
        /// <param name="lines">The entries, oldest first.</param>
        /// <param name="error">Whether they go to the error stream.</param>
        /// <param name="urgent">Whether the store should write them through rather than batch them.</param>
        /// <remarks>
        /// Joined with newlines, which is also how the store joins separate entries, so the record
        /// reads the same as if each had been appended alone.
        /// </remarks>
        private static void WriteRun(List<string> lines, bool error, bool urgent)
        {
            string text = string.Join('\n', lines);
            LogInterop.Append(text, urgent);
            if (error)
            {
                Console.Error.WriteLine(text);
            }
            else
            {
                Console.WriteLine(text);
            }
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

                // Formatted here, so the timestamp is when it happened rather than when it was written.
                string line = LogEntryFormat.Compose(logLevel, category, formatter(state, exception), exception);
                Write(line, logLevel);
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
