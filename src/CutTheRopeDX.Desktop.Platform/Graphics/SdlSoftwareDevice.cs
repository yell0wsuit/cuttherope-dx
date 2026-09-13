using System;

using CutTheRopeDX.Framework.Diagnostics;

using Microsoft.Extensions.Logging;

using SDL3;

using SkiaSharp;

namespace CutTheRopeDX.Desktop.Platform.Graphics
{
    /// <summary>CPU raster drawing and presentation for desktops without usable GPU drivers.</summary>
    public sealed class SdlSoftwareDevice : SdlGraphicsDevice
    {
        private nint renderer;
        private nint texture;
        private SKBitmap pixels;

        /// <summary>Creates a software SDL renderer without an accelerated window framebuffer.</summary>
        public void Initialize()
        {
            GlHintScope hints = Own(new GlHintScope());
            hints.Set("SDL_FRAMEBUFFER_ACCELERATION", "0");
            CreateWindow(0);
            renderer = SDL.CreateRenderer(Window, "software");
            if (renderer == 0)
            {
                throw new InvalidOperationException(SDL.GetError());
            }
            Own(() => SDL.DestroyRenderer(renderer));
            Own(ReleasePixels);
            Resize();
            ILogger logger = Log.For(LogCategories.SdlGraphics);
            GraphicsDeviceLog.Adapter(logger,
                "software", "Skia raster / SDL software", "CPU");
        }

        /// <inheritdoc />
        public override bool AcquireFrame()
        {
            if (!GetDrawableSize(out int width, out int height))
            {
                return false;
            }
            if (!HasFrame || width != Width || height != Height)
            {
                Resize();
            }
            return HasFrame;
        }

        /// <inheritdoc />
        public override void Resize()
        {
            if (!GetDrawableSize(out int width, out int height))
            {
                return;
            }
            ClearSurface();
            ReleasePixels();
            Width = width;
            Height = height;
            pixels = new SKBitmap(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul));
            SetRasterSurface(SKSurface.Create(pixels.Info, pixels.GetPixels(), pixels.RowBytes));
            // Supported desktop targets are little-endian; ABGR8888 stores RGBA bytes there.
            texture = SDL.CreateTexture(renderer, SDL.PixelFormat.ABGR8888, SDL.TextureAccess.Streaming, width, height);
            if (texture == 0)
            {
                throw new InvalidOperationException(SDL.GetError());
            }
            Check(SDL.SetTextureBlendMode(texture, SDL.BlendMode.None));
        }

        /// <inheritdoc />
        public override void Present()
        {
            CheckThread();
            Check(SDL.UpdateTexture(texture, 0, pixels.GetPixels(), pixels.RowBytes));
            Check(SDL.RenderTexture(renderer, texture, 0, 0));
            Check(SDL.RenderPresent(renderer));
        }

        private void ReleasePixels()
        {
            if (texture != 0)
            {
                SDL.DestroyTexture(texture);
                texture = 0;
            }
            pixels?.Dispose();
            pixels = null;
        }
    }
}
