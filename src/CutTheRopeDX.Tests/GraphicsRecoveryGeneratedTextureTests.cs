using System;

using CutTheRopeDX.Framework;
using CutTheRopeDX.Framework.Core;
using CutTheRopeDX.Framework.Platform;
using CutTheRopeDX.Framework.Visual;
using CutTheRopeDX.GameMain.Tutorials;
using CutTheRopeDX.Tests.Interactions;

using Xunit;

namespace CutTheRopeDX.Tests
{
    /// <summary>
    /// What a device loss does to a texture that was built rather than loaded.
    /// </summary>
    /// <remarks>
    /// Recovery reloads a texture from the content path it remembers, which leaves the ones that
    /// have no path. A captured frame is genuinely gone - nothing can produce it again - but a
    /// recolored atlas frame is not: the cache that built it is still holding it and can build it
    /// again from an atlas recovery has already reloaded. The two look alike from the texture
    /// alone, so what tells them apart has to travel with it.
    /// </remarks>
    public sealed class GraphicsRecoveryGeneratedTextureTests
    {
        private static readonly RGBAColor Red = RGBAColor.MakeRGBA(1f, 0f, 0f, 1f);

        [Fact]
        public void ARecoloredFrameIsBuiltAgainRatherThanLeftBlank()
        {
            using Probe probe = new();
            using TutorialSignTints tints = new();

            CTRTexture2D tinted = tints.Tinted(probe.Atlas, 4, Red);
            Handle before = Assert.IsType<Handle>(tinted.textureHandle_);
            Assert.Null(tinted._resName);
            Assert.Equal(1, probe.TintCalls);

            _ = GraphicsRecovery.Complete(GraphicsRecovery.Begin());

            // The handle the lost device owned is gone, and a live one has taken its place, so
            // the sign draws instead of being dropped for having nothing to sample.
            Assert.True(before.Disposed);
            Assert.NotNull(tinted.textureHandle_);
            Assert.NotSame(before, tinted.textureHandle_);
            Assert.Equal(2, probe.TintCalls);
        }

        [Fact]
        public void TheCacheKeepsHandingOutTheSameTextureAcrossARecovery()
        {
            using Probe probe = new();
            using TutorialSignTints tints = new();

            CTRTexture2D tinted = tints.Tinted(probe.Atlas, 4, Red);

            _ = GraphicsRecovery.Complete(GraphicsRecovery.Begin());

            // Whoever is drawing the sign is holding this, so rebuilding has to happen inside the
            // texture the scene already has rather than by handing out a replacement.
            Assert.Same(tinted, tints.Tinted(probe.Atlas, 4, Red));
            Assert.Equal(2, probe.TintCalls);
        }

        [Fact]
        public void ACapturedFrameIsStillDroppedBecauseNothingCanProduceItAgain()
        {
            using Probe probe = new();
            CTRTexture2D capture = new CTRTexture2D().InitWithHandle(new Handle(8, 8), 8, 8);
            try
            {
                Handle before = (Handle)capture.textureHandle_;

                GraphicsRecoveryPlan plan = GraphicsRecovery.Begin();
                _ = GraphicsRecovery.Complete(plan);

                Assert.True(before.Disposed);
                Assert.Null(capture.textureHandle_);
                Assert.True(plan.DroppedCaptures >= 1);
            }
            finally
            {
                capture.Unreg();
                capture.Dispose();
            }
        }

        /// <summary>Stands in for the platform, counting what it is asked to build.</summary>
        private sealed class Probe : IAssetPlatform, IDisposable
        {
            private readonly IAssetPlatform inner;

            public Probe()
            {
                _ = HeadlessGame.Boot();
                inner = AssetPlatform.Current;
                AssetPlatform.Current = this;
                Atlas = new CTRTexture2D().InitWithHandle(new Handle(256, 956), 256, 956);
                Atlas.SetQuadsCapacity(11);
                Atlas.SetQuadAt(new CTRRectangle(1f, 243f, 246f, 235f), 4);
            }

            public CTRTexture2D Atlas { get; }

            public int TintCalls { get; private set; }

            public (int W, int H)? ImageDimensions(string path) => inner.ImageDimensions(path);

            public ITextureHandle ImageTexture(string path) => inner.ImageTexture(path);

            public ITextureHandle TintedRegion(
                ITextureHandle source, int x, int y, int width, int height, RGBAColor tint)
            {
                TintCalls++;
                return new Handle(width, height);
            }

            public void FreeImage(string path) => inner.FreeImage(path);

            public FontGeneric Font(string name) => inner.Font(name);

            public void ClearFontCache() => inner.ClearFontCache();

            public void Dispose()
            {
                AssetPlatform.Current = inner;
                Atlas.Unreg();
                Atlas.Dispose();
            }
        }

        private sealed class Handle(int width, int height) : ITextureHandle
        {
            public int Width => width;

            public int Height => height;

            public bool Disposed { get; private set; }

            public void Dispose() => Disposed = true;
        }
    }
}
