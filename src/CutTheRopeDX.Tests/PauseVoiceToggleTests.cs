using System.Collections.Generic;

using CutTheRopeDX.Framework.Core;
using CutTheRopeDX.Framework.Visual;
using CutTheRopeDX.GameMain;

using Xunit;

namespace CutTheRopeDX.Tests
{
    /// <summary>
    /// Covers the voice toggle the Experiments menus add to the pause menu's audio row.
    /// </summary>
    public sealed class PauseVoiceToggleTests
    {
        [Fact]
        public void ExperimentsPauseMenuHasAVoiceToggle()
        {
            WithStyle(MenuStyle.Experiments, () =>
            {
                Texture2D audio = Application.GetTexture(Resources.Img.MenuExpAudio);
                int audioToggles = 0;
                foreach (ToggleButton toggle in Toggles(LoadController().GetView(0)))
                {
                    if (FirstImage(toggle)?.texture == audio)
                    {
                        audioToggles++;
                    }
                }
                Assert.Equal(3, audioToggles);
            });
        }

        [Fact]
        public void ClassicPauseMenuKeepsTwoAudioToggles()
        {
            WithStyle(MenuStyle.Classic, () => Assert.Equal(2, Toggles(LoadController().GetView(0)).Count));
        }

        [Fact]
        public void PauseVoiceToggleFlipsTheVoicePreference()
        {
            bool original = Preferences.GetBooleanForKey(ExperimentsVoice.PreferenceKey);
            try
            {
                WithStyle(MenuStyle.Experiments, () =>
                {
                    GameController controller = LoadController();
                    Preferences.SetBooleanForKey(true, ExperimentsVoice.PreferenceKey, false);
                    controller.OnButtonPressed(GameControllerButtonId.ToggleVoice);
                    Assert.False(Preferences.GetBooleanForKey(ExperimentsVoice.PreferenceKey));
                    controller.OnButtonPressed(GameControllerButtonId.ToggleVoice);
                    Assert.True(Preferences.GetBooleanForKey(ExperimentsVoice.PreferenceKey));
                });
            }
            finally
            {
                Preferences.SetBooleanForKey(original, ExperimentsVoice.PreferenceKey, false);
            }
        }

        private static GameController LoadController()
        {
            _ = HeadlessGame.Boot();
            return HeadlessGame.LoadLevelWithController(0, 1);
        }

        private static List<ToggleButton> Toggles(BaseElement root)
        {
            List<ToggleButton> found = [];
            foreach (BaseElement child in root.GetChilds().Values)
            {
                if (child is ToggleButton toggle)
                {
                    found.Add(toggle);
                }
                if (child != null)
                {
                    found.AddRange(Toggles(child));
                }
            }
            return found;
        }

        private static Image FirstImage(BaseElement root)
        {
            foreach (BaseElement child in root.GetChilds().Values)
            {
                if (child is Image image)
                {
                    return image;
                }
                Image nested = child == null ? null : FirstImage(child);
                if (nested != null)
                {
                    return nested;
                }
            }
            return null;
        }

        private static void WithStyle(MenuStyle style, System.Action body)
        {
            MenuStyle previous = MenuTheme.Current;
            MenuTheme.Current = style;
            try
            {
                body();
            }
            finally
            {
                MenuTheme.Current = previous;
            }
        }
    }
}
