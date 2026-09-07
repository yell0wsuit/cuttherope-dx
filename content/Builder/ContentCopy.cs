using System.Security.Cryptography;

namespace CutTheRopeDX.Content
{
    /// <summary>
    /// One glob of source files to carry into the build output.
    /// </summary>
    /// <param name="Pattern">Glob relative to the source root, in POSIX form.</param>
    /// <param name="Required">Whether a build fails when the glob matches nothing.</param>
    public sealed record ContentRule(string Pattern, bool Required = false);

    /// <summary>What one content build did.</summary>
    /// <param name="Copied">Files written because they were new or had changed.</param>
    /// <param name="Unchanged">Files already present in the output and identical.</param>
    /// <param name="Removed">Outputs deleted because their source is gone.</param>
    public sealed record ContentCopyResult(
        IReadOnlyList<string> Copied,
        IReadOnlyList<string> Unchanged,
        IReadOnlyList<string> Removed);

    /// <summary>
    /// Raised when the source tree cannot produce a usable content build.
    /// </summary>
    /// <param name="message">What is wrong with the source tree.</param>
    public sealed class ContentBuildException(string message) : Exception(message);

    /// <summary>
    /// Mirrors the source assets the game reads into the build output, byte for byte.
    /// </summary>
    /// <remarks>
    /// Every asset the game loads is already in a format it can read at runtime: PNG through Skia,
    /// WAV through the mixer, and XML, JSON, fonts and video straight off disk. Nothing here
    /// converts anything, so the bytes the build ships are the bytes that were fetched and hashed,
    /// and the manifest that verified the source also describes the output.
    /// <para>
    /// Sources are matched by explicit rules rather than by copying the tree wholesale, because
    /// the source directory also holds the builder, its build outputs, and editor leftovers that
    /// have no business in a shipped game.
    /// </para>
    /// </remarks>
    public static class ContentCopy
    {
        /// <summary>
        /// The assets the desktop game reads, and where they come from.
        /// </summary>
        /// <remarks>
        /// Images and sounds are required: a build that produced neither would still start and
        /// would fail much later, one missing texture at a time, on a machine that is not the one
        /// that built it.
        /// </remarks>
        public static IReadOnlyList<ContentRule> DesktopRules { get; } =
        [
            new("images/**/*.png", Required: true),
            new("images/**/*.json"),

            // The character and effect animations are XML beside the sheets they animate, and one
            // of them is the startup splash, so a build without them does not reach the menu.
            new("images/**/*.xml", Required: true),
            new("sounds/**/*.wav", Required: true),
            new("maps/*.*", Required: true),
            new("locales/*.*", Required: true),
            new("fonts/*.*", Required: true),
            new("video_hd/*.*"),
            new("*.xml"),
            new("*.json"),
            new("*.cur"),
        ];

        /// <summary>Directories in the source tree that never hold shipped assets.</summary>
        private static readonly string[] ExcludedRoots = ["bin", "obj", "Builder"];

        /// <summary>File names that are editor or tooling leftovers.</summary>
        private static readonly string[] ExcludedNames = [".DS_Store", "Thumbs.db"];

        /// <summary>
        /// Brings <paramref name="outputDirectory"/> into line with the sources the rules select.
        /// </summary>
        /// <param name="sourceDirectory">Root of the content source tree.</param>
        /// <param name="outputDirectory">Directory the build writes into.</param>
        /// <param name="rules">Which sources to carry across.</param>
        /// <returns>What was written, skipped and deleted.</returns>
        /// <exception cref="ContentBuildException">A required rule matched nothing.</exception>
        public static ContentCopyResult Run(
            string sourceDirectory,
            string outputDirectory,
            IReadOnlyList<ContentRule> rules)
        {
            ArgumentNullException.ThrowIfNull(rules);
            string source = Path.GetFullPath(sourceDirectory);
            string output = Path.GetFullPath(outputDirectory);
            if (!Directory.Exists(source))
            {
                throw new ContentBuildException($"No content source directory at {source}.");
            }

            if (IsInside(output, source) && !IsExcludedDirectory(source, output))
            {
                throw new ContentBuildException(
                    $"The output directory {output} is inside the source tree and would be read back "
                    + "as input, which would make the build its own source.");
            }

            SortedDictionary<string, string> selected = Select(source, rules);
            _ = Directory.CreateDirectory(output);

            List<string> copied = [];
            List<string> unchanged = [];
            foreach ((string relativePath, string sourcePath) in selected)
            {
                string destination = Resolve(output, relativePath);
                if (IsUpToDate(sourcePath, destination))
                {
                    unchanged.Add(relativePath);
                    continue;
                }

                _ = Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                File.Copy(sourcePath, destination, overwrite: true);
                copied.Add(relativePath);
            }

            List<string> removed = RemoveStale(output, selected.Keys);
            return new ContentCopyResult(copied, unchanged, removed);
        }

        /// <summary>
        /// Resolves the sources the rules select, keyed by their path in the output.
        /// </summary>
        /// <param name="source">Root of the content source tree, already absolute.</param>
        /// <param name="rules">Which sources to carry across.</param>
        /// <returns>Output-relative path to source path, in a stable order.</returns>
        /// <exception cref="ContentBuildException">A required rule matched nothing.</exception>
        public static SortedDictionary<string, string> Select(
            string source,
            IReadOnlyList<ContentRule> rules)
        {
            ArgumentNullException.ThrowIfNull(rules);
            SortedDictionary<string, string> selected = new(StringComparer.Ordinal);
            foreach (ContentRule rule in rules)
            {
                int matched = 0;
                foreach (string path in Match(source, rule.Pattern))
                {
                    string relativePath = ToPosix(Path.GetRelativePath(source, path));

                    // A file the build generates is never also copied from the source. An earlier
                    // build's output left behind in the source tree otherwise gets copied over the
                    // fresh one and then replaced by it, so no build ever settles and the stale
                    // copy would win outright if generation were ever skipped.
                    if (GeneratedFiles.Contains(relativePath))
                    {
                        continue;
                    }

                    selected[relativePath] = path;
                    matched++;
                }

                if (matched == 0 && rule.Required)
                {
                    throw new ContentBuildException(
                        $"No content matched '{rule.Pattern}'. The source tree is incomplete; "
                        + "fetch the external assets before building.");
                }
            }

            return selected;
        }

        /// <summary>Expands one glob against the source tree.</summary>
        /// <param name="source">Root of the content source tree.</param>
        /// <param name="pattern">Glob relative to that root, in POSIX form.</param>
        private static IEnumerable<string> Match(string source, string pattern)
        {
            int split = pattern.LastIndexOf('/');
            string directory = split < 0 ? string.Empty : pattern[..split];
            string filePattern = split < 0 ? pattern : pattern[(split + 1)..];
            bool recursive = directory.EndsWith("/**", StringComparison.Ordinal);
            if (recursive)
            {
                directory = directory[..^3];
            }

            string root = directory.Length == 0
                ? source
                : Path.Combine(source, directory.Replace('/', Path.DirectorySeparatorChar));
            if (!Directory.Exists(root))
            {
                yield break;
            }

            SearchOption depth = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            foreach (string path in Directory.EnumerateFiles(root, filePattern, depth))
            {
                if (!IsExcluded(source, path))
                {
                    yield return path;
                }
            }
        }

        /// <summary>Whether a source file is builder machinery rather than a game asset.</summary>
        /// <param name="source">Root of the content source tree.</param>
        /// <param name="path">Absolute path of the candidate file.</param>
        private static bool IsExcluded(string source, string path)
        {
            if (ExcludedNames.Contains(Path.GetFileName(path), StringComparer.OrdinalIgnoreCase))
            {
                return true;
            }

            string relativePath = ToPosix(Path.GetRelativePath(source, path));
            return ExcludedRoots.Any(root =>
                relativePath.StartsWith(root + "/", StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Turns an output-relative path into an absolute one, refusing to leave the output tree.
        /// </summary>
        /// <param name="output">The output directory, already absolute.</param>
        /// <param name="relativePath">Path of the file within it.</param>
        /// <remarks>
        /// The rules are ours, but the file names under them are whatever the asset bundle
        /// contains, and a build that can be talked into writing outside its own output directory
        /// is a build that can overwrite anything the person running it can.
        /// </remarks>
        private static string Resolve(string output, string relativePath)
        {
            string destination = Path.GetFullPath(
                Path.Combine(output, relativePath.Replace('/', Path.DirectorySeparatorChar)));
            return IsInside(destination, output)
                ? destination
                : throw new ContentBuildException(
                    $"'{relativePath}' resolves outside the output directory.");
        }

        /// <summary>
        /// Whether a directory inside the source tree is one the rules never read from.
        /// </summary>
        /// <param name="source">Root of the content source tree, already absolute.</param>
        /// <param name="directory">The directory to classify, already absolute.</param>
        /// <remarks>
        /// The build output has always lived under the builder's own directory, which is excluded
        /// from every rule. That is safe for the reason the check exists: nothing there can be
        /// selected as a source, so the build cannot read its own output back.
        /// </remarks>
        private static bool IsExcludedDirectory(string source, string directory)
        {
            string relativePath = ToPosix(Path.GetRelativePath(source, directory));
            return ExcludedRoots.Any(root =>
                relativePath.Equals(root, StringComparison.OrdinalIgnoreCase)
                || relativePath.StartsWith(root + "/", StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>Whether <paramref name="candidate"/> sits within <paramref name="root"/>.</summary>
        /// <param name="candidate">The path to test, already absolute.</param>
        /// <param name="root">The directory it must be inside.</param>
        private static bool IsInside(string candidate, string root)
        {
            string rooted = root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            return candidate.StartsWith(rooted, StringComparison.Ordinal);
        }

        /// <summary>
        /// Whether the output already holds this exact source file.
        /// </summary>
        /// <param name="sourcePath">The source file.</param>
        /// <param name="destination">Where it would be written.</param>
        /// <remarks>
        /// Size and write time settle almost every file for nothing. Only a file that looks
        /// current is hashed, which is what stops a source edited within the timestamp's
        /// resolution, or restored from a backup with an old time, from being silently skipped.
        /// </remarks>
        private static bool IsUpToDate(string sourcePath, string destination)
        {
            FileInfo from = new(sourcePath);
            FileInfo to = new(destination);
            return to.Exists
                && from.Length == to.Length
                && from.LastWriteTimeUtc <= to.LastWriteTimeUtc
                && SameContent(sourcePath, destination);
        }

        /// <summary>Compares two files by hash.</summary>
        /// <param name="left">One file.</param>
        /// <param name="right">The other.</param>
        private static bool SameContent(string left, string right)
        {
            using FileStream first = File.OpenRead(left);
            using FileStream second = File.OpenRead(right);
            return SHA256.HashData(first).AsSpan().SequenceEqual(SHA256.HashData(second));
        }

        /// <summary>
        /// Deletes outputs whose source has gone, and the directories that empties.
        /// </summary>
        /// <param name="output">The output directory, already absolute.</param>
        /// <param name="expected">Output-relative paths the build produced.</param>
        /// <remarks>
        /// Without this a renamed or deleted asset stays in the output for as long as the
        /// directory survives, and ships.
        /// </remarks>
        private static List<string> RemoveStale(string output, IEnumerable<string> expected)
        {
            HashSet<string> keep = new(expected, StringComparer.Ordinal);
            List<string> removed = [];
            foreach (string path in Directory.EnumerateFiles(output, "*", SearchOption.AllDirectories))
            {
                string relativePath = ToPosix(Path.GetRelativePath(output, path));
                if (!keep.Contains(relativePath) && !GeneratedFiles.Contains(relativePath))
                {
                    File.Delete(path);
                    removed.Add(relativePath);
                }
            }

            foreach (string directory in Directory
                .EnumerateDirectories(output, "*", SearchOption.AllDirectories)
                .OrderByDescending(static path => path.Length))
            {
                if (!Directory.EnumerateFileSystemEntries(directory).Any())
                {
                    Directory.Delete(directory);
                }
            }

            return removed;
        }

        /// <summary>Outputs the build writes itself, which have no source to match.</summary>
        private static readonly HashSet<string> GeneratedFiles =
            new(StringComparer.Ordinal) { "images/image_dimensions.json" };

        /// <summary>Rewrites a platform path as the POSIX form used for output paths.</summary>
        /// <param name="path">The path to normalize.</param>
        private static string ToPosix(string path)
        {
            return path.Replace(Path.DirectorySeparatorChar, '/').Replace('\\', '/');
        }
    }
}
