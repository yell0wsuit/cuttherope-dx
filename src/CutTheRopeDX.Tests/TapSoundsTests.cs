using System.Collections.Generic;
using System.Xml.Linq;

using CutTheRopeDX.Framework.Core;
using CutTheRopeDX.GameMain;

using Xunit;

namespace CutTheRopeDX.Tests
{
    public sealed class TapSoundsTests
    {
        [Fact]
        public void TheClassicMenusPlayTheClassicTap()
        {
            WithStyle(MenuStyle.Classic, () =>
            {
                for (int i = 0; i < 50; i++)
                {
                    Assert.Equal(Resources.Snd.Tap, TapSounds.Next());
                }
            });
        }

        [Fact]
        public void TimeTravelPicksAtRandomBetweenItsTwoTaps()
        {
            WithStyle(MenuStyle.TimeTravel, () =>
            {
                HashSet<string> heard = [];
                for (int i = 0; i < 200; i++)
                {
                    _ = heard.Add(TapSounds.Next());
                }

                Assert.Equal([Resources.Snd.TapTimeTravel1, Resources.Snd.TapTimeTravel2], heard);
            });
        }

        [Fact]
        public void TheTimeTravelMenuPackLoadsBothTaps()
        {
            WithStyle(MenuStyle.TimeTravel, () =>
            {
                Assert.Contains(Resources.Snd.TapTimeTravel1, RootController.PackMenu);
                Assert.Contains(Resources.Snd.TapTimeTravel2, RootController.PackMenu);
            });
        }

        [Fact]
        public void TheTimeTravelMenuPackLoadsTheLevelClick()
        {
            WithStyle(MenuStyle.TimeTravel, () => Assert.Contains(Resources.Snd.LevelIconTimeTravel, RootController.PackMenu));
        }

        [Fact]
        public void TimeTravelLevelsLoadBothTaps()
        {
            WithStyle(MenuStyle.TimeTravel, () =>
            {
                string[] resources = LevelResourceScanner.GetRequiredResources(XElement.Parse("<map><gameDesign /></map>"), 0);

                Assert.Contains(Resources.Snd.TapTimeTravel1, resources);
                Assert.Contains(Resources.Snd.TapTimeTravel2, resources);
            });
        }

        /// <summary>Runs <paramref name="body"/> under a menu style, restoring the previous one.</summary>
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
