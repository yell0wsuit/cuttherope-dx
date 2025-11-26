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
            resNames_ ??= new Dictionary<int, string>
                {
                    { IMG_DEFAULT, "zeptolab_no_link" },
                    { IMG_LOADERBAR_FULL, "loaderbar_full" },
                    { IMG_MENU_BUTTON_DEFAULT, "menu_button_default" },
                    { FNT_BIG_FONT, "big_font" },
                    { FNT_SMALL_FONT, "small_font" },
                    { IMG_MENU_LOADING, "menu_loading" },
                    { IMG_MENU_NOTIFICATION, "menu_notification" },
                    { IMG_MENU_ACHIEVEMENT, "menu_achievement" },
                    { IMG_MENU_OPTIONS, "menu_options" },
                    { SND_TAP, "tap" },
                    { STR_MENU, "menu_strings" },
                    { SND_BUTTON, "button" },
                    { SND_BUBBLE_BREAK, "bubble_break" },
                    { SND_BUBBLE, "bubble" },
                    { SND_CANDY_BREAK, "candy_break" },
                    { SND_MONSTER_CHEWING, "monster_chewing" },
                    { SND_MONSTER_CLOSE, "monster_close" },
                    { SND_MONSTER_OPEN, "monster_open" },
                    { SND_MONSTER_SAD, "monster_sad" },
                    { SND_RING, "ring" },
                    { SND_ROPE_BLEAK_1, "rope_bleak_1" },
                    { SND_ROPE_BLEAK_2, "rope_bleak_2" },
                    { SND_ROPE_BLEAK_3, "rope_bleak_3" },
                    { SND_ROPE_BLEAK_4, "rope_bleak_4" },
                    { SND_ROPE_GET, "rope_get" },
                    { SND_STAR_1, "star_1" },
                    { SND_STAR_2, "star_2" },
                    { SND_STAR_3, "star_3" },
                    { SND_ELECTRIC, "electric" },
                    { SND_PUMP_1, "pump_1" },
                    { SND_PUMP_2, "pump_2" },
                    { SND_PUMP_3, "pump_3" },
                    { SND_PUMP_4, "pump_4" },
                    { SND_SPIDER_ACTIVATE, "spider_activate" },
                    { SND_SPIDER_FALL, "spider_fall" },
                    { SND_SPIDER_WIN, "spider_win" },
                    { SND_WHEEL, "wheel" },
                    { SND_WIN, "win" },
                    { SND_GRAVITY_OFF, "gravity_off" },
                    { SND_GRAVITY_ON, "gravity_on" },
                    { SND_CANDY_LINK, "candy_link" },
                    { SND_BOUNCER, "bouncer" },
                    { SND_SPIKE_ROTATE_IN, "spike_rotate_in" },
                    { SND_SPIKE_ROTATE_OUT, "spike_rotate_out" },
                    { SND_BUZZ, "buzz" },
                    { SND_TELEPORT, "teleport" },
                    { SND_SCRATCH_IN, "scratch_in" },
                    { SND_SCRATCH_OUT, "scratch_out" },
                    { IMG_MENU_BGR, "menu_bgr" },
                    { IMG_MENU_POPUP, "menu_popup" },
                    { IMG_MENU_LOGO, "menu_logo" },
                    { IMG_MENU_LEVEL_SELECTION, "menu_level_selection" },
                    { IMG_MENU_PACK_SELECTION, "menu_pack_selection" },
                    { IMG_MENU_PACK_SELECTION2, "menu_pack_selection2" },
                    { IMG_MENU_EXTRA_BUTTONS, "menu_extra_buttons" },
                    { IMG_MENU_SCROLLBAR, "menu_scrollbar" },
                    { IMG_MENU_LEADERBOARD, "menu_leaderboard" },
                    { IMG_MENU_PROCESSING, "menu_processing_hd" },
                    { IMG_MENU_SCROLLBAR_CHANGENAME, "menu_scrollbar_changename" },
                    { IMG_MENU_BUTTON_ACHIV_CUP, "menu_button_achiv_cup" },
                    { IMG_MENU_BGR_SHADOW, "menu_bgr_shadow" },
                    { IMG_MENU_BUTTON_SHORT, "menu_button_short" },
                    { IMG_HUD_BUTTONS, "hud_buttons" },
                    { IMG_OBJ_CANDY_01, "obj_candy_01" },
                    { IMG_OBJ_SPIDER, "obj_spider" },
                    { IMG_CONFETTI_PARTICLES, "confetti_particles" },
                    { IMG_MENU_PAUSE, "menu_pause" },
                    { IMG_MENU_RESULT, "menu_result" },
                    { FNT_FONT_NUMBERS_BIG, "font_numbers_big" },
                    { IMG_HUD_BUTTONS_EN, "hud_buttons_en" },
                    { IMG_MENU_RESULT_EN, "menu_result_en" },
                    { IMG_OBJ_STAR_DISAPPEAR, "obj_star_disappear" },
                    { IMG_OBJ_BUBBLE_FLIGHT, "obj_bubble_flight" },
                    { IMG_OBJ_BUBBLE_POP, "obj_bubble_pop" },
                    { IMG_OBJ_HOOK_AUTO, "obj_hook_auto" },
                    { IMG_OBJ_BUBBLE_ATTACHED, "obj_bubble_attached" },
                    { IMG_OBJ_HOOK_01, "obj_hook_01" },
                    { IMG_OBJ_HOOK_02, "obj_hook_02" },
                    { IMG_OBJ_STAR_IDLE, "obj_star_idle" },
                    { IMG_HUD_STAR, "hud_star" },
                    { IMG_CHAR_ANIMATIONS, "char_animations" },
                    { IMG_OBJ_HOOK_REGULATED, "obj_hook_regulated" },
                    { IMG_OBJ_HOOK_MOVABLE, "obj_hook_movable" },
                    { IMG_OBJ_PUMP, "obj_pump" },
                    { IMG_TUTORIAL_SIGNS, "tutorial_signs" },
                    { IMG_OBJ_SOCKS, "obj_hat" },
                    { IMG_OBJ_BOUNCER_01, "obj_bouncer_01" },
                    { IMG_OBJ_BOUNCER_02, "obj_bouncer_02" },
                    { IMG_OBJ_SPIKES_01, "obj_spikes_01" },
                    { IMG_OBJ_SPIKES_02, "obj_spikes_02" },
                    { IMG_OBJ_SPIKES_03, "obj_spikes_03" },
                    { IMG_OBJ_SPIKES_04, "obj_spikes_04" },
                    { IMG_OBJ_ELECTRODES, "obj_electrodes" },
                    { IMG_OBJ_ROTATABLE_SPIKES_01, "obj_rotatable_spikes_01" },
                    { IMG_OBJ_ROTATABLE_SPIKES_02, "obj_rotatable_spikes_02" },
                    { IMG_OBJ_ROTATABLE_SPIKES_03, "obj_rotatable_spikes_03" },
                    { IMG_OBJ_ROTATABLE_SPIKES_04, "obj_rotatable_spikes_04" },
                    { IMG_OBJ_ROTATABLE_SPIKES_BUTTON, "obj_rotatable_spikes_button" },
                    { IMG_OBJ_BEE_HD, "obj_bee_hd" },
                    { IMG_OBJ_POLLEN_HD, "obj_pollen_hd" },
                    { IMG_CHAR_SUPPORTS, "char_supports" },
                    { IMG_CHAR_ANIMATIONS2, "char_animations2" },
                    { IMG_CHAR_ANIMATIONS3, "char_animations3" },
                    { IMG_OBJ_VINIL, "obj_vinil" },
                    { IMG_BGR_01_P1, "bgr_01_p1" },
                    { IMG_BGR_01_P2, "bgr_01_p2" },
                    { IMG_BGR_02_P1, "bgr_02_p1" },
                    { IMG_BGR_02_P2, "bgr_02_p2" },
                    { IMG_BGR_03_P1, "bgr_03_p1" },
                    { IMG_BGR_03_P2, "bgr_03_p2" },
                    { IMG_BGR_04_P1, "bgr_04_p1" },
                    { IMG_BGR_04_P2, "bgr_04_p2" },
                    { IMG_BGR_05_P1, "bgr_05_p1" },
                    { IMG_BGR_05_P2, "bgr_05_p2" },
                    { IMG_BGR_06_P1, "bgr_06_p1" },
                    { IMG_BGR_06_P2, "bgr_06_p2" },
                    { IMG_BGR_07_P1, "bgr_07_p1" },
                    { IMG_BGR_07_P2, "bgr_07_p2" },
                    { IMG_BGR_08_P1, "bgr_08_p1" },
                    { IMG_BGR_08_P2, "bgr_08_p2" },
                    { IMG_BGR_09_P1, "bgr_09_p1" },
                    { IMG_BGR_09_P2, "bgr_09_p2" },
                    { IMG_BGR_10_P1, "bgr_10_p1" },
                    { IMG_BGR_10_P2, "bgr_10_p2" },
                    { IMG_BGR_11_P1, "bgr_11_p1" },
                    { IMG_BGR_11_P2, "bgr_11_p2" },
                    { IMG_BGR_COVER_01, "bgr_01_cover" },
                    { IMG_BGR_COVER_02, "bgr_02_cover" },
                    { IMG_BGR_COVER_03, "bgr_03_cover" },
                    { IMG_BGR_COVER_04, "bgr_04_cover" },
                    { IMG_BGR_COVER_05, "bgr_05_cover" },
                    { IMG_BGR_COVER_06, "bgr_06_cover" },
                    { IMG_BGR_COVER_07, "bgr_07_cover" },
                    { IMG_BGR_COVER_08, "bgr_08_cover" },
                    { IMG_BGR_COVER_09, "bgr_09_cover" },
                    { IMG_BGR_COVER_10, "bgr_10_cover" },
                    { IMG_BGR_COVER_11, "bgr_11_cover" },
                    { IMG_MENU_EXTRA_BUTTONS_FR, "menu_extra_buttons_fr" },
                    { IMG_MENU_EXTRA_BUTTONS_GR, "menu_extra_buttons_gr" },
                    { IMG_MENU_EXTRA_BUTTONS_RU, "menu_extra_buttons_ru" },
                    { IMG_HUD_BUTTONS_RU, "hud_buttons_ru" },
                    { IMG_HUD_BUTTONS_GR, "hud_buttons_gr" },
                    { IMG_MENU_RESULT_RU, "menu_result_ru" },
                    { IMG_MENU_RESULT_FR, "menu_result_fr" },
                    { IMG_MENU_RESULT_GR, "menu_result_gr" },
                    { SND_MENU_MUSIC, "menu_music" },
                    { SND_GAME_MUSIC, "game_music" },
                    { SND_GAME_MUSIC2, "game_music2" },
                    { SND_GAME_MUSIC3, "game_music3" },
                    { SND_GAME_MUSIC4, "game_music4" },
                    { IMG_MENU_EXTRA_BUTTONS_EN, "menu_extra_buttons_en" },
                    { SND_GHOST_PUFF, "ghost_puff" },
                    { IMG_BGR_COVER_12, "bgr_12_cover" },
                    { IMG_BGR_12_P1, "bgr_12_p1" },
                    { IMG_BGR_12_P2, "bgr_12_p2" },
                    { IMG_OBJ_GHOST, "obj_ghost" }
                };
            _ = resNames_.TryGetValue(HandleLocalizedResource(resId), out string value);
            return value;
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

        private static Dictionary<int, string> resNames_;
    }
}
