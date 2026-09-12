using System;
using System.Collections.Generic;

using Microsoft.Extensions.Logging;

namespace CutTheRopeDX.Tests
{
    /// <summary>One captured log entry.</summary>
    internal readonly record struct LogRecord(
        string Category,
        LogLevel Level,
        string Message,
        Exception Exception);

    /// <summary>Logger provider that keeps every entry for a test to assert against.</summary>
    internal sealed class RecordingLoggerProvider : ILoggerProvider
    {
        private readonly List<LogRecord> records = [];

        public IReadOnlyList<LogRecord> Records
        {
            get { lock (records) { return [.. records]; } }
        }

        public ILogger CreateLogger(string categoryName)
        {
            return new RecordingLogger(this, categoryName);
        }

        public void Dispose()
        {
        }

        private void Add(LogRecord record)
        {
            lock (records) { records.Add(record); }
        }

        private sealed class RecordingLogger(RecordingLoggerProvider owner, string category) : ILogger
        {
            public IDisposable BeginScope<TState>(TState state)
                where TState : notnull
            {
                return null;
            }

            public bool IsEnabled(LogLevel logLevel)
            {
                return true;
            }

            public void Log<TState>(
                LogLevel logLevel,
                EventId eventId,
                TState state,
                Exception exception,
                Func<TState, Exception, string> formatter)
            {
                owner.Add(new LogRecord(category, logLevel, formatter(state, exception), exception));
            }
        }
    }
}
