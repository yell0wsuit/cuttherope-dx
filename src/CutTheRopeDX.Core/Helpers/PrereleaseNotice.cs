using CutTheRopeDX.Commons;
using CutTheRopeDX.Framework.Core;
using CutTheRopeDX.GameMain;

using static CutTheRopeDX.Commons.PopupBuilder;

namespace CutTheRopeDX.Helpers
{
    /// <summary>
    /// Tells players of a CI prerelease build that it is a testing version, once per version.
    /// </summary>
    internal static class PrereleaseNotice
    {
        /// <summary>Preference key holding the last prerelease version the notice was shown for.</summary>
        private const string ShownVersionKey = "PREFS_PRERELEASE_NOTICE_VERSION";

        /// <summary>
        /// Determines whether the notice is due for the given version.
        /// </summary>
        /// <param name="version">The running version.</param>
        /// <returns><see langword="true"/> for a prerelease whose notice has not been shown yet; otherwise <see langword="false"/>.</returns>
        public static bool IsDue(string version)
        {
            return AppVersion.IsPrereleaseVersion(version)
                && Preferences.GetStringForKey(ShownVersionKey) != version;
        }

        /// <summary>
        /// Shows the notice and records the version, so it does not appear again for this build.
        /// </summary>
        /// <param name="builder">The popup builder used to create and display the popup.</param>
        /// <param name="version">The running version the notice is shown for.</param>
        public static void Show(PopupBuilder builder, string version)
        {
            Preferences.SetStringForKey(version, ShownVersionKey, true);

            PopupTemplate template = PopupTemplate.Create(PopupSize.Large)
                .WithScaleMode(PopupScaleMode.Background)
                .AddText(Application.GetString("PRERELEASE_TITLE"), Resources.Fnt.BigFont, PopupAnchor.Text2, wrapWidth: 900f, offsetY: -200f)
                .AddScrollableText(Application.GetString("PRERELEASE_TEXT"), Resources.Fnt.SmallFont, PopupAnchor.Text3, wrapWidth: 800f, scrollHeight: 400f, offsetY: -20f)
                .AddButton(Application.GetString("OK"), MenuButtonId.PopupOk);

            _ = builder.Show(template);
        }
    }
}
