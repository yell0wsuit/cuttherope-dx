using System;
using System.IO;

using CutTheRopeDX.Framework;
using CutTheRopeDX.Framework.Core;
using CutTheRopeDX.Framework.Platform;
using CutTheRopeDX.Framework.Visual;
using CutTheRopeDX.GameMain;

using SkiaSharp;

using Xunit;
namespace CutTheRopeDX.Rendering.Skia.Tests
{
    public sealed class FontLayoutTests
    {
        [Theory]
        [InlineData("PlaypenSans-SemiBold.ttf", "Rope", false)]
        [InlineData("MPLUSRounded1c-Medium.ttf", "日本語", true)]
        public void MultilingualTextRendersUnderModelRotation(string file, string text, bool rotated)
        {
            string path = Path.Combine(AppContext.BaseDirectory, "fonts", file);
            Assert.SkipUnless(File.Exists(path), $"Font '{file}' is a fetched asset and is not present.");
            using SKTypeface typeface = SKTypeface.FromFile(path);
            using SkiaFont font = new(typeface, new FontConfiguration { FontFile = file, Size = 20, Color = Color.White });
            Assert.InRange(font.FontHeight(), 19.9f, 20.1f);
            foreach (char c in text) { Assert.True(font.CanDraw(c)); Assert.True(font.GetCharWidth(c) > 0); }
            using FakeSkiaSurface surface = new();
            using SkiaRenderBackend renderer = new(surface);
            IRenderBackend previous = PlatformServices.Render;
            try
            {
                PlatformServices.Render = renderer;
                surface.Canvas.Clear(SKColors.Transparent);
                if (rotated) { renderer.Translate(100, 0, 0); renderer.Rotate(90, 0, 0, 1); }
                TextDrawCall call = new([new FormattedString().InitWithStringAndWidth(text, 60)], 10, 10, 100, 1, -1, new RGBAColor(1, 1, 1, 1), new RGBAColor(1, 1, 1, 1), false, 0, 0, 0, 0);
                font.DrawText(call); renderer.EndFrame();
                using SKBitmap pixels = surface.Pixels();
                int visible = 0;
                for (int y = 0; y < 128; y++)
                {
                    for (int x = 0; x < 128; x++)
                    {
                        if (pixels.GetPixel(x, y).Alpha == 0)
                        {
                            continue;
                        }

                        visible++;
                        if (rotated) { Assert.InRange(x, 65, 90); Assert.InRange(y, 10, 80); }
                        else { Assert.InRange(x, 10, 100); Assert.InRange(y, 10, 35); }
                    }
                }

                Assert.True(visible > 50);
                font.Dispose();
                Assert.False(font.IsAlive); Assert.Equal(0, font.GetCharWidth(text[0]));
            }
            finally { PlatformServices.Render = previous; }
        }
    }
}
