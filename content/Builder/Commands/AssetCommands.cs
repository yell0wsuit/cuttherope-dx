using CutTheRopeDX.Content.Assets;

namespace CutTheRopeDX.Content.Commands
{
    /// <summary>
    /// Executes content acquisition, verification, and build commands.
    /// </summary>
    public static class AssetCommands
    {
        private const string AssetsUrl =
            "https://github.com/yell0wsuit/ctrdx-assets/releases/latest/download/ctrdx-assets-vk.zip";
        private const string ManifestName = "file_manifest.json";

        /// <summary>
        /// Executes a parsed content-builder command.
        /// </summary>
        /// <param name="commandLine">Parsed command-line arguments.</param>
        /// <returns>The process exit code.</returns>
        public static async Task<int> RunAsync(ContentCommandLine commandLine)
        {
            try
            {
                return commandLine.Command switch
                {
                    ContentCommand.Build => RunBuild(commandLine),
                    ContentCommand.Fetch => await RunFetchAsync(
                        ResolveSourceDirectory(commandLine.SourceDirectory)),
                    ContentCommand.Verify => RunVerify(
                        ResolveSourceDirectory(commandLine.SourceDirectory)),
                    _ => throw new ArgumentOutOfRangeException(
                        nameof(commandLine),
                        commandLine.Command,
                        "Unknown content command."),
                };
            }
            catch (Exception exception) when (
                exception is ArgumentException or
                    ContentBuildException or
                    HttpRequestException or
                    TaskCanceledException or
                    IOException or
                    InvalidDataException)
            {
                Console.Error.WriteLine(exception.Message);
                return 1;
            }
        }

        private static int RunBuild(ContentCommandLine commandLine)
        {
            string source = ResolveSourceDirectory(commandLine.SourceDirectory);
            if (commandLine.OutputDirectory is null)
            {
                throw new ArgumentException("The build command requires an output directory.");
            }

            ContentCopyResult result = GameContentBuilder.Build(
                source, Path.GetFullPath(commandLine.OutputDirectory));
            Console.WriteLine(
                $"Content build: {result.Copied.Count} written, {result.Unchanged.Count} unchanged, "
                + $"{result.Removed.Count} removed.");
            return 0;
        }

        private static async Task<int> RunFetchAsync(string contentDirectory)
        {
            AssetManifest manifest = ReadRequiredManifest(contentDirectory);
            AssetStore store = new(contentDirectory, manifest);
            IReadOnlyList<string> missing = store.FindMissing();

            if (missing.Count == 0)
            {
                return 0;
            }

            Console.WriteLine(
                $"Content assets missing ({missing.Count}/{manifest.Files.Count} listed) — fetching.");
            string temporaryDirectory = Path.Combine(
                Path.GetTempPath(),
                $"ctrdx-assets-fetch-{Guid.NewGuid():N}");
            _ = Directory.CreateDirectory(temporaryDirectory);

            try
            {
                string archivePath = Path.Combine(temporaryDirectory, "ctrdx-assets-vk.zip");
                using AssetDownloader downloader = new();
                Console.WriteLine($"Downloading content assets from {AssetsUrl}...");
                await downloader.DownloadAsync(AssetsUrl, archivePath, retries: 3);

                AssetArchive archive = new(archivePath, manifest);
                AssetVerificationResult verification = archive.Verify();

                if (!verification.Success)
                {
                    WriteVerificationFailures(verification);
                    Console.Error.WriteLine(
                        $"Downloaded bundle doesn't match {ManifestName}; aborting.");
                    return 1;
                }

                archive.Install(contentDirectory, missing);
                Console.WriteLine(
                    $"Restored {missing.Count} missing binary asset(s) into {contentDirectory}.");
                return 0;
            }
            finally
            {
                try
                {
                    Directory.Delete(temporaryDirectory, recursive: true);
                }
                catch
                {
                    // Best-effort cleanup.
                }
            }
        }

        private static int RunVerify(string contentDirectory)
        {
            AssetManifest manifest = ReadRequiredManifest(contentDirectory);
            AssetVerificationResult verification =
                new AssetStore(contentDirectory, manifest).Verify();

            if (verification.Success)
            {
                Console.WriteLine(
                    $"All {manifest.Files.Count} content assets verified OK.");
                return 0;
            }

            WriteVerificationFailures(verification);
            Console.Error.WriteLine(
                $"Verify failed: {verification.Missing.Count} missing, " +
                $"{verification.Mismatched.Count} mismatched of {manifest.Files.Count}.");
            return 1;
        }

        private static AssetManifest ReadRequiredManifest(string contentDirectory)
        {
            string manifestPath = Path.Combine(contentDirectory, ManifestName);

            return File.Exists(manifestPath)
                ? AssetManifest.Read(manifestPath)
                : throw new FileNotFoundException(
                    $"No {ManifestName} found in {contentDirectory}.",
                    manifestPath);
        }

        private static string ResolveSourceDirectory(string? sourceDirectory)
        {
            return Path.GetFullPath(sourceDirectory ?? "content");
        }

        private static void WriteVerificationFailures(
            AssetVerificationResult verification)
        {
            foreach (string path in verification.Missing)
            {
                Console.Error.WriteLine($"missing:  {path}");
            }

            foreach (string path in verification.Mismatched)
            {
                Console.Error.WriteLine($"mismatch: {path}");
            }
        }
    }
}
