using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Xml.Linq;

using CutTheRopeDX.Framework.Diagnostics;
using CutTheRopeDX.Framework.Helpers;
using CutTheRopeDX.Framework.Platform;
using CutTheRopeDX.Framework.Visual;
using CutTheRopeDX.GameMain;
using CutTheRopeDX.Helpers;

using Microsoft.Extensions.Logging;

using static CutTheRopeDX.Helpers.ParsingHelpers;

namespace CutTheRopeDX.Framework.Core
{
    /// <summary>
    /// Loads, caches, frees, and background-prefetches resources such as textures, fonts, and sounds,
    /// resolving localized variants for the active language.
    /// </summary>
    internal class ResourceMgr : FrameworkTypes
    {
        /// <summary>
        /// Adjusts a resource name for the active language when localized variants exist.
        /// </summary>
        /// <param name="resourceName">The string name of the resource to look up.</param>
        /// <returns>The localized resource name variant, or <paramref name="resourceName"/> unchanged if no variant exists.</returns>
        public static string HandleLocalizedResource(string resourceName)
        {
            return string.IsNullOrEmpty(resourceName)
                ? resourceName
                : resourceName switch
                {
                    _ when resourceName == Resources.Img.MenuExtraButtonsEn => LanguageHelper.Current switch
                    {
                        Language.LANGEN => Resources.Img.MenuExtraButtonsEn,
                        Language.LANGRU => Resources.Img.MenuExtraButtonsRu,
                        Language.LANGDE => Resources.Img.MenuExtraButtonsGr,
                        Language.LANGFR => Resources.Img.MenuExtraButtonsFr,
                        Language.LANGES => Resources.Img.MenuExtraButtonsEn,
                        Language.LANGIT => Resources.Img.MenuExtraButtonsEn,
                        Language.LANGNL => Resources.Img.MenuExtraButtonsEn,
                        Language.LANGPTBR => Resources.Img.MenuExtraButtonsEn,
                        Language.LANGKO => Resources.Img.MenuExtraButtonsEn,
                        Language.LANGJA => Resources.Img.MenuExtraButtonsEn,
                        Language.LANGZH => Resources.Img.MenuExtraButtonsEn,
                        Language.LANGZHTW => Resources.Img.MenuExtraButtonsEn,
                        _ => Resources.Img.MenuExtraButtonsEn,
                    },
                    _ => resourceName,
                };
        }

        /// <summary>
        /// Resolves a localized XNA resource name for a string resource name.
        /// </summary>
        /// <param name="resourceName">The string name of the resource.</param>
        /// <returns>The localized resource name.</returns>
        public static string XNA_ResName(string resourceName)
        {
            return HandleLocalizedResource(resourceName);
        }

        /// <summary>
        /// Returns the texture quad index for the localized result stamp overlay.
        /// </summary>
        /// <returns>Quad index for the current language's result stamp.</returns>
        public static int GetResultStampQuad()
        {
            return LanguageHelper.Current switch
            {
                Language.LANGEN => 17,
                Language.LANGFR => 18,
                Language.LANGDE => 19,
                Language.LANGRU => 20,
                Language.LANGPTBR => 21,
                Language.LANGES => 22,
                Language.LANGIT => 23,
                Language.LANGJA => 24,
                Language.LANGKO => 25,
                Language.LANGNL => 26,
                Language.LANGZH => 27,
                Language.LANGZHTW => 27, // no zh_tw stamp, use English
                _ => 17,
            };
        }

        /// <summary>
        /// Returns the texture quad offset for the localized HUD button sprite.
        /// </summary>
        /// <returns>Quad offset for the current language's HUD button.</returns>
        public static int GetHudButtonQuadOffset()
        {
            return LanguageHelper.Current switch
            {
                Language.LANGEN => 12,
                Language.LANGDE => 13,
                Language.LANGRU => 14,
                Language.LANGJA => 15,
                Language.LANGKO => 16,
                Language.LANGZH => 17,
                Language.LANGZHTW => 17, // use zh
                Language.LANGES => 18,
                Language.LANGFR => 12,
                Language.LANGIT => 12,
                Language.LANGNL => 12,
                Language.LANGPTBR => 12,
                _ => 12, // en fallback
            };
        }

        /// <summary>
        /// Loads a resource by its string name. Auto-assigns an ID if needed.
        /// </summary>
        /// <param name="resourceName">The string name of the resource to load.</param>
        /// <param name="resType">The type of resource to load (image, sound, etc.).</param>
        /// <returns>The loaded resource object.</returns>
        public static object LoadResourceByName(string resourceName, ResourceType resType)
        {
            ResourceMgr mgr = new();
            return mgr.LoadResource(resourceName, resType);
        }

        /// <summary>
        /// Adds a resource to the load queue by resource name.
        /// </summary>
        /// <param name="resourceName">Logical resource name to enqueue.</param>
        public void AddResourceToLoadQueue(string resourceName)
        {
            if (TryResolveResource(resourceName, out string localizedName))
            {
                loadQueue.Add(localizedName);
                loadCount++;

                // Decoding starts while the resources ahead of this one are still loading, so that
                // by the time the queue reaches it only the upload is left.
                PrepareDecodeWindow();
            }
        }

        /// <summary>
        /// Starts decoding an image resource in the background, ahead of loading it and apart from
        /// the load queue. The decode survives <see cref="InitLoading"/> until
        /// <see cref="DiscardPreparedImageResource"/> drops it. Does nothing for a resource that is
        /// not an image or is already loaded.
        /// </summary>
        /// <param name="resourceName">Logical resource name.</param>
        public void PrepareImageResource(string resourceName)
        {
            if (TryResolveResource(resourceName, out string localizedName) && IsPendingImage(localizedName))
            {
                _ = heldPreparations.Add(localizedName);
                AssetPlatform.Current.PrepareImage(ImageContentPath(localizedName));
            }
        }

        /// <summary>
        /// Drops a background decode started by <see cref="PrepareImageResource"/> that is no
        /// longer wanted. Never frees a loaded resource.
        /// </summary>
        /// <param name="resourceName">Logical resource name.</param>
        public void DiscardPreparedImageResource(string resourceName)
        {
            if (TryResolveResource(resourceName, out string localizedName) && IsImageResource(localizedName))
            {
                _ = heldPreparations.Remove(localizedName);
                AssetPlatform.Current.DiscardPreparedImage(ImageContentPath(localizedName));
            }
        }

        /// <summary>
        /// Starts decoding the first <see cref="DecodeAheadImages"/> images in the load queue that
        /// are not loaded yet.
        /// </summary>
        /// <remarks>
        /// Each image in the window is either still decoding or decoded and waiting for its upload,
        /// and the queue is loaded in order, so the window bounds how many decoded images a batch
        /// holds at once. It moves along as images load.
        /// </remarks>
        private void PrepareDecodeWindow()
        {
            int inWindow = 0;
            foreach (string name in loadQueue)
            {
                if (inWindow == DecodeAheadImages)
                {
                    break;
                }

                if (IsPendingImage(name))
                {
                    AssetPlatform.Current.PrepareImage(ImageContentPath(name));
                    inWindow++;
                }
            }
        }

        /// <summary>
        /// Drops the decodes of the load queue's window, except those held by
        /// <see cref="PrepareImageResource"/>, for a batch that is being abandoned.
        /// </summary>
        private void DiscardDecodeWindow()
        {
            int inWindow = 0;
            foreach (string name in loadQueue)
            {
                if (inWindow == DecodeAheadImages)
                {
                    break;
                }

                if (IsPendingImage(name))
                {
                    inWindow++;
                    if (!heldPreparations.Contains(name))
                    {
                        AssetPlatform.Current.DiscardPreparedImage(ImageContentPath(name));
                    }
                }
            }
        }

        /// <summary>Whether a resolved resource name is an image that is not loaded yet.</summary>
        /// <param name="localizedName">Resolved resource name.</param>
        /// <returns><see langword="true"/> for an image still to be loaded.</returns>
        private bool IsPendingImage(string localizedName)
        {
            return IsImageResource(localizedName) && !s_Resources.ContainsKey(localizedName);
        }

        /// <summary>Content path an image resource is loaded from.</summary>
        /// <param name="resourceName">Resolved image resource name.</param>
        /// <returns>The content-relative path of the image.</returns>
        internal static string ImageContentPath(string resourceName)
        {
            return Resources.IsBackgroundImg(resourceName)
                ? ContentPaths.GetBackgroundImageContentPath(resourceName)
                : ContentPaths.GetImageContentPath(resourceName);
        }

        /// <summary>Whether a resolved resource name is loaded as a texture.</summary>
        /// <param name="localizedName">Resolved resource name.</param>
        /// <returns><see langword="true"/> for an image resource.</returns>
        private static bool IsImageResource(string localizedName)
        {
            return localizedName != Resources.Str.MenuStrings
                && !Resources.IsSound(localizedName)
                && !Resources.IsFont(localizedName);
        }

        /// <summary>
        /// Whether loading a queued resource now would not wait on a background decode.
        /// </summary>
        /// <param name="localizedName">Resolved resource name.</param>
        /// <returns><see langword="true"/> when the resource can load without blocking on a decode.</returns>
        private bool IsResourceReady(string localizedName)
        {
            return !IsImageResource(localizedName)
                || s_Resources.ContainsKey(localizedName)
                || AssetPlatform.Current.IsImageReady(ImageContentPath(localizedName));
        }

        /// <summary>
        /// Removes cached font resources and clears the shared font cache.
        /// </summary>
        public void ClearCachedFonts()
        {
            List<string> fontKeys = [];
            foreach (KeyValuePair<string, object> kvp in s_Resources)
            {
                if (kvp.Value is FontGeneric)
                {
                    fontKeys.Add(kvp.Key);
                }
            }
            foreach (string key in fontKeys)
            {
                _ = s_Resources.Remove(key);
            }
            AssetPlatform.Current.ClearFontCache();
        }

        /// <summary>
        /// Loads a resource using its string identifier while preserving caching semantics.
        /// </summary>
        /// <param name="resourceName">Logical resource name to load.</param>
        /// <param name="resType">Expected resource type.</param>
        /// <returns>Loaded resource instance, or <see langword="null" /> when the name cannot be resolved.</returns>
        public virtual object LoadResource(string resourceName, ResourceType resType)
        {
            return !TryResolveResource(resourceName, out string localizedName)
                ? null
                : LoadResourceInternal(localizedName, resType);
        }

        /// <summary>
        /// Loads or retrieves a cached resource using a resolved resource name.
        /// </summary>
        /// <param name="resourceName">Resolved resource name.</param>
        /// <param name="resType">Expected resource type.</param>
        /// <returns>Loaded resource instance, or <see langword="null" /> on failure.</returns>
        private object LoadResourceInternal(string resourceName, ResourceType resType)
        {
            if (s_Resources.TryGetValue(resourceName, out object value))
            {
                return value;
            }

            string path = XNA_ResName(resourceName);
            float scaleX = GetNormalScaleX(resourceName);
            float scaleY = GetNormalScaleY(resourceName);
            switch (resType)
            {
                case ResourceType.IMAGE:
                    value = LoadTextureImageInfo(resourceName, path, null, false, scaleX, scaleY);
                    break;
                case ResourceType.FONT:
                    value = LoadVariableFontInfo(path, resourceName, false);
                    _ = s_Resources.Remove(resourceName);
                    break;
                case ResourceType.SOUND:
                    value = LoadSoundInfo(path);
                    break;
                case ResourceType.BINARY:
                    break;
                case ResourceType.ELEMENT:
                    break;
                default:
                    break;
            }
            if (value != null)
            {
                s_Resources[resourceName] = value;
            }
            return value;
        }

        /// <summary>
        /// Resolves a logical resource name to its localized runtime resource name and validates it.
        /// </summary>
        /// <param name="resourceName">Logical resource name.</param>
        /// <param name="localizedName">Resolved localized resource name when successful.</param>
        /// <returns><see langword="true" /> when the resource name is valid and resolved; otherwise <see langword="false" />.</returns>
        private static bool TryResolveResource(string resourceName, out string localizedName)
        {
            localizedName = string.IsNullOrEmpty(resourceName)
                ? resourceName
                : HandleLocalizedResource(resourceName);

            return !string.IsNullOrEmpty(localizedName) && Resources.IsValidResourceName(localizedName);
        }

        /// <summary>
        /// Loads sound metadata for the specified content <paramref name="path"/>.
        /// The base implementation returns a placeholder object.
        /// </summary>
        /// <param name="path">Resolved content path.</param>
        /// <returns>Loaded sound metadata object.</returns>
        public virtual FrameworkTypes LoadSoundInfo(string path)
        {
            return new FrameworkTypes();
        }

        /// <summary>
        /// Loads a variable font resource using the configured font system.
        /// </summary>
        /// <param name="path">Resolved content path.</param>
        /// <param name="resourceName">Logical font resource name.</param>
        /// <param name="isWvga">Whether WVGA scaling rules should be applied.</param>
        /// <returns>Loaded font resource.</returns>
        public virtual FontGeneric LoadVariableFontInfo(string path, string resourceName, bool isWvga)
        {
            // Check if user prefers old font system for supported languages (en, de, fr, ru)
            // Disabled because new quad system doesn't support old sprite fonts well
            bool preferOldFontSystem = false;
            bool isLanguageSupported = LanguageHelper.IsCurrentAny(
                Language.LANGEN,
                Language.LANGDE,
                Language.LANGFR,
                Language.LANGRU
            );

            if (preferOldFontSystem && isLanguageSupported)
            {
                // Use old sprite-based font system
                return LoadSpriteFontInfo(path, resourceName);
            }

            if (string.IsNullOrEmpty(resourceName))
            {
                // Fallback to old sprite font loading if no resource name found
                return LoadSpriteFontInfo(path, resourceName);
            }

            // Font loading goes through the asset platform so headless runs can supply a stub.
            return AssetPlatform.Current.Font(resourceName);
        }

        /// <summary>
        /// Legacy sprite font loading (kept for backward compatibility).
        /// </summary>
        /// <param name="path">Resolved XML font definition path.</param>
        /// <param name="resourceName">Logical texture resource name that backs the sprite font.</param>
        /// <returns>Loaded sprite-font instance.</returns>
        private static Font LoadSpriteFontInfo(string path, string resourceName)
        {
            XElement xmlNode = ContentPaths.LoadXml(path);
            int charOffset = ParseIntOrZero(xmlNode.Attribute("charoff")?.Value);
            int lineOffset = ParseIntOrZero(xmlNode.Attribute("lineoff")?.Value);
            int spaceWidth = ParseIntOrZero(xmlNode.Attribute("space")?.Value);
            XElement charsNode = xmlNode.Elements().FirstOrDefault(e => e.Name.LocalName == "chars");
            XElement kerningNode = xmlNode.Elements().FirstOrDefault(e => e.Name.LocalName == "kerning");
            string charsData = charsNode.Value;
            if (kerningNode != null)
            {
                _ = kerningNode.Value;
            }
            Font font = new Font().InitWithVariableSizeCharscharMapFileKerning(charsData, Application.GetTexture(resourceName));
            font.SetCharOffsetLineOffsetSpaceWidth(charOffset, lineOffset, spaceWidth);
            return font;
        }

        /// <summary>
        /// Loads a texture resource together with optional atlas/quad metadata.
        /// </summary>
        /// <param name="resourceName">Logical texture resource name.</param>
        /// <param name="path">Resolved content path.</param>
        /// <param name="i">Legacy XML metadata node, unused by the current implementation.</param>
        /// <param name="isWvga">Whether WVGA scaling rules should be applied.</param>
        /// <param name="scaleX">Horizontal texture scale.</param>
        /// <param name="scaleY">Vertical texture scale.</param>
        /// <returns>Loaded texture resource.</returns>
        public virtual Texture2D LoadTextureImageInfo(string resourceName, string path, XElement i, bool isWvga, float scaleX, float scaleY)
        {
            TextureAtlasConfig atlasConfig = GetTextureAtlasConfig(resourceName);
            float aspectRatioScaleX = GetAspectRatioScaleX();
            (scaleX, scaleY) = ResolveTextureScales(
                scaleX,
                scaleY,
                atlasConfig?.ScaleRes,
                aspectRatioScaleX);

            ParsedTexturePackerAtlas parsedAtlas = LoadTexturePackerAtlas(atlasConfig, resourceName);

            bool useAntialias = atlasConfig?.UseAntialias ?? true;
            string pngPath = ImageContentPath(resourceName);
            if (useAntialias)
            {
                Texture2D.SetAntiAliasTexParameters();
            }
            else
            {
                Texture2D.SetAliasTexParameters();
            }

            Texture2D texture2D = new Texture2D().InitWithPath(pngPath) ?? throw new FileNotFoundException(
                    $"Resource '{resourceName}' is missing the PNG. Did you forget to add {resourceName}.png?",
                    pngPath);

            if (isWvga)
            {
                texture2D.SetWvga();
            }

            texture2D.SetScale(scaleX, scaleY);

            ApplyTexturePackerInfo(texture2D, parsedAtlas, isWvga, scaleX, scaleY);

            return texture2D;
        }

        /// <summary>
        /// Adjusts texture scales according to atlas configuration and aspect-ratio rules.
        /// </summary>
        /// <param name="defaultScaleX">Default horizontal scale.</param>
        /// <param name="defaultScaleY">Default vertical scale.</param>
        /// <param name="scaleRes">Optional atlas scale mode.</param>
        /// <param name="aspectRatioScaleX">Aspect-ratio X scale used by legacy assets.</param>
        /// <returns>Resolved texture scales.</returns>
        private static (float scaleX, float scaleY) ResolveTextureScales(
            float defaultScaleX,
            float defaultScaleY,
            int? scaleRes,
            float aspectRatioScaleX)
        {
            if (scaleRes == 0 && aspectRatioScaleX > 0f)
            {
                // iOS legacy behavior: scaleRes=0 applies aspect-ratio scaling on X only.
                return (aspectRatioScaleX, 1f);
            }

            return (defaultScaleX, defaultScaleY);
        }

        /// <summary>
        /// Returns the aspect-ratio compensation scale applied to the X axis.
        /// </summary>
        /// <returns>Aspect-ratio scale factor.</returns>
        private static float GetAspectRatioScaleX()
        {
            int width = ScreenPresentation.Instance.SurfaceWidth;
            int height = ScreenPresentation.Instance.SurfaceHeight;
            if (width <= 0 || height <= 0)
            {
                return 1f;
            }

            // iOS ScreenSizeMgr derives ASPECT_RATIO from logical 640x960 fit scale.
            // This mirrors min(scaleX, scaleY) against that logical reference size.
            float scaleByWidth = width / 640f;
            float scaleByHeight = height / 960f;
            float scale = MathF.Min(scaleByWidth, scaleByHeight);
            return scale > 0f ? scale : 1f;
        }

        /// <summary>
        /// Returns atlas configuration for the specified texture resource, if any.
        /// </summary>
        /// <param name="resourceName">Logical texture resource name.</param>
        /// <returns>Atlas configuration, or <see langword="null" /> for background images, whose dimensions come from the texture itself.</returns>
        private static TextureAtlasConfig GetTextureAtlasConfig(string resourceName)
        {
            // Background images don't need JSON atlas - dimensions auto-detected from texture
            if (Resources.IsBackgroundImg(resourceName))
            {
                return null;
            }

            // Convention-based: all textures use JSON+PNG pairs in images folder
            return new TextureAtlasConfig
            {
                AtlasPath = ContentPaths.GetImagePath(resourceName, ".json"),
                ResourceName = resourceName,
                UseAntialias = true,
                CenterOffsets = false,
                ScaleRes = null
            };
        }

        /// <summary>
        /// Loads and parses TexturePacker atlas metadata for the specified resource.
        /// </summary>
        /// <param name="config">Atlas configuration for the resource.</param>
        /// <param name="resourceName">Logical texture resource name.</param>
        /// <returns>Parsed atlas metadata, or <see langword="null" /> when no atlas is configured.</returns>
        /// <exception cref="FileNotFoundException">Thrown when the expected atlas JSON file is missing.</exception>
        /// <exception cref="InvalidDataException">Thrown when the atlas JSON is empty.</exception>
        private static ParsedTexturePackerAtlas LoadTexturePackerAtlas(TextureAtlasConfig config, string resourceName)
        {
            // No atlas config means use full texture (e.g., background images)
            if (config == null)
            {
                return null;
            }

            string atlasPath = config.AtlasPath;
            if (string.IsNullOrEmpty(atlasPath))
            {
                throw new FileNotFoundException(
                    $"Resource '{resourceName}' is missing the quad JSON. Did you forget to add {resourceName}.json?",
                    resourceName + ".json");
            }

            string json;
            try
            {
                using Stream stream = ContentPaths.OpenStream(atlasPath);
                using StreamReader reader = new(stream);
                json = reader.ReadToEnd();
            }
            catch (FileNotFoundException)
            {
                throw new FileNotFoundException(
                    $"Resource '{resourceName}' is missing the quad JSON. Did you forget to add {resourceName}.json?",
                    atlasPath);
            }

            if (string.IsNullOrEmpty(json))
            {
                throw new InvalidDataException(
                    $"Resource '{resourceName}' has an empty JSON file at {atlasPath}.");
            }

            TexturePackerParserOptions options = null;
            if (config?.CenterOffsets ?? false)
            {
                options = new TexturePackerParserOptions
                {
                    NormalizeOffsetsToCenter = true
                };
            }

            return TexturePackerAtlasParser.Parse(json, options);
        }

        /// <summary>
        /// Applies parsed <paramref name="atlas"/> rectangles, offsets, and source sizes to a <paramref name="texture"/>.
        /// </summary>
        /// <param name="texture">Texture to update.</param>
        /// <param name="atlas">Parsed atlas metadata.</param>
        /// <param name="isWvga">Whether WVGA scaling rules should be applied.</param>
        /// <param name="scaleX">Horizontal texture scale.</param>
        /// <param name="scaleY">Vertical texture scale.</param>
        private static void ApplyTexturePackerInfo(Texture2D texture, ParsedTexturePackerAtlas atlas, bool isWvga, float scaleX, float scaleY)
        {
            texture.preCutSize = vectUndefined;
            if (atlas == null || atlas.Rects.Count == 0)
            {
                return;
            }

            float[] quadData = new float[atlas.Rects.Count * 4];
            for (int i = 0; i < atlas.Rects.Count; i++)
            {
                Rectangle rect = atlas.Rects[i];
                int index = i * 4;
                quadData[index] = rect.x;
                quadData[index + 1] = rect.y;
                quadData[index + 2] = rect.w;
                quadData[index + 3] = rect.h;
            }
            SetQuadsInfo(texture, quadData, scaleX, scaleY);

            if (atlas.Offsets.Count == atlas.Rects.Count && atlas.HasNonZeroOffset)
            {
                float[] offsetData = new float[atlas.Offsets.Count * 2];
                for (int j = 0; j < atlas.Offsets.Count; j++)
                {
                    int offsetIndex = j * 2;
                    offsetData[offsetIndex] = atlas.Offsets[j].X;
                    offsetData[offsetIndex + 1] = atlas.Offsets[j].Y;
                }
                SetOffsetsInfo(texture, offsetData, offsetData.Length, scaleX, scaleY);
            }

            if (atlas.PreCutWidth > 0f && atlas.PreCutHeight > 0f)
            {
                texture.preCutSize = Vect(atlas.PreCutWidth, atlas.PreCutHeight);
                if (isWvga)
                {
                    texture.preCutSize.X /= 1.5f;
                    texture.preCutSize.Y /= 1.5f;
                }
            }

            if (atlas.SourceSizes.Count == atlas.Rects.Count)
            {
                texture.preCutSizes = new Vector[atlas.SourceSizes.Count];
                for (int k = 0; k < atlas.SourceSizes.Count; k++)
                {
                    Vector size = atlas.SourceSizes[k];
                    if (isWvga)
                    {
                        size.X /= 1.5f;
                        size.Y /= 1.5f;
                    }
                    texture.preCutSizes[k] = size;
                }
            }
        }

        /// <summary>
        /// Converts parsed atlas rectangle <paramref name="data"/> into engine quad information on the <paramref name="texture"/>.
        /// </summary>
        /// <param name="texture">Texture to update.</param>
        /// <param name="data">Flat quad rectangle data array.</param>
        /// <param name="scaleX">Horizontal texture scale.</param>
        /// <param name="scaleY">Vertical texture scale.</param>
        private static void SetQuadsInfo(Texture2D texture, float[] data, float scaleX, float scaleY)
        {
            int quadCount = data.Length / 4;
            texture.SetQuadsCapacity(quadCount);
            int lowestPoint = -1;
            for (int i = 0; i < quadCount; i++)
            {
                int quadDataIndex = i * 4;
                Rectangle rect = MakeRectangle(data[quadDataIndex], data[quadDataIndex + 1], data[quadDataIndex + 2], data[quadDataIndex + 3]);
                if (lowestPoint < rect.h + rect.y)
                {
                    lowestPoint = (int)Ceil(rect.h + rect.y);
                }
                rect.x /= scaleX;
                rect.y /= scaleY;
                rect.w /= scaleX;
                rect.h /= scaleY;
                texture.SetQuadAt(rect, i);
            }
            if (lowestPoint != -1)
            {
                texture._lowypoint = lowestPoint;
            }
            Texture2D.OptimizeMemory();
        }

        /// <summary>
        /// Applies parsed atlas offset <paramref name="data"/> to <paramref name="texture"/> quad offsets.
        /// </summary>
        /// <param name="texture">Texture to update.</param>
        /// <param name="data">Flat offset data array.</param>
        /// <param name="offsetDataSize">Number of float entries stored in <paramref name="data"/>.</param>
        /// <param name="scaleX">Horizontal texture scale.</param>
        /// <param name="scaleY">Vertical texture scale.</param>
        private static void SetOffsetsInfo(Texture2D texture, float[] data, int offsetDataSize, float scaleX, float scaleY)
        {
            int offsetCount = offsetDataSize / 2;
            for (int i = 0; i < offsetCount; i++)
            {
                int offsetDataIndex = i * 2;
                texture.quadOffsets[i].X = data[offsetDataIndex];
                texture.quadOffsets[i].Y = data[offsetDataIndex + 1];
                Vector[] quadOffsets = texture.quadOffsets;
                quadOffsets[i].X = quadOffsets[i].X / scaleX;
                quadOffsets[i].Y = quadOffsets[i].Y / scaleY;
            }
        }

        /// <summary>
        /// Returns the default horizontal scale used for the specified resource.
        /// </summary>
        /// <param name="resourceName">Logical resource name.</param>
        /// <returns>Horizontal scale factor.</returns>
        public virtual float GetNormalScaleX(string resourceName)
        {
            return 1f;
        }

        /// <summary>
        /// Returns the default vertical scale used for the specified resource.
        /// </summary>
        /// <param name="resourceName">Logical resource name.</param>
        /// <returns>Vertical scale factor.</returns>
        public virtual float GetNormalScaleY(string resourceName)
        {
            return 1f;
        }

        /// <summary>
        /// Resets queued loading state before building a new load batch.
        /// </summary>
        public virtual void InitLoading()
        {
            // A batch abandoned partway would otherwise leave its window's decoded images held
            // with nothing left to take them.
            DiscardDecodeWindow();
            loadQueue.Clear();
            loaded = 0;
            loadCount = 0;
            longestUpdateMs = 0;
            _ = AssetPlatform.Current.TakePeakPreparedPixelBytes();
            batchStartedTicks = Stopwatch.GetTimestamp();
        }

        /// <summary>
        /// Returns the percentage of queued resources that have completed loading.
        /// </summary>
        /// <returns>Load completion percentage from 0 to 100.</returns>
        public virtual int GetPercentLoaded()
        {
            return loadCount == 0 ? 100 : 100 * loaded / GetLoadCount();
        }

        /// <summary>
        /// Queues each resource in a <see langword="null"/>-terminated <paramref name="pack"/> array for loading.
        /// </summary>
        /// <param name="pack">Pack array of logical resource names.</param>
        public virtual void LoadPack(string[] pack)
        {
            if (pack == null)
            {
                return;
            }

            int i = 0;
            while (i < pack.Length && !string.IsNullOrEmpty(pack[i]))
            {
                AddResourceToLoadQueue(pack[i]);
                i++;
            }
        }

        /// <summary>
        /// Queues a batch of resources for opportunistic background prefetch.
        /// </summary>
        /// <param name="pack">Resource names to enqueue for silent warming.</param>
        public void QueuePrefetchPack(IEnumerable<string> pack)
        {
            if (pack == null)
            {
                return;
            }

            foreach (string resourceName in pack)
            {
                QueuePrefetchResource(resourceName);
            }
        }

        /// <summary>
        /// Queues a single resource for background prefetch if it is not already cached or queued.
        /// </summary>
        /// <param name="resourceName">The string resource identifier to enqueue.</param>
        public void QueuePrefetchResource(string resourceName)
        {
            if (!TryResolveResource(resourceName, out string localizedName))
            {
                return;
            }

            if (s_Resources.ContainsKey(localizedName) || !prefetchQueueSet.Add(localizedName))
            {
                return;
            }

            prefetchQueue.Add(localizedName);
        }

        /// <summary>
        /// Indicates whether any background prefetch work remains queued.
        /// </summary>
        /// <returns><see langword="true"/> when at least one prefetched resource is still pending.</returns>
        public bool HasPendingPrefetchResources()
        {
            return prefetchQueue.Count > 0;
        }

        /// <summary>
        /// Loads the next queued prefetch resource, if any remain.
        /// </summary>
        /// <param name="loadedResourceName">The resource name that was loaded, or <see langword="null"/> if nothing was loaded.</param>
        /// <returns><see langword="true"/> when a resource was loaded; otherwise, <see langword="false"/>.</returns>
        public bool PrefetchNextResource(out string loadedResourceName)
        {
            while (prefetchQueue.Count > 0)
            {
                string resourceName = prefetchQueue[0];
                prefetchQueue.RemoveAt(0);
                _ = prefetchQueueSet.Remove(resourceName);

                if (s_Resources.ContainsKey(resourceName))
                {
                    continue;
                }

                LoadResource(resourceName);
                loadedResourceName = resourceName;
                return true;
            }

            loadedResourceName = null;
            return false;
        }

        /// <summary>
        /// Clears all queued prefetch work without touching already-cached resources.
        /// </summary>
        public void ClearPrefetchQueue()
        {
            prefetchQueue.Clear();
            prefetchQueueSet.Clear();
        }

        /// <summary>
        /// Frees each resource in a <see langword="null"/>-terminated <paramref name="pack"/> array.
        /// </summary>
        /// <param name="pack">Pack array of logical resource names.</param>
        public virtual void FreePack(string[] pack)
        {
            if (pack == null)
            {
                return;
            }

            ReportFreed(FreePackResources(pack));
        }

        /// <summary>
        /// Frees several <see langword="null"/>-terminated packs, reported as one entry rather than
        /// one per pack.
        /// </summary>
        /// <param name="packs">Pack arrays of logical resource names; <see langword="null"/> ones are skipped.</param>
        /// <remarks>
        /// For teardowns that free a pack per box: a log entry is not free everywhere, and a line
        /// per box says nothing the total does not.
        /// </remarks>
        public void FreePacks(IEnumerable<string[]> packs)
        {
            int freed = 0;
            foreach (string[] pack in packs)
            {
                if (pack != null)
                {
                    freed += FreePackResources(pack);
                }
            }

            ReportFreed(freed);
        }

        /// <summary>Frees the resources of one pack without reporting it.</summary>
        /// <param name="pack">Pack array of logical resource names.</param>
        /// <returns>How many entries the pack held before its terminator.</returns>
        private int FreePackResources(string[] pack)
        {
            int i = 0;
            while (i < pack.Length && !string.IsNullOrEmpty(pack[i]))
            {
                FreeResource(pack[i]);
                i++;
            }

            return i;
        }

        private static void ReportFreed(int count)
        {
            ILogger logger = Log.For(LogCategories.ContentResources);
            string memory = MemoryReport.Describe();
            ResourceMgrLog.PackFreed(logger, count, memory);
        }

        /// <summary>
        /// Loads every currently queued resource immediately on the calling thread.
        /// </summary>
        public virtual void LoadImmediately()
        {
            long startedTicks = Stopwatch.GetTimestamp();
            int drained = 0;
            while (loadQueue.Count != 0)
            {
                string resourceName = loadQueue[0];
                loadQueue.RemoveAt(0);
                LoadResource(resourceName);
                PrepareDecodeWindow();
                loaded++;
                drained++;
            }

            ReportBatchComplete(drained, "immediate", Stopwatch.GetElapsedTime(startedTicks).TotalMilliseconds);
        }

        /// <summary>
        /// Starts timer-driven incremental loading when a resource delegate is available.
        /// </summary>
        public virtual void StartLoading()
        {
            if (resourcesDelegate != null)
            {
                DelayedDispatcher.DispatchFunc dispatchFunc = new(Rmgr_internalUpdate);
                Timer = TimerManager.Schedule(dispatchFunc, this, 1f / 60f);
            }
        }

        /// <summary>
        /// Returns the number of resources currently queued for loading.
        /// </summary>
        /// <returns>Queued resource count.</returns>
        private int GetLoadCount()
        {
            return loadCount;
        }

        /// <summary>
        /// Loads queued resources for one frame and notifies the delegate when the batch is complete.
        /// </summary>
        /// <remarks>
        /// A frame loads resources in order while the next one is ready and the frame's budget
        /// lasts. Images are decoded in the background once they enter the decode-ahead window, so a
        /// ready one costs only its upload and several fit in a frame. When the next image is still
        /// decoding, the frame stops there, possibly having loaded nothing, because loading it would
        /// block on the decode.
        /// </remarks>
        public void Update()
        {
            long frameStartedTicks = Stopwatch.GetTimestamp();
            while (loaded < GetLoadCount()
                && Stopwatch.GetElapsedTime(frameStartedTicks).TotalMilliseconds < FrameLoadBudgetMilliseconds)
            {
                if (loadQueue.Count > 0)
                {
                    // The window already covers the head. Preparing it here as well keeps an image
                    // that somehow was not from reading as ready, since an image nothing is
                    // decoding is ready, and then being decoded inline on this thread.
                    string resourceName = loadQueue[0];
                    if (IsPendingImage(resourceName))
                    {
                        AssetPlatform.Current.PrepareImage(ImageContentPath(resourceName));
                    }

                    if (!IsResourceReady(resourceName))
                    {
                        break;
                    }

                    loadQueue.RemoveAt(0);
                    LoadResource(resourceName);
                    PrepareDecodeWindow();
                }
                loaded++;
            }

            // Taken before the completion callback, which builds controllers that are not loading.
            longestUpdateMs = Math.Max(longestUpdateMs, Stopwatch.GetElapsedTime(frameStartedTicks).TotalMilliseconds);
            if (loaded >= GetLoadCount())
            {
                if (Timer >= 0)
                {
                    TimerManager.StopTimer(Timer);
                }
                Timer = -1;
                ReportBatchComplete(GetLoadCount(), "incremental", longestUpdateMs);
                resourcesDelegate.AllResourcesLoaded();
            }
        }

        /// <summary>
        /// Timer callback adapter that forwards timer updates to the resource manager instance.
        /// </summary>
        /// <param name="obj">Resource manager instance to update.</param>
        private static void Rmgr_internalUpdate(FrameworkTypes obj)
        {
            ((ResourceMgr)obj).Update();
        }

        /// <summary>
        /// Loads a single resource by logical name using the application-level resource entry points.
        /// </summary>
        /// <param name="resourceName">Logical resource name to load.</param>
        private static void LoadResource(string resourceName)
        {
            ILogger logger = Log.For(LogCategories.ContentResources);
            ResourceMgrLog.ResourceLoading(logger, resourceName);
            if (!TryResolveResource(resourceName, out string localizedName))
            {
                return;
            }

            if (localizedName == Resources.Str.MenuStrings)
            {
                LocalizationManager.EnsureLoaded();
                return;
            }
            if (Resources.IsSound(localizedName))
            {
                _ = Application.SharedSoundMgr().GetSound(localizedName);
                return;
            }
            if (Resources.IsFont(localizedName))
            {
                _ = Application.GetFont(localizedName);
                return;
            }
            try
            {
                _ = Application.GetTexture(localizedName);
            }
            catch (Exception failure)
            {
                ResourceMgrLog.TextureLoadFailed(logger, localizedName, failure);
            }
        }

        /// <summary>
        /// Frees a cached resource by its string identifier if it has been loaded.
        /// </summary>
        /// <param name="resourceName">Logical resource name to free.</param>
        public void FreeResource(string resourceName)
        {
            ILogger logger = Log.For(LogCategories.ContentResources);
            ResourceMgrLog.ResourceFreeing(logger, resourceName);
            if (!TryResolveResource(resourceName, out string localizedName))
            {
                return;
            }

            if (localizedName == Resources.Str.MenuStrings)
            {
                LocalizationManager.ClearCache();
                return;
            }
            if (Resources.IsSound(localizedName))
            {
                Application.SharedSoundMgr().FreeSound(localizedName);
                return;
            }
            if (s_Resources.TryGetValue(localizedName, out object value))
            {
                if (value is IDisposable disposable)
                {
                    disposable.Dispose();
                }
                _ = s_Resources.Remove(localizedName);
            }
        }

        /// <summary>
        /// Delegate notified when asynchronous loading completes.
        /// </summary>
        public IResourceMgrDelegate resourcesDelegate;

        /// <summary>
        /// Stores all cached resources (textures, fonts, sounds, strings)
        /// </summary>
        private readonly Dictionary<string, object> s_Resources = [];

        /// <summary>
        /// Reports what a finished load batch cost, in resources, time and memory.
        /// </summary>
        /// <param name="count">How many resources the batch loaded.</param>
        /// <param name="mode">Which drain produced it, immediate or incremental.</param>
        /// <param name="longestUpdateMs">
        /// Longest single loading step, not counting the completion callback: the longest frame for
        /// an incremental batch, the whole drain for an immediate one.
        /// </param>
        /// <remarks>
        /// A batch of nothing is not reported: entering a controller that needs no new resource
        /// still runs a batch, and those would otherwise be most of the lines here.
        /// </remarks>
        private void ReportBatchComplete(int count, string mode, double longestUpdateMs)
        {
            if (count <= 0)
            {
                return;
            }

            double elapsedMs = Stopwatch.GetElapsedTime(batchStartedTicks).TotalMilliseconds;
            double peakPreparedMiB = AssetPlatform.Current.TakePeakPreparedPixelBytes() / (1024.0 * 1024.0);
            ILogger logger = Log.For(LogCategories.ContentResources);
            string memory = MemoryReport.Describe();
            ResourceMgrLog.BatchLoaded(logger, count, mode, elapsedMs, longestUpdateMs, peakPreparedMiB, memory);
        }

        /// <summary>
        /// Number of resources loaded in the current batch.
        /// </summary>
        private int loaded;

        /// <summary>When the current batch was started, for the completion report.</summary>
        private long batchStartedTicks = Stopwatch.GetTimestamp();

        /// <summary>
        /// Total number of resources queued in the current batch.
        /// </summary>
        private int loadCount;

        /// <summary>
        /// Pending resource names for batch loading.
        /// </summary>
        private readonly List<string> loadQueue = [];

        /// <summary>
        /// Pending resource names for background prefetch.
        /// </summary>
        private readonly List<string> prefetchQueue = [];

        /// <summary>
        /// Set mirror used to avoid duplicate entries in <see cref="prefetchQueue"/>.
        /// </summary>
        private readonly HashSet<string> prefetchQueueSet = [];

        /// <summary>
        /// Timer identifier used for incremental loading.
        /// </summary>
        private int Timer = -1;

        /// <summary>
        /// Milliseconds of loading one frame may spend before leaving the rest of the queue to the
        /// next frame, about half of a 60 Hz frame.
        /// </summary>
        private const double FrameLoadBudgetMilliseconds = 8.0;

        /// <summary>
        /// How many images ahead in the load queue are decoded at once, which bounds how many
        /// decoded images wait for upload: four full-screen 2048² images are about 64 MiB.
        /// </summary>
        private const int DecodeAheadImages = 4;

        /// <summary>
        /// Images whose decodes <see cref="PrepareImageResource"/> started apart from the load queue,
        /// which abandoning a batch leaves running.
        /// </summary>
        private readonly HashSet<string> heldPreparations = [];

        /// <summary>Longest single loading step of the current batch, for the completion report.</summary>
        private double longestUpdateMs;

        /// <summary>
        /// Resource categories supported by the resource manager.
        /// </summary>
        public enum ResourceType
        {
            /// <summary>
            /// Image or texture resource.
            /// </summary>
            IMAGE,

            /// <summary>
            /// Font resource.
            /// </summary>
            FONT,

            /// <summary>
            /// Sound resource.
            /// </summary>
            SOUND,

            /// <summary>
            /// Binary data resource.
            /// </summary>
            BINARY,

            /// <summary>
            /// Structured element resource.
            /// </summary>
            ELEMENT
        }
    }

    /// <summary>Log messages for resource loading.</summary>
    internal static partial class ResourceMgrLog
    {
        [LoggerMessage(Level = LogLevel.Warning, Message = "Could not load texture '{ResourceName}'")]
        public static partial void TextureLoadFailed(ILogger logger, string resourceName, Exception exception);

        [LoggerMessage(
            Level = LogLevel.Information,
            Message = "Loaded {Count} resources ({Mode}) in {ElapsedMs:F1} ms; longest loading step "
                + "{LongestUpdateMs:F1} ms; peak decoded pixels awaiting upload {PeakPreparedMiB:F1} MiB; {Memory}")]
        public static partial void BatchLoaded(
            ILogger logger,
            int count,
            string mode,
            double elapsedMs,
            double longestUpdateMs,
            double peakPreparedMiB,
            string memory);

        [LoggerMessage(Level = LogLevel.Information, Message = "Freed {Count} resources; {Memory}")]
        public static partial void PackFreed(ILogger logger, int count, string memory);

        [LoggerMessage(Level = LogLevel.Debug, Message = "Loading resource '{ResourceName}'")]
        public static partial void ResourceLoading(ILogger logger, string resourceName);

        [LoggerMessage(Level = LogLevel.Debug, Message = "Freeing resource '{ResourceName}'")]
        public static partial void ResourceFreeing(ILogger logger, string resourceName);
    }
}
