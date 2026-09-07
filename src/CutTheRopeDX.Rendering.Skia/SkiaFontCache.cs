using System;
using System.Collections.Generic;
using System.IO;

using CutTheRopeDX.Framework.Platform;
using CutTheRopeDX.Framework.Visual;
using CutTheRopeDX.GameMain;

using SkiaSharp;

namespace CutTheRopeDX.Rendering.Skia
{
    /// <summary>Loads and caches the subset typefaces the game renders text with.</summary>
    internal static class SkiaFontCache
    {
        private static readonly Dictionary<string, SKTypeface> Typefaces = [];
        private static readonly Dictionary<string, SkiaFont> Fonts = [];

        /// <summary>
        /// Content-relative path of a font file exactly as the configuration names it. Hosts that
        /// ship the authored files use this, so a configuration naming an OpenType face resolves
        /// to that face rather than to a TrueType sibling that was never produced.
        /// </summary>
        /// <param name="fontFile">Font file name from <see cref="FontConfiguration.FontFile"/>.</param>
        public static string PathForConfiguredFile(string fontFile)
        {
            return $"fonts/{fontFile}";
        }

        /// <summary>
        /// Content-relative path of the TrueType file the web content pipeline converts every
        /// authored face into, whatever the configuration calls it.
        /// </summary>
        /// <param name="fontFile">Font file name from <see cref="FontConfiguration.FontFile"/>.</param>
        public static string PathForConvertedFile(string fontFile)
        {
            return $"fonts/{Path.GetFileNameWithoutExtension(fontFile)}.ttf";
        }

        /// <summary>Returns the font for a configuration, building it on first use.</summary>
        /// <param name="config">Resolved font configuration for the current language.</param>
        /// <param name="pathFor">
        /// Maps the configured font file name to its content-relative path. Required rather than
        /// defaulted: the two hosts ship different files, and a default here silently gave the
        /// browser's answer to the desktop.
        /// </param>
        public static FontGeneric Load(FontConfiguration config, Func<string, string> pathFor)
        {
            ArgumentNullException.ThrowIfNull(pathFor);

            string key = $"{config.FontFile}|{config.Size}|{config.LineSpacing}|{config.TopSpacing}";
            if (Fonts.TryGetValue(key, out SkiaFont cached))
            {
                // Rebuild the font if the cached instance was disposed by FreePack/FreeResource.
                if (cached.IsAlive)
                {
                    return cached;
                }

                _ = Fonts.Remove(key);
            }

            SkiaFont font = new(GetTypeface(pathFor(config.FontFile)), config);
            Fonts[key] = font;
            return font;
        }

        /// <summary>Drops every cached font and typeface.</summary>
        public static void Clear()
        {
            foreach (SkiaFont font in Fonts.Values)
            {
                font.Dispose();
            }
            Fonts.Clear();

            foreach (SKTypeface typeface in Typefaces.Values)
            {
                typeface.Dispose();
            }
            Typefaces.Clear();
        }

        private static SKTypeface GetTypeface(string contentPath)
        {
            if (Typefaces.TryGetValue(contentPath, out SKTypeface cached))
            {
                return cached;
            }

            byte[] bytes = PlatformServices.Content.Read(contentPath);
            using SKData data = SKData.CreateCopy(bytes);
            SKTypeface typeface = SKTypeface.FromData(data)
                ?? throw new InvalidOperationException($"Skia could not decode font '{contentPath}'.");
            Typefaces[contentPath] = typeface;
            return typeface;
        }
    }
}
