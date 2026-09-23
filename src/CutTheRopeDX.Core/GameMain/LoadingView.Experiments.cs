using System;

using CutTheRopeDX.Framework;
using CutTheRopeDX.Framework.Core;
using CutTheRopeDX.Framework.Platform;
using CutTheRopeDX.Framework.Visual;

namespace CutTheRopeDX.GameMain
{
    /// <content>
    /// The Cut the Rope: Experiments loading screen: the level transition's blind pulled down, with
    /// a porthole machine on it, the candy behind its glass filling from the bottom as resources
    /// load, a key winding it up, and the bulb lighting once everything is in. The iOS HD
    /// <c>LoadingController</c>.
    /// </content>
    /// <remarks>
    /// Every machine piece is drawn at its atlas offset, which places it in iOS's portrait frame
    /// (1536 by 2048 iPad-retina units, scaled by <see cref="MenuController.IosToAsset"/>). That
    /// frame is centered on the design box, so the porthole stays centered on any viewport while
    /// the sheet and its scroll cover it edge to edge.
    /// </remarks>
    internal sealed partial class LoadingView
    {
        /// <summary>Loading atlas quads, in iOS numbering.</summary>
        private const int ExpQuadCandy = 1;
        private const int ExpQuadLampOff = 2;
        private const int ExpQuadLampOn = 3;
        private const int ExpQuadFillLine = 4;
        private const int ExpQuadFill = 5;
        private const int ExpQuadPorthole = 6;
        private const int ExpQuadBulbGlow = 7;
        private const int ExpQuadFirstKey = 8;
        private const int ExpQuadLastKey = 16;

        /// <summary>Seconds per frame of the key animation (iOS 0.05, looping).</summary>
        private const float ExpKeyFrameSeconds = 0.05f;

        /// <summary>Seconds the lamps take to come on once loading completes (iOS 0.05).</summary>
        private const float ExpLampFadeSeconds = 0.05f;

        /// <summary>
        /// Seconds from loading completing to the screen handing over: the bulb's timeline holds
        /// for 0.5 and its end deactivates the controller.
        /// </summary>
        private const float ExpLitHoldSeconds = 0.5f;

        /// <summary>Width of iOS's portrait frame in atlas pixels.</summary>
        private const float ExpFrameWidth = 1536f * MenuController.IosToAsset;

        /// <summary>
        /// Top of iOS's portrait frame in the design box, chosen so the porthole, bulb and label
        /// together sit on the design box's middle.
        /// </summary>
        private const float ExpFrameTop = -17f;

        /// <summary>
        /// How far below the sheet's bottom edge the scroll's middle hangs, in design units. iOS
        /// anchors the scroll's vertical center to the bottom of the loading backdrop, 40 lower,
        /// so only the rod shows while the blind is down and the pull ring hangs off the screen.
        /// </summary>
        internal const float ExpScrollCenterBelow = 40f * MenuController.IosToAsset;

        /// <summary>
        /// Scale the scroll art is drawn at vertically. It was cut at the atlases' scale, both iOS
        /// halves joined, so it is drawn at its own height. Horizontally it is stretched to the
        /// sheet's width instead.
        /// </summary>
        internal const float ExpScrollScaleY = 1f;

        /// <summary>
        /// Where the loading label is centered, below the design box's middle: the center of the
        /// iOS label marker, quad 17 at (776, 1390).
        /// </summary>
        internal const float ExpLabelY = ExpFrameTop + (1391f * MenuController.IosToAsset) - (ViewportLayout.DesignHeight / 2f);

        /// <summary>Seconds the screen has been up, driving the key.</summary>
        private float expElapsed;

        /// <summary>Seconds since loading completed, or a negative value while it is still running.</summary>
        private float expLitElapsed = -1f;

        /// <summary>Key frame shown; held where it was when loading completed.</summary>
        private int expKeyQuad = ExpQuadFirstKey;

        /// <summary>
        /// Restarts the Experiments animation for a new load.
        /// </summary>
        private void ResetExperiments()
        {
            expElapsed = 0f;
            expLitElapsed = -1f;
            expKeyQuad = ExpQuadFirstKey;
        }

        /// <summary>
        /// Gets whether the bulb has been lit long enough to hand over.
        /// </summary>
        /// <returns><see langword="true"/> once the lit hold has run out.</returns>
        private bool IsExperimentsComplete()
        {
            return expLitElapsed >= ExpLitHoldSeconds;
        }

        /// <inheritdoc />
        public override void Update(float delta)
        {
            base.Update(delta);
            if (!MenuTheme.IsExperiments)
            {
                return;
            }

            expElapsed += delta;
            if (expLitElapsed >= 0f)
            {
                expLitElapsed += delta;
            }
            else if (Application.SharedResourceMgr().GetPercentLoaded() >= 100)
            {
                expLitElapsed = 0f;
            }
            else
            {
                int frames = ExpQuadLastKey - ExpQuadFirstKey + 1;
                expKeyQuad = ExpQuadFirstKey + ((int)(expElapsed / ExpKeyFrameSeconds) % frames);
            }
        }

        /// <summary>
        /// Draws the Experiments loading screen.
        /// </summary>
        private void DrawExperiments()
        {
            PlatformServices.Cursor?.Enable(true);
            Renderer.Enable(Renderer.GL_TEXTURE_2D);
            Renderer.Enable(Renderer.GL_BLEND);
            Renderer.SetBlendFunc(BlendingFactor.GLONE, BlendingFactor.GLONEMINUSSRCALPHA);
            PreDraw();
            Renderer.SetColor(Color.White);
            Rectangle visible = VisibleBounds;

            // The screen is the level transition's blind, pulled all the way down: the backdrop
            // sheet and the scroll along its bottom edge, cover-fitted the way BoxOpenClose fits
            // the blind, so the two meet exactly when one hands over to the other.
            Rectangle cover = LayoutMath.CoverInside(ViewportLayout.DesignWidth, ViewportLayout.DesignHeight, visible);
            float coverScale = cover.w / ViewportLayout.DesignWidth;
            Texture2D backdrop = Application.GetTexture(Resources.BackgroundImg.MenuExpLoadingBgr);
            Texture2D scroll = Application.GetTexture(Resources.Img.MenuExpLoadingScroll);
            Vector scrollSize = Image.GetQuadSize(Resources.Img.MenuExpLoadingScroll, 0);
            Renderer.PushMatrix();
            Renderer.Translate(cover.x, cover.y, 0f);
            Renderer.Scale(coverScale, coverScale, 1f);
            Renderer.PushMatrix();
            Renderer.Scale(ViewportLayout.DesignWidth / backdrop._realWidth, ViewportLayout.DesignHeight / backdrop._realHeight, 1f);
            DrawHelper.DrawImageQuad(backdrop, -1, 0f, 0f);
            Renderer.PopMatrix();
            float scrollHeight = scrollSize.Y * ExpScrollScaleY;
            Renderer.Translate(0f, ViewportLayout.DesignHeight + ExpScrollCenterBelow - (scrollHeight / 2f), 0f);
            Renderer.Scale(ViewportLayout.DesignWidth / scrollSize.X, ExpScrollScaleY, 1f);
            DrawHelper.DrawImageQuad(scroll, 0, 0f, 0f);
            Renderer.PopMatrix();

            ExperimentsFramePlacement frame = ExperimentsFrame(visible);
            float scale = frame.Scale;
            float frameX = frame.X;
            float frameY = frame.Y;

            Texture2D atlas = Application.GetTexture(Resources.Img.MenuExpLoading);
            float lit = expLitElapsed < 0f ? 0f : MathF.Min(1f, expLitElapsed / ExpLampFadeSeconds);
            float percent = MathF.Min(100f, Application.SharedResourceMgr().GetPercentLoaded());
            Renderer.PushMatrix();
            Renderer.Translate(frameX, frameY, 0f);
            Renderer.Scale(scale, scale, 1f);
            DrawAtOffset(atlas, ExpQuadLampOff);
            DrawFaded(atlas, ExpQuadLampOn, lit);
            DrawFaded(atlas, ExpQuadBulbGlow, lit);
            DrawAtOffset(atlas, ExpQuadCandy);

            // The fill rises over the candy from the bottom, its surface marked by the line. A
            // scissor is a device rectangle rather than geometry, so the window is worked out in
            // logical units.
            Vector fillOffset = Image.GetQuadOffset(Resources.Img.MenuExpLoading, ExpQuadFill);
            Vector fillSize = Image.GetQuadSize(Resources.Img.MenuExpLoading, ExpQuadFill);
            float surfaceY = fillOffset.Y + (fillSize.Y * (1f - (percent / 100f)));
            if (percent > 0f)
            {
                Renderer.Enable(Renderer.GL_SCISSOR_TEST);
                Renderer.SetScissor(
                    frameX + (fillOffset.X * scale),
                    frameY + (surfaceY * scale),
                    fillSize.X * scale,
                    (fillOffset.Y + fillSize.Y - surfaceY) * scale);
                DrawAtOffset(atlas, ExpQuadFill);
                Renderer.Disable(Renderer.GL_SCISSOR_TEST);
            }
            Vector lineSize = Image.GetQuadSize(Resources.Img.MenuExpLoading, ExpQuadFillLine);
            DrawHelper.DrawImageQuad(
                atlas,
                ExpQuadFillLine,
                fillOffset.X + ((fillSize.X - lineSize.X) / 2f),
                surfaceY - (lineSize.Y / 2f));

            DrawAtOffset(atlas, ExpQuadPorthole);
            DrawAtOffset(atlas, expKeyQuad);
            Renderer.PopMatrix();

            PostDraw();
            Renderer.SetColor(Color.White);
            Renderer.Disable(Renderer.GL_TEXTURE_2D);
            Renderer.Disable(Renderer.GL_BLEND);
        }

        /// <summary>
        /// Builds the sheet the loading screen, the level picker and the level transition share:
        /// the backdrop with the scroll hung along its bottom edge, laid out over the design box.
        /// </summary>
        /// <param name="onSheet">Drawn over the backdrop and under the scroll, or <see langword="null"/>.</param>
        /// <returns>The sheet, <see cref="ViewportLayout.DesignWidth"/> by <see cref="ViewportLayout.DesignHeight"/>.</returns>
        internal static BaseElement CreateExperimentsSheet(BaseElement onSheet = null)
        {
            float width = ViewportLayout.DesignWidth;
            float height = ViewportLayout.DesignHeight;
            BaseElement sheet = new()
            {
                width = (int)width,
                height = (int)height,
            };
            sheet.anchor = sheet.parentAnchor = 9;

            Image backdrop = Image.FromResource(Resources.BackgroundImg.MenuExpLoadingBgr);
            backdrop.anchor = backdrop.parentAnchor = 9;
            backdrop.scaleX = width / backdrop.width;
            backdrop.scaleY = height / backdrop.height;
            backdrop.x = (width - backdrop.width) / 2f;
            backdrop.y = (height - backdrop.height) / 2f;
            _ = sheet.AddChild(backdrop);
            if (onSheet != null)
            {
                _ = sheet.AddChild(onSheet);
            }

            Image scroll = Image.FromResource(Resources.Img.MenuExpLoadingScroll, 0);
            scroll.anchor = scroll.parentAnchor = 9;
            scroll.scaleX = width / scroll.width;
            scroll.scaleY = ExpScrollScaleY;
            scroll.x = (width - scroll.width) / 2f;
            scroll.y = height + ExpScrollCenterBelow - (scroll.height / 2f);
            _ = sheet.AddChild(scroll);
            return sheet;
        }

        /// <summary>
        /// How far the sheet's scroll hangs below the sheet's bottom edge, pull ring included.
        /// </summary>
        /// <returns>The overhang in design units.</returns>
        internal static float ExperimentsSheetOverhang()
        {
            return ExpScrollCenterBelow + (Image.GetQuadSize(Resources.Img.MenuExpLoadingScroll, 0).Y * ExpScrollScaleY / 2f);
        }

        /// <summary>
        /// Where iOS's portrait frame, which the porthole machine is drawn in, lands on the screen.
        /// </summary>
        /// <param name="visible">The logical region the viewport exposes.</param>
        /// <returns>The frame's top left in logical units and its scale.</returns>
        internal static ExperimentsFramePlacement ExperimentsFrame(Rectangle visible)
        {
            float scale = ContentFit.Scale;
            Rectangle fitted = LayoutMath.PlaceBox(ViewportLayout.DesignWidth, ViewportLayout.DesignHeight, visible, scale);
            return new ExperimentsFramePlacement(
                fitted.x + ((ViewportLayout.DesignWidth - ExpFrameWidth) / 2f * scale),
                fitted.y + (ExpFrameTop * scale),
                scale);
        }

        /// <summary>
        /// Draws a loading atlas quad where the atlas places it in iOS's frame.
        /// </summary>
        /// <param name="atlas">The loading atlas.</param>
        /// <param name="quad">Quad to draw.</param>
        private static void DrawAtOffset(Texture2D atlas, int quad)
        {
            Vector offset = Image.GetQuadOffset(Resources.Img.MenuExpLoading, quad);
            DrawHelper.DrawImageQuad(atlas, quad, offset.X, offset.Y);
        }

        /// <summary>
        /// Draws a loading atlas quad at its offset with the given opacity.
        /// </summary>
        /// <param name="atlas">The loading atlas.</param>
        /// <param name="quad">Quad to draw.</param>
        /// <param name="alpha">Opacity, 0 to 1.</param>
        private static void DrawFaded(Texture2D atlas, int quad, float alpha)
        {
            if (alpha <= 0f)
            {
                return;
            }

            Renderer.SetColor(RGBAColor.MakeRGBA(alpha, alpha, alpha, alpha).ToColor());
            DrawAtOffset(atlas, quad);
            Renderer.SetColor(Color.White);
        }
    }

    /// <summary>
    /// Where the Experiments porthole machine's frame is drawn.
    /// </summary>
    /// <param name="X">Left edge of the frame, in logical units.</param>
    /// <param name="Y">Top edge of the frame, in logical units.</param>
    /// <param name="Scale">Scale from atlas pixels to logical units.</param>
    internal readonly record struct ExperimentsFramePlacement(float X, float Y, float Scale);
}
