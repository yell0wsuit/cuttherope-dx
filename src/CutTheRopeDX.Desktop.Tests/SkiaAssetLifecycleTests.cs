using System.IO;

using CutTheRopeDX.Framework.Platform;

using SkiaSharp;

using Xunit;

namespace CutTheRopeDX.Desktop.Tests
{
    public sealed class SkiaAssetLifecycleTests
    {
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
