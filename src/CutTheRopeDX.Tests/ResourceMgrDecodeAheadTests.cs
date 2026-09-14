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
    /// <summary>
    /// How the loading queue starts image decodes early and paces what it loads per frame.
    /// </summary>
    public sealed class ResourceMgrDecodeAheadTests : IDisposable
    {
        private static readonly string[] Images =
        [
            Resources.Img.MenuButtons,
            Resources.Img.MenuLevelUi,
            Resources.Img.MenuExtraButtons,
        ];

        private readonly CTRResourceMgr resources;
        private readonly RecordingPlatform platform;
        private readonly IResourceMgrDelegate previousDelegate;
        private readonly CompletionRecorder completion = new();

        public ResourceMgrDecodeAheadTests()
        {
            _ = HeadlessGame.Boot();
            resources = Application.SharedResourceMgr();
            previousDelegate = resources.resourcesDelegate;
            resources.resourcesDelegate = completion;
            FreeTestImages();
            platform = new RecordingPlatform(AssetPlatform.Current);
            AssetPlatform.Current = platform;
        }

        public void Dispose()
        {
            AssetPlatform.Current = platform.Inner;
            FreeTestImages();
            resources.InitLoading();
            resources.resourcesDelegate = previousDelegate;
        }

        [Fact]
        public void QueuingAnImageStartsItsDecodeButQueuingASoundDoesNot()
        {
            resources.InitLoading();
            resources.LoadPack([Resources.Img.MenuButtons, Resources.Snd.Tap, null]);

            Assert.Equal([ResourceMgr.ImageContentPath(Resources.Img.MenuButtons)], platform.Prepared);
        }

        [Fact]
        public void OneFrameLoadsEveryImageWhoseDecodeHasFinished()
        {
            resources.InitLoading();
            resources.LoadPack([.. Images, null]);

            resources.Update();

            Assert.Equal(PathsOf(Images), platform.Loaded);
            Assert.Equal(1, completion.Calls);
        }

        [Fact]
        public void AFrameStopsBeforeAnImageThatIsStillDecoding()
        {
            _ = platform.StillDecoding.Add(ResourceMgr.ImageContentPath(Images[1]));
            resources.InitLoading();
            resources.LoadPack([.. Images, null]);

            resources.Update();

            Assert.Equal(PathsOf(Images[0]), platform.Loaded);
            Assert.Equal(0, completion.Calls);

            platform.StillDecoding.Clear();
            resources.Update();

            Assert.Equal(PathsOf(Images), platform.Loaded);
            Assert.Equal(1, completion.Calls);
        }

        [Fact]
        public void AFrameLoadsNothingWhileTheFirstImageIsStillDecoding()
        {
            _ = platform.StillDecoding.Add(ResourceMgr.ImageContentPath(Images[0]));
            resources.InitLoading();
            resources.LoadPack([.. Images, null]);

            resources.Update();

            Assert.Empty(platform.Loaded);
            Assert.Equal(0, completion.Calls);
            Assert.Equal(0, resources.GetPercentLoaded());

            platform.StillDecoding.Clear();
            resources.Update();

            Assert.Equal(PathsOf(Images), platform.Loaded);
            Assert.Equal(1, completion.Calls);
        }

        [Fact]
        public void AResourceThatIsNotAnImageLoadsAheadOfAnImageStillDecoding()
        {
            _ = platform.StillDecoding.Add(ResourceMgr.ImageContentPath(Images[0]));
            resources.InitLoading();
            resources.LoadPack([Resources.Snd.Tap, Images[0], null]);

            resources.Update();

            Assert.Empty(platform.Loaded);
            Assert.Equal(50, resources.GetPercentLoaded());
            Assert.Equal(0, completion.Calls);
        }

        private void FreeTestImages()
        {
            foreach (string image in Images)
            {
                resources.FreeResource(image);
            }
        }

        private static List<string> PathsOf(params string[] images)
        {
            List<string> paths = [];
            foreach (string image in images)
            {
                paths.Add(ResourceMgr.ImageContentPath(image));
            }

            return paths;
        }

        private sealed class CompletionRecorder : IResourceMgrDelegate
        {
            public int Calls { get; private set; }

            public void AllResourcesLoaded()
            {
                Calls++;
            }
        }

        /// <summary>Stands in for the platform, recording what is prepared and loaded.</summary>
        private sealed class RecordingPlatform(IAssetPlatform inner) : IAssetPlatform
        {
            public IAssetPlatform Inner => inner;

            public List<string> Prepared { get; } = [];

            public List<string> Loaded { get; } = [];

            public HashSet<string> StillDecoding { get; } = [];

            public void PrepareImage(string contentPath)
            {
                Prepared.Add(contentPath);
            }

            public bool IsImageReady(string contentPath)
            {
                return !StillDecoding.Contains(contentPath);
            }

            public void DiscardPreparedImage(string contentPath)
            {
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

            public void FreeImage(string contentPath)
            {
                inner.FreeImage(contentPath);
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
