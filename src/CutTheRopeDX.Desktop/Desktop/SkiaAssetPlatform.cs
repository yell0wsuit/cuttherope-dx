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
    internal sealed class SkiaAssetPlatform(IContentStore content, GRContext context) : IAssetPlatform, IDisposable
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

            SkiaTexture texture = new(image);
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

        public void FreeImage(string contentPath)
        {
            if (textures.Remove(contentPath, out SkiaTexture texture))
            {
                texture.Dispose();
            }
        }
        public FontGeneric Font(string resourceName)
        {
            return SkiaFontCache.Load(
                Resources.FontConfig.GetConfiguration(resourceName, LanguageHelper.CurrentAsInt),
                SkiaFontCache.PathForConfiguredFile);
        }

        public void ClearFontCache()
        {
            SkiaFontCache.Clear();
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
