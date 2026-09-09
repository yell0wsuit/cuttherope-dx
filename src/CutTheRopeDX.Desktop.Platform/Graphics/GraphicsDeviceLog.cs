using Microsoft.Extensions.Logging;

namespace CutTheRopeDX.Desktop.Platform.Graphics
{
    /// <summary>
    /// Log messages for graphics device construction.
    /// </summary>
    /// <remarks>
    /// Every backend reports its adapter through the one message, so the field is greppable
    /// across platforms rather than three differently shaped lines.
    /// </remarks>
    internal static partial class GraphicsDeviceLog
    {
        /// <summary>Reports the adapter a backend selected.</summary>
        /// <param name="logger">Destination logger.</param>
        /// <param name="adapterType">Hardware class, or <c>unknown</c> where the API cannot say.</param>
        /// <param name="adapterName">Adapter name as the driver reports it.</param>
        /// <param name="adapterVersion">
        /// Driver or API version, in whatever form the backend can state one.
        /// </param>
        [LoggerMessage(
            Level = LogLevel.Information,
            Message = "Adapter type={AdapterType} name={AdapterName} version={AdapterVersion}")]
        public static partial void Adapter(
            ILogger logger, string adapterType, string adapterName, string adapterVersion);
    }
}
