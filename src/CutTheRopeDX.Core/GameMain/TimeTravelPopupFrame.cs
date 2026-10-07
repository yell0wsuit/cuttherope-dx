using System;

using CutTheRopeDX.Framework;
using CutTheRopeDX.Framework.Core;
using CutTheRopeDX.Framework.Visual;

namespace CutTheRopeDX.GameMain
{
    /// <summary>
    /// The Time Travel popup window, as iOS <c>CTRPopup</c> builds it: a top cap, a strip tiled
    /// down the middle and a bottom cap, with the round close button over the top cap's corner.
    /// It is drawn into the box the classic popup background fills, so every popup's content keeps
    /// the place it was composed for.
    /// </summary>
    internal static class TimeTravelPopupFrame
    {
        /// <summary>How much the close button grows while pressed, as iOS draws it.</summary>
        public const float ClosePressedScale = 1.2f;

        /// <summary>
        /// The box the classic popup background draws in: its art's rectangle on the sheet's
        /// canvas, scaled about the canvas middle as the element scales.
        /// </summary>
        /// <param name="background">The classic background, sized and scaled but not drawn.</param>
        /// <returns>The box, in the popup's design coordinates.</returns>
        public static Rectangle ClassicBox(Image background)
        {
            Vector offset = Image.GetQuadOffset(Resources.Img.MenuPopup, background.quadToDraw);
            Vector art = Image.GetQuadSize(Resources.Img.MenuPopup, background.quadToDraw);
            float centerX = background.x + (background.width >> 1);
            float centerY = background.y + (background.height >> 1);
            return new Rectangle(
                centerX + ((background.x + offset.X - centerX) * background.scaleX),
                centerY + ((background.y + offset.Y - centerY) * background.scaleY),
                art.X * background.scaleX,
                art.Y * background.scaleY);
        }

        /// <summary>Builds the window to fill a box.</summary>
        /// <param name="box">The box, in the popup's design coordinates.</param>
        /// <returns>The window, its caps scaled to the box's width.</returns>
        public static BaseElement Create(Rectangle box)
        {
            string sheet = Resources.Img.MenuPopupTimeTravel;
            BaseElement frame = new()
            {
                x = box.x,
                y = box.y,
                width = (int)MathF.Round(box.w),
                height = (int)MathF.Round(box.h),
            };
            frame.anchor = frame.parentAnchor = 9;
            frame.SetName("ttPopupFrame");

            float scale = box.w / Image.GetQuadSize(sheet, TimeTravelArt.PopupTop).X;
            Image top = Piece(Image.FromResource(sheet, TimeTravelArt.PopupTop), "ttPopupTop", scale, 0f);
            Image bottom = Image.FromResource(sheet, TimeTravelArt.PopupBottom);
            _ = Piece(bottom, "ttPopupBottom", scale, box.h - (bottom.height * scale));

            // The strip is tiled at the caps' scale through whatever height they leave between them.
            TiledImage strip = Image.InitializeFromResource(new TiledImage(), sheet, TimeTravelArt.PopupStrip);
            strip.SetTile(TimeTravelArt.PopupStrip);
            strip.height = (int)MathF.Ceiling((box.h - ((top.height + bottom.height) * scale)) / scale);
            _ = Piece(strip, "ttPopupStrip", scale, top.height * scale);

            _ = frame.AddChild(strip);
            _ = frame.AddChild(top);
            _ = frame.AddChild(bottom);
            return frame;
        }

        /// <summary>
        /// The round close button, placed where the iOS canvas puts it against the top cap of a
        /// window filling <paramref name="box"/>.
        /// </summary>
        /// <param name="box">The box the window fills.</param>
        /// <param name="id">The button the close button stands in for.</param>
        /// <param name="d">Delegate that receives the press.</param>
        /// <returns>The button.</returns>
        public static Button CreateCloseButton(Rectangle box, ButtonId id, IButtonDelegation d)
        {
            string sheet = Resources.Img.MenuPopupTimeTravel;
            float scale = box.w / Image.GetQuadSize(sheet, TimeTravelArt.PopupTop).X;
            BaseElement up = TimeTravelPlates.Plate(sheet, TimeTravelArt.PopupClose, scale);
            BaseElement down = new() { width = up.width, height = up.height };
            Image pressed = Image.FromResource(sheet, TimeTravelArt.PopupClose);
            pressed.anchor = pressed.parentAnchor = 18;
            pressed.scaleX = pressed.scaleY = scale * ClosePressedScale;
            _ = down.AddChild(pressed);

            Button close = new Button().InitWithUpElementDownElementandID(up, down, id);
            close.delegateButtonDelegate = d;
            close.SetName("ttPopupClose");
            close.anchor = close.parentAnchor = 9;
            Vector closeOffset = Image.GetQuadOffset(sheet, TimeTravelArt.PopupClose);
            Vector topOffset = Image.GetQuadOffset(sheet, TimeTravelArt.PopupTop);
            close.x = box.x + ((closeOffset.X - topOffset.X) * scale);
            close.y = box.y + ((closeOffset.Y - topOffset.Y) * scale);
            return close;
        }

        /// <summary>
        /// Scales a piece and places its scaled top left at the window's left edge and a height,
        /// taking back the drift scaling about its own middle gives it.
        /// </summary>
        private static Image Piece(Image piece, string name, float scale, float top)
        {
            piece.SetName(name);
            piece.anchor = piece.parentAnchor = 9;
            piece.scaleX = piece.scaleY = scale;
            piece.x = -((piece.width >> 1) * (1f - scale));
            piece.y = top - ((piece.height >> 1) * (1f - scale));
            return piece;
        }
    }
}
