namespace CutTheRopeDX.Content
{
    /// <summary>
    /// One glob of source files the game ships.
    /// </summary>
    /// <param name="Pattern">Glob relative to the source root, in POSIX form.</param>
    /// <param name="Required">Whether a build fails when the glob matches nothing.</param>
    public sealed record ContentRule(string Pattern, bool Required = false);

    /// <summary>
    /// Raised when the source tree cannot produce a usable content build.
    /// </summary>
    /// <param name="message">What is wrong with the source tree.</param>
    public sealed class ContentBuildException(string message) : Exception(message);

    /// <summary>
    /// Decides which source assets the game ships.
    /// </summary>
    /// <remarks>
    /// Nothing is converted and nothing is copied here. Every asset the game loads is already in
    /// a format it opens directly - PNG through Skia, WAV through the mixer, and XML, JSON, fonts
    /// and video straight off disk - so the build's whole job is to say which files those are.
    /// MSBuild does the copying from that answer, which is what puts them in the output directory,
    /// the publish directory and the macOS bundle without this having to know about any of them.
    /// <para>
    /// Sources are matched by explicit rules rather than by taking the tree wholesale, because the
    /// source directory also holds the builder, its build outputs, and editor leftovers that have
    /// no business in a shipped game.
    /// </para>
    /// </remarks>
    public static class ContentSelection
    {
        /// <summary>
        /// The assets the desktop game reads.
        /// </summary>
        /// <remarks>
        /// The bulk are required: a build that shipped no images or no levels would still start
        /// and would fail much later, one missing asset at a time, on a machine that is not the
        /// one that built it.
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

        /// <summary>Outputs the build generates, which are never also taken from the source.</summary>
        public static IReadOnlySet<string> GeneratedFiles { get; } =
            new HashSet<string>(StringComparer.Ordinal) { "images/image_dimensions.json" };

        /// <summary>Directories in the source tree that never hold shipped assets.</summary>
        private static readonly string[] ExcludedRoots = ["bin", "obj", "Builder"];

        /// <summary>File names that are editor or tooling leftovers.</summary>
        private static readonly string[] ExcludedNames = [".DS_Store", "Thumbs.db"];

        /// <summary>
        /// Resolves the sources the rules select, keyed by their path in the shipped tree.
        /// </summary>
        /// <param name="sourceDirectory">Root of the content source tree.</param>
        /// <param name="rules">Which sources to ship.</param>
        /// <returns>Content-relative path to absolute source path, in a stable order.</returns>
        /// <exception cref="ContentBuildException">A required rule matched nothing.</exception>
        public static SortedDictionary<string, string> Select(
            string sourceDirectory,
            IReadOnlyList<ContentRule> rules)
        {
            ArgumentNullException.ThrowIfNull(rules);
            string source = Path.GetFullPath(sourceDirectory);
            if (!Directory.Exists(source))
            {
                throw new ContentBuildException($"No content source directory at {source}.");
            }

            SortedDictionary<string, string> selected = new(StringComparer.Ordinal);
            foreach (ContentRule rule in rules)
            {
                int matched = 0;
                foreach (string path in Match(source, rule.Pattern))
                {
                    string relativePath = ToPosix(Path.GetRelativePath(source, path));

                    // A file the build generates is never also shipped from the source. An earlier
                    // build's output left behind in the source tree would otherwise be deployed in
                    // place of the fresh one.
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

        /// <summary>
        /// Finds source files no rule ships, so a new kind of asset cannot go missing quietly.
        /// </summary>
        /// <param name="sourceDirectory">Root of the content source tree.</param>
        /// <param name="selected">What <see cref="Select"/> decided to ship.</param>
        /// <returns>Content-relative paths of files in the tree that nothing selected.</returns>
        /// <remarks>
        /// The rules name extensions and directories, so a file of a kind nobody anticipated -
        /// a <c>.webp</c>, or anything under a new subdirectory of one that is matched shallowly
        /// - matches no rule and is dropped. Nothing fails: a required rule only catches a glob
        /// that matched <em>nothing</em>, never a file that matched no glob. The first sign would
        /// be a missing asset on a player's machine, so the build says so instead.
        /// </remarks>
        public static IReadOnlyList<string> Unmatched(
            string sourceDirectory,
            SortedDictionary<string, string> selected)
        {
            ArgumentNullException.ThrowIfNull(selected);
            string source = Path.GetFullPath(sourceDirectory);
            if (!Directory.Exists(source))
            {
                return [];
            }

            List<string> unmatched = [];
            foreach (string path in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
            {
                if (IsExcluded(source, path))
                {
                    continue;
                }

                string relativePath = ToPosix(Path.GetRelativePath(source, path));
                if (!selected.ContainsKey(relativePath) && !GeneratedFiles.Contains(relativePath))
                {
                    unmatched.Add(relativePath);
                }
            }

            unmatched.Sort(StringComparer.Ordinal);
            return unmatched;
        }

        /// <summary>Expands one glob against the source tree.</summary>
        /// <param name="source">Root of the content source tree, already absolute.</param>
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

        /// <summary>Rewrites a platform path as the POSIX form used for content paths.</summary>
        /// <param name="path">The path to normalize.</param>
        private static string ToPosix(string path)
        {
            return path.Replace(Path.DirectorySeparatorChar, '/').Replace('\\', '/');
        }
    }
}
