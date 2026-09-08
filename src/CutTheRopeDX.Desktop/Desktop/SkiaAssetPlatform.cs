using System;
using System.Collections.Generic;
using System.IO;

using CutTheRopeDX.Framework;
using CutTheRopeDX.Framework.Platform;
using CutTheRopeDX.Framework.Visual;
using CutTheRopeDX.GameMain;
using CutTheRopeDX.Rendering.Skia;

using SkiaSharp;

namespace CutTheRopeDX.Desktop
{
    /// <summary>PNG assets owned by the SDL renderer and its context lifetime.</summary>
    /// <param name="content">Raw content byte access.</param>
    /// <param name="context">The context that uploads the images, or null to keep them on the CPU.</param>
    /// <param name="registry">
    /// Records which device generation each upload belongs to, so recovery knows what to load
    /// again. Null where the device is never replaced.
    /// </param>
    internal sealed class SkiaAssetPlatform(
        IContentStore content, GRContext context, SkiaResourceRegistry registry = null)
        : IAssetPlatform, IDisposable
    {
        private readonly Dictionary<string, SkiaTexture> textures = [];
        private bool disposed;

        internal static string ResolveContentRoot(string executableDirectory)
        {
            DirectoryInfo directory = new(Path.GetFullPath(executableDirectory));
            for (DirectoryInfo current = directory; current != null; current = current.Parent)
            {
                if (current.Name == "Contents" && current.Parent?.Name.EndsWith(".app", StringComparison.OrdinalIgnoreCase) == true)
                {
                    return Path.Combine(current.FullName, "Resources", "content");
                }
            }
            return Path.Combine(directory.FullName, "content");
        }

        private SkiaTexture Load(string path)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (textures.TryGetValue(path, out SkiaTexture cached))
            {
                return cached;
            }

            byte[] bytes;
            try { bytes = content.Read(path + ".png"); }
            catch (FileNotFoundException) { return null; }
            catch (DirectoryNotFoundException) { return null; }
            using SKData data = SKData.CreateCopy(bytes);
            SKImage decoded = SKImage.FromEncodedData(data)
                ?? throw new InvalidDataException($"Could not decode PNG '{path}'.");
            SKImage image;
            try
            {
                image = (context == null ? decoded.ToRasterImage() : decoded.ToTextureImage(context)) ?? throw new InvalidOperationException($"Could not upload PNG '{path}'.");
            }
            catch
            {
                decoded.Dispose();
                throw;
            }

            // Both conversions hand back the source image itself when it already satisfies the
            // request, and SkiaSharp maps one native handle to one managed instance. Disposing the
            // decoded image unconditionally would therefore free the image the texture now owns.
            if (!ReferenceEquals(image, decoded))
            {
                decoded.Dispose();
            }

            SkiaTexture texture = new(
                image, registry?.TrackDurable(path) ?? SkiaResourceRegistry.DeviceIndependent);
            textures.Add(path, texture);
            return texture;
        }

        public (int W, int H)? ImageDimensions(string contentPath)
        {
            SkiaTexture texture = Load(contentPath);
            return texture == null ? null : (texture.Width, texture.Height);
        }
        public ITextureHandle ImageTexture(string contentPath)
        {
            return Load(contentPath);
        }

        public ITextureHandle TintedRegion(
            ITextureHandle source,
            int x,
            int y,
            int width,
            int height,
            RGBAColor tint)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (source is not SkiaTexture texture)
            {
                return null;
            }

            // Skia's public colors are straight, so the region is read back premultiplied here to
            // give the shared tint the pixels it expects.
            SKImageInfo info = new(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
            using SKBitmap region = new(info);
            if (!texture.Image.ReadPixels(info, region.GetPixels(), region.RowBytes, x, y))
            {
                return null;
            }

            byte[] pixels = region.Bytes;
            PremultipliedTint.Apply(pixels, tint);
            SKImage raster = SKImage.FromPixelCopy(info, pixels);
            if (context == null)
            {
                return new SkiaTexture(raster);
            }

            SKImage uploaded;
            try
            {
                uploaded = raster.ToTextureImage(context)
                    ?? throw new InvalidOperationException("Could not upload a tinted region.");
            }
            catch
            {
                raster.Dispose();
                throw;
            }

            // As in Load: the conversion hands back the source image when it already satisfies the
            // request, and disposing it then would free the image the texture now owns.
            if (!ReferenceEquals(uploaded, raster))
            {
                raster.Dispose();
            }

            // The copy is built from a level's atlas rather than a content path, so a lost device
            // drops it instead of loading it again.
            return new SkiaTexture(
                uploaded, registry?.TrackTransient() ?? SkiaResourceRegistry.DeviceIndependent);
        }

        public void FreeImage(string contentPath)
        {
            if (textures.Remove(contentPath, out SkiaTexture texture))
            {
                registry?.Forget(contentPath);
                texture.Dispose();
            }
        }
        public FontGeneric Font(string resourceName)
        {
            return SkiaFontCache.Load(Resources.FontConfig.GetConfiguration(resourceName, LanguageHelper.CurrentAsInt));
        }

        public void ClearFontCache()
        {
            SkiaFontCache.Clear();
        }

        /// <summary>
        /// Releases every uploaded image while the context that owns them is still alive.
        /// </summary>
        /// <remarks>
        /// Fonts are deliberately left alone. Typefaces, metrics and paints are all CPU-side, so
        /// they outlive any device; only Skia's own glyph atlas is device-resident, and the
        /// replacement context rebuilds that itself the first time text is drawn.
        /// </remarks>
        internal void DiscardDeviceResources()
        {
            foreach (SkiaTexture texture in textures.Values)
            {
                texture.Dispose();
            }

            textures.Clear();
        }

        /// <summary>Uploads subsequent images through a replacement device.</summary>
        /// <param name="replacement">The new context, or null to keep images on the CPU.</param>
        /// <remarks>
        /// Nothing is loaded here. Which assets are actually needed is known to the textures that
        /// reference them, not to this cache, so the reload is driven from there and only touches
        /// what the running game still holds.
        /// </remarks>
        internal void Rebind(GRContext replacement)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            DiscardDeviceResources();
            context = replacement;
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            foreach (SkiaTexture texture in textures.Values)
            {
                texture.Dispose();
            }

            textures.Clear();
            ClearFontCache();
        }
    }
}
