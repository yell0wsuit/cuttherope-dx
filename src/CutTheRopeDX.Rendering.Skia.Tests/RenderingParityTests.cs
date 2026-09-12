using System;
using System.Numerics;

using CutTheRopeDX.Framework.Core;
using CutTheRopeDX.Framework.Platform;
using CutTheRopeDX.Framework.Visual;

using SkiaSharp;

using Xunit;
namespace CutTheRopeDX.Rendering.Skia.Tests
{
    public sealed class RenderingParityTests
    {
        [Theory]
        [InlineData(false, 128, 0, 127)]
        [InlineData(true, 128, 0, 127)]
        public void AlphaAndSourceAlphaWeightMatchFixedFunction(bool weighted, int red, int green, int blue)
        {
            using FakeSkiaSurface surface = new();
            using SkiaRenderBackend renderer = new(surface);
            surface.Canvas.Clear(SKColors.Blue);
            renderer.SetBlendFunc(weighted ? BlendingFactor.GLSRCALPHA : BlendingFactor.GLONE, BlendingFactor.GLONEMINUSSRCALPHA);
            DrawRect(renderer, weighted ? new Color(255, 0, 0, 128) : new Color(128, 0, 0, 128));
            renderer.EndFrame();
            using SKBitmap pixels = surface.Pixels();
            Near(new SKColor((byte)red, (byte)green, (byte)blue), pixels.GetPixel(8, 16));
        }
        [Theory]
        [InlineData((int)BlendingFactor.GLONE)]
        [InlineData((int)BlendingFactor.GLSRCALPHA)]
        public void AdditiveBlendingAddsToWhatIsAlreadyThere(int source)
        {
            // Both pairs the game asks for additively: blending mode 2 weights the source by its
            // own alpha and mode 3 does not, and at full alpha the two agree. Mode 3 is what a
            // bomb's fragments use to read as hot, so a pair that falls through to source-over
            // composites the explosion flat over the scene instead of lighting it.
            using FakeSkiaSurface surface = new();
            using SkiaRenderBackend renderer = new(surface);
            surface.Canvas.Clear(new SKColor(40, 40, 40));
            renderer.SetBlendFunc((BlendingFactor)source, BlendingFactor.GLONE);
            DrawRect(renderer, new Color(100, 0, 0, 255));
            renderer.EndFrame();
            using SKBitmap pixels = surface.Pixels();
            Near(new SKColor(140, 40, 40), pixels.GetPixel(8, 16));
        }

        [Fact]
        public void DisablingBlendingReplacesTheDestinationRatherThanCompositingOverIt()
        {
            using FakeSkiaSurface surface = new();
            using SkiaRenderBackend renderer = new(surface);
            surface.Canvas.Clear(SKColors.Blue);
            renderer.Disable(1);
            DrawRect(renderer, new Color(255, 0, 0, 128));
            renderer.EndFrame();
            using SKBitmap pixels = surface.Pixels();

            // The opaque blend state the fixed-function pipeline used: the half-transparent
            // source lands as written, with none of the blue underneath showing through.
            Near(new SKColor(255, 0, 0, 128), pixels.GetPixel(8, 16));
        }

        [Fact]
        public void ScissorReplacementAndMatrixStackAffectPixels()
        {
            using FakeSkiaSurface surface = new();
            using SkiaRenderBackend renderer = new(surface);
            surface.Canvas.Clear(SKColors.Black);
            renderer.SetScissor(0, 0, 4, 32);
            DrawRect(renderer, new Color(255, 0, 0, 255));
            renderer.SetScissor(0, 0, 32, 32);
            renderer.PushMatrix(); renderer.Translate(8, 0, 0);
            DrawRect(renderer, new Color(0, 128, 0, 255));
            renderer.PopMatrix(); renderer.EndFrame();
            using SKBitmap pixels = surface.Pixels();
            Near(SKColors.Red, pixels.GetPixel(2, 16));
            Near(SKColors.Black, pixels.GetPixel(6, 16));
            Near(new SKColor(0, 128, 0), pixels.GetPixel(12, 16));
            Near(SKColors.Black, pixels.GetPixel(35, 16));
        }
        [Fact]
        public void TextureRgbTintAndAlphaAreAppliedOnce()
        {
            using FakeSkiaSurface surface = new();
            using SkiaRenderBackend renderer = new(surface);
            using SKBitmap bitmap = new(2, 2);
            bitmap.Erase(new SKColor(200, 100, 50));
            using SkiaTexture texture = new(SKImage.FromBitmap(bitmap));
            CTRTexture2D wrapper = new() { textureHandle_ = texture };
            surface.Canvas.Clear(SKColors.Black);
            renderer.BindTexture(wrapper);
            renderer.SetColor(new Color(128, 255, 128, 255));
            VertexPositionNormalTexture[] vertices = [new(new Vector3(0, 0, 0), Vector3.Zero, Vector2.Zero), new(new Vector3(32, 0, 0), Vector3.Zero, Vector2.UnitX), new(new Vector3(0, 32, 0), Vector3.Zero, Vector2.UnitY), new(new Vector3(32, 32, 0), Vector3.Zero, Vector2.One)];
            renderer.DrawTriangleStrip(vertices, 4); renderer.EndFrame();
            using SKBitmap pixels = surface.Pixels();
            Near(new SKColor(100, 100, 25), pixels.GetPixel(8, 16));
        }
        internal static void DrawRect(SkiaRenderBackend renderer, Color color)
        {
            VertexPositionColor[] vertices = [new(new Vector3(0, 0, 0), color), new(new Vector3(32, 0, 0), color), new(new Vector3(0, 32, 0), color), new(new Vector3(32, 32, 0), color)];
            renderer.DrawTriangleStrip(vertices, 4);
        }
        internal static void Near(SKColor expected, SKColor actual)
        {
            Assert.InRange(Math.Abs(actual.Red - expected.Red), 0, 2);
            Assert.InRange(Math.Abs(actual.Green - expected.Green), 0, 2);
            Assert.InRange(Math.Abs(actual.Blue - expected.Blue), 0, 2);
            Assert.InRange(Math.Abs(actual.Alpha - expected.Alpha), 0, 2);
        }
    }
}
