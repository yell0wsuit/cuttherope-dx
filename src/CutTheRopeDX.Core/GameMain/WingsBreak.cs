using CutTheRopeDX.Framework;
using CutTheRopeDX.Framework.Core;
using CutTheRopeDX.Framework.Helpers;
using CutTheRopeDX.Framework.Platform;
using CutTheRopeDX.Framework.Visual;

namespace CutTheRopeDX.GameMain
{
    /// <summary>
    /// One broken wing's burst when a flying candy loses its wings: three spinning feathers that
    /// fly off to one side, drift with the candy's own sideways speed, and fade out. A flying
    /// candy sheds two, one per wing.
    /// </summary>
    /// <remarks>
    /// Time Travel steps this system twice per frame, so the feathers age and travel at double
    /// speed, while the sideways drift decays over the authored lifetime at the normal rate.
    /// </remarks>
    internal sealed class WingsBreak : RotateableMultiParticles
    {
        /// <summary>Feathers in one wing's burst.</summary>
        public const int FeatherCount = 3;

        /// <summary>Feather width relative to its quad (theirs: 0.7).</summary>
        private const float FeatherScaleX = 0.7f;

        /// <summary>Feather height relative to its quad (theirs: 0.9).</summary>
        private const float FeatherScaleY = 0.9f;

        /// <summary>Magnitude of the starting sideways drift, run down to zero over the lifetime.</summary>
        private float baseGravityX;

        /// <summary>Time the system has been running, advanced once per particle step.</summary>
        private float runTime;

        /// <summary>Initializes one wing's burst.</summary>
        /// <param name="grid">The <see cref="Resources.Img.ObjCandyTimeTravel"/> atlas.</param>
        /// <param name="angle">Emission direction in degrees.</param>
        /// <param name="drift">Starting sideways drift, in world units per second squared.</param>
        /// <returns>The initialized system, or <see langword="null"/> if initialization fails.</returns>
        public WingsBreak Init(Image grid, float angle, float drift)
        {
            if (InitWithTotalParticlesandImageGrid(FeatherCount, grid) == null)
            {
                return null;
            }

            float scale = BombDefinition.TimeTravelToWorldScale;
            duration = 5f;
            life = 5f;
            lifeVar = 0f;
            gravity.X = drift;
            gravity.Y = 75f * scale;
            baseGravityX = drift < 0f ? -drift : drift;
            this.angle = angle;
            angleVar = 45f;
            speed = 100f * scale;
            speedVar = 10f * scale;
            radialAccel = 0f;
            radialAccelVar = 1f;
            tangentialAccel = 0f;
            tangentialAccelVar = 1f;
            posVar.X = 0f;
            posVar.Y = 0f;
            size = 1f;
            sizeVar = 0f;
            emissionRate = 0f;
            startColor = RGBAColor.MakeRGBA(1f, 1f, 1f, 1f);
            startColorVar = RGBAColor.MakeRGBA(0f, 0f, 0f, 0f);
            endColor = RGBAColor.MakeRGBA(0f, 0f, 0f, 0f);
            endColorVar = RGBAColor.MakeRGBA(0f, 0f, 0f, 0f);
            rotateSpeed = 0f;
            rotateSpeedVar = 300f;
            blendAdditive = false;
            // The original's blending mode 1: premultiplied alpha, which the all-zero end color
            // fades out cleanly.
            blendingMode = 1;
            return this;
        }

        /// <inheritdoc />
        public override void Update(float delta)
        {
            base.Update(delta);

            // Time Travel's particle system stops itself once its duration has run, emitting or not,
            // which is what lets the finished burst be cleared away. Its clock advances with each of
            // the two steps.
            runTime += 2f * delta;
            if (active && runTime > duration)
            {
                StopSystem();
            }

            float gravityX = gravity.X;
            _ = Mover.MoveVariableToTarget(ref gravityX, 0f, baseGravityX / life, delta);
            gravity.X = gravityX;
        }

        /// <inheritdoc />
        public override void InitParticle(ref Particle particle)
        {
            base.InitParticle(ref particle);
            int quadIndex = RND_RANGE(CandyFlightDefinition.FirstDebrisQuad, CandyFlightDefinition.LastDebrisQuad);
            SetParticleQuad(ref particle, quadIndex, 1f);
            particle.width *= FeatherScaleX;
            particle.height *= FeatherScaleY;
        }

        /// <inheritdoc />
        public override void Draw()
        {
            // PreDraw applies blendingMode 1 and PostDraw puts the previous blend back.
            PreDraw();
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

        /// <summary>Cached vertex array reused across draw calls to avoid per-frame allocation.</summary>
        private VertexPositionColorTexture[] verticesCache;

        /// <summary>Returns a cached vertex array, reallocating if the cache is too small.</summary>
        /// <param name="vertexCount">Minimum required capacity.</param>
        /// <returns>The cached or newly allocated array.</returns>
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
