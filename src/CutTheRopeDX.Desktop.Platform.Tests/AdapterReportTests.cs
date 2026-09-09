using CutTheRopeDX.Desktop.Platform.Graphics;
using CutTheRopeDX.Framework.Diagnostics;
using CutTheRopeDX.Tests;

using Microsoft.Extensions.Logging;

using Xunit;

namespace CutTheRopeDX.Desktop.Platform.Tests
{
    public class AdapterReportTests
    {
        [Fact]
        public void AnAdapterReportNamesItsTypeAndName()
        {
            RecordingLoggerProvider recorder = new();
            using ILoggerFactory factory = LoggerFactory.Create(builder => builder.AddProvider(recorder));
            Log.Factory = factory;
            try
            {
                ILogger logger = Log.For(LogCategories.SdlGraphics);
                GraphicsDeviceLog.Adapter(logger, "discrete", "Test GPU", "API 1.3.280, driver 0x0226ECC0");

                LogRecord entry = Assert.Single(recorder.Records);
                Assert.Equal(LogCategories.SdlGraphics, entry.Category);
                Assert.Equal(LogLevel.Information, entry.Level);
                Assert.Contains("discrete", entry.Message);
                Assert.Contains("Test GPU", entry.Message);
                Assert.Contains("API 1.3.280", entry.Message);
            }
            finally
            {
                Log.Factory = null;
            }
        }
    }
}
