using System.Text.Json;

namespace CutTheRopeDX.Content
{
    /// <summary>What one content build decided.</summary>
    /// <param name="Files">How many source assets the game will ship.</param>
    /// <param name="ListPath">The file list MSBuild deploys from.</param>
    /// <param name="Unmatched">Source files no rule ships, which is worth saying out loud.</param>
    public sealed record ContentBuildResult(
        int Files, string ListPath, IReadOnlyList<string> Unmatched);

    /// <summary>
    /// Works out what the game ships and writes the two small files a build needs.
    /// </summary>
    /// <remarks>
    /// Neither output is content. One is the list of source assets for MSBuild to copy, the other
    /// is the image dimensions manifest, and together they are a few hundred kilobytes: the assets
    /// themselves are never duplicated into a staging tree on the way to the application.
    /// </remarks>
    public static class GameContentBuilder
    {
        /// <summary>Name of the file list the MSBuild targets read.</summary>
        public const string FileListName = "content_files.txt";

        /// <summary>Works out what to ship and writes the build's own outputs.</summary>
        /// <param name="sourceDirectory">Root of the content source tree.</param>
        /// <param name="intermediateDirectory">Directory the build writes its own outputs to.</param>
        /// <returns>What the build decided.</returns>
        public static ContentBuildResult Build(string sourceDirectory, string intermediateDirectory)
        {
            SortedDictionary<string, string> selected =
                ContentSelection.Select(sourceDirectory, ContentSelection.DesktopRules);

            _ = Directory.CreateDirectory(intermediateDirectory);
            string listPath = Path.Combine(intermediateDirectory, FileListName);
            File.WriteAllLines(listPath, selected.Values);

            EmitImageDimensionsManifest(
                Path.Combine(sourceDirectory, "images"),
                Path.Combine(intermediateDirectory, "images"));

            return new ContentBuildResult(
                selected.Count, listPath, ContentSelection.Unmatched(sourceDirectory, selected));
        }

        /// <summary>
        /// Writes pixel dimensions for every source image so headless runs can size textures
        /// without a graphics device.
        /// </summary>
        /// <remarks>
        /// The PNGs themselves ship, so the sizes could be read back from them, but that means
        /// opening every one of a few hundred files to reach the twenty-fourth byte. Reading them
        /// once at build time and shipping the answer keeps a headless run's startup to one file.
        /// </remarks>
        /// <param name="imagesSourceDir">Directory holding the source PNGs.</param>
        /// <param name="imagesOutputDir">Directory the manifest is written to.</param>
        public static void EmitImageDimensionsManifest(string imagesSourceDir, string imagesOutputDir)
        {
            Dictionary<string, ImageSize> images = [];
            foreach (string png in Directory.EnumerateFiles(imagesSourceDir, "*.png", SearchOption.AllDirectories))
            {
                (int w, int h) = ReadPngSize(png);
                string key = Path.GetRelativePath(imagesSourceDir, png)[..^4]
                    .Replace(Path.DirectorySeparatorChar, '/');
                images[key] = new ImageSize(w, h);
            }

            _ = Directory.CreateDirectory(imagesOutputDir);
            string outPath = Path.Combine(imagesOutputDir, "image_dimensions.json");
            File.WriteAllText(outPath, JsonSerializer.Serialize(new ImageDimensionsManifest(images)));
        }

        /// <summary>Reads width/height from a PNG IHDR header (bytes 16-23, big-endian).</summary>
        private static (int Width, int Height) ReadPngSize(string path)
        {
            byte[] header = new byte[24];
            using FileStream fs = File.OpenRead(path);
            if (fs.ReadAtLeast(header, 24, throwOnEndOfStream: false) < 24)
            {
                throw new InvalidDataException("Not a valid PNG: " + path);
            }

            int w = (header[16] << 24) | (header[17] << 16) | (header[18] << 8) | header[19];
            int h = (header[20] << 24) | (header[21] << 16) | (header[22] << 8) | header[23];
            return (w, h);
        }

        /// <summary>One image's pixel dimensions, as serialized into the manifest.</summary>
        /// <param name="W">Width in pixels.</param>
        /// <param name="H">Height in pixels.</param>
        private sealed record ImageSize(
            [property: System.Text.Json.Serialization.JsonPropertyName("w")] int W,
            [property: System.Text.Json.Serialization.JsonPropertyName("h")] int H);

        /// <summary>Root of the image dimensions manifest.</summary>
        /// <param name="Images">Dimensions keyed by resource name relative to <c>images/</c>.</param>
        private sealed record ImageDimensionsManifest(
            [property: System.Text.Json.Serialization.JsonPropertyName("images")]
            Dictionary<string, ImageSize> Images);
    }
}
