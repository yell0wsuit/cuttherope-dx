using CutTheRopeDX.Framework.Diagnostics;

using Microsoft.Extensions.Logging;

using Xunit;

namespace CutTheRopeDX.Tests
{
    public class LogSeamTests
    {
        [Fact]
        public void UnsetFactoryLogsNowhereAndDoesNotThrow()
        {
            Log.Factory = null;

            Log.For("Any").Log(LogLevel.Error, default, "dropped", null, static (message, _) => message);
        }

        [Fact]
        public void SameCategoryReturnsTheSameLogger()
        {
            Log.Factory = null;

            Assert.Same(Log.For("Repeat"), Log.For("Repeat"));
        }

        [Fact]
        public void ReplacingTheFactoryRetiresCachedLoggers()
        {
            Log.Factory = null;
            _ = Log.For("Retired");

            RecordingLoggerProvider recorder = new();
            using ILoggerFactory factory = LoggerFactory.Create(builder => builder.AddProvider(recorder));
            Log.Factory = factory;
            try
            {
                Log.For("Retired").Log(LogLevel.Warning, default, "kept", null, static (message, _) => message);

                LogRecord entry = Assert.Single(recorder.Records);
                Assert.Equal("Retired", entry.Category);
                Assert.Equal(LogLevel.Warning, entry.Level);
                Assert.Equal("kept", entry.Message);
            }
            finally
            {
                Log.Factory = null;
            }
        }
    }
}
