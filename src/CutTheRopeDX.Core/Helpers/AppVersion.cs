using System;
using System.Reflection;

namespace CutTheRopeDX.Helpers
{
    /// <summary>
    /// The running game's version, as the host executable records it.
    /// </summary>
    /// <remarks>
    /// Read from the entry assembly rather than Core: release builds stamp every project with the
    /// same version, but only the host carries the development suffix, and Core appends its own
    /// source revision on top of whatever it was given.
    /// </remarks>
    internal static class AppVersion
    {
        /// <summary>The marker a CI prerelease build carries, as in <c>1.0.0-prerelease+57</c>.</summary>
        private const string PrereleaseMarker = "-prerelease";

        /// <summary>
        /// Gets the informational version of the running host, or "Unknown" when it carries none.
        /// </summary>
        public static string Current { get; } = Resolve();

        /// <summary>
        /// Gets whether the running build is a CI prerelease.
        /// </summary>
        public static bool IsPrerelease => IsPrereleaseVersion(Current);

        /// <summary>
        /// Determines whether a version string names a CI prerelease build.
        /// </summary>
        /// <param name="version">The version to check.</param>
        /// <returns><see langword="true"/> when the version carries the prerelease marker; otherwise <see langword="false"/>.</returns>
        public static bool IsPrereleaseVersion(string version)
        {
            return version != null && version.Contains(PrereleaseMarker, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Shortens a full source-revision hash in a version string to its first seven characters,
        /// so a development build's version fits where a release's would.
        /// </summary>
        /// <param name="version">The version to shorten.</param>
        /// <returns>The version with any 40-character revision hash abbreviated.</returns>
        public static string Abbreviate(string version)
        {
            int plus = version.IndexOf('+', StringComparison.Ordinal);
            if (plus < 0)
            {
                return version;
            }

            string[] parts = version[(plus + 1)..].Split('.');
            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i].Length == 40 && IsHex(parts[i]))
                {
                    parts[i] = parts[i][..7];
                }
            }
            return version[..(plus + 1)] + string.Join('.', parts);
        }

        private static bool IsHex(string text)
        {
            foreach (char c in text)
            {
                if (!char.IsAsciiHexDigit(c))
                {
                    return false;
                }
            }
            return true;
        }

        private static string Resolve()
        {
            Assembly assembly = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
            return assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                ?? assembly.GetName().Version?.ToString(3)
                ?? "Unknown";
        }
    }
}
