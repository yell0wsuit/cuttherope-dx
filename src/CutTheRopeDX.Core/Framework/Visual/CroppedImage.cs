using System;

using CutTheRopeDX.Framework.Core;
using CutTheRopeDX.Framework.Platform;

namespace CutTheRopeDX.Framework.Visual
{
    /// <summary>
    /// An image that draws only a horizontal band of its quad, the rows between
    /// <see cref="CropTop"/> and <see cref="CropBottom"/> cut away. The band keeps the place it has
    /// in the whole quad, so a cropped image and its uncropped twin register.
    /// </summary>
    internal sealed class CroppedImage : Image
    {
        /// <summary>Gets or sets how many rows are cut from the top, in quad pixels.</summary>
        public float CropTop { get; set; }

        /// <summary>Gets or sets how many rows are cut from the bottom, in quad pixels.</summary>
        public float CropBottom { get; set; }

        /// <summary>Creates a cropped image of a quad.</summary>
        /// <param name="resource">Sheet holding the quad.</param>
        /// <param name="quad">Quad to draw.</param>
        /// <returns>The image, cropped by nothing yet.</returns>
        public static CroppedImage Create(string resource, int quad)
        {
            return InitializeFromResource(new CroppedImage(), resource, quad);
        }

        /// <inheritdoc />
        public override void DrawQuad(int n)
        {
            float w = texture.quadRects[n].w;
            float h = texture.quadRects[n].h;
            float top = Math.Clamp(CropTop, 0f, h);
            float bottom = Math.Clamp(h - CropBottom, top, h);
            if (bottom - top <= 0f)
            {
                return;
            }

            float x = drawX;
            float y = drawY;
            if (restoreCutTransparency)
            {
                x += texture.quadOffsets[n].X;
                y += texture.quadOffsets[n].Y;
            }
            Quad2D quad = texture.quads[n];
            float v0 = quad.tlY + ((quad.brY - quad.tlY) * (top / h));
            float v1 = quad.tlY + ((quad.brY - quad.tlY) * (bottom / h));
            Renderer.Enable(Renderer.GL_TEXTURE_2D);
            Renderer.BindTexture(texture);
            VertexPositionNormalTexture[] vertices = QuadVertexCache.GetTexturedQuad(
                x, y + top, w, bottom - top,
                quad.tlX, v0, quad.brX, v1);
            Renderer.DrawTriangleStrip(vertices);
        }
    }
}
