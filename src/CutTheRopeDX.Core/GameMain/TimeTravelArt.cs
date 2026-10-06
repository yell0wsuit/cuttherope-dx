using System;

using CutTheRopeDX.Framework.Core;
using CutTheRopeDX.Framework.Visual;

namespace CutTheRopeDX.GameMain
{
    /// <summary>
    /// Where the Time Travel menu art lives: DX quad indices in the packed sheets, each named for the
    /// iOS quad it was packed from, and the offsets that turn an iOS canvas position into a
    /// position in a packed sheet.
    /// </summary>
    internal static class TimeTravelArt
    {
        /// <summary>Scale the sheets were packed at, from iOS canvas pixels to asset pixels.</summary>
        public const float CanvasToAsset = 0.78f;

        /// <summary>The clock part of the Time Travel logo animation.</summary>
        public const string LogoAnimationXml = "logo_ani_timetravel.xml";

        /// <summary>
        /// Where the iOS canvas origin sits in the main-menu sheet: split onto a canvas grown by 84
        /// iOS pixels upward, so its top-row sprites keep their overhang.
        /// </summary>
        public const float MenuMainOriginY = 84f * CanvasToAsset;

        /// <summary>Main menu: the big Play button (iOS q19).</summary>
        public const int MainPlayUp = 13;

        /// <summary>Main menu: the big Play button pressed (iOS q20).</summary>
        public const int MainPlayDown = 14;

        /// <summary>Main menu: the top band (iOS q25).</summary>
        public const int MainTopBand = 18;

        /// <summary>Main menu sheet: the round plate behind the settings views' corner fan (iOS q0).</summary>
        public const int FanPlate = 0;

        /// <summary>Main menu sheet: the corner fan (iOS q1).</summary>
        public const int Fan = 1;

        /// <summary>Main menu sheet: the highlight over the fan's hub (iOS q2).</summary>
        public const int FanHub = 2;

        /// <summary>Glow sheet: the rotating light behind Play (iOS q0).</summary>
        public const int Glow = 0;

        /// <summary>Back button plate (iOS q0).</summary>
        public const int BackPlateUp = 0;

        /// <summary>Back button plate pressed (iOS q1).</summary>
        public const int BackPlateDown = 1;

        /// <summary>Back button arrow (iOS q2).</summary>
        public const int BackArrow = 2;

        /// <summary>Long plate (iOS q0).</summary>
        public const int LongPlateUp = 0;

        /// <summary>Long plate pressed (iOS q1).</summary>
        public const int LongPlateDown = 1;

        /// <summary>Capsule (iOS q0).</summary>
        public const int CapsuleUp = 0;

        /// <summary>Capsule pressed (iOS q1).</summary>
        public const int CapsuleDown = 1;

        /// <summary>Short capsule (iOS q2).</summary>
        public const int ShortCapsuleUp = 2;

        /// <summary>Short capsule pressed, also the selected look (iOS q3).</summary>
        public const int ShortCapsuleDown = 3;

        /// <summary>Round audio plate (iOS q4).</summary>
        public const int AudioPlateUp = 4;

        /// <summary>Round audio plate pressed (iOS q5).</summary>
        public const int AudioPlateDown = 5;

        /// <summary>Music icon (iOS q8).</summary>
        public const int MusicIcon = 8;

        /// <summary>Sound icon (iOS q9).</summary>
        public const int SoundIcon = 9;

        /// <summary>The cross drawn over a switched-off audio icon (iOS q12).</summary>
        public const int AudioCross = 12;

        /// <summary>
        /// Where the pause menu's round audio buttons center their icon, in the small-button
        /// canvas: the middle of the iOS marker quad q17, a 16 pixel square at (262, 143) on the
        /// HD canvas. The packed sheet keeps that quad but not its place, so the point is held here.
        /// </summary>
        public static readonly Vector RoundAudioIconMarker = new(270f * CanvasToAsset, 151f * CanvasToAsset);

        /// <summary>Settings: the wide credits window border (iOS q6).</summary>
        public const int WindowBorderWide = 6;

        /// <summary>Settings: the thin credits window border (iOS q7).</summary>
        public const int WindowBorderThin = 7;
    }

    /// <summary>Builds Time Travel buttons with DX labels on iOS plates.</summary>
    internal static class TimeTravelPlates
    {
        /// <summary>
        /// Share of a plate's width a label may take; the rest stays clear for the plate's rounded
        /// ends.
        /// </summary>
        public const float LabelWidthShare = 0.85f;

        /// <summary>A plate drawn at a scale, inside a container sized to what it draws.</summary>
        /// <param name="resource">Sheet holding the plate.</param>
        /// <param name="quad">Plate quad.</param>
        /// <param name="plateScale">Scale the plate is drawn at.</param>
        /// <returns>The container.</returns>
        public static BaseElement Plate(string resource, int quad, float plateScale)
        {
            Image plate = Image.FromResource(resource, quad);
            plate.anchor = plate.parentAnchor = 18;
            plate.scaleX = plate.scaleY = plateScale;
            BaseElement container = new()
            {
                width = (int)MathF.Round(plate.width * plateScale),
                height = (int)MathF.Round(plate.height * plateScale),
            };
            _ = container.AddChild(plate);
            return container;
        }

        /// <summary>The scale that gives a Time Travel plate the height of a classic one.</summary>
        /// <param name="resource">Sheet holding the Time Travel plate.</param>
        /// <param name="quad">Time Travel plate quad.</param>
        /// <param name="classicResource">Sheet holding the classic plate.</param>
        /// <param name="classicQuad">Classic plate quad.</param>
        /// <returns>The scale.</returns>
        public static float HeightMatching(string resource, int quad, string classicResource, int classicQuad)
        {
            return Image.GetQuadSize(classicResource, classicQuad).Y / Image.GetQuadSize(resource, quad).Y;
        }

        /// <summary>A plate with a label centered on it in the big DX font.</summary>
        /// <param name="resource">Sheet holding the plate.</param>
        /// <param name="quad">Plate quad.</param>
        /// <param name="text">Label.</param>
        /// <param name="plateScale">Scale the plate is drawn at.</param>
        /// <returns>The labeled plate.</returns>
        public static BaseElement LabeledPlate(string resource, int quad, string text, float plateScale)
        {
            BaseElement plate = Plate(resource, quad, plateScale);
            AddLabel(plate, text, plate.width * LabelWidthShare);
            return plate;
        }

        /// <summary>
        /// The room a plate keeps clear on either side of its label, together: the share of its art
        /// width that <see cref="LabelWidthShare"/> leaves for the rounded ends.
        /// </summary>
        /// <param name="resource">Sheet holding the plate.</param>
        /// <param name="quad">Plate quad.</param>
        /// <param name="plateScale">Scale the plate is drawn at.</param>
        /// <returns>The padding, in drawn units.</returns>
        public static float SidePadding(string resource, int quad, float plateScale)
        {
            return Image.GetQuadSize(resource, quad).X * plateScale * (1f - LabelWidthShare);
        }

        /// <summary>
        /// A pill drawn at a scale and stretched to a width through its center, inside a container
        /// sized to what it draws.
        /// </summary>
        /// <param name="resource">Sheet holding the pill.</param>
        /// <param name="quad">Pill quad.</param>
        /// <param name="plateScale">Scale the pill is drawn at.</param>
        /// <param name="width">Drawn width; the art's own when narrower.</param>
        /// <returns>The container.</returns>
        public static BaseElement SlicedPlate(string resource, int quad, float plateScale, float width)
        {
            SlicedImage plate = SlicedImage.Create(resource, quad);
            plate.anchor = plate.parentAnchor = 18;
            plate.scaleX = plate.scaleY = plateScale;
            BaseElement container = new();
            _ = container.AddChild(plate);
            SizePlate(container, plate, plateScale, width);
            return container;
        }

        /// <summary>A stretched pill with a label centered on it in the big DX font.</summary>
        /// <param name="resource">Sheet holding the pill.</param>
        /// <param name="quad">Pill quad.</param>
        /// <param name="text">Label.</param>
        /// <param name="plateScale">Scale the pill is drawn at.</param>
        /// <param name="width">Drawn width; the art's own when narrower.</param>
        /// <returns>The labeled pill.</returns>
        public static BaseElement LabeledSlicedPlate(string resource, int quad, string text, float plateScale, float width)
        {
            BaseElement plate = SlicedPlate(resource, quad, plateScale, width);
            Text label = CreateLabel(text);
            _ = plate.AddChild(label);
            FitLabel(label, PillLabelRoom(plate, resource, quad, plateScale, width));
            return plate;
        }

        /// <summary>
        /// Stretches both pills of a button made by <see cref="CreatePillButton"/> to a new width,
        /// refitting their labels.
        /// </summary>
        /// <param name="button">The button.</param>
        /// <param name="resource">Sheet holding the pills.</param>
        /// <param name="upQuad">Pill shown at rest.</param>
        /// <param name="downQuad">Pill shown while pressed.</param>
        /// <param name="plateScale">Scale both pills are drawn at.</param>
        /// <param name="width">Drawn width of both pills.</param>
        public static void ResizePillButton(Button button, string resource, int upQuad, int downQuad, float plateScale, float width)
        {
            ResizePill(button.GetChild(0), resource, upQuad, plateScale, width);
            ResizePill(button.GetChild(1), resource, downQuad, plateScale, width);
            button.width = button.GetChild(0).width;
            button.height = button.GetChild(0).height;
        }

        /// <summary>A text button on a stretched Time Travel pill, labeled in the big DX font.</summary>
        /// <param name="resource">Sheet holding the pills.</param>
        /// <param name="upQuad">Pill shown at rest.</param>
        /// <param name="downQuad">Pill shown while pressed.</param>
        /// <param name="text">Label.</param>
        /// <param name="id">Button identifier.</param>
        /// <param name="d">Delegate that receives the press.</param>
        /// <param name="plateScale">Scale both pills are drawn at.</param>
        /// <param name="width">Drawn width of both pills.</param>
        /// <returns>The button.</returns>
        public static Button CreatePillButton(string resource, int upQuad, int downQuad, string text, ButtonId id, IButtonDelegation d, float plateScale, float width)
        {
            Button button = new Button().InitWithUpElementDownElementandID(
                LabeledSlicedPlate(resource, upQuad, text, plateScale, width),
                LabeledSlicedPlate(resource, downQuad, text, plateScale, width),
                id);
            button.SetTouchIncreaseLeftRightTopBottom(15, 15, 15, 15);
            button.delegateButtonDelegate = d;
            return button;
        }

        /// <summary>A label in the big DX font, centered and gently pulsing like the classic buttons'.</summary>
        /// <param name="text">Label.</param>
        /// <returns>The label.</returns>
        private static Text CreateLabel(string text)
        {
            Text label = new Text().InitWithFont(Application.GetFont(Resources.Fnt.BigFont));
            label.SetString(text);
            label.anchor = label.parentAnchor = 18;
            label.pingPongEnabled = true;
            return label;
        }

        /// <summary>Centers a label on a plate, shrinking it to fit the room it may take.</summary>
        /// <param name="plate">The plate.</param>
        /// <param name="text">Label.</param>
        /// <param name="room">Width the label may take.</param>
        private static void AddLabel(BaseElement plate, string text, float room)
        {
            Text label = CreateLabel(text);
            FitLabel(label, room);
            _ = plate.AddChild(label);
        }

        /// <summary>Shrinks a label that would spill out of its room, or restores one that fits.</summary>
        /// <param name="label">The label.</param>
        /// <param name="room">Width the label may take.</param>
        private static void FitLabel(Text label, float room)
        {
            // DX labels were sized for the classic plates; one that would spill is shrunk.
            label.scaleX = label.scaleY = label.width > room ? room / label.width : 1f;
        }

        /// <summary>Sizes a stretched pill and its container to a drawn width.</summary>
        private static void SizePlate(BaseElement container, SlicedImage plate, float plateScale, float width)
        {
            plate.width = Math.Max(plate.ArtWidth, (int)MathF.Ceiling((width / plateScale) - 0.01f));
            container.width = (int)MathF.Round(plate.width * plateScale);
            container.height = (int)MathF.Round(plate.height * plateScale);
        }

        /// <summary>Restretches one labeled pill and refits its label.</summary>
        private static void ResizePill(BaseElement container, string resource, int quad, float plateScale, float width)
        {
            SizePlate(container, (SlicedImage)container.GetChild(0), plateScale, width);
            if (container.GetChild(1) is Text label)
            {
                FitLabel(label, PillLabelRoom(container, resource, quad, plateScale, width));
            }
        }

        /// <summary>
        /// The room a stretched pill leaves its label: measured from the width asked for, which the
        /// drawn width only rounds.
        /// </summary>
        private static float PillLabelRoom(BaseElement container, string resource, int quad, float plateScale, float width)
        {
            return MathF.Max(container.width, width) - SidePadding(resource, quad, plateScale);
        }

        /// <summary>A text button on a Time Travel plate, labeled in the big DX font.</summary>
        /// <param name="resource">Sheet holding the plates.</param>
        /// <param name="upQuad">Plate shown at rest.</param>
        /// <param name="downQuad">Plate shown while pressed.</param>
        /// <param name="text">Label.</param>
        /// <param name="id">Button identifier.</param>
        /// <param name="d">Delegate that receives the press.</param>
        /// <param name="plateScale">Scale both plates are drawn at.</param>
        /// <returns>The button.</returns>
        public static Button CreateTextButton(string resource, int upQuad, int downQuad, string text, ButtonId id, IButtonDelegation d, float plateScale)
        {
            Button button = new Button().InitWithUpElementDownElementandID(
                LabeledPlate(resource, upQuad, text, plateScale),
                LabeledPlate(resource, downQuad, text, plateScale),
                id);
            button.SetTouchIncreaseLeftRightTopBottom(15, 15, 15, 15);
            button.delegateButtonDelegate = d;
            return button;
        }
    }
}
