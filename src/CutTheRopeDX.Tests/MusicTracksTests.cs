using CutTheRopeDX.GameMain;

using Xunit;

namespace CutTheRopeDX.Tests
{
    public sealed class MusicTracksTests
    {
        [Fact]
        public void ExperimentsMenusPlayTheExperimentsMenuTrack()
        {
            WithStyle(MenuStyle.Experiments, () =>
                Assert.Equal(Resources.Music.MenuMusicExp, MusicTracks.Menu()));
        }

        [Fact]
        public void ExperimentsGameplayPlaysOnlyTheExperimentsTrackWhateverThePack()
        {
            WithStyle(MenuStyle.Experiments, () =>
            {
                for (int pack = 0; pack < PackConfig.PackCount; pack++)
                {
                    Assert.Equal([Resources.Music.GameMusicExp], MusicTracks.Game(pack));
                }
            });
        }

        [Fact]
        public void HalloweenMenusPlayTheHalloweenMenuTrack()
        {
            WithStyle(MenuStyle.Classic, () => SeasonalDate.With(SeasonalDate.Halloween, () =>
                Assert.Equal(Resources.Music.MenuMusicHalloween, MusicTracks.Menu())));
        }

        [Fact]
        public void HalloweenGameplayKeepsEachPacksOwnMusic()
        {
            WithStyle(MenuStyle.Classic, () =>
            {
                for (int pack = 0; pack < PackConfig.PackCount; pack++)
                {
                    string[] usual = null;
                    SeasonalDate.With(SeasonalDate.NoEvent, () => usual = MusicTracks.Game(pack));
                    SeasonalDate.With(SeasonalDate.Halloween, () => Assert.Equal(usual, MusicTracks.Game(pack)));
                }
            });
        }

        [Fact]
        public void ExperimentsMenusKeepTheirTrackDuringHalloween()
        {
            WithStyle(MenuStyle.Experiments, () => SeasonalDate.With(SeasonalDate.Halloween, () =>
                Assert.Equal(Resources.Music.MenuMusicExp, MusicTracks.Menu())));
        }

        [Fact]
        public void OutsideEventsTheMenusPlayTheDefaultTrack()
        {
            WithStyle(MenuStyle.Classic, () => SeasonalDate.With(SeasonalDate.NoEvent, () =>
                Assert.Equal(Resources.Music.MenuMusic, MusicTracks.Menu())));
        }

        /// <summary>Runs <paramref name="body"/> under a menu style, restoring the previous one.</summary>
        /// <param name="style">Menu style to apply.</param>
        /// <param name="body">Checks to run.</param>
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
