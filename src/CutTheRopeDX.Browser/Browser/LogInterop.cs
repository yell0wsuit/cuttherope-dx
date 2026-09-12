using System.Runtime.InteropServices.JavaScript;
using System.Threading.Tasks;

namespace CutTheRopeDX.Browser
{
    /// <summary>Thin managed wrapper over the log.js storage module.</summary>
    internal static partial class LogInterop
    {
        /// <summary>Imports log.js. Must be awaited once before any other call.</summary>
        public static Task ImportAsync()
        {
            return JSHost.ImportAsync("log", "../log.js");
        }

        /// <summary>Opens this run's session and writes the banner naming the build.</summary>
        /// <param name="header">The banner text.</param>
        /// <returns>The session identifier.</returns>
        [JSImport("begin", "log")]
        public static partial string Begin(string header);

        /// <summary>Adds one formatted entry.</summary>
        /// <param name="line">The entry.</param>
        /// <param name="urgent">Whether to write it through rather than wait for the batch.</param>
        [JSImport("append", "log")]
        public static partial void Append(string line, bool urgent);
    }
}
