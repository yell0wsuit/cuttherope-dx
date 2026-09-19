using System;
using System.Collections.Generic;

using CutTheRopeDX.Framework;
using CutTheRopeDX.Framework.Core;
using CutTheRopeDX.Framework.Platform;
using CutTheRopeDX.Framework.Visual;
using CutTheRopeDX.GameMain;

using Xunit;

namespace CutTheRopeDX.Tests
{
    /// <summary>Freeing several packs together.</summary>
    public sealed class ResourceMgrFreePacksTests : IDisposable
    {
        private readonly ResourceMgr resources;
        private readonly LoadRecorder platform;

        public ResourceMgrFreePacksTests()
        {
            _ = HeadlessGame.Boot();
            resources = Application.SharedResourceMgr();
            resources.InitLoading();
            platform = new LoadRecorder(AssetPlatform.Current);
            AssetPlatform.Current = platform;
        }

        public void Dispose()
        {
            AssetPlatform.Current = platform.Inner;
        }

        [Fact]
        public void EveryPackIsFreedUpToItsTerminatorAndMissingPacksAreSkipped()
        {
            string[] first = [Resources.Img.MenuButtons, null, Resources.Img.MenuPopup];
            string[] second = [Resources.Img.MenuLevelUi];
            resources.InitLoading();
            resources.LoadPack([Resources.Img.MenuButtons, Resources.Img.MenuLevelUi, Resources.Img.MenuPopup, null]);
            resources.LoadImmediately();

            resources.FreePacks([first, null, second]);

            // Loading the same images again only fetches the ones that were freed.
            platform.Loaded.Clear();
            resources.InitLoading();
            resources.LoadPack([Resources.Img.MenuButtons, Resources.Img.MenuLevelUi, Resources.Img.MenuPopup, null]);
            resources.LoadImmediately();

            Assert.Equal(
                [ResourceMgr.ImageContentPath(Resources.Img.MenuButtons), ResourceMgr.ImageContentPath(Resources.Img.MenuLevelUi)],
                platform.Loaded);
        }

        /// <summary>Stands in for the platform, recording which images are loaded.</summary>
        private sealed class LoadRecorder(IAssetPlatform inner) : IAssetPlatform
        {
            public IAssetPlatform Inner => inner;

            public List<string> Loaded { get; } = [];

            public void FreeImage(string contentPath)
            {
                inner.FreeImage(contentPath);
            }

            public (int W, int H)? ImageDimensions(string contentPath)
            {
                return inner.ImageDimensions(contentPath);
            }

            public ITextureHandle ImageTexture(string contentPath)
            {
                Loaded.Add(contentPath);
                return inner.ImageTexture(contentPath);
            }

            public ITextureHandle TintedRegion(
                ITextureHandle source, int x, int y, int width, int height, RGBAColor tint)
            {
                return inner.TintedRegion(source, x, y, width, height, tint);
            }

            public void PrepareImage(string contentPath)
            {
                inner.PrepareImage(contentPath);
            }

            public bool IsImageReady(string contentPath)
            {
                return inner.IsImageReady(contentPath);
            }

            public void DiscardPreparedImage(string contentPath)
            {
                inner.DiscardPreparedImage(contentPath);
            }

            public long TakePeakPreparedPixelBytes()
            {
                return inner.TakePeakPreparedPixelBytes();
            }

            public FontGeneric Font(string resourceName)
            {
                return inner.Font(resourceName);
            }

            public void ClearFontCache()
            {
                inner.ClearFontCache();
            }
        }
    }
}
