namespace CutTheRopeDX.Framework.Diagnostics
{
    /// <summary>
    /// Category names, which carry what the bracketed message prefixes used to. Named here so
    /// two call sites in the same area cannot spell one differently and split its output.
    /// </summary>
    internal static class LogCategories
    {
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

        /// <summary>Pack configuration and tutorial prompt loading.</summary>
        public const string ContentPacks = "Content.Packs";
    }
}
