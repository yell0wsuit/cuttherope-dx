using System.Text.Json;

using Xunit;

namespace CutTheRopeDX.Content.Tests
{
    /// <summary>
    /// What the content build produces from a source tree, and what it refuses to produce.
    /// </summary>
    public sealed class ContentBuildTests : IDisposable
    {
        private readonly string root = Path.Combine(
            Path.GetTempPath(), $"ctrdx-content-{Guid.NewGuid():N}");

        private string Source => Path.Combine(root, "source");

        private string Output => Path.Combine(root, "output");

        public ContentBuildTests()
        {
            _ = Directory.CreateDirectory(Source);
            _ = Directory.CreateDirectory(Output);
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
        public void EveryAssetTheGameReadsReachesTheOutputUnderItsOwnName()
        {
            Fill();

            _ = GameContentBuilder.Build(Source, Output);

            Assert.True(File.Exists(Path.Combine(Output, "images", "menu", "logo.png")));
            Assert.True(File.Exists(Path.Combine(Output, "images", "menu", "logo.json")));
            Assert.True(File.Exists(Path.Combine(Output, "images", "animations", "fx_pause.xml")));
            Assert.True(File.Exists(Path.Combine(Output, "sounds", "menu_music.wav")));
            Assert.True(File.Exists(Path.Combine(Output, "sounds", "sfx", "tap.wav")));
            Assert.True(File.Exists(Path.Combine(Output, "maps", "1_1.xml")));
            Assert.True(File.Exists(Path.Combine(Output, "locales", "en.json")));
            Assert.True(File.Exists(Path.Combine(Output, "fonts", "Gooddog.ttf")));
            Assert.True(File.Exists(Path.Combine(Output, "video_hd", "intro.mp4")));
            Assert.True(File.Exists(Path.Combine(Output, "packlist.json")));
        }

        [Fact]
        public void APngReachesTheOutputByteForByte()
        {
            Fill();
            string sourcePng = Path.Combine(Source, "images", "menu", "logo.png");

            _ = GameContentBuilder.Build(Source, Output);

            Assert.Equal(
                File.ReadAllBytes(sourcePng),
                File.ReadAllBytes(Path.Combine(Output, "images", "menu", "logo.png")));
        }

        [Fact]
        public void TheImageDimensionsManifestNamesEveryImageByItsResourceName()
        {
            Fill();

            _ = GameContentBuilder.Build(Source, Output);

            using JsonDocument manifest = JsonDocument.Parse(
                File.ReadAllText(Path.Combine(Output, "images", "image_dimensions.json")));
            JsonElement images = manifest.RootElement.GetProperty("images");
            JsonElement logo = images.GetProperty("menu/logo");
            Assert.Equal(12, logo.GetProperty("w").GetInt32());
            Assert.Equal(34, logo.GetProperty("h").GetInt32());
        }

        [Fact]
        public void BuilderMachineryAndEditorLeftoversAreNotShipped()
        {
            Fill();
            Write("Builder/Program.cs", "// not an asset");
            Write("bin/Debug/stale.json", "{}");
            Write("obj/work.json", "{}");
            Write("images/.DS_Store", "junk");

            _ = GameContentBuilder.Build(Source, Output);

            Assert.False(Directory.Exists(Path.Combine(Output, "Builder")));
            Assert.False(Directory.Exists(Path.Combine(Output, "bin")));
            Assert.False(Directory.Exists(Path.Combine(Output, "obj")));
            Assert.False(File.Exists(Path.Combine(Output, "images", ".DS_Store")));
        }

        [Fact]
        public void ASecondBuildWithNothingChangedWritesNothing()
        {
            Fill();
            _ = GameContentBuilder.Build(Source, Output);

            ContentCopyResult second = GameContentBuilder.Build(Source, Output);

            Assert.Empty(second.Copied);
            Assert.Empty(second.Removed);
            Assert.NotEmpty(second.Unchanged);
        }

        [Fact]
        public void AChangedSourceIsWrittenAgainEvenWhenItsSizeIsTheSame()
        {
            Fill();
            _ = GameContentBuilder.Build(Source, Output);
            string map = Path.Combine(Source, "maps", "1_1.xml");
            File.WriteAllText(map, "<level id=\"2\" />");
            File.SetLastWriteTimeUtc(map, File.GetLastWriteTimeUtc(map).AddYears(-5));

            ContentCopyResult rebuilt = GameContentBuilder.Build(Source, Output);

            Assert.Contains("maps/1_1.xml", rebuilt.Copied);
            Assert.Equal(
                "<level id=\"2\" />",
                File.ReadAllText(Path.Combine(Output, "maps", "1_1.xml")));
        }

        [Fact]
        public void ADeletedSourceTakesItsOutputWithIt()
        {
            Fill();
            Write("maps/1_2.xml", "<level id=\"2\" />");
            _ = GameContentBuilder.Build(Source, Output);
            Assert.True(File.Exists(Path.Combine(Output, "maps", "1_2.xml")));

            File.Delete(Path.Combine(Source, "maps", "1_2.xml"));
            ContentCopyResult rebuilt = GameContentBuilder.Build(Source, Output);

            Assert.Contains("maps/1_2.xml", rebuilt.Removed);
            Assert.False(File.Exists(Path.Combine(Output, "maps", "1_2.xml")));
        }

        [Fact]
        public void AStaleGeneratedManifestInTheSourceTreeIsNeverCopiedOverTheFreshOne()
        {
            Fill();
            Write("images/image_dimensions.json", "{\"images\":{\"gone\":{\"w\":1,\"h\":1}}}");

            ContentCopyResult first = GameContentBuilder.Build(Source, Output);
            ContentCopyResult second = GameContentBuilder.Build(Source, Output);

            Assert.DoesNotContain("images/image_dimensions.json", first.Copied);
            Assert.Empty(second.Copied);
            using JsonDocument manifest = JsonDocument.Parse(
                File.ReadAllText(Path.Combine(Output, "images", "image_dimensions.json")));
            JsonElement images = manifest.RootElement.GetProperty("images");
            Assert.False(images.TryGetProperty("gone", out _));
            Assert.True(images.TryGetProperty("menu/logo", out _));
        }

        [Fact]
        public void TheGeneratedManifestSurvivesTheStaleOutputSweep()
        {
            Fill();
            _ = GameContentBuilder.Build(Source, Output);

            ContentCopyResult rebuilt = GameContentBuilder.Build(Source, Output);

            Assert.DoesNotContain("images/image_dimensions.json", rebuilt.Removed);
            Assert.True(File.Exists(Path.Combine(Output, "images", "image_dimensions.json")));
        }

        [Fact]
        public void AnEmptiedDirectoryDoesNotSurviveAsAnEmptyShell()
        {
            Fill();
            Write("video_hd/outro.mp4", "frames");
            _ = GameContentBuilder.Build(Source, Output);

            File.Delete(Path.Combine(Source, "video_hd", "intro.mp4"));
            File.Delete(Path.Combine(Source, "video_hd", "outro.mp4"));
            _ = GameContentBuilder.Build(Source, Output);

            Assert.False(Directory.Exists(Path.Combine(Output, "video_hd")));
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
                () => GameContentBuilder.Build(Source, Output));

            Assert.Contains(missing, failure.Message, StringComparison.Ordinal);
            Assert.Contains("fetch the external assets", failure.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void AnOutputTheBuildWouldReadBackAsInputIsRefused()
        {
            Fill();

            ContentBuildException failure = Assert.Throws<ContentBuildException>(
                () => GameContentBuilder.Build(Source, Path.Combine(Source, "images", "built")));

            Assert.Contains("its own source", failure.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void AnOutputUnderAnExcludedDirectoryIsAllowed()
        {
            Fill();

            _ = GameContentBuilder.Build(Source, Path.Combine(Source, "Builder", "bin", "content"));

            Assert.True(File.Exists(
                Path.Combine(Source, "Builder", "bin", "content", "images", "menu", "logo.png")));
        }

        [Fact]
        public void AMissingSourceTreeIsReportedRatherThanProducingAnEmptyBuild()
        {
            ContentBuildException failure = Assert.Throws<ContentBuildException>(
                () => GameContentBuilder.Build(Path.Combine(root, "absent"), Output));

            Assert.Contains("No content source directory", failure.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void OutputPathsAreConfinedToTheOutputDirectory()
        {
            Fill();
            SortedDictionary<string, string> selected = ContentCopy.Select(Source, ContentCopy.DesktopRules);
            string output = Path.GetFullPath(Output);

            Assert.All(selected.Keys, relativePath =>
            {
                string resolved = Path.GetFullPath(Path.Combine(output, relativePath));
                Assert.StartsWith(output + Path.DirectorySeparatorChar, resolved, StringComparison.Ordinal);
            });
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
