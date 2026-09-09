using System;
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
                ILoggerFactory factory = LoggingSetup.Create(root, null);
                Log.Factory = factory;
                CrashHandlers.Install(factory);

                CrashHandlers.OnUnhandled(
                    null,
                    new UnhandledExceptionEventArgs(new InvalidOperationException("no drawable"), true));

                // Install's own dispose already ran inside the handler; nothing further may write.
                string written = File.ReadAllText(Path.Combine(root, "logs", "ctrdx.log"));
                Assert.Contains("no drawable", written);
                Assert.Contains("Critical", written);
            }
            finally
            {
                Log.Factory = null;
                Directory.Delete(root, recursive: true);
            }
        }

        [Fact]
        public void AHandlerFailureDoesNotEscape()
        {
            Log.Factory = null;

            CrashHandlers.OnUnhandled(null, new UnhandledExceptionEventArgs("not an exception", isTerminating: true));
        }
    }
}
