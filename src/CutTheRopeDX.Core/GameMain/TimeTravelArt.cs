using System;

using CutTheRopeDX.Framework;
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

        /// <summary>Back button plate (iOS q0), also under every pause and result icon.</summary>
        public const int BackPlateUp = 0;

        /// <summary>Back button plate pressed (iOS q1).</summary>
        public const int BackPlateDown = 1;

        /// <summary>Back button arrow (iOS q2).</summary>
        public const int BackArrow = 2;

        /// <summary>Flame marking the hardest pack (iOS q9).</summary>
        public const int HardestBadge = 8;

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

        /// <summary>
        /// The middle of the round audio plate's disc face, in the small-button canvas, measured
        /// from the art. The iOS marker rule lands the speaker's weight within a pixel of it.
        /// </summary>
        public static readonly Vector RoundAudioFaceCenter = new(207f, 112.5f);

        /// <summary>
        /// The middle of the capsule's face at rest, in the small-button canvas, measured from the
        /// art: where the settings audio toggles center their icons' weight.
        /// </summary>
        public static readonly Vector PillFaceCenter = new(209f, 114f);

        /// <summary>
        /// The speaker's weight center inside its quad: its alpha-weighted middle, measured from
        /// the art, left of the quad's middle where the waves leave room.
        /// </summary>
        public static readonly Vector SoundIconWeightCenter = new(67.3f, 57.3f);

        /// <summary>
        /// The music note's weight center inside its quad: its alpha-weighted middle, measured from
        /// the art, which sits low and right of the quad's middle for the note heads and flag.
        /// </summary>
        public static readonly Vector MusicIconWeightCenter = new(62.1f, 62.3f);

        /// <summary>Popup frame: the bottom cap (iOS q0).</summary>
        public const int PopupBottom = 0;

        /// <summary>Popup frame: the strip tiled between the caps (iOS q1).</summary>
        public const int PopupStrip = 1;

        /// <summary>Popup frame: the top cap (iOS q2).</summary>
        public const int PopupTop = 2;

        /// <summary>Popup: the round close button over the top cap's corner (iOS q10).</summary>
        public const int PopupClose = 3;

        /// <summary>Settings: the wide credits window border (iOS q6).</summary>
        public const int WindowBorderWide = 6;

        /// <summary>Settings: the thin credits window border (iOS q7).</summary>
        public const int WindowBorderThin = 7;

        /// <summary>Back button sheet: the page arrow (iOS q3).</summary>
        public const int PageArrowUp = 3;

        /// <summary>Back button sheet: the page arrow pressed (iOS q4).</summary>
        public const int PageArrowDown = 4;

        /// <summary>The pack pages' loading clock animation.</summary>
        public const string LoadingAnimationXml = "menu_loading_timetravel.xml";

        /// <summary>The burst a pressed level plays.</summary>
        public const string LevelBurstAnimationXml = "fx_menu_levels_timetravel.xml";

        /// <summary>The padlock on a locked pack, which splits apart when the pack opens.</summary>
        public const string PackLockAnimationXml = "menu_pack_selection_lock_ani_timetravel.xml";

        /// <summary>Pack sheet: the rays behind the pack icon (iOS q0).</summary>
        public const int PackRays = 0;

        /// <summary>Pack sheet: a page bullet (iOS q5).</summary>
        public const int PageBullet = 4;

        /// <summary>Pack sheet: the current page's bullet (iOS q6).</summary>
        public const int PageBulletCurrent = 5;

        /// <summary>Pack sheet: the star beside the star count (iOS q7).</summary>
        public const int CounterStar = 6;

        /// <summary>Pack sheet: the line across a locked icon where its stars reach (iOS q23).</summary>
        public const int PackProgressLine = 22;

        /// <summary>Pack sheet: the star on a padlock (iOS q25).</summary>
        public const int LockStar = 23;

        /// <summary>Pack sheet: the particle emitted when a padlock opens (iOS q26).</summary>
        public const int LockParticle = 24;

        /// <summary>Pack sheet: the speech bubble on the coming-soon page (iOS q41).</summary>
        public const int ComingSoonBubble = 29;

        /// <summary>Pack sheet: the first of the three coming-soon gadgets (iOS q43 to q45).</summary>
        public const int ComingSoonFirstGadget = 31;

        /// <summary>Number of coming-soon gadgets.</summary>
        public const int ComingSoonGadgets = 3;

        /// <summary>Pack icon sheet: the badge on a pack with every star (iOS q8).</summary>
        public const int PerfectBadge = 8;

        /// <summary>Number of pack icons, which DX packs take in turn.</summary>
        public const int PackIcons = 12;

        /// <summary>Level sheet: the level played last (iOS q0).</summary>
        public const int LevelLastPlayed = 0;

        /// <summary>Level sheet: a locked level (iOS q1).</summary>
        public const int LevelLocked = 1;

        /// <summary>Level sheet: an open level (iOS q2).</summary>
        public const int LevelOpen = 2;

        /// <summary>Level sheet: no stars won; one more star per quad up to three (iOS q3 to q6).</summary>
        public const int LevelStars = 3;

        /// <summary>
        /// The pack page whose background and tint the coming-soon page takes: page 14, the one
        /// iOS gives it when the snowflake page is not shown.
        /// </summary>
        public const int ComingSoonPage = 14;

        /// <summary>
        /// Rects of the iOS pack sheet's position quads, in canvas pixels from the iOS canvas
        /// origin. The packed sheet keeps these as blank frames without their places, so the
        /// places are held here.
        /// </summary>
        public static class PackMarkers
        {
            /// <summary>Where the pack title is centered, and how wide it may run (iOS q4).</summary>
            public static readonly Rectangle Title = new(223f, 269f, 837f, 300f);

            /// <summary>The rays' quad (iOS q0): spun about its middle.</summary>
            public static readonly Rectangle Rays = new(-151f, 196f, 1582f, 1530f);

            /// <summary>A page bullet (iOS q5).</summary>
            public static readonly Rectangle Bullet = new(605f, 1765f, 40f, 40f);

            /// <summary>The current page's bullet (iOS q6): the row is centered on its height.</summary>
            public static readonly Rectangle BulletCurrent = new(658f, 1745f, 76f, 75f);

            /// <summary>The right page arrow's box (iOS q9): the arrow's top right sits on its own.</summary>
            public static readonly Rectangle RightArrow = new(1122f, 838f, 106f, 216f);

            /// <summary>The left page arrow's box (iOS q10): the arrow's top left sits on its own.</summary>
            public static readonly Rectangle LeftArrow = new(60f, 838f, 106f, 216f);

            /// <summary>The coming-soon page's text box (iOS q21).</summary>
            public static readonly Rectangle ComingSoonText = new(336f, 934f, 610f, 252f);

            /// <summary>The padlock half (iOS q22) its star and price are placed from.</summary>
            public static readonly Rectangle LockHalf = new(372f, 687f, 244f, 581f);

            /// <summary>The line across a locked icon (iOS q23).</summary>
            public static readonly Rectangle ProgressLine = new(412f, 955f, 442f, 27f);

            /// <summary>Where the padlock's star is centered (iOS q29).</summary>
            public static readonly Rectangle LockStarMarker = new(636f, 1066f, 10f, 10f);

            /// <summary>Where the padlock's price is centered (iOS q30).</summary>
            public static readonly Rectangle LockPriceMarker = new(644f, 1056f, 10f, 10f);

            /// <summary>The coming-soon bubble (iOS q41).</summary>
            public static readonly Rectangle ComingSoonBubble = new(798f, 1389f, 403f, 332f);

            /// <summary>The coming-soon bubble's text box (iOS q42).</summary>
            public static readonly Rectangle ComingSoonBubbleText = new(851f, 1474f, 292f, 244f);

            /// <summary>The coming-soon gadgets (iOS q43 to q45).</summary>
            public static readonly Rectangle[] ComingSoonGadgets =
            [
                new(1f, 358f, 247f, 312f),
                new(491f, 91f, 355f, 539f),
                new(1074f, 358f, 269f, 301f),
            ];
        }

        /// <summary>The level sheet's places, in canvas pixels on the level sheet's canvas.</summary>
        public static class LevelMarkers
        {
            /// <summary>An open level's plate (iOS q2), which the rest are placed from.</summary>
            public static readonly Rectangle Plate = new(214f, 353f, 250f, 267f);

            /// <summary>The star strip (iOS q3).</summary>
            public static readonly Rectangle Stars = new(285f, 540f, 163f, 84f);

            /// <summary>Where the level number is centered (iOS q8).</summary>
            public static readonly Rectangle Number = new(336f, 474f, 10f, 10f);
        }

        /// <summary>The HUD's star counter, one star a copy (iOS <c>hud.pb</c>).</summary>
        public const string HudStarAnimationXml = "hud_timetravel.xml";

        /// <summary>The lightning that strikes as a level opens (iOS <c>fx_restart.pb</c>).</summary>
        public const string LightningAnimationXml = "fx_restart_timetravel.xml";

        /// <summary>The time spiral Om Nom passes through (iOS <c>fx_spiral.pb</c>).</summary>
        public const string SpiralAnimationXml = "fx_spiral_timetravel.xml";

        /// <summary>The result screen (iOS <c>result_screen.pb</c>).</summary>
        public const string ResultAnimationXml = "result_screen_timetravel.xml";

        /// <summary>The original unlocked-chapter capsule animation.</summary>
        public const string ResultUnlockedAnimationXml = "result_screen_unlocked_timetravel.xml";

        /// <summary>HUD: the pause pill (iOS q0).</summary>
        public const int HudPauseUp = 0;

        /// <summary>HUD: the pause pill pressed (iOS q1).</summary>
        public const int HudPauseDown = 1;

        /// <summary>HUD: the restart disc (iOS q2).</summary>
        public const int HudRestartUp = 2;

        /// <summary>HUD: the restart disc pressed (iOS q3).</summary>
        public const int HudRestartDown = 3;

        /// <summary>Small button sheet: the play icon on the pause menu's resume button (iOS q14).</summary>
        public const int ResumeIcon = 13;

        /// <summary>Small button sheet: the skip icon (iOS q15).</summary>
        public const int SkipIcon = 14;

        /// <summary>Result sheet: the folding blind's half (iOS q0).</summary>
        public const int ResultFold = 0;

        /// <summary>Result sheet: the next-level icon (iOS q3).</summary>
        public const int ResultNextIcon = 3;

        /// <summary>Result sheet: the replay icon (iOS q5).</summary>
        public const int ResultRestartIcon = 5;

        /// <summary>Result sheet: the level-grid icon (iOS q7).</summary>
        public const int ResultMenuIcon = 7;

        /// <summary>Result sheet: yellow star particles (native q26 and q27).</summary>
        public const int ResultStarParticleFirst = 26;

        /// <summary>
        /// The HUD sheet's places (iOS q7 and its position quads q10 to q15), in canvas pixels from
        /// the iOS canvas origin.
        /// </summary>
        public static class HudMarkers
        {
            /// <summary>The second HUD star (iOS q7); the stars step by the gap to q6 from it.</summary>
            public static readonly Rectangle Star = new(297f, 16f, 117f, 112f);

            /// <summary>How far one HUD star is from the next (iOS q6 from q7).</summary>
            public const float StarStep = 126f;

            /// <summary>The pause menu's level-grid button (iOS q10).</summary>
            public static readonly Rectangle PauseLevelSelect = new(302f, 1287f, 6f, 6f);

            /// <summary>The pause menu's sound button (iOS q11).</summary>
            public static readonly Rectangle PauseSound = new(637f, 1594f, 6f, 6f);

            /// <summary>The pause menu's replay button (iOS q12).</summary>
            public static readonly Rectangle PauseRestart = new(637f, 1287f, 6f, 6f);

            /// <summary>The pause menu's resume button (iOS q13).</summary>
            public static readonly Rectangle PauseResume = new(972f, 1287f, 6f, 6f);

            /// <summary>The pause menu's shop button (iOS q14).</summary>
            public static readonly Rectangle PauseShop = new(369f, 1594f, 6f, 6f);

            /// <summary>The pause menu's skip button (iOS q15).</summary>
            public static readonly Rectangle PauseSkip = new(910f, 1594f, 6f, 6f);
        }

        /// <summary>
        /// The result sheet's places, in canvas pixels from the iOS canvas origin. Its position
        /// quads are blank frames in the packed sheet, which was also split onto a grown canvas, so
        /// every place the result screen is laid out from is held here.
        /// </summary>
        public static class ResultMarkers
        {
            /// <summary>The next-level button (iOS q4).</summary>
            public static readonly Rectangle Next = new(932f, 1374f, 8f, 8f);

            /// <summary>The replay button (iOS q6).</summary>
            public static readonly Rectangle Restart = new(636f, 1396f, 8f, 8f);

            /// <summary>The level-grid button (iOS q8).</summary>
            public static readonly Rectangle Menu = new(342f, 1374f, 8f, 8f);

            /// <summary>The replay icon (iOS q5): every round icon is set off its plate's middle as this one is off q6.</summary>
            public static readonly Rectangle RestartIcon = new(580f, 1326f, 116f, 118f);

            /// <summary>The display (iOS q21) the buttons, stars and title hang from.</summary>
            public static readonly Rectangle Display = new(106f, 680f, 534f, 464f);

            /// <summary>The improved-result banner (iOS q22).</summary>
            public static readonly Rectangle ImprovedBanner = new(248f, 1059f, 833f, 373f);

            /// <summary>The three stars (iOS q30 to q32).</summary>
            public static readonly Rectangle[] Stars =
            [
                new(304f, 570f, 10f, 8f),
                new(526f, 540f, 10f, 10f),
                new(746f, 540f, 8f, 10f),
            ];

            /// <summary>The improved-result banner's text box (iOS q41).</summary>
            public static readonly Rectangle ImprovedText = new(502f, 1091f, 418f, 229f);

            /// <summary>The unlocked-chapter capsule (native q2).</summary>
            public static readonly Rectangle UnlockedCapsule = new(194f, 1497f, 898f, 367f);

            /// <summary>The unlocked-chapter capsule's text box (native q40).</summary>
            public static readonly Rectangle UnlockedText = new(490f, 1556f, 482f, 254f);

            /// <summary>The score (iOS q42).</summary>
            public static readonly Rectangle Score = new(414f, 966f, 454f, 98f);

            /// <summary>The title over the panel (iOS q43).</summary>
            public static readonly Rectangle Title = new(636f, 152f, 8f, 8f);

            /// <summary>The star bonus and time line (iOS q44).</summary>
            public static readonly Rectangle Data = new(230f, 773f, 822f, 151f);
        }

        /// <summary>
        /// The icon a pack shows: its sheet and quad. DX packs take the twelve iOS icons in turn.
        /// </summary>
        /// <param name="pack">Pack index.</param>
        /// <returns>The sheet and the quad in it.</returns>
        public static (string Sheet, int Quad) PackIcon(int pack)
        {
            int picture = pack >= 0 && pack < PackConfig.Packs.Count ? PackConfig.Packs[pack].TimeTravelPackPicture : -1;
            int icon = picture is >= 0 and < PackIcons ? picture : ((pack % PackIcons) + PackIcons) % PackIcons;
            return icon switch
            {
                < 6 => (Resources.Img.MenuPackSelectionIconsTimeTravel, icon),
                < 10 => (Resources.Img.MenuPackSelectionIcons1TimeTravel, icon - 6),
                _ => (Resources.Img.MenuPackSelectionIcons2TimeTravel, icon - 10),
            };
        }

        /// <summary>
        /// Page tints (iOS <c>bgrColors</c>), one per iOS page: page 0 was the more-games page and
        /// pack <c>n</c> sat on page <c>n + 1</c>.
        /// </summary>
        private static readonly RGBAColor[] PageColors =
        [
            Rgb(203, 245, 7), Rgb(231, 172, 255), Rgb(255, 162, 162), Rgb(82, 255, 220), Rgb(255, 255, 0),
            Rgb(245, 255, 77), Rgb(255, 222, 0), Rgb(230, 100, 170), Rgb(255, 189, 90), Rgb(239, 107, 0),
            Rgb(129, 128, 125), Rgb(169, 254, 211), Rgb(164, 255, 116), Rgb(59, 211, 255), Rgb(180, 255, 255),
        ];

        /// <summary>Number of iOS pages, each with its own background and tint.</summary>
        public static int PageCount => PageColors.Length;

        /// <summary>
        /// The iOS page a DX pack page draws: the page its icon had, or the coming-soon page.
        /// </summary>
        /// <param name="pack">Pack index; the one past the last pack is the coming-soon page.</param>
        /// <param name="comingSoon">Whether this is the coming-soon page.</param>
        /// <returns>The iOS page, which is also its background quad.</returns>
        public static int PackPage(int pack, bool comingSoon)
        {
            if (comingSoon)
            {
                return ComingSoonPage;
            }
            int background = pack >= 0 && pack < PackConfig.Packs.Count ? PackConfig.Packs[pack].TimeTravelPackBackground : -1;
            return background >= 0 && background < PageCount ? background : (((pack % PackIcons) + PackIcons) % PackIcons) + 1;
        }

        /// <summary>The tint of an iOS page.</summary>
        /// <param name="page">iOS page.</param>
        /// <returns>The tint, opaque.</returns>
        public static RGBAColor PageColor(int page)
        {
            return PageColors[Math.Clamp(page, 0, PageColors.Length - 1)];
        }

        private static RGBAColor Rgb(int r, int g, int b)
        {
            return RGBAColor.MakeRGBA(r / 255f, g / 255f, b / 255f, 1f);
        }
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

        /// <summary>
        /// Redraws both pills of a button made by <see cref="CreatePillButton"/> from other quads
        /// of the same sheet, keeping the width they are stretched to.
        /// </summary>
        /// <param name="button">The button.</param>
        /// <param name="upQuad">Pill to show at rest.</param>
        /// <param name="downQuad">Pill to show while pressed.</param>
        public static void SetPillQuads(Button button, int upQuad, int downQuad)
        {
            SetPillQuad(button.GetChild(0), upQuad);
            SetPillQuad(button.GetChild(1), downQuad);
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

        /// <summary>Redraws one stretched pill from another quad, keeping its width.</summary>
        private static void SetPillQuad(BaseElement container, int quad)
        {
            SlicedImage plate = (SlicedImage)container.GetChild(0);

            // Choosing a quad resets an image to the quad's own width.
            int width = plate.width;
            plate.SetDrawQuad(quad);
            plate.width = Math.Max(width, plate.ArtWidth);
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
