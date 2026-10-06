using System;

using CutTheRopeDX.Framework.Core;
using CutTheRopeDX.Framework.Platform;

namespace CutTheRopeDX.Framework.Visual
{
    /// <summary>
    /// A quad drawn wider than its art without stretching its ends: the left and right caps keep
    /// their art, and a narrow strip at the art's center stretches to fill the width between them.
    /// Suits art whose middle is the same from column to column, like a pill-shaped plate. The
    /// pieces are cut from the one quad, so the texels beside every cut are the art's own and
    /// filtering blends across the joins seamlessly.
    /// </summary>
    internal sealed class SlicedImage : Image
    {
        /// <summary>Width of the center strip that stretches, in art pixels.</summary>
        public const float StripWidth = 4f;

        /// <summary>One piece of the cut: where it is read from the art and where it is drawn.</summary>
        /// <param name="SourceStart">Left edge in the art, in art pixels.</param>
        /// <param name="SourceWidth">Width read from the art.</param>
        /// <param name="DrawStart">Left edge as drawn, from the element's left.</param>
        /// <param name="DrawWidth">Width as drawn.</param>
        public readonly record struct Slice(float SourceStart, float SourceWidth, float DrawStart, float DrawWidth);

        /// <summary>Creates a sliced image of one quad, as wide as its art until widened.</summary>
        /// <param name="resourceName">Texture resource name.</param>
        /// <param name="quad">Quad to draw.</param>
        /// <returns>The image.</returns>
        public static SlicedImage Create(string resourceName, int quad)
        {
            return InitializeFromResource(new SlicedImage(), resourceName, quad);
        }

        /// <summary>Gets the width of the art this image draws.</summary>
        public int ArtWidth => (int)texture.quadRects[quadToDraw].w;

        /// <summary>Cuts art into its left cap, center strip and right cap for a drawn width.</summary>
        /// <param name="artWidth">Width of the art.</param>
        /// <param name="width">Width to draw at; never less than the art's.</param>
        /// <returns>The three pieces, left to right.</returns>
        public static (Slice Left, Slice Middle, Slice Right) Cut(float artWidth, float width)
        {
            // Whole art pixels on the left, so the strip is read on texel boundaries.
            float leftCap = MathF.Floor((artWidth - StripWidth) / 2f);
            float rightStart = leftCap + StripWidth;
            float rightCap = artWidth - rightStart;
            float middleWidth = MathF.Max(width, artWidth) - leftCap - rightCap;
            return (
                new Slice(0f, leftCap, 0f, leftCap),
                new Slice(leftCap, StripWidth, leftCap, middleWidth),
                new Slice(rightStart, rightCap, leftCap + middleWidth, rightCap));
        }

        /// <inheritdoc />
        public override void DrawQuad(int n)
        {
            float artWidth = texture.quadRects[n].w;
            if (width <= artWidth)
            {
                base.DrawQuad(n);
                return;
            }
            float h = texture.quadRects[n].h;
            float x = drawX;
            float y = drawY;
            if (restoreCutTransparency)
            {
                x += texture.quadOffsets[n].X;
                y += texture.quadOffsets[n].Y;
            }
            Quad2D quad = texture.quads[n];
            Renderer.Enable(Renderer.GL_TEXTURE_2D);
            Renderer.BindTexture(texture);
            (Slice left, Slice middle, Slice right) = Cut(artWidth, width);
            DrawSlice(left, quad, artWidth, x, y, h);
            DrawSlice(middle, quad, artWidth, x, y, h);
            DrawSlice(right, quad, artWidth, x, y, h);
        }

        /// <summary>Draws one piece, reading its span of the quad's texture coordinates.</summary>
        private static void DrawSlice(Slice slice, Quad2D quad, float artWidth, float x, float y, float h)
        {
            float span = quad.brX - quad.tlX;
            float u0 = quad.tlX + (span * slice.SourceStart / artWidth);
            float u1 = quad.tlX + (span * (slice.SourceStart + slice.SourceWidth) / artWidth);
            VertexPositionNormalTexture[] vertices = QuadVertexCache.GetTexturedQuad(
                x + slice.DrawStart, y, slice.DrawWidth, h,
                u0, quad.tlY, u1, quad.brY);
            Renderer.DrawTriangleStrip(vertices);
        }
    }
}
