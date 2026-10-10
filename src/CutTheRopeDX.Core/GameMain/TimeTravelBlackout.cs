using System;

using CutTheRopeDX.Framework;
using CutTheRopeDX.Framework.Core;
using CutTheRopeDX.Framework.Platform;
using CutTheRopeDX.Framework.Visual;

using Vector3 = System.Numerics.Vector3;

namespace CutTheRopeDX.GameMain
{
    /// <summary>The native screen dim with a soft, transparent spotlight around Om Nom.</summary>
    internal sealed class TimeTravelBlackout : BaseElement
    {
        private readonly VertexPositionColor[] strip = new VertexPositionColor[258];
        private Vector focus;
        private float units = 3f;

        /// <summary>Initializes the spotlight overlay.</summary>
        public TimeTravelBlackout()
        {
            SetName("ttOmNomBlackout");
            anchor = parentAnchor = 9;
            touchable = false;
        }

        /// <summary>Places the spotlight in screen space.</summary>
        /// <param name="bounds">Visible viewport bounds.</param>
        /// <param name="center">Om Nom's screen position.</param>
        /// <param name="scale">Screen units per native logical unit.</param>
        public void Place(Rectangle bounds, Vector center, float scale)
        {
            width = (int)MathF.Ceiling(bounds.w);
            height = (int)MathF.Ceiling(bounds.h);
            focus = center;
            units = scale;
        }

        /// <summary>Samples the original quarter-resolution mask's radial alpha formula.</summary>
        /// <param name="point">Screen position to sample.</param>
        /// <returns>The mask alpha before its timeline fade.</returns>
        internal float AlphaAt(Vector point)
        {
            float dx = (point.X - focus.X) / (4f * units);
            float dy = (point.Y - focus.Y) / (4f * units);
            return (1f - (0.5f / ((((dx * dx) + (dy * dy)) / 1600f) + 0.5f))) * (200f / 255f);
        }

        /// <inheritdoc />
        public override void Draw()
        {
            PreDraw();
            Renderer.Disable(Renderer.GL_TEXTURE_2D);
            Renderer.SetColor(Color.White);
            int columns = Math.Clamp((int)MathF.Ceiling(width / 32f), 1, 128);
            int rows = Math.Clamp((int)MathF.Ceiling(height / 32f), 1, 128);
            for (int row = 0; row < rows; row++)
            {
                for (int column = 0; column <= columns; column++)
                {
                    for (int edge = 0; edge < 2; edge++)
                    {
                        float px = width * (float)column / columns;
                        float py = height * (float)(row + edge) / rows;
                        strip[(column * 2) + edge] = new VertexPositionColor(
                            new Vector3(drawX + px, drawY + py, 0f),
                            new Color(0f, 0f, 0f, AlphaAt(new Vector(px, py)) * color.AlphaChannel));
                    }
                }
                Renderer.DrawTriangleStrip(strip, (columns + 1) * 2);
            }
            Renderer.Enable(Renderer.GL_TEXTURE_2D);
            PostDraw();
        }
    }
}
