using CutTheRopeDX.Framework.Visual;

namespace CutTheRopeDX.GameMain
{
    /// <content>
    /// The Halloween dressing of the classic menus: Cut the Rope 3.3.0's webs and bats over the
    /// backdrop, and its witch hat on the logo.
    /// </content>
    /// <remarks>
    /// <para>
    /// 3.3.0 lays the decorations out on a portrait 1280x1920 screen. Each one is pinned here to the
    /// screen edge or corner it sits nearest there, at the same inset, so a decoration that hung off
    /// the portrait screen hangs off the landscape one too. Insets are in design units: the atlas is
    /// that screen at 0.78.
    /// </para>
    /// <para>
    /// The hat keeps its place on the lettering. 3.3.0's logo is the classic one, which matches the
    /// top of <see cref="Resources.Img.MenuLogoNew"/> at 1.53 times its size, 17 units up and left.
    /// </para>
    /// </remarks>
    internal sealed partial class MenuController
    {
        /// <summary>Decorations of every menu but the main one: two webs and three bats.</summary>
        private static readonly HalloweenDecoration[] HalloweenInnerDecorations =
        [
            // Lower than 3.3.0's 138 units up: the web's strands run to the sprite's bottom edge,
            // so it sits on the screen's bottom edge rather than ending in mid-air above it.
            new(Quad: 0, Anchor: 33, X: -100f, Y: 0f),
            new(Quad: 1, Anchor: 12, X: 2f, Y: -1f),
            new(Quad: 2, Anchor: 36, X: -21f, Y: -486f),
            new(Quad: 3, Anchor: 36, X: 134f, Y: -217f),
            new(Quad: 4, Anchor: 36, X: 82f, Y: -411f),
        ];

        /// <summary>Decorations of the main menu: a web and three bats.</summary>
        private static readonly HalloweenDecoration[] HalloweenMainDecorations =
        [
            new(Quad: 5, Anchor: 33, X: -100f, Y: 38f),
            new(Quad: 6, Anchor: 9, X: -99f, Y: 487f),
            new(Quad: 7, Anchor: 12, X: 83f, Y: 247f),
            new(Quad: 8, Anchor: 10, X: 144f, Y: 15f),
        ];

        /// <summary>Witch hat brim, drawn behind the logo, from the logo's top-left corner.</summary>
        private static readonly HalloweenDecoration HalloweenHatBack = new(Quad: 9, Anchor: 9, X: 75f, Y: -35f);

        /// <summary>Witch hat crown, drawn over the logo, from the logo's top-left corner.</summary>
        private static readonly HalloweenDecoration HalloweenHatFront = new(Quad: 10, Anchor: 9, X: 77f, Y: -110f);

        /// <summary>
        /// Adds the webs and bats over a menu backdrop.
        /// </summary>
        /// <param name="backdrop">Backdrop sized to the visible screen.</param>
        /// <param name="mainMenu">Whether this is the main menu, which has its own set.</param>
        private static void AddHalloweenDecorations(BaseElement backdrop, bool mainMenu)
        {
            foreach (HalloweenDecoration decoration in mainMenu ? HalloweenMainDecorations : HalloweenInnerDecorations)
            {
                _ = backdrop.AddChild(decoration.Create());
            }
        }

        /// <summary>
        /// Puts the witch hat on the logo: the brim behind the lettering, the crown over it.
        /// </summary>
        /// <param name="logoParent">Element the logo is about to be added to.</param>
        /// <param name="logo">The logo, not yet added, so the brim goes in first and draws under it.</param>
        private static void AddHalloweenLogoHat(BaseElement logoParent, Image logo)
        {
            // The brim shares the logo's parent and anchoring, so it moves with the logo.
            Image brim = HalloweenHatBack.Create();
            brim.parentAnchor = logo.parentAnchor;
            brim.x = logo.x - (logo.width / 2f) + HalloweenHatBack.X;
            brim.y = logo.y + HalloweenHatBack.Y;
            _ = logoParent.AddChild(brim);
            _ = logo.AddChild(HalloweenHatFront.Create());
        }

        /// <summary>One decoration and where it is pinned.</summary>
        /// <param name="Quad">Quad in <see cref="Resources.Img.MenuBgrHalloweenDecorations"/>.</param>
        /// <param name="Anchor">Anchor flags, used for both the sprite and the point it hangs from.</param>
        /// <param name="X">Horizontal offset from that point; positive is right.</param>
        /// <param name="Y">Vertical offset from that point; positive is down.</param>
        private readonly record struct HalloweenDecoration(int Quad, sbyte Anchor, float X, float Y)
        {
            /// <summary>Creates the sprite at its pinned place.</summary>
            /// <returns>The sprite.</returns>
            public Image Create()
            {
                Image image = Image.FromResource(Resources.Img.MenuBgrHalloweenDecorations, Quad);
                image.anchor = image.parentAnchor = Anchor;
                image.x = X;
                image.y = Y;
                image.touchable = false;
                return image;
            }
        }
    }
}
