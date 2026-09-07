using System.Text.Json;

using Xunit;

namespace CutTheRopeDX.Content.Tests
{
    /// <summary>
    /// Which source assets the build says the game ships, and what it refuses to ship.
    /// </summary>
    /// <remarks>
    /// The build copies nothing; MSBuild deploys from the list it writes. So these cover the
    /// decision - which files, under which names, and when the source tree is too incomplete to
    /// build at all - rather than any file movement.
    /// </remarks>
    public sealed class ContentBuildTests : IDisposable
    {
        private readonly string root = Path.Combine(
            Path.GetTempPath(), $"ctrdx-content-{Guid.NewGuid():N}");

        private string Source => Path.Combine(root, "source");

        private string Intermediate => Path.Combine(root, "obj");

        public ContentBuildTests()
        {
            _ = Directory.CreateDirectory(Source);
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(root, recursive: true);
            }
            catch (IOException)
            {
                // A temporary directory left behind fails nothing.
            }
        }

        [Fact]
        public void EveryAssetTheGameReadsIsShippedUnderItsOwnName()
        {
            Fill();

            SortedDictionary<string, string> selected =
                ContentSelection.Select(Source, ContentSelection.DesktopRules);

            Assert.Equal(
                [
                    "ctroriginal_packs.json",
                    "fonts/Gooddog.ttf",
                    "images/animations/fx_pause.xml",
                    "images/menu/logo.json",
                    "images/menu/logo.png",
                    "locales/en.json",
                    "maps/1_1.xml",
                    "packlist.json",
                    "sounds/menu_music.wav",
                    "sounds/sfx/tap.wav",
                    "video_hd/intro.mp4",
                ],
                selected.Keys);
        }

        [Fact]
        public void TheShippedNameMapsBackToTheSourceFile()
        {
            Fill();

            SortedDictionary<string, string> selected =
                ContentSelection.Select(Source, ContentSelection.DesktopRules);

            Assert.Equal(
                Path.Combine(Source, "images", "menu", "logo.png"),
                selected["images/menu/logo.png"]);
        }

        [Fact]
        public void TheFileListNamesEverySelectedSourceOncePerLine()
        {
            Fill();

            ContentBuildResult result = GameContentBuilder.Build(Source, Intermediate);

            string[] lines = File.ReadAllLines(result.ListPath);
            Assert.Equal(result.Files, lines.Length);
            Assert.All(lines, line => Assert.True(Path.IsPathFullyQualified(line)));
            Assert.All(lines, line => Assert.True(File.Exists(line)));
            Assert.Equal(lines.Length, new HashSet<string>(lines, StringComparer.Ordinal).Count);
        }

        [Fact]
        public void TheBuildWritesNothingButItsOwnTwoOutputs()
        {
            Fill();

            _ = GameContentBuilder.Build(Source, Intermediate);

            string[] written = [.. Directory
                .EnumerateFiles(Intermediate, "*", SearchOption.AllDirectories)
                .Select(path => Path.GetRelativePath(Intermediate, path)
                    .Replace(Path.DirectorySeparatorChar, '/'))
                .Order(StringComparer.Ordinal)];
            Assert.Equal(["content_files.txt", "images/image_dimensions.json"], written);
        }

        [Fact]
        public void TheImageDimensionsManifestNamesEveryImageByItsResourceName()
        {
            Fill();

            _ = GameContentBuilder.Build(Source, Intermediate);

            using JsonDocument manifest = JsonDocument.Parse(
                File.ReadAllText(Path.Combine(Intermediate, "images", "image_dimensions.json")));
            JsonElement logo = manifest.RootElement.GetProperty("images").GetProperty("menu/logo");
            Assert.Equal(12, logo.GetProperty("w").GetInt32());
            Assert.Equal(34, logo.GetProperty("h").GetInt32());
        }

        [Fact]
        public void BuilderMachineryAndEditorLeftoversAreNotShipped()
        {
            Fill();
            Write("Builder/Program.cs", "// not an asset");
            Write("Builder/obj/content/content_files.txt", "stale");
            Write("bin/Debug/stale.json", "{}");
            Write("obj/work.json", "{}");
            Write("images/.DS_Store", "junk");

            SortedDictionary<string, string> selected =
                ContentSelection.Select(Source, ContentSelection.DesktopRules);

            Assert.DoesNotContain(selected.Keys, key => key.StartsWith("Builder/", StringComparison.Ordinal));
            Assert.DoesNotContain(selected.Keys, key => key.StartsWith("bin/", StringComparison.Ordinal));
            Assert.DoesNotContain(selected.Keys, key => key.StartsWith("obj/", StringComparison.Ordinal));
            Assert.DoesNotContain(selected.Keys, key => key.EndsWith(".DS_Store", StringComparison.Ordinal));
        }

        [Fact]
        public void AStaleGeneratedManifestInTheSourceTreeIsNeverShipped()
        {
            Fill();
            Write("images/image_dimensions.json", "{\"images\":{\"gone\":{\"w\":1,\"h\":1}}}");

            ContentBuildResult result = GameContentBuilder.Build(Source, Intermediate);

            Assert.DoesNotContain(
                "images/image_dimensions.json",
                ContentSelection.Select(Source, ContentSelection.DesktopRules).Keys);
            Assert.DoesNotContain(
                File.ReadAllLines(result.ListPath),
                line => line.EndsWith("image_dimensions.json", StringComparison.Ordinal));
        }

        [Fact]
        public void TheSelectionIsTheSameEveryTime()
        {
            Fill();

            Assert.Equal(
                ContentSelection.Select(Source, ContentSelection.DesktopRules),
                ContentSelection.Select(Source, ContentSelection.DesktopRules));
        }

        [Theory]
        [InlineData("images")]
        [InlineData("sounds")]
        [InlineData("maps")]
        [InlineData("locales")]
        [InlineData("fonts")]
        public void ASourceTreeMissingRequiredAssetsFailsTheBuild(string missing)
        {
            Fill();
            Directory.Delete(Path.Combine(Source, missing), recursive: true);

            ContentBuildException failure = Assert.Throws<ContentBuildException>(
                () => GameContentBuilder.Build(Source, Intermediate));

            Assert.Contains(missing, failure.Message, StringComparison.Ordinal);
            Assert.Contains("fetch the external assets", failure.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void ASourceTreeWithNoAnimationsFailsRatherThanShippingAGameThatCannotStart()
        {
            Fill();
            File.Delete(Path.Combine(Source, "images", "animations", "fx_pause.xml"));

            ContentBuildException failure = Assert.Throws<ContentBuildException>(
                () => GameContentBuilder.Build(Source, Intermediate));

            Assert.Contains("images/**/*.xml", failure.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void AMissingSourceTreeIsReportedRatherThanProducingAnEmptyBuild()
        {
            ContentBuildException failure = Assert.Throws<ContentBuildException>(
                () => GameContentBuilder.Build(Path.Combine(root, "absent"), Intermediate));

            Assert.Contains("No content source directory", failure.Message, StringComparison.Ordinal);
        }

        /// <summary>Builds a source tree holding one of everything the game reads.</summary>
        private void Fill()
        {
            WritePng("images/menu/logo.png", 12, 34);
            Write("images/menu/logo.json", "{\"frames\":[]}");
            Write("images/animations/fx_pause.xml", "<animation />");
            Write("sounds/menu_music.wav", "RIFF....WAVE");
            Write("sounds/sfx/tap.wav", "RIFF....WAVE");
            Write("maps/1_1.xml", "<level id=\"1\" />");
            Write("locales/en.json", "{\"PLAY\":\"Play\"}");
            Write("fonts/Gooddog.ttf", "sfnt");
            Write("video_hd/intro.mp4", "frames");
            Write("packlist.json", "{\"packs\":[]}");
            Write("ctroriginal_packs.json", "{}");
        }

        /// <summary>Writes a text file into the source tree.</summary>
        /// <param name="relativePath">Path within the source tree, in POSIX form.</param>
        /// <param name="content">What to write.</param>
        private void Write(string relativePath, string content)
        {
            string path = Path.Combine(Source, relativePath.Replace('/', Path.DirectorySeparatorChar));
            _ = Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, content);
        }

        /// <summary>
        /// Writes a PNG whose header carries the given dimensions, which is all the build reads.
        /// </summary>
        /// <param name="relativePath">Path within the source tree, in POSIX form.</param>
        /// <param name="width">Width to record in the IHDR.</param>
        /// <param name="height">Height to record in the IHDR.</param>
        private void WritePng(string relativePath, int width, int height)
        {
            byte[] png = new byte[24];
            byte[] signature = [0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A];
            signature.CopyTo(png, 0);
            WriteBigEndian(png, 16, width);
            WriteBigEndian(png, 20, height);
            string path = Path.Combine(Source, relativePath.Replace('/', Path.DirectorySeparatorChar));
            _ = Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllBytes(path, png);
        }

        private static void WriteBigEndian(byte[] buffer, int offset, int value)
        {
            buffer[offset] = (byte)(value >> 24);
            buffer[offset + 1] = (byte)(value >> 16);
            buffer[offset + 2] = (byte)(value >> 8);
            buffer[offset + 3] = (byte)value;
        }
    }
}
