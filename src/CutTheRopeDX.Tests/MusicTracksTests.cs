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
