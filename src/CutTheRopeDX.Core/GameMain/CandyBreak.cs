using CutTheRopeDX.Framework;
using CutTheRopeDX.Framework.Core;
using CutTheRopeDX.Framework.Platform;
using CutTheRopeDX.Framework.Visual;

namespace CutTheRopeDX.GameMain
{
    /// <summary>
    /// Particle effect that breaks the candy into spinning fragments when it is destroyed.
    /// </summary>
    internal sealed class CandyBreak : RotateableMultiParticles
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
            gravity.Y = ActivePhysicsConstants.CandyBreakGravityY;
            angle = -90f;
            angleVar = 50f;
            speed = 150f;
            speedVar = 70f;
            radialAccel = 0f;
            radialAccelVar = 1f;
            tangentialAccel = 0f;
            tangentialAccelVar = 1f;
            posVar.X = 0f;
            posVar.Y = 0f;
            life = 3f;
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
            blendAdditive = false;
            return this;
        }

        /// <inheritdoc />
        public override void InitParticle(ref Particle particle)
        {
            base.InitParticle(ref particle);
            int quadIndex = RND_RANGE(3, 7);
            Quad2D qt = imageGrid.texture.quads[quadIndex];
            Quad3D qv = Quad3D.MakeQuad3D(0f, 0f, 0f, 0f, 0f);
            drawer.SetTextureQuadatVertexQuadatIndex(qt, qv, particleCount);
            Rectangle rectangle = imageGrid.texture.quadRects[quadIndex];
            particle.width = rectangle.w * particle.size;
            particle.height = rectangle.h * particle.size;
        }

        /// <inheritdoc />
        public override void Draw()
        {
            PreDraw();
            Renderer.SetBlendFunc(BlendingFactor.GLONE, BlendingFactor.GLONEMINUSSRCALPHA);
            Renderer.Enable(Renderer.GL_TEXTURE_2D);
            Renderer.BindTexture(drawer.image.texture.Name());
            int quadCount = particleIdx;
            if (quadCount > 0)
            {
                VertexPositionNormalTexture[] vertexBuffer = GetVertexBuffer(quadCount * 4);
                Renderer.FillTexturedVertices(drawer.vertices, drawer.texCoordinates, vertexBuffer, quadCount);
                Renderer.DrawTriangleList(vertexBuffer, drawer.indices, quadCount * 6);
            }
            PostDraw();
        }

        /// <summary>Cached vertex array reused across draw calls to avoid per-frame allocation.</summary>
        private VertexPositionNormalTexture[] verticesCache;

        /// <summary>
        /// Returns a cached vertex array, reallocating if the cache is too small.
        /// </summary>
        /// <param name="vertexCount">Minimum required capacity.</param>
        /// <returns>The cached or newly allocated array.</returns>
        private VertexPositionNormalTexture[] GetVertexBuffer(int vertexCount)
        {
            if (verticesCache == null || verticesCache.Length < vertexCount)
            {
                verticesCache = new VertexPositionNormalTexture[vertexCount];
            }
            return verticesCache;
        }
    }
}
