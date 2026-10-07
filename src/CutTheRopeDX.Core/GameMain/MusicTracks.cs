using System;
using System.Collections.Generic;

using CutTheRopeDX.Framework.Diagnostics;
using CutTheRopeDX.Framework.Media;

using Microsoft.Extensions.Logging;

namespace CutTheRopeDX.GameMain
{
    /// <summary>
    /// Decides which music plays in the menus and in gameplay. The first active override wins;
    /// with none active, the menus play the default track and gameplay plays the pack's own music.
    /// </summary>
    internal static class MusicTracks
    {
        /// <summary>
        /// Menu styles and events that replace the usual music, highest priority first.
        /// </summary>
        private static readonly MusicOverride[] Overrides =
        [
            new(() => MenuTheme.IsExperiments, Resources.Music.MenuMusicExp, Resources.Music.GameMusicExp),
            new(() => SpecialEvents.IsXmas, Resources.Music.MenuMusicXmas, Resources.Music.GameMusicXmas),

            // Cut the Rope 3.3.0 dressed only its menus for Halloween; levels keep their music.
            new(() => SpecialEvents.IsHalloween, Resources.Music.MenuMusicHalloween, Game: null),
        ];

        /// <summary>
        /// Track sets a pack config can name in its <c>musicPack</c> field.
        /// </summary>
        private static readonly Dictionary<string, string[]> NamedPacks = new()
        {
            ["ctr_original"] =
            [
                Resources.Music.GameMusic,
                Resources.Music.GameMusic2,
                Resources.Music.GameMusic3,
                Resources.Music.GameMusic4,
                Resources.Music.GameMusic5,
            ],
        };

        /// <summary>
        /// Gets the menu track for the active override, or the default menu track.
        /// </summary>
        /// <returns>The menu music resource name.</returns>
        public static string Menu()
        {
            return ActiveOverride()?.Menu ?? Resources.Music.MenuMusic;
        }

        /// <summary>
        /// Gets the gameplay tracks to pick from: the active override's track, or the pack's own music
        /// when no override is active or the active one leaves gameplay alone.
        /// </summary>
        /// <param name="pack">Pack being played.</param>
        /// <returns>The candidate music resource names, empty when the pack names none.</returns>
        public static string[] Game(int pack)
        {
            MusicOverride active = ActiveOverride();
            if (active?.Game != null)
            {
                return [active.Game];
            }

            string musicPack = PackConfig.GetMusicPackOrDefault(pack);
            if (musicPack == null)
            {
                string[] musicList = PackConfig.GetMusicListOrDefault(pack);
                if (musicList.Length == 0)
                {
                    MusicTracksLog.MissingMusicList(Log.For(LogCategories.GameMusic), pack);
                }
                return musicList;
            }
            if (NamedPacks.TryGetValue(musicPack, out string[] tracks))
            {
                return tracks;
            }
            MusicTracksLog.UnknownMusicPack(Log.For(LogCategories.GameMusic), musicPack);
            return [];
        }

        /// <summary>
        /// Plays the menu track.
        /// </summary>
        public static void PlayMenuMusic()
        {
            SoundMgr.PlayMusic(Menu());
        }

        /// <summary>
        /// Plays one of the gameplay tracks for a pack.
        /// </summary>
        /// <param name="pack">Pack being played.</param>
        public static void PlayGameMusic(int pack)
        {
            SoundMgr.PlayRandomMusic(Game(pack));
        }

        /// <summary>
        /// Finds the highest-priority override that is active now.
        /// </summary>
        /// <returns>The active override, or <see langword="null"/> when none applies.</returns>
        private static MusicOverride ActiveOverride()
        {
            return Array.Find(Overrides, entry => entry.IsActive());
        }

        /// <summary>
        /// Music that replaces the usual tracks while a menu style or event is active.
        /// </summary>
        /// <param name="IsActive">Whether the override applies right now.</param>
        /// <param name="Menu">Menu music resource name.</param>
        /// <param name="Game">Gameplay music resource name, or <see langword="null"/> to keep the pack's own music.</param>
        private sealed record MusicOverride(Func<bool> IsActive, string Menu, string Game);
    }

    /// <summary>Log messages for music pack resolution.</summary>
    internal static partial class MusicTracksLog
    {
        [LoggerMessage(
            Level = LogLevel.Warning,
            Message = "Missing either musicPack or musicList for pack {Pack}.")]
        public static partial void MissingMusicList(ILogger logger, int pack);

        [LoggerMessage(Level = LogLevel.Warning, Message = "Unknown musicPack '{MusicPack}'")]
        public static partial void UnknownMusicPack(ILogger logger, string musicPack);
    }
}
