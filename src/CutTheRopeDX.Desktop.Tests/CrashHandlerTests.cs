using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

using CutTheRopeDX.Framework.Diagnostics;
using CutTheRopeDX.Tests;

using Microsoft.Extensions.Logging;

using Xunit;

namespace CutTheRopeDX.Desktop.Tests
{
    public class CrashHandlerTests
    {
        [Fact]
        public void AnUnhandledExceptionIsLoggedAsCriticalWithTheException()
        {
            RecordingLoggerProvider recorder = new();
            using ILoggerFactory factory = LoggerFactory.Create(builder => builder.AddProvider(recorder));
            Log.Factory = factory;
            try
            {
                InvalidOperationException thrown = new("no drawable");

                CrashHandlers.OnUnhandled(null, new UnhandledExceptionEventArgs(thrown, isTerminating: true));

                LogRecord entry = Assert.Single(recorder.Records);
                Assert.Equal(LogLevel.Critical, entry.Level);
                Assert.Same(thrown, entry.Exception);
            }
            finally
            {
                Log.Factory = null;
            }
        }

        [Fact]
        public void AnUnobservedTaskExceptionIsLoggedAsErrorAndMarkedObserved()
        {
            RecordingLoggerProvider recorder = new();
            using ILoggerFactory factory = LoggerFactory.Create(builder => builder.AddProvider(recorder));
            Log.Factory = factory;
            try
            {
                AggregateException thrown = new(new TimeoutException("update check"));
                UnobservedTaskExceptionEventArgs args = new(thrown);

                CrashHandlers.OnUnobserved(null, args);

                LogRecord entry = Assert.Single(recorder.Records);
                Assert.Equal(LogLevel.Error, entry.Level);
                Assert.Same(thrown, entry.Exception);
                Assert.True(args.Observed);
            }
            finally
            {
                Log.Factory = null;
            }
        }

        [Fact]
        public void TheCrashRecordSurvivesToDisk()
        {
            string root = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            _ = Directory.CreateDirectory(root);
            try
            {
                DateTime stamp = new(2026, 9, 9, 11, 30, 0, DateTimeKind.Local);
                ILoggerFactory factory = LoggingSetup.Create(root, null, stamp);
                Log.Factory = factory;
                CrashHandlers.Install(factory, Path.Combine(root, "logs"));

                CrashHandlers.OnUnhandled(
                    null,
                    new UnhandledExceptionEventArgs(new InvalidOperationException("no drawable"), true));

                // Install's own dispose already ran inside the handler; nothing further may write.
                string written = File.ReadAllText(
                    Path.Combine(root, "logs", LoggingSetup.LogFileName(stamp, fallback: false)));
                Assert.Contains("no drawable", written);
                Assert.Contains("Critical", written);
            }
            finally
            {
                // Install hooks the process-wide events, and a handler left behind would inject a
                // record into another test's recorder from a task finalized much later.
                AppDomain.CurrentDomain.UnhandledException -= CrashHandlers.OnUnhandled;
                TaskScheduler.UnobservedTaskException -= CrashHandlers.OnUnobserved;
                Log.Factory = null;
                Directory.Delete(root, recursive: true);
            }
        }

        [Fact]
        public void AThreadThatLogsAfterTheCrashFlushIsNotHandedADisposedFactory()
        {
            string root = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            _ = Directory.CreateDirectory(root);
            try
            {
                DateTime stamp = new(2026, 9, 9, 11, 30, 0, DateTimeKind.Local);
                ILoggerFactory factory = LoggingSetup.Create(root, null, stamp);
                Log.Factory = factory;
                CrashHandlers.Install(factory, Path.Combine(root, "logs"));

                CrashHandlers.OnUnhandled(
                    null,
                    new UnhandledExceptionEventArgs(new InvalidOperationException("no drawable"), true));

                // The dialog blocks the crashing thread, so the decode and audio threads keep
                // running behind it. One reporting its own failure asks for a category nothing
                // has logged to yet, which is the case that has to build a logger from the
                // factory the flush just closed.
                ILogger later = Log.For("ctrdx.test.category.not.used.before");
                later.LogError("reported from another thread while the dialog is up");
            }
            finally
            {
                AppDomain.CurrentDomain.UnhandledException -= CrashHandlers.OnUnhandled;
                TaskScheduler.UnobservedTaskException -= CrashHandlers.OnUnobserved;
                Log.Factory = null;
                Directory.Delete(root, recursive: true);
            }
        }

        [Fact]
        public void ASecondFailureRaisedWhileTheFirstIsReportedIsNotReportedAgain()
        {
            ReentrantLoggerProvider recorder = new();
            using ILoggerFactory factory = LoggerFactory.Create(builder => builder.AddProvider(recorder));
            Log.Factory = factory;
            try
            {
                // Reporting shows a modal window, which blocks. A second failure arriving while
                // that is up must not run the whole handler again and stack another window
                // behind the first with nothing left to dismiss either.
                CrashHandlers.OnUnhandled(
                    null,
                    new UnhandledExceptionEventArgs(new InvalidOperationException("first"), true));

                LogRecord entry = Assert.Single(recorder.Records);
                Assert.Contains("first", entry.Exception.Message);
            }
            finally
            {
                Log.Factory = null;
            }
        }

        /// <summary>Reports a second failure from inside the first one's write, as another thread would.</summary>
        private sealed class ReentrantLoggerProvider : ILoggerProvider
        {
            private readonly List<LogRecord> records = [];
            private bool raised;

            public IReadOnlyList<LogRecord> Records => [.. records];

            public ILogger CreateLogger(string categoryName) => new ReentrantLogger(this, categoryName);

            public void Dispose()
            {
            }

            private sealed class ReentrantLogger(ReentrantLoggerProvider owner, string category) : ILogger
            {
                public IDisposable BeginScope<TState>(TState state) where TState : notnull => null;

                public bool IsEnabled(LogLevel logLevel) => true;

                public void Log<TState>(
                    LogLevel logLevel,
                    EventId eventId,
                    TState state,
                    Exception exception,
                    Func<TState, Exception, string> formatter)
                {
                    owner.records.Add(
                        new LogRecord(category, logLevel, formatter(state, exception), exception));
                    if (owner.raised)
                    {
                        return;
                    }

                    owner.raised = true;
                    CrashHandlers.OnUnhandled(
                        null,
                        new UnhandledExceptionEventArgs(new InvalidOperationException("second"), true));
                }
            }
        }

        [Fact]
        public void AHandlerFailureDoesNotEscape()
        {
            Log.Factory = null;

            CrashHandlers.OnUnhandled(null, new UnhandledExceptionEventArgs("not an exception", isTerminating: true));
        }

        [Fact]
        public void APayloadThatIsNotAnExceptionStillDescribesItself()
        {
            RecordingLoggerProvider recorder = new();
            using ILoggerFactory factory = LoggerFactory.Create(builder => builder.AddProvider(recorder));
            Log.Factory = factory;
            try
            {
                CrashHandlers.OnUnhandled(
                    null,
                    new UnhandledExceptionEventArgs("not an exception", isTerminating: true));

                LogRecord entry = Assert.Single(recorder.Records);
                Assert.Equal(LogLevel.Critical, entry.Level);
                Assert.Contains("not an exception", entry.Message);
            }
            finally
            {
                Log.Factory = null;
            }
        }
    }
}
