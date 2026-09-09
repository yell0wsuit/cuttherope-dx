using CutTheRopeDX.Framework.Diagnostics;
using CutTheRopeDX.Framework.Media;
using CutTheRopeDX.Tests;

using Microsoft.Extensions.Logging;

using Xunit;

namespace CutTheRopeDX.Desktop.Tests
{
    public class VideoPlayerLoggingTests
    {
        [Fact]
        public void LifecycleChatterIsTraceSoADefaultRunStaysQuiet()
        {
            RecordingLoggerProvider recorder = new();
            using ILoggerFactory factory = LoggerFactory.Create(builder =>
            {
                _ = builder.SetMinimumLevel(LogLevel.Information);
                _ = builder.AddProvider(recorder);
            });
            Log.Factory = factory;
            try
            {
                ILogger logger = Log.For(LogCategories.MediaAVFoundation);

                VideoPlayerLog.Stop(logger);

                Assert.Empty(recorder.Records);
            }
            finally
            {
                Log.Factory = null;
            }
        }

        [Fact]
        public void ASkippedCutsceneWarnsWithItsReason()
        {
            RecordingLoggerProvider recorder = new();
            using ILoggerFactory factory = LoggerFactory.Create(builder => builder.AddProvider(recorder));
            Log.Factory = factory;
            try
            {
                ILogger logger = Log.For(LogCategories.MediaFFmpeg);

                VideoPlayerLog.SkippingMovie(logger, "intro.webm", false, true);

                LogRecord entry = Assert.Single(recorder.Records);
                Assert.Equal(LogLevel.Warning, entry.Level);
                Assert.Contains("intro.webm", entry.Message);
            }
            finally
            {
                Log.Factory = null;
            }
        }
    }
}
