using CutTheRopeDX.Framework;
using CutTheRopeDX.Framework.Core;
using CutTheRopeDX.Framework.Platform;
using CutTheRopeDX.Framework.Visual;

namespace CutTheRopeDX.GameMain
{
    /// <summary>
    /// Particle effect used for the star break burst.
    /// </summary>
    /// <remarks>
    /// Unused in the game.
    /// </remarks>
    internal sealed class StarsBreak : RotateableMultiParticles
    {
        /// <inheritdoc />
        public override Particles InitWithTotalParticlesandImageGrid(int p, Image grid)
        {
            if (base.InitWithTotalParticlesandImageGrid(p, grid) == null)
            {
                return null;
            }
            duration = 2f;
            gravity.X = 0f;
            gravity.Y = 200f;
            angle = -90f;
            angleVar = 50f;
            speed = 150f;
            speedVar = 70f;
            radialAccel = 0f;
            radialAccelVar = 1f;
            tangentialAccel = 0f;
            tangentialAccelVar = 1f;
            x = SCREEN_WIDTH / 2f;
            y = SCREEN_HEIGHT / 2f;
            posVar.X = SCREEN_WIDTH / 2f;
            posVar.Y = SCREEN_HEIGHT / 2f;
            life = 4f;
            lifeVar = 0f;
            size = 1f;
            sizeVar = 0f;
            emissionRate = 100f;
            startColor = RGBAColor.MakeRGBA(1f, 1f, 1f, 1f);
            startColorVar = RGBAColor.MakeRGBA(0f, 0f, 0f, 0f);
            endColor = RGBAColor.MakeRGBA(1f, 1f, 1f, 1f);
            endColorVar = RGBAColor.MakeRGBA(0f, 0f, 0f, 0f);
            rotateSpeed = 0f;
            rotateSpeedVar = 600f;
            blendAdditive = true;
            return this;
        }

        /// <inheritdoc />
        public override void Draw()
        {
            PreDraw();
            Renderer.SetBlendFunc(BlendingFactor.GLSRCALPHA, BlendingFactor.GLONE);
            Renderer.Enable(Renderer.GL_TEXTURE_2D);
            Renderer.BindTexture(drawer.image.texture);
            int quadCount = particleIdx;
            if (quadCount > 0)
            {
                VertexPositionColorTexture[] vertexBuffer = GetVertexBuffer(quadCount * 4);
                Renderer.FillTexturedColoredVertices(drawer.vertices, drawer.texCoordinates, colors, vertexBuffer, quadCount);
                Renderer.DrawTriangleList(vertexBuffer, drawer.indices, quadCount * 6);
            }
            PostDraw();
        }

        /// <summary>Reusable vertex buffer for drawing the active particle quads.</summary>
        private VertexPositionColorTexture[] verticesCache;

        /// <summary>
        /// Gets a reusable vertex buffer with at least the requested capacity.
        /// </summary>
        /// <param name="vertexCount">Minimum number of vertices required.</param>
        /// <returns>A vertex buffer with at least <paramref name="vertexCount"/> entries.</returns>
        private VertexPositionColorTexture[] GetVertexBuffer(int vertexCount)
        {
            if (verticesCache == null || verticesCache.Length < vertexCount)
            {
                verticesCache = new VertexPositionColorTexture[vertexCount];
            }
            return verticesCache;
        }
    }
}
