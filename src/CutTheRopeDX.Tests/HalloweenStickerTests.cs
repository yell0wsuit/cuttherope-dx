using System;

using CutTheRopeDX.Framework;
using CutTheRopeDX.Framework.Core;
using CutTheRopeDX.Framework.Visual;
using CutTheRopeDX.GameMain;

using Xunit;

namespace CutTheRopeDX.Tests
{
    /// <summary>
    /// Covers the costumed Om Nom sticker left next to the result panel once the Halloween bats
    /// have gone.
    /// </summary>
    public sealed class HalloweenStickerTests
    {
        private static readonly Rectangle Panel = new(800f, 250f, 950f, 1040f);

        [Theory]
        [InlineData(48f, 48f, 2464f, 1344f)]
        [InlineData(760f, -500f, 1030f, 2440f)]
        public void PlacementStaysClearOfThePanelAndInsideTheArea(float ax, float ay, float aw, float ah)
        {
            Rectangle area = new(ax, ay, aw, ah);
            Random random = new(1);
            for (int i = 0; i < 2000; i++)
            {
                float tilt = (4f + (11f * random.NextSingle())) * (random.Next(2) == 0 ? -1f : 1f);
                float width = 300f + (200f * random.NextSingle());
                float height = 300f + (100f * random.NextSingle());
                Assert.True(HalloweenSticker.TryPlace(Panel, area, width, height, tilt, out float x, out float y, out float scale));
                AssertClearOfPanel(Panel, area, x, y, width * scale, height * scale, tilt);
            }
        }

        [Fact]
        public void OnATallScreenTheStickerGoesAboveOrBelowThePanel()
        {
            // As wide as the panel plus a little: no room at either side, plenty above and below.
            Rectangle tall = new(760f, -500f, 1030f, 2440f);
            Assert.True(HalloweenSticker.TryPlace(Panel, tall, 482f, 300f, 10f, out _, out float y, out float scale));
            Assert.True(y < Panel.y || y > Panel.y + Panel.h);
            Assert.InRange(scale, HalloweenSticker.MinSizeShare, 1f);
        }

        [Fact]
        public void NoCostumeShowsTwiceInARow()
        {
            int previous = HalloweenSticker.NextCostume();
            bool[] seen = new bool[HalloweenSticker.Costumes];
            for (int i = 0; i < 500; i++)
            {
                int costume = HalloweenSticker.NextCostume();
                Assert.InRange(costume, 0, HalloweenSticker.Costumes - 1);
                Assert.NotEqual(previous, costume);
                seen[costume] = true;
                previous = costume;
            }
            Assert.All(seen, Assert.True);
        }

        [Fact]
        public void EachStickerRollsItsOwnSize()
        {
            Rectangle roomy = new(48f, 48f, 2464f, 1344f);
            float smallest = float.MaxValue;
            float largest = 0f;
            for (int i = 0; i < 500; i++)
            {
                Assert.True(HalloweenSticker.TryPlace(Panel, roomy, 400f, 350f, 8f, out _, out _, out float scale));
                smallest = MathF.Min(smallest, scale);
                largest = MathF.Max(largest, scale);
            }
            Assert.InRange(smallest, HalloweenSticker.MinSizeShare, 0.85f);
            Assert.InRange(largest, 0.95f, 1f);
        }

        [Fact]
        public void NoStickerWhenNoSideHasRoom()
        {
            Rectangle hugging = new(760f, 210f, 1030f, 1120f);
            Assert.False(HalloweenSticker.TryPlace(Panel, hugging, 482f, 300f, 10f, out _, out _, out _));
        }

        [Fact]
        public void ATightSideShrinksTheStickerToFit()
        {
            // 400 units free on the left only: a 482-wide sticker has to shrink.
            Rectangle area = new(360f, 48f, 1430f, 1344f);
            Assert.True(HalloweenSticker.TryPlace(Panel, area, 482f, 300f, 5f, out float x, out _, out float scale));
            Assert.True(x < Panel.x);
            Assert.InRange(scale, HalloweenSticker.MinFitScale, 0.99f);
        }

        [Theory]
        [InlineData(2560, 1440)]
        [InlineData(720, 1280)]
        [InlineData(320, 480)]
        public void StickerArrivesOnlyOnceTheLastBatHasGone(int width, int height)
        {
            // Booting resets the surface, so the size goes on between boot and load.
            _ = HeadlessGame.Boot();
            LayoutSurfaces.WithSurface(width, height, () => SeasonalDate.With(SeasonalDate.Halloween, () =>
            {
                MenuStyle previous = MenuTheme.Current;
                MenuTheme.Current = MenuStyle.Classic;
                try
                {
                    GameController controller = HeadlessGame.LoadLevelWithController(1, 4);
                    BoxOpenClose box = (BoxOpenClose)controller.GetView(0).GetChild(4);
                    box.confettiAnims.RemoveAllChilds();
                    Rectangle panel = DesignExtent.Measure(box.result);
                    Assert.InRange(panel.w, 500f, 1600f);
                    box.ShowConfetti();
                    Texture2D stickers = Application.GetTexture(Resources.Img.MenuResultScreenHalloween);

                    Tick(box.confettiAnims, 3.2f);
                    Assert.Null(FindSticker(box.confettiAnims, stickers));

                    Tick(box.confettiAnims, 1f);
                    Image sticker = FindSticker(box.confettiAnims, stickers);
                    Assert.NotNull(sticker);
                    Assert.InRange(MathF.Abs(sticker.rotation), HalloweenSticker.MinTilt, HalloweenSticker.MaxTilt);
                    Assert.Equal(1f, sticker.color.AlphaChannel, 3);
                    Assert.InRange(sticker.scaleX, HalloweenSticker.MinFitScale, 1f);
                    AssertClearOfPanel(panel, null, sticker.x, sticker.y, sticker.width * sticker.scaleX, sticker.height * sticker.scaleY, sticker.rotation);
                }
                finally
                {
                    MenuTheme.Current = previous;
                }
            }));
        }

        [Fact]
        public void UnderThreeStarsTheStickerComesWithoutBats()
        {
            _ = HeadlessGame.Boot();
            SeasonalDate.With(SeasonalDate.Halloween, () =>
            {
                MenuStyle previous = MenuTheme.Current;
                MenuTheme.Current = MenuStyle.Classic;
                try
                {
                    GameController controller = HeadlessGame.LoadLevelWithController(1, 4);
                    BoxOpenClose box = (BoxOpenClose)controller.GetView(0).GetChild(4);
                    box.confettiAnims.RemoveAllChilds();
                    box.shouldShowConfetti = false;
                    box.PostBoxClosed();
                    Texture2D stickers = Application.GetTexture(Resources.Img.MenuResultScreenHalloween);

                    Assert.Equal(1, box.confettiAnims.ChildsCount());
                    Assert.IsNotType<HalloweenBatSwarm>(box.confettiAnims.GetChild(0));
                    Tick(box.confettiAnims, 0.5f);
                    Image sticker = FindSticker(box.confettiAnims, stickers);
                    Assert.NotNull(sticker);
                    Assert.Equal(1f, sticker.color.AlphaChannel, 3);
                }
                finally
                {
                    MenuTheme.Current = previous;
                }
            });
        }

        [Fact]
        public void OutsideHalloweenAResultUnderThreeStarsGetsNothing()
        {
            _ = HeadlessGame.Boot();
            SeasonalDate.With(SeasonalDate.NoEvent, () =>
            {
                GameController controller = HeadlessGame.LoadLevelWithController(1, 4);
                BoxOpenClose box = (BoxOpenClose)controller.GetView(0).GetChild(4);
                box.confettiAnims.RemoveAllChilds();
                box.shouldShowConfetti = false;
                box.PostBoxClosed();
                Assert.Equal(0, box.confettiAnims.ChildsCount());
            });
        }

        private static void AssertClearOfPanel(Rectangle panel, Rectangle? area, float x, float y, float width, float height, float tilt)
        {
            (float halfW, float halfH) = HalfBounds(width, height, tilt);
            float left = x - halfW;
            float right = x + halfW;
            float top = y - halfH;
            float bottom = y + halfH;
            if (area is Rectangle a)
            {
                Assert.True(left >= a.x - 0.01f && right <= a.x + a.w + 0.01f, $"sticker {left}..{right} leaves the area");
                Assert.True(top >= a.y - 0.01f && bottom <= a.y + a.h + 0.01f, $"sticker {top}..{bottom} leaves the area");
            }
            float gap = HalloweenSticker.PanelGap - 0.01f;
            bool clear = right <= panel.x - gap
                || left >= panel.x + panel.w + gap
                || bottom <= panel.y - gap
                || top >= panel.y + panel.h + gap;
            Assert.True(clear, $"sticker ({left}, {top})..({right}, {bottom}) overlaps the panel");
        }

        private static (float HalfW, float HalfH) HalfBounds(float width, float height, float tilt)
        {
            float radians = tilt * MathF.PI / 180f;
            float cos = MathF.Abs(MathF.Cos(radians));
            float sin = MathF.Abs(MathF.Sin(radians));
            return (((width * cos) + (height * sin)) / 2f, ((width * sin) + (height * cos)) / 2f);
        }

        private static Image FindSticker(BaseElement container, Texture2D stickers)
        {
            for (int i = 0, seen = 0; seen < container.ChildsCount(); i++)
            {
                BaseElement child = container.GetChild(i);
                if (child == null)
                {
                    continue;
                }
                seen++;
                if (child is Image image && image.texture == stickers)
                {
                    return image;
                }
            }
            return null;
        }

        private static void Tick(BaseElement element, float seconds)
        {
            const float step = 1f / 60f;
            for (float t = 0f; t < seconds; t += step)
            {
                element.Update(step);
            }
        }
    }
}
