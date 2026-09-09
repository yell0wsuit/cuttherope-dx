using System.Reflection;

namespace CutTheRopeDX.Browser
{
    /// <summary>Names the build a web log came from.</summary>
    internal static class BrowserBuild
    {
        /// <summary>The Cut the Rope: DX name, as the page's title shows it.</summary>
        private const string ProductName = "Cut The Rope: DX";

        /// <summary>Which build this is, as the banner reports it.</summary>
#if DEBUG
        private const string Configuration = "Debug";
#else
        private const string Configuration = "Release";
#endif

        /// <summary>
        /// Builds the banner that opens a run's log.
        /// </summary>
        /// <returns>The banner, as three newline-separated lines.</returns>
        /// <remarks>
        /// Deliberately not a log entry, matching the desktop build: this is the first thing
        /// anyone reads on a report, and per-line decoration would only get in the way of it.
        /// </remarks>
        public static string ComposeHeader()
        {
            Assembly assembly = typeof(BrowserBuild).Assembly;
            string version =
                assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                ?? assembly.GetName().Version?.ToString()
                ?? "Unknown";
            return $"{ProductName}\n{Configuration} version (web)\nVersion: {version}";
        }
    }
}
