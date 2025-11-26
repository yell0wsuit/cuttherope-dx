using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

using CutTheRope.Framework;
using CutTheRope.Framework.Core;

namespace CutTheRope.GameMain
{
    internal sealed class CTRResourceMgr : ResourceMgr
    {
        public static int HandleLocalizedResource(int r)
        {
            if (r != IMG_HUD_BUTTONS_EN)
            {
                if (r != IMG_MENU_RESULT_EN)
                {
                    if (r == IMG_MENU_EXTRA_BUTTONS_EN)
                    {
                        if (LANGUAGE == Language.LANGRU)
                        {
                            return IMG_MENU_EXTRA_BUTTONS_RU;
                        }
                        if (LANGUAGE == Language.LANGDE)
                        {
                            return IMG_MENU_EXTRA_BUTTONS_GR;
                        }
                        if (LANGUAGE == Language.LANGFR)
                        {
                            return IMG_MENU_EXTRA_BUTTONS_FR;
                        }
                    }
                }
                else
                {
                    if (LANGUAGE == Language.LANGRU)
                    {
                        return IMG_MENU_RESULT_RU;
                    }
                    if (LANGUAGE == Language.LANGDE)
                    {
                        return IMG_MENU_RESULT_GR;
                    }
                    if (LANGUAGE == Language.LANGFR)
                    {
                        return IMG_MENU_RESULT_FR;
                    }
                }
            }
            else
            {
                if (LANGUAGE == Language.LANGRU)
                {
                    return IMG_HUD_BUTTONS_RU;
                }
                if (LANGUAGE == Language.LANGDE)
                {
                    return IMG_HUD_BUTTONS_GR;
                }
                if (LANGUAGE == Language.LANGFR)
                {
                    return IMG_HUD_BUTTONS_EN;
                }
            }
            return r;
        }

        public static string XNA_ResName(int resId)
        {
            // Use the new string-based resource ID system
            return GetResourceName(HandleLocalizedResource(resId));
        }

        /// <summary>
        /// Loads a resource by its string name. Auto-assigns an ID if needed.
        /// </summary>
        public static object LoadResource(string resourceName, ResourceType resType)
        {
            int resourceId = GetResourceId(resourceName);
            CTRResourceMgr mgr = new();
            return mgr.LoadResource(resourceId, resType);
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
    }
}
