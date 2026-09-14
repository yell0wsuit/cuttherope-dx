using System;
using System.Collections.Generic;

using CutTheRopeDX.Framework;
using CutTheRopeDX.Framework.Platform;
using CutTheRopeDX.Framework.Visual;
using CutTheRopeDX.GameMain;
using CutTheRopeDX.Rendering.Skia;

using SkiaSharp;

namespace CutTheRopeDX.Browser
{
    /// <summary>Asset loading backed by the browser content store and Skia decoding.</summary>
    /// <param name="surface">Supplies the GPU context textures are uploaded into.</param>
    internal sealed class BrowserAssetPlatform(SkiaSurface surface) : IAssetPlatform, IDisposable
    {
        /// <summary>Extension the web content pipeline writes images as.</summary>
        public const string ImageExtension = ".webp";

        private readonly Dictionary<string, SkiaTexture> _textures = [];

        private readonly SkiaImageDecodeQueue _decodes = new(
            path => SkiaImageDecodeQueue.DecodeRaster(PlatformServices.Content.Read(path + ImageExtension)),
            SkiaImageDecodeQueue.DefaultConcurrency);

        private SkiaTexture Load(string contentPath)
        {
            if (_textures.TryGetValue(contentPath, out SkiaTexture cached))
            {
                return cached;
            }

            // A prepared image arrives already decoded, off the game thread. One that was not, or
            // whose background decode failed, is decoded here as it always was.
            SKImage decoded = _decodes.Take(contentPath);
            if (decoded is null)
            {
                byte[] encoded = PlatformServices.Content.Read(contentPath + ImageExtension);
                using SKData data = SKData.CreateCopy(encoded);
                decoded = SKImage.FromEncodedData(data);
                if (decoded is null)
                {
                    return null;
                }
            }

            SKImage image;
            try
            {
                image = decoded.ToTextureImage(surface.Context)
                    ?? throw new InvalidOperationException($"Could not upload image '{contentPath}'.");
            }
            catch
            {
                decoded.Dispose();
                throw;
            }

            // The same two guards the desktop loader carries, for the same reasons: an upload that
            // hands back nothing would otherwise make a texture whose every measurement throws, and
            // the conversion returns the source itself when it already satisfies the request, where
            // SkiaSharp maps one native handle to one managed instance - so disposing the decoded
            // image unconditionally would free the one the texture now owns.
            if (!ReferenceEquals(image, decoded))
            {
                decoded.Dispose();
            }

            SkiaTexture texture = new(image);
            _textures[contentPath] = texture;
            return texture;
        }

        /// <inheritdoc />
        public (int W, int H)? ImageDimensions(string contentPath)
        {
            SkiaTexture texture = Load(contentPath);
            return texture is null ? null : (texture.Width, texture.Height);
        }

        /// <inheritdoc />
        public ITextureHandle ImageTexture(string contentPath)
        {
            return Load(contentPath);
        }

        /// <inheritdoc />
        public ITextureHandle TintedRegion(
            ITextureHandle source,
            int x,
            int y,
            int width,
            int height,
            RGBAColor tint)
        {
            if (source is not SkiaTexture texture)
            {
                return null;
            }

            // Skia's public colors are straight, so the region is read back premultiplied here to
            // give the shared tint the same pixels the desktop backend hands it.
            SKImageInfo info = new(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
            using SKBitmap region = new(info);
            if (!texture.Image.ReadPixels(info, region.GetPixels(), region.RowBytes, x, y))
            {
                return null;
            }

            byte[] pixels = region.Bytes;
            PremultipliedTint.Apply(pixels, tint);
            using SKImage tinted = SKImage.FromPixelCopy(info, pixels);
            return new SkiaTexture(tinted.ToTextureImage(surface.Context));
        }

        /// <inheritdoc />
        public void PrepareImage(string contentPath)
        {
            if (!_textures.ContainsKey(contentPath))
            {
                _decodes.Prepare(contentPath);
            }
        }

        /// <inheritdoc />
        public bool IsImageReady(string contentPath)
        {
            return _textures.ContainsKey(contentPath) || _decodes.IsReady(contentPath);
        }

        /// <inheritdoc />
        public void DiscardPreparedImage(string contentPath)
        {
            _decodes.Discard(contentPath);
        }

        /// <inheritdoc />
        public long TakePeakPreparedPixelBytes()
        {
            return _decodes.TakePeakPreparedPixelBytes();
        }

        /// <inheritdoc />
        public void FreeImage(string contentPath)
        {
            _decodes.Discard(contentPath);
            if (_textures.Remove(contentPath, out SkiaTexture texture))
            {
                texture.Dispose();
            }
        }

        /// <inheritdoc />
        public FontGeneric Font(string resourceName)
        {
            FontConfiguration config = Resources.FontConfig.GetConfiguration(
                resourceName, LanguageHelper.CurrentAsInt);
            return SkiaFontCache.Load(config);
        }

        /// <inheritdoc />
        public void ClearFontCache()
        {
            SkiaFontCache.Clear();
        }

        /// <summary>Releases pending background decodes and every loaded texture.</summary>
        public void Dispose()
        {
            _decodes.Dispose();
            foreach (SkiaTexture texture in _textures.Values)
            {
                texture.Dispose();
            }

            _textures.Clear();
        }
    }
}
