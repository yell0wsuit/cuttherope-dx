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

        /// <summary>Settings: the ZeptoLab logo (iOS q4).</summary>
        public const int ZeptoLabLogo = 4;

        /// <summary>Settings: the wide credits window border (iOS q6).</summary>
        public const int WindowBorderWide = 6;

        /// <summary>Settings: the thin credits window border (iOS q7).</summary>
        public const int WindowBorderThin = 7;
    }

    /// <summary>Builds Time Travel buttons with DX labels on iOS plates.</summary>
    internal static class TimeTravelPlates
    {
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
            Text label = new Text().InitWithFont(Application.GetFont(Resources.Fnt.BigFont));
            label.SetString(text);
            label.anchor = label.parentAnchor = 18;
            label.pingPongEnabled = true;
            _ = plate.AddChild(label);
            return plate;
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
