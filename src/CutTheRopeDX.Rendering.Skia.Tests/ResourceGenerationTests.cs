using System.Numerics;

using CutTheRopeDX.Framework.Core;
using CutTheRopeDX.Framework.Visual;

using SkiaSharp;

using Xunit;

namespace CutTheRopeDX.Rendering.Skia.Tests
{
    public sealed class ResourceGenerationTests
    {
        [Fact]
        public void ADeviceLossRetiresTheGenerationAndNamesWhatToLoadAgain()
        {
            SkiaResourceRegistry registry = new();
            int firstGeneration = registry.TrackDurable("images/menu");
            _ = registry.TrackDurable("images/hud");
            _ = registry.TrackTransient();

            Assert.Equal(1, registry.TransientCount);
            System.Collections.Generic.IReadOnlyList<string> rebuild = registry.Invalidate();

            Assert.Equal(["images/hud", "images/menu"], Sorted(rebuild));
            Assert.Equal(0, registry.TransientCount);
            Assert.Empty(registry.DurableDescriptions);
            Assert.False(registry.IsCurrent(firstGeneration));
            Assert.True(registry.IsCurrent(registry.Generation));
        }

        [Fact]
        public void AResourceNoDeviceOwnsSurvivesEveryLoss()
        {
            SkiaResourceRegistry registry = new();

            for (int loss = 0; loss < 3; loss++)
            {
                _ = registry.Invalidate();
            }

            Assert.True(registry.IsCurrent(SkiaResourceRegistry.DeviceIndependent));
        }

        [Fact]
        public void AnAssetFreedBeforeALossIsNotLoadedAgainAfterOne()
        {
            SkiaResourceRegistry registry = new();
            _ = registry.TrackDurable("images/menu");
            _ = registry.TrackDurable("images/hud");
            _ = registry.TrackTransient();

            registry.Forget("images/menu");
            registry.ForgetTransient();

            Assert.Equal(["images/hud"], Sorted(registry.Invalidate()));
            Assert.Equal(0, registry.TransientCount);
        }

        [Fact]
        public void ATextureFromARetiredGenerationIsRefusedInsteadOfSampled()
        {
            SkiaResourceRegistry registry = new();
            using FakeSkiaSurface surface = new();
            using SkiaRenderBackend renderer = new(surface, registry);
            using SkiaTexture texture = Red(registry.TrackDurable("images/menu"));
            CTRTexture2D wrapper = new() { textureHandle_ = texture };

            _ = registry.Invalidate();
            surface.Canvas.Clear(SKColors.Black);
            renderer.BindTexture(wrapper);
            renderer.SetColor(new Color(255, 255, 255, 255));
            renderer.DrawTriangleStrip(Quad(), 4);
            renderer.EndFrame();

            Assert.Equal(1, renderer.RejectedBinds);
            using SKBitmap pixels = surface.Pixels();
            RenderingParityTests.Near(SKColors.Black, pixels.GetPixel(8, 16));
        }

        [Fact]
        public void ATextureFromTheLiveGenerationStillDraws()
        {
            SkiaResourceRegistry registry = new();
            using FakeSkiaSurface surface = new();
            using SkiaRenderBackend renderer = new(surface, registry);
            using SkiaTexture texture = Red(registry.TrackDurable("images/menu"));
            CTRTexture2D wrapper = new() { textureHandle_ = texture };

            surface.Canvas.Clear(SKColors.Black);
            renderer.BindTexture(wrapper);
            renderer.SetColor(new Color(255, 255, 255, 255));
            renderer.DrawTriangleStrip(Quad(), 4);
            renderer.EndFrame();

            Assert.Equal(0, renderer.RejectedBinds);
            using SKBitmap pixels = surface.Pixels();
            RenderingParityTests.Near(SKColors.Red, pixels.GetPixel(8, 16));
        }

        [Fact]
        public void ARendererWithNoRegistryDrawsEverythingItIsGiven()
        {
            using FakeSkiaSurface surface = new();
            using SkiaRenderBackend renderer = new(surface);
            using SkiaTexture texture = Red(SkiaResourceRegistry.DeviceIndependent);
            CTRTexture2D wrapper = new() { textureHandle_ = texture };

            surface.Canvas.Clear(SKColors.Black);
            renderer.BindTexture(wrapper);
            renderer.SetColor(new Color(255, 255, 255, 255));
            renderer.DrawTriangleStrip(Quad(), 4);
            renderer.EndFrame();

            Assert.Equal(0, renderer.RejectedBinds);
            using SKBitmap pixels = surface.Pixels();
            RenderingParityTests.Near(SKColors.Red, pixels.GetPixel(8, 16));
        }

        [Fact]
        public void DiscardingDeviceResourcesDropsTheQuadsQueuedAgainstThem()
        {
            SkiaResourceRegistry registry = new();
            using FakeSkiaSurface surface = new();
            using SkiaRenderBackend renderer = new(surface, registry);
            using SkiaTexture texture = Red(registry.TrackDurable("images/menu"));
            CTRTexture2D wrapper = new() { textureHandle_ = texture };

            surface.Canvas.Clear(SKColors.Black);
            renderer.BindTexture(wrapper);
            renderer.SetColor(new Color(255, 255, 255, 255));
            renderer.DrawTriangleStrip(Quad(), 4);
            renderer.DiscardDeviceResources();
            renderer.EndFrame();

            using SKBitmap pixels = surface.Pixels();
            RenderingParityTests.Near(SKColors.Black, pixels.GetPixel(8, 16));
        }

        /// <summary>
        /// The batch paint keeps a reference to the shader it last drew through, and that shader
        /// keeps the device's image. Every other handle to a lost device is dropped before the
        /// device is destroyed, so a paint that kept holding one would be the image's last owner
        /// and would free it through a context that no longer exists.
        /// </summary>
        [Fact]
        public void DiscardingDeviceResourcesReleasesTheShaderTheBatchPaintDrawsThrough()
        {
            SkiaResourceRegistry registry = new();
            using FakeSkiaSurface surface = new();
            using SkiaRenderBackend renderer = new(surface, registry);
            using SkiaTexture texture = Red(registry.TrackDurable("images/menu"));

            surface.Canvas.Clear(SKColors.Black);
            renderer.BindTexture(new CTRTexture2D { textureHandle_ = texture });
            renderer.SetColor(new Color(255, 255, 255, 255));
            renderer.DrawTriangleStrip(Quad(), 4);
            renderer.EndFrame();
            Assert.True(renderer.RetainsDeviceShader);

            renderer.DiscardDeviceResources();

            Assert.False(renderer.RetainsDeviceShader);
        }

        [Fact]
        public void ARebindDrawsIntoTheReplacementSurface()
        {
            SkiaResourceRegistry registry = new();
            using FakeSkiaSurface lost = new();
            using FakeSkiaSurface replacement = new();
            using SkiaRenderBackend renderer = new(lost, registry);
            lost.Canvas.Clear(SKColors.Black);
            replacement.Canvas.Clear(SKColors.Black);

            _ = registry.Invalidate();
            renderer.Rebind(replacement);
            using SkiaTexture texture = Red(registry.TrackDurable("images/menu"));
            renderer.BindTexture(new CTRTexture2D { textureHandle_ = texture });
            renderer.SetColor(new Color(255, 255, 255, 255));
            renderer.DrawTriangleStrip(Quad(), 4);
            renderer.EndFrame();

            using SKBitmap replaced = replacement.Pixels();
            using SKBitmap abandoned = lost.Pixels();
            RenderingParityTests.Near(SKColors.Red, replaced.GetPixel(8, 16));
            RenderingParityTests.Near(SKColors.Black, abandoned.GetPixel(8, 16));
        }

        [Fact]
        public void AMovieFrameSurfaceBelongsToNoDevice()
        {
            SkiaResourceRegistry registry = new();
            using FakeSkiaSurface surface = new();
            using SkiaRenderBackend renderer = new(surface, registry);

            using SkiaVideoFrameTexture frame = (SkiaVideoFrameTexture)renderer.CreateVideoFrameTexture(4, 4);
            _ = registry.Invalidate();
            frame.Update(new byte[4 * 4 * 4]);

            Assert.Equal(0, registry.TransientCount);
            Assert.NotNull(frame.Bitmap);
        }

        /// <summary>A one-color image stamped with a device generation.</summary>
        private static SkiaTexture Red(int generation)
        {
            using SKBitmap bitmap = new(2, 2);
            bitmap.Erase(SKColors.Red);
            return new SkiaTexture(SKImage.FromBitmap(bitmap), generation);
        }

        /// <summary>A 32x32 textured quad covering the sampled pixel.</summary>
        private static VertexPositionNormalTexture[] Quad()
        {
            return
            [
                new(new Vector3(0, 0, 0), Vector3.Zero, Vector2.Zero),
                new(new Vector3(32, 0, 0), Vector3.Zero, Vector2.UnitX),
                new(new Vector3(0, 32, 0), Vector3.Zero, Vector2.UnitY),
                new(new Vector3(32, 32, 0), Vector3.Zero, Vector2.One),
            ];
        }

        private static string[] Sorted(System.Collections.Generic.IEnumerable<string> values)
        {
            string[] sorted = [.. values];
            System.Array.Sort(sorted, System.StringComparer.Ordinal);
            return sorted;
        }
    }
}
