using CutTheRopeDX.Framework.Core;
using CutTheRopeDX.Framework.Helpers;
using CutTheRopeDX.Framework.Media;

namespace CutTheRopeDX.GameMain
{
    /// <summary>
    /// The professor's spoken lines in Experiments: one as a level opens, a verdict on
    /// the stars when it is won, and now and then a sigh when it restarts. The WP7
    /// <c>GameController.levelFirstStart</c> and <c>levelWon</c> and
    /// <c>GameScene.animateLevelRestart</c> calls, which iOS HD matches; iOS also gives voices a
    /// channel of their own (<see cref="SoundMgr.PlayVoice"/>).
    /// </summary>
    internal static class ExperimentsVoice
    {
        /// <summary>Lines a level can open with.</summary>
        private static readonly string[] StartLines =
        [
            Resources.Snd.VoiceStart01,
            Resources.Snd.VoiceStart02,
            Resources.Snd.VoiceStart03,
        ];

        /// <summary>
        /// Lines a win can earn, by stars collected. The table is <c>levelWon_starsSnd</c>.
        /// </summary>
        /// <remarks>
        /// Experiments keeps three more three-star lines in a fifth row that only its fourth pack
        /// reaches, by counting one star extra there. DX's packs are not those packs, so those
        /// lines join the three-star row instead of going unplayed.
        /// </remarks>
        private static readonly string[][] StarLines =
        [
            [Resources.Snd.VoiceStar00A, Resources.Snd.VoiceStar00B],
            [Resources.Snd.VoiceStar01A, Resources.Snd.VoiceStar01B],
            [Resources.Snd.VoiceStar02A, Resources.Snd.VoiceStar02B, Resources.Snd.VoiceStar02C],
            [
                Resources.Snd.VoiceStar03A,
                Resources.Snd.VoiceStar03B,
                Resources.Snd.VoiceStar03C,
                Resources.Snd.VoiceStar03D,
                Resources.Snd.VoiceStar03E,
                Resources.Snd.VoiceStar03F,
            ],
        ];

        /// <summary>Lines for a restart.</summary>
        private static readonly string[] FailLines =
        [
            Resources.Snd.VoiceFail01,
            Resources.Snd.VoiceFail02,
        ];

        /// <summary>The line played when the voice is switched back on.</summary>
        private const string ToggleOnLine = Resources.Snd.VoiceStart03;

        /// <summary>Preference that keeps the voice on or off.</summary>
        public const string PreferenceKey = "PREFS_EXP_VOICE_ON";

        /// <summary>
        /// Switches the voice on or off, from the options or the pause menu. Switching it off cuts
        /// the line being spoken; switching it on says one.
        /// </summary>
        public static void Toggle()
        {
            bool wasOn = Preferences.GetBooleanForKey(PreferenceKey);
            Preferences.SetBooleanForKey(!wasOn, PreferenceKey, true);
            if (wasOn)
            {
                SoundMgr.StopVoice();
            }
            else
            {
                SoundMgr.PlayVoice(ToggleOnLine);
            }
        }

        /// <summary>Speaks as a level opens after loading.</summary>
        public static void LevelStarted()
        {
            if (MenuTheme.IsExperiments)
            {
                SoundMgr.PlayVoice(StartLines[MathHelper.RND_RANGE(0, StartLines.Length - 1)]);
            }
        }

        /// <summary>Half the time, comments on the stars a won level earned.</summary>
        /// <param name="starsCollected">Stars collected in the level.</param>
        public static void LevelWon(int starsCollected)
        {
            if (!MenuTheme.IsExperiments || MathHelper.RND_RANGE(0, 1) != 0)
            {
                return;
            }

            string[] row = StarLines[System.Math.Clamp(starsCollected, 0, StarLines.Length - 1)];
            SoundMgr.PlayVoice(row[MathHelper.RND_RANGE(0, row.Length - 1)]);
        }

        /// <summary>Four times in eleven, sighs as the level restarts.</summary>
        public static void LevelRestarting()
        {
            if (MenuTheme.IsExperiments && MathHelper.RND_RANGE(0, 10) < 4)
            {
                SoundMgr.PlayVoice(FailLines[MathHelper.RND_RANGE(0, FailLines.Length - 1)]);
            }
        }
    }
}
