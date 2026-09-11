using CutTheRopeDX.Desktop.Platform.Graphics;
using CutTheRopeDX.Framework.Diagnostics;
using CutTheRopeDX.Tests;

using Microsoft.Extensions.Logging;

using Xunit;

namespace CutTheRopeDX.Desktop.Tests
{
    public class SdlHostLoggingTests
    {
        [Fact]
        public void ARejectedRendererIsLoggedAtErrorUnderTheHostCategory()
        {
            RecordingLoggerProvider recorder = new();
            using ILoggerFactory factory = LoggerFactory.Create(builder => builder.AddProvider(recorder));
            Log.Factory = factory;
            try
            {
                ILogger logger = Log.For(LogCategories.SdlHost);

                SdlDesktopHostLog.RejectedRenderer(logger, GraphicsBackendKind.Angle, "no drawable");

                LogRecord entry = Assert.Single(recorder.Records);
                Assert.Equal(LogCategories.SdlHost, entry.Category);
                Assert.Equal(LogLevel.Error, entry.Level);
                Assert.Contains("no drawable", entry.Message);
                // Which renderer was passed over is the half that makes the line usable.
                Assert.Contains("Angle", entry.Message);
            }
            finally
            {
                Log.Factory = null;
            }
        }
    }
}
