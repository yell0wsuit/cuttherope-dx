using System;

using CutTheRopeDX.Framework.Core;
using CutTheRopeDX.Framework.Visual;
using CutTheRopeDX.GameMain;

using Xunit;

namespace CutTheRopeDX.Tests
{
    public sealed class TimeTravelLogoTests
    {
        private sealed class NoDelegate : IButtonDelegation
        {
            public void OnButtonPressed(ButtonId buttonId)
            {
            }
        }

        [Fact]
        public void PlaysTheIntroThenWaitsFiveToTwentyFiveSecondsForTheIdle()
        {
            MenuTimeTravelTests.WithTimeTravel(2560, 1440, _ =>
            {
                TimeTravelLogo logo = TimeTravelLogo.Create(new Random(7));
                Assert.Equal(TimeTravelLogo.IntroTimeline, logo.PlayingTimeline);

                for (int i = 0; i < 120 && logo.PlayingTimeline == TimeTravelLogo.IntroTimeline; i++)
                {
                    logo.Update(1f / 60f);
                }

                Assert.Equal(-1, logo.PlayingTimeline);
                Assert.InRange(logo.IdleDelay, 5f, 25f);
            });
        }

        [Fact]
        public void TheIdleRepeatsAfterEachWait()
        {
            MenuTimeTravelTests.WithTimeTravel(2560, 1440, _ =>
            {
                TimeTravelLogo logo = TimeTravelLogo.Create(new Random(1));
                int idleStarts = 0;
                int previous = logo.PlayingTimeline;
                for (int i = 0; i < 60 * 120; i++)
                {
                    logo.Update(1f / 60f);
                    if (logo.PlayingTimeline == TimeTravelLogo.IdleTimeline && previous != TimeTravelLogo.IdleTimeline)
                    {
                        idleStarts++;
                    }
                    previous = logo.PlayingTimeline;
                }

                // 120 s holds at least 120 / (25 + 3.2) = 4 idles and at most 120 / (5 + 3.1) = 14.
                Assert.InRange(idleStarts, 4, 14);
            });
        }

        [Fact]
        public void SizesItselfToTheLogoArt()
        {
            MenuTimeTravelTests.WithTimeTravel(2560, 1440, _ =>
            {
                TimeTravelLogo logo = TimeTravelLogo.Create(new Random(0));
                Texture2D art = Application.GetTexture(Resources.Img.MenuLogoNew);

                Assert.Equal((int)art.quadRects[TimeTravelLogo.ArtQuad].w, logo.width);
                Assert.Equal((int)art.quadRects[TimeTravelLogo.ArtQuad].h, logo.height);
            });
        }

        [Fact]
        public void CarriesTheCandySelectButtonOnlyWhenAsked()
        {
            MenuTimeTravelTests.WithTimeTravel(2560, 1440, _ =>
            {
                TimeTravelLogo plain = TimeTravelLogo.Create(new Random(0));
                TimeTravelLogo withCandy = TimeTravelLogo.Create(new Random(0), new NoDelegate());

                Assert.Null(MenuTimeTravelTests.All<Button>(plain).Find(b => b.buttonID == MenuButtonId.CandySelect));
                Assert.NotNull(MenuTimeTravelTests.All<Button>(withCandy).Find(b => b.buttonID == MenuButtonId.CandySelect));
            });
        }
    }
}
