using System;
using System.Collections.Concurrent;
using System.Threading;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace CutTheRopeDX.Framework.Diagnostics
{
    /// <summary>
    /// The logging seam. A host installs a factory at boot; one that installs nothing logs
    /// nowhere, which is how headless runs and the browser stay silent without a null check
    /// at every call site.
    /// </summary>
    internal static class Log
    {
        // Each lookup uses one snapshot of the factory and its logger cache. Replacing the
        // factory publishes a fresh cache for subsequent lookups.
        private sealed record State(ILoggerFactory Factory, ConcurrentDictionary<string, ILogger> Loggers);

        private static State state = NewState(NullLoggerFactory.Instance);

        /// <summary>
        /// The factory every logger is drawn from. Assigning retires the cached loggers, so the
        /// order in which types first log does not matter. Assigning null restores the no-op.
        /// </summary>
        public static ILoggerFactory Factory
        {
            get => Volatile.Read(ref state).Factory;
            set => Volatile.Write(ref state, NewState(value ?? NullLoggerFactory.Instance));
        }

        /// <summary>Returns the logger for a category, creating it once.</summary>
        /// <param name="category">Category name, from <see cref="LogCategories"/>.</param>
        /// <returns>The cached logger.</returns>
        public static ILogger For(string category)
        {
            State current = Volatile.Read(ref state);
            return current.Loggers.GetOrAdd(
                category,
                static (name, factory) => factory.CreateLogger(name),
                current.Factory);
        }

        private static State NewState(ILoggerFactory factory)
        {
            return new State(factory, new ConcurrentDictionary<string, ILogger>(StringComparer.Ordinal));
        }
    }
}
