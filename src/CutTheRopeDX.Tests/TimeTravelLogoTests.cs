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

        [Theory]
        [InlineData(3, 45)]
        [InlineData(6, 0)]
        [InlineData(9, 15)]
        [InlineData(12, 30)]
        [InlineData(7, 40)]
        [InlineData(4, 20)]
        public void EachHandTurnsAboutTheHubWithItsBaseTuckedUnder(int hour, int minute)
        {
            MenuTimeTravelTests.WithTimeTravel(2560, 1440, _ =>
            {
                TimeTravelLogo logo = TimeTravelLogo.Create(new Random(0), now: () => new DateTime(2026, 10, 6, hour, minute, 0));
                RunIntro(logo);

                AssertBaseOnHub(logo, HourHand(logo), TimeTravelLogo.HourHandArtAngle, TimeTravelLogo.HourHandCenterlineOffset);
                AssertBaseOnHub(logo, MinuteHand(logo), TimeTravelLogo.MinuteHandArtAngle, TimeTravelLogo.MinuteHandCenterlineOffset);
            });
        }

        [Theory]
        [InlineData(3, 45)]
        [InlineData(6, 0)]
        [InlineData(9, 15)]
        [InlineData(12, 30)]
        [InlineData(7, 40)]
        [InlineData(10, 10)]
        [InlineData(4, 20)]
        public void EachHandsThickEdgeFacesAwayFromTheLight(int hour, int minute)
        {
            MenuTimeTravelTests.WithTimeTravel(2560, 1440, _ =>
            {
                TimeTravelLogo logo = TimeTravelLogo.Create(new Random(0), now: () => new DateTime(2026, 10, 6, hour, minute, 0));
                RunIntro(logo);

                AssertThickEdgeShaded(HourHand(logo), TimeTravelLogo.HourHandArtAngle);
                AssertThickEdgeShaded(MinuteHand(logo), TimeTravelLogo.MinuteHandArtAngle);
            });
        }

        [Fact]
        public void AMirroredHandKeepsItsSizeThroughTheTimelines()
        {
            MenuTimeTravelTests.WithTimeTravel(2560, 1440, _ =>
            {
                // 7:40 points both hands down and left, where both are mirrored.
                TimeTravelLogo logo = TimeTravelLogo.Create(new Random(0), now: () => new DateTime(2026, 10, 6, 7, 40, 0));
                bool idlePlayed = false;
                for (int i = 0; i < 60 * 40 && !(idlePlayed && logo.PlayingTimeline == -1); i++)
                {
                    logo.Update(1f / 60f);
                    idlePlayed |= logo.PlayingTimeline == TimeTravelLogo.IdleTimeline;
                }

                Assert.True(idlePlayed);
                foreach (Image hand in new[] { HourHand(logo), MinuteHand(logo) })
                {
                    Assert.Equal(-1f, hand.scaleX, 3);
                    Assert.Equal(1f, hand.scaleY, 3);
                }
                AssertBaseOnHub(logo, MinuteHand(logo), TimeTravelLogo.MinuteHandArtAngle, TimeTravelLogo.MinuteHandCenterlineOffset);
            });
        }

        private static void RunIntro(TimeTravelLogo logo)
        {
            for (int i = 0; i < 120 && logo.PlayingTimeline == TimeTravelLogo.IntroTimeline; i++)
            {
                logo.Update(1f / 60f);
            }
        }

        /// <summary>
        /// Asserts the point on a hand's centerline <see cref="TimeTravelLogo.HubOverlap"/> out from
        /// the hub is drawn on the hub's center, transformed the way the element draws it.
        /// </summary>
        private static void AssertBaseOnHub(TimeTravelLogo logo, Image hand, float artAngle, float centerlineOffset)
        {
            Image hub = ClockPart(logo, 0);
            float centerX = hub.x + (hub.width / 2f);
            float centerY = hub.y + (hub.height / 2f);
            float art = artAngle * MathF.PI / 180f;

            // Along the art's axis, and across it toward its clockwise side.
            float pointX = centerX + (MathF.Sin(art) * TimeTravelLogo.HubOverlap) + (MathF.Cos(art) * centerlineOffset);
            float pointY = centerY - (MathF.Cos(art) * TimeTravelLogo.HubOverlap) + (MathF.Sin(art) * centerlineOffset);
            float pivotX = hand.x + (hand.width >> 1) + hand.rotationCenterX;
            float pivotY = hand.y + (hand.height >> 1) + hand.rotationCenterY;
            float localX = (pointX + hand.translateX - pivotX) * hand.scaleX;
            float localY = (pointY + hand.translateY - pivotY) * hand.scaleY;
            float turn = hand.rotation * MathF.PI / 180f;
            float drawnX = pivotX + (localX * MathF.Cos(turn)) - (localY * MathF.Sin(turn));
            float drawnY = pivotY + (localX * MathF.Sin(turn)) + (localY * MathF.Cos(turn));

            Assert.Equal(1f, MathF.Abs(hand.scaleX), 3);
            Assert.InRange(drawnX - centerX, -0.01f, 0.01f);
            Assert.InRange(drawnY - centerY, -0.01f, 0.01f);
        }

        /// <summary>
        /// Asserts a hand's thick edge, drawn on its art's clockwise side, is turned no more than a
        /// right angle from the way shadows fall.
        /// </summary>
        private static void AssertThickEdgeShaded(Image hand, float artAngle)
        {
            float edge = DrawnAngle(hand, artAngle) + (float.IsNegative(hand.scaleX) ? -90f : 90f);
            float toShadow = (edge - TimeTravelLogo.ShadowAngle) * MathF.PI / 180f;
            Assert.True(MathF.Cos(toShadow) >= -0.001f, $"thick edge at {edge % 360f} faces the light");
        }

        /// <summary>The clock angle a hand points at as drawn: a mirrored art points the other way.</summary>
        private static float DrawnAngle(Image hand, float artAngle)
        {
            return (float.IsNegative(hand.scaleX) ? -artAngle : artAngle) + hand.rotation;
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
            return MenuTimeTravelTests.All<Image>(logo).Find(i => i is FlashXmlImage && i.texture == sheet && i.quadToDraw == quad);
        }

        /// <summary>Asserts a hand drawn at <paramref name="artAngle"/> now points at a clock angle.</summary>
        private static void AssertPointsAt(float expected, Image hand, float artAngle)
        {
            float off = ((((DrawnAngle(hand, artAngle) - expected) % 360f) + 540f) % 360f) - 180f;
            Assert.InRange(off, -0.01f, 0.01f);
        }
    }
}
