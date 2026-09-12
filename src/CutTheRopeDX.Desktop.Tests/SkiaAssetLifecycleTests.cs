using System.IO;

using CutTheRopeDX.Framework;
using CutTheRopeDX.Framework.Platform;
using CutTheRopeDX.GameMain;
using CutTheRopeDX.Rendering.Skia;

using SkiaSharp;

using Xunit;

namespace CutTheRopeDX.Desktop.Tests
{
    public sealed class SkiaAssetLifecycleTests
    {
        [Theory]
        [InlineData(Language.LANGKO, "fonts/Cafe24DongdongRegular.otf")]
        [InlineData(Language.LANGEN, "fonts/gooddog_new-webfont.ttf")]
        public void FontsAreRequestedByTheirConfiguredFileName(Language language, string expected)
        {
            // Every host ships each face under its configured name, so a configuration naming
            // an OpenType file resolves to that file. Asking for a TrueType sibling finds nothing.
            Language previousLanguage = LanguageHelper.Current;
            IContentStore previousContent = PlatformServices.Content;
            CountingStore store = new(null);
            using SkiaAssetPlatform assets = new(store, null);
            try
            {
                LanguageHelper.Current = language;
                PlatformServices.Content = store;
                _ = Assert.Throws<FileNotFoundException>(() => assets.Font(Resources.Fnt.SmallFont));
                Assert.Equal(expected, store.LastPath);
            }
            finally
            {
                SkiaFontCache.Clear();
                LanguageHelper.Current = previousLanguage;
                PlatformServices.Content = previousContent;
            }
        }

        [Fact]
        public void PngDimensionsReuseAndReleaseReload()
        {
            using SKBitmap bitmap = new(7, 11);
            bitmap.Erase(SKColors.Red);
            using SKImage image = SKImage.FromBitmap(bitmap);
            using SKData data = image.Encode(SKEncodedImageFormat.Png, 100);
            CountingStore store = new(data.ToArray());
            using SkiaAssetPlatform assets = new(store, null);
            Assert.Equal((7, 11), assets.ImageDimensions("images/test"));
            ITextureHandle first = assets.ImageTexture("images/test");
            Assert.Same(first, assets.ImageTexture("images/test"));
            Assert.Equal(1, store.Reads);
            assets.FreeImage("images/test");
            Assert.NotSame(first, assets.ImageTexture("images/test"));
            Assert.Equal(2, store.Reads);
            Assert.Equal("images/test.png", store.LastPath);
        }

        [Fact]
        public void MissingImageHasNoDimensionsOrTexture()
        {
            using SkiaAssetPlatform assets = new(new CountingStore(null), null);
            Assert.Null(assets.ImageDimensions("missing"));
            Assert.Null(assets.ImageTexture("missing"));
        }

        [Theory]
        [InlineData("/portable", "/portable/content")]
        [InlineData("/Game.app/Contents/MacOS", "/Game.app/Contents/Resources/content")]
        [InlineData("/Game.app/Contents/MonoBundle", "/Game.app/Contents/Resources/content")]
        public void ResolvesPortableAndBundleContent(string executableDirectory, string expected)
        {
            Assert.Equal(Path.GetFullPath(expected), SkiaAssetPlatform.ResolveContentRoot(executableDirectory));
        }

        private sealed class CountingStore(byte[] bytes) : IContentStore
        {
            public int Reads { get; private set; }
            public string LastPath { get; private set; }
            public byte[] Read(string relativePath)
            {
                Reads++;
                LastPath = relativePath;
                return bytes ?? throw new FileNotFoundException(relativePath);
            }
        }
    }
}
