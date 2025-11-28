using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

using CutTheRope.Framework;
using CutTheRope.Framework.Core;

namespace CutTheRope.GameMain
{
    /// <summary>
    /// Resource manager wrapper that preserves legacy numeric identifiers while enabling string-based lookups.
    /// </summary>
    internal sealed class CTRResourceMgr : ResourceMgr
    {
        /// <summary>
        /// Adjusts a legacy resource identifier for the active language when localized variants exist.
        /// </summary>
        public static int HandleLocalizedResource(int r)
        {
            if (r != HudButtonsEnId)
            {
                if (r != MenuResultEnId)
                {
                    if (r == MenuExtraButtonsEnId)
                    {
                        if (LANGUAGE == Language.LANGRU)
                        {
                            return MenuExtraButtonsRuId;
                        }
                        if (LANGUAGE == Language.LANGDE)
                        {
                            return MenuExtraButtonsGrId;
                        }
                        if (LANGUAGE == Language.LANGFR)
                        {
                            return MenuExtraButtonsFrId;
                        }
                    }
                }
                else
                {
                    if (LANGUAGE == Language.LANGRU)
                    {
                        return MenuResultRuId;
                    }
                    if (LANGUAGE == Language.LANGDE)
                    {
                        return MenuResultGrId;
                    }
                    if (LANGUAGE == Language.LANGFR)
                    {
                        return MenuResultFrId;
                    }
                }
            }
            else
            {
                if (LANGUAGE == Language.LANGRU)
                {
                    return HudButtonsRuId;
                }
                if (LANGUAGE == Language.LANGDE)
                {
                    return HudButtonsGrId;
                }
                if (LANGUAGE == Language.LANGFR)
                {
                    return HudButtonsEnId;
                }
            }
            return r;
        }

        /// <summary>
        /// Resolves a localized XNA resource name for a given legacy identifier.
        /// </summary>
        public static string XNA_ResName(int resId)
        {
            // Use the new string-based resource ID system
            return GetResourceName(HandleLocalizedResource(resId));
        }

        /// <summary>
        /// Loads a resource by its string name. Auto-assigns an ID if needed.
        /// </summary>
        public static object LoadResourceByName(string resourceName, ResourceType resType)
        {
            CTRResourceMgr mgr = new();
            return mgr.LoadResource(resourceName, resType);
        }

        public override object LoadResource(int resID, ResourceType resType)
        {
            return base.LoadResource(HandleLocalizedResource(resID), resType);
        }

        public override void FreeResource(int resID)
        {
            base.FreeResource(HandleLocalizedResource(resID));
        }

        protected override TextureAtlasConfig GetTextureAtlasConfig(int resId)
        {
            Dictionary<int, TextureAtlasConfig> configs = LoadTexturePackerRegistry();
            return configs.TryGetValue(resId, out TextureAtlasConfig config) ? config : null;
        }

        private static Dictionary<int, TextureAtlasConfig> LoadTexturePackerRegistry()
        {
            Dictionary<int, TextureAtlasConfig> result = [];

            try
            {
                string registryPath = "content/TexturePackerRegistry.json";
                if (!File.Exists(registryPath))
                {
                    return result; // Return empty dict if no registry file
                }

                string json = File.ReadAllText(registryPath);
                using JsonDocument doc = JsonDocument.Parse(json);

                if (!doc.RootElement.TryGetProperty("textures", out JsonElement texturesElement) ||
                    texturesElement.ValueKind != JsonValueKind.Array)
                {
                    return result;
                }

                foreach (JsonElement textureElement in texturesElement.EnumerateArray())
                {
                    if (!textureElement.TryGetProperty("resourceId", out JsonElement idElement) ||
                        !textureElement.TryGetProperty("atlasPath", out JsonElement atlasPathElement))
                    {
                        continue;
                    }

                    int resourceId = idElement.GetInt32();
                    string atlasPath = atlasPathElement.GetString();

                    TextureAtlasConfig config = new()
                    {
                        Format = TextureAtlasFormat.TexturePackerJson,
                        AtlasPath = atlasPath,
                        ResourceName = ResourceNameTranslator.TranslateLegacyId(resourceId),
                        UseAntialias = GetBoolProperty(textureElement, "useAntialias", true),
                        FrameOrder = GetStringArrayProperty(textureElement, "frameOrder"),
                        CenterOffsets = GetBoolProperty(textureElement, "centerOffsets", false)
                    };

                    result[resourceId] = config;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to load TexturePackerRegistry.json: {ex.Message}");
            }

            return result;
        }

        private static bool GetBoolProperty(JsonElement element, string propertyName, bool defaultValue)
        {
            return (element.TryGetProperty(propertyName, out JsonElement prop) &&
                prop.ValueKind == JsonValueKind.True) || ((!element.TryGetProperty(propertyName, out prop) ||
                prop.ValueKind != JsonValueKind.False) && defaultValue);
        }

        private static string[] GetStringArrayProperty(JsonElement element, string propertyName)
        {
            if (!element.TryGetProperty(propertyName, out JsonElement prop) ||
                prop.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            List<string> result = [];
            foreach (JsonElement item in prop.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.String)
                {
                    result.Add(item.GetString());
                }
            }

            return result.Count > 0 ? [.. result] : null;
        }

        private static readonly int HudButtonsEnId = ResourceNameTranslator.ToResourceId(Resources.Img.HudButtonsEn);

        private static readonly int HudButtonsRuId = ResourceNameTranslator.ToResourceId(Resources.Img.HudButtonsRu);

        private static readonly int HudButtonsGrId = ResourceNameTranslator.ToResourceId(Resources.Img.HudButtonsGr);

        private static readonly int MenuResultEnId = ResourceNameTranslator.ToResourceId(Resources.Img.MenuResultEn);

        private static readonly int MenuResultRuId = ResourceNameTranslator.ToResourceId(Resources.Img.MenuResultRu);

        private static readonly int MenuResultFrId = ResourceNameTranslator.ToResourceId(Resources.Img.MenuResultFr);

        private static readonly int MenuResultGrId = ResourceNameTranslator.ToResourceId(Resources.Img.MenuResultGr);

        private static readonly int MenuExtraButtonsEnId = ResourceNameTranslator.ToResourceId(Resources.Img.MenuExtraButtonsEn);

        private static readonly int MenuExtraButtonsRuId = ResourceNameTranslator.ToResourceId(Resources.Img.MenuExtraButtonsRu);

        private static readonly int MenuExtraButtonsGrId = ResourceNameTranslator.ToResourceId(Resources.Img.MenuExtraButtonsGr);

        private static readonly int MenuExtraButtonsFrId = ResourceNameTranslator.ToResourceId(Resources.Img.MenuExtraButtonsFr);
    }
}
