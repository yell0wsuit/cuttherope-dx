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

        [Theory]
        [InlineData(3, 0, 90f, 0f)]
        [InlineData(6, 30, 195f, 180f)]
        [InlineData(15, 45, 112.5f, 270f)]
        [InlineData(0, 0, 0f, 0f)]
        [InlineData(12, 0, 0f, 0f)]
        public void TheHandsShowTheTime(int hour, int minute, float hourAngle, float minuteAngle)
        {
            MenuTimeTravelTests.WithTimeTravel(2560, 1440, _ =>
            {
                TimeTravelLogo logo = TimeTravelLogo.Create(new Random(0), now: () => new DateTime(2026, 10, 6, hour, minute, 0));
                logo.Update(1f / 60f);

                AssertPointsAt(hourAngle, HourHand(logo), TimeTravelLogo.HourHandArtAngle);
                AssertPointsAt(minuteAngle, MinuteHand(logo), TimeTravelLogo.MinuteHandArtAngle);
            });
        }

        [Fact]
        public void TheHandsPointAtTheTimeBeforeTheFirstUpdate()
        {
            MenuTimeTravelTests.WithTimeTravel(2560, 1440, _ =>
            {
                TimeTravelLogo logo = TimeTravelLogo.Create(new Random(0), now: () => new DateTime(2026, 10, 6, 9, 0, 0));

                AssertPointsAt(270f, HourHand(logo), TimeTravelLogo.HourHandArtAngle);
                AssertPointsAt(0f, MinuteHand(logo), TimeTravelLogo.MinuteHandArtAngle);
            });
        }

        [Fact]
        public void TheHandsSweepAsTheClockRuns()
        {
            MenuTimeTravelTests.WithTimeTravel(2560, 1440, _ =>
            {
                DateTime time = new(2026, 10, 6, 1, 10, 0);
                TimeTravelLogo logo = TimeTravelLogo.Create(new Random(0), now: () => time);
                logo.Update(1f / 60f);
                AssertPointsAt(35f, HourHand(logo), TimeTravelLogo.HourHandArtAngle);
                AssertPointsAt(60f, MinuteHand(logo), TimeTravelLogo.MinuteHandArtAngle);

                time = time.AddMinutes(12).AddSeconds(30);
                logo.Update(1f / 60f);

                AssertPointsAt(41.25f, HourHand(logo), TimeTravelLogo.HourHandArtAngle);
                AssertPointsAt(135f, MinuteHand(logo), TimeTravelLogo.MinuteHandArtAngle);
            });
        }

        [Fact]
        public void EachHandIsTuckedUnderTheHubAlongItsLength()
        {
            MenuTimeTravelTests.WithTimeTravel(2560, 1440, _ =>
            {
                TimeTravelLogo logo = TimeTravelLogo.Create(new Random(0), now: () => new DateTime(2026, 10, 6, 10, 10, 0));
                logo.Update(1f / 60f);

                AssertTucked(HourHand(logo), TimeTravelLogo.HourHandArtAngle);
                AssertTucked(MinuteHand(logo), TimeTravelLogo.MinuteHandArtAngle);
            });
        }

        /// <summary>Asserts a hand is shifted toward the hub along its art's axis by the hub overlap.</summary>
        private static void AssertTucked(Image hand, float artAngle)
        {
            float radians = artAngle * MathF.PI / 180f;
            float alongX = MathF.Sin(radians);
            float alongY = -MathF.Cos(radians);
            float along = (hand.translateX * alongX) + (hand.translateY * alongY);
            float across = (hand.translateX * -alongY) + (hand.translateY * alongX);

            Assert.InRange(along, -TimeTravelLogo.HubOverlap - 0.001f, -TimeTravelLogo.HubOverlap + 0.001f);
            Assert.InRange(across, -0.001f, 0.001f);
            Assert.True(TimeTravelLogo.HubOverlap > 0f);
        }

        private static Image HourHand(TimeTravelLogo logo)
        {
            return ClockPart(logo, 1);
        }

        private static Image MinuteHand(TimeTravelLogo logo)
        {
            return ClockPart(logo, 2);
        }

        private static Image ClockPart(TimeTravelLogo logo, int quad)
        {
            Texture2D sheet = Application.GetTexture(Resources.Img.LogoClockTimeTravel);
            return MenuTimeTravelTests.All<Image>(logo).Find(i => i.texture == sheet && i.quadToDraw == quad);
        }

        /// <summary>Asserts a hand drawn at <paramref name="artAngle"/> now points at a clock angle.</summary>
        private static void AssertPointsAt(float expected, Image hand, float artAngle)
        {
            float off = ((artAngle + hand.rotation - expected) % 360f + 540f) % 360f - 180f;
            Assert.InRange(off, -0.01f, 0.01f);
        }
    }
}
