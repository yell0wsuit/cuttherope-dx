using System.Text.Json;

namespace CutTheRopeDX.Content
{
    /// <summary>
    /// Produces the content tree the game reads at runtime.
    /// </summary>
    /// <remarks>
    /// The build is a byte-preserving copy plus one generated manifest. Nothing is re-encoded and
    /// nothing is packed into a container: every format here is one the game already opens
    /// directly, so a conversion step could only lose fidelity, cost build time, and hide which
    /// bytes actually shipped.
    /// </remarks>
    public static class GameContentBuilder
    {
        /// <summary>Runs one content build.</summary>
        /// <param name="sourceDirectory">Root of the content source tree.</param>
        /// <param name="outputDirectory">Directory the build writes into.</param>
        /// <returns>What was written, skipped and deleted.</returns>
        public static ContentCopyResult Build(string sourceDirectory, string outputDirectory)
        {
            ContentCopyResult result = ContentCopy.Run(
                sourceDirectory, outputDirectory, ContentCopy.DesktopRules);
            EmitImageDimensionsManifest(
                Path.Combine(sourceDirectory, "images"),
                Path.Combine(outputDirectory, "images"));
            return result;
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
