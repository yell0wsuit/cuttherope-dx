namespace CutTheRopeDX.Framework.Diagnostics
{
    /// <summary>
    /// Category names, which carry what the bracketed message prefixes used to. Named here so
    /// two call sites in the same area cannot spell one differently and split its output.
    /// </summary>
    internal static class LogCategories
    {
        /// <summary>Application lifecycle: runtime startup, teardown, and root controller flow.</summary>
        public const string Application = "Application";

        /// <summary>The SDL host: renderer selection, device loss, shutdown.</summary>
        public const string SdlHost = "Sdl.Host";

        /// <summary>Graphics device construction and adapter reporting.</summary>
        public const string SdlGraphics = "Sdl.Graphics";

        /// <summary>The FFmpeg video player.</summary>
        public const string MediaFFmpeg = "Media.FFmpeg";

        /// <summary>The AVFoundation video player.</summary>
        public const string MediaAVFoundation = "Media.AVFoundation";

        /// <summary>Preference storage: the save directory, migration, save and load failures.</summary>
        public const string Preferences = "Preferences";

        /// <summary>Music pack resolution.</summary>
        public const string GameMusic = "Game.Music";

        /// <summary>Localization string loading.</summary>
        public const string Localization = "Localization";

        /// <summary>Texture atlas parsing.</summary>
        public const string ContentAtlas = "Content.Atlas";

        /// <summary>Pack configuration, skin manifest and tutorial prompt loading.</summary>
        public const string ContentPacks = "Content.Packs";

        /// <summary>XML content loading: maps, and the level data read from them.</summary>
        public const string ContentXml = "Content.Xml";

        /// <summary>Texture and asset resolution for the resource manager.</summary>
        public const string ContentResources = "Content.Resources";

        /// <summary>Sound effect and music playback.</summary>
        public const string MediaSound = "Media.Sound";

        /// <summary>The playtest session: the editor's level, its reloads, and its failures.</summary>
        public const string Playtest = "Playtest";

        /// <summary>The background release check.</summary>
        public const string UpdateCheck = "UpdateCheck";

        /// <summary>Discord rich presence.</summary>
        public const string RichPresence = "RichPresence";
    }
}
