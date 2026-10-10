using CutTheRopeDX.Commons;
using CutTheRopeDX.Framework;
using CutTheRopeDX.Framework.Core;
using CutTheRopeDX.Framework.Visual;
using CutTheRopeDX.GameMain;

using Xunit;

using static CutTheRopeDX.Commons.PopupBuilder;

namespace CutTheRopeDX.Tests
{
    public sealed class TimeTravelPopupTests
    {
        private static Popup ShowPopup(MenuController controller, PopupSize size)
        {
            controller.ShowView(MenuController.VIEW_MAIN_MENU);
            PopupTemplate template = PopupTemplate.Create(size)
                .WithScaleMode(PopupScaleMode.Background)
                .AddText("Text", Resources.Fnt.BigFont, PopupAnchor.Text2)
                .AddButton("Yes", MenuButtonId.QuitGame)
                .AddButton("No", MenuButtonId.ClosePopup);
            Popup popup = new PopupBuilder(controller).Show(template);
            Settle(popup);
            return popup;
        }

        /// <summary>Plays the show animation out and resolves every drawn position.</summary>
        private static void Settle(Popup popup)
        {
            for (int i = 0; i < 60; i++)
            {
                popup.Update(1f / 60f);
            }
            MenuTimeTravelTests.ResolveDrawPositions(popup);
        }

        /// <summary>The box the classic popup background draws in, for the same template.</summary>
        private static Rectangle ClassicBox(MenuController controller, PopupSize size)
        {
            MenuStyle previous = MenuTheme.Current;
            MenuTheme.Current = MenuStyle.Classic;
            try
            {
                Popup popup = ShowPopup(controller, size);
                Texture2D sheet = Application.GetTexture(Resources.Img.MenuPopup);
                Image background = MenuTimeTravelTests.All<Image>(popup).Find(i => i.texture == sheet && i.quadToDraw == 0);
                Assert.NotNull(background);
                Vector offset = Image.GetQuadOffset(Resources.Img.MenuPopup, 0);
                Vector art = Image.GetQuadSize(Resources.Img.MenuPopup, 0);
                Rectangle drawn = MenuTimeTravelTests.DrawnBox(background);
                float scale = drawn.w / background.width;
                popup.parent?.RemoveChild(popup);
                return new Rectangle(drawn.x + (offset.X * scale), drawn.y + (offset.Y * scale), art.X * scale, art.Y * scale);
            }
            finally
            {
                MenuTheme.Current = previous;
            }
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(2)]
        public void TheFrameFillsTheBoxTheClassicPopupDrawsIn(int sizeIndex)
        {
            PopupSize size = (PopupSize)sizeIndex;
            MenuTimeTravelTests.WithTimeTravel(2560, 1440, controller =>
            {
                Rectangle classic = ClassicBox(controller, size);
                Popup popup = ShowPopup(controller, size);
                Rectangle frame = MenuTimeTravelTests.DrawnBox(popup.GetChildWithName("ttPopupFrame"));

                Assert.InRange(frame.x - classic.x, -1f, 1f);
                Assert.InRange(frame.y - classic.y, -1f, 1f);
                Assert.InRange(frame.w - classic.w, -1f, 1f);
                Assert.InRange(frame.h - classic.h, -1f, 1f);
                Texture2D sheet = Application.GetTexture(Resources.Img.MenuPopup);
                Assert.Null(MenuTimeTravelTests.All<Image>(popup).Find(i => i.texture == sheet && i.quadToDraw == 0));
            });
        }

        [Fact]
        public void TheFramePiecesStackWithoutGapsAcrossTheWholeWidth()
        {
            MenuTimeTravelTests.WithTimeTravel(2560, 1440, controller =>
            {
                Popup popup = ShowPopup(controller, PopupSize.Large);
                Rectangle frame = MenuTimeTravelTests.DrawnBox(popup.GetChildWithName("ttPopupFrame"));
                Rectangle top = MenuTimeTravelTests.DrawnBox(popup.GetChildWithName("ttPopupTop"));
                Rectangle strip = MenuTimeTravelTests.DrawnBox(popup.GetChildWithName("ttPopupStrip"));
                Rectangle bottom = MenuTimeTravelTests.DrawnBox(popup.GetChildWithName("ttPopupBottom"));

                Assert.InRange(top.y - frame.y, -0.5f, 0.5f);
                Assert.InRange(strip.y - (top.y + top.h), -0.5f, 0.5f);
                Assert.InRange(bottom.y - (strip.y + strip.h), -0.5f, 0.5f);
                Assert.InRange(frame.y + frame.h - (bottom.y + bottom.h), -0.5f, 0.5f);
                foreach (Rectangle piece in new[] { top, strip, bottom })
                {
                    Assert.InRange(piece.x - frame.x, -0.5f, 0.5f);
                    Assert.InRange(piece.w - frame.w, -1f, 1f);
                }
            });
        }

        [Fact]
        public void TheCloseButtonPressesThePopupsWayOut()
        {
            MenuTimeTravelTests.WithTimeTravel(2560, 1440, controller =>
            {
                Popup popup = ShowPopup(controller, PopupSize.Normal);
                Button close = (Button)popup.GetChildWithName("ttPopupClose");

                Assert.Equal(MenuButtonId.ClosePopup, close.buttonID);
                Assert.Same(controller, close.delegateButtonDelegate);
            });
        }

        [Fact]
        public void TheCloseButtonSitsOnTheTopCapsCornerAsTheCanvasPlacesIt()
        {
            MenuTimeTravelTests.WithTimeTravel(2560, 1440, controller =>
            {
                Popup popup = ShowPopup(controller, PopupSize.Normal);
                Rectangle top = MenuTimeTravelTests.DrawnBox(popup.GetChildWithName("ttPopupTop"));
                Rectangle close = MenuTimeTravelTests.DrawnBox(popup.GetChildWithName("ttPopupClose"));
                string sheet = Resources.Img.MenuPopupTimeTravel;
                float scale = top.w / Image.GetQuadSize(sheet, TimeTravelArt.PopupTop).X;
                Vector closeOffset = Image.GetQuadOffset(sheet, TimeTravelArt.PopupClose);
                Vector topOffset = Image.GetQuadOffset(sheet, TimeTravelArt.PopupTop);

                Assert.InRange(close.x - (top.x + ((closeOffset.X - topOffset.X) * scale)), -1f, 1f);
                Assert.InRange(close.y - (top.y + ((closeOffset.Y - topOffset.Y) * scale)), -1f, 1f);
                Assert.InRange(close.w - (Image.GetQuadSize(sheet, TimeTravelArt.PopupClose).X * scale), -1f, 1f);
            });
        }

        [Fact]
        public void TheClassicPopupKeepsItsBackgroundAndHasNoCloseButton()
        {
            MenuTimeTravelTests.WithTimeTravel(2560, 1440, controller =>
            {
                MenuTheme.Current = MenuStyle.Classic;
                Popup popup = ShowPopup(controller, PopupSize.Normal);
                Texture2D sheet = Application.GetTexture(Resources.Img.MenuPopup);

                Assert.NotNull(MenuTimeTravelTests.All<Image>(popup).Find(i => i.texture == sheet && i.quadToDraw == 0));
                Assert.Null(popup.GetChildWithName("ttPopupFrame"));
                Assert.Null(popup.GetChildWithName("ttPopupClose"));
            });
        }

        [Fact]
        public void TheTimeTravelMenuPackLoadsThePopupSheet()
        {
            MenuStyle previous = MenuTheme.Current;
            MenuTheme.Current = MenuStyle.TimeTravel;
            try
            {
                Assert.Contains(Resources.Img.MenuPopupTimeTravel, RootController.PackMenu);
            }
            finally
            {
                MenuTheme.Current = previous;
            }
        }

        [Fact]
        public void ATimeTravelPopupOvershootsWideButNotTall()
        {
            MenuTimeTravelTests.WithTimeTravel(2560, 1440, _ =>
            {
                // iOS CTRPopup: 0 -> (1.1, 1.0) over 0.3 s, then 0.9, then 1.
                Popup popup = new();
                popup.ShowPopup();
                popup.Update(0.3f);

                Assert.Equal(1.1f, popup.scaleX, 2);
                Assert.Equal(1f, popup.scaleY, 2);
            });
        }

        [Fact]
        public void AClassicPopupOvershootsEvenly()
        {
            MenuTimeTravelTests.WithTimeTravel(2560, 1440, _ =>
            {
                MenuTheme.Current = MenuStyle.Classic;
                Popup popup = new();
                popup.ShowPopup();
                popup.Update(0.3f);

                Assert.Equal(1.1f, popup.scaleX, 2);
                Assert.Equal(1.1f, popup.scaleY, 2);
            });
        }
    }
}
