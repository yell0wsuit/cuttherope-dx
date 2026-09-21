using CutTheRopeDX.Framework;
using CutTheRopeDX.Framework.Core;
using CutTheRopeDX.Framework.Visual;

namespace CutTheRopeDX.GameMain
{
    /// <summary>
    /// Particle system that emits small spark particles from the rocket's exhaust.
    /// Particles are randomly selected from quad indices 6–9 of the rocket sprite sheet.
    /// </summary>
    internal class RocketSparks : RotatableScalableMultiParticles
    {
        /// <summary>
        /// Initializes the spark particle system with the given particle count, emission angle,
        /// and image grid. Configures particle lifetime, speed, color fade, and additive blending.
        /// </summary>
        /// <param name="p">The maximum number of particles.</param>
        /// <param name="a">The base emission angle in radians.</param>
        /// <param name="grid">The image grid containing spark particle quads.</param>
        /// <returns>This instance if initialization succeeds; otherwise, <see langword="null" />.</returns>
        public virtual Particles InitWithTotalParticlesAngleandImageGrid(int p, float a, Image grid)
        {
            if (InitWithTotalParticlesandImageGrid(p, grid) == null)
            {
                return null;
            }
            duration = -1f;
            gravity.X = 0f;
            gravity.Y = 0f;
            angle = a;
            angleVar = 10f;
            speed = 50f;
            speedVar = 10f;
            radialAccel = 0f;
            radialAccelVar = 0f;
            tangentialAccel = 0f;
            tangentialAccelVar = 0f;
            posVar.X = 5f;
            posVar.Y = 5f;
            life = 0.5f;
            lifeVar = 0.1f;
            size = 0.5f;
            sizeVar = 0f;
            endSize = size;
            emissionRate = 20f;
            startColor = RGBAColor.MakeRGBA(1f, 1f, 1f, 1f);
            startColorVar = RGBAColor.MakeRGBA(0f, 0f, 0f, 0f);
            endColor = RGBAColor.MakeRGBA(0f, 0f, 0f, 0f);
            endColorVar = RGBAColor.MakeRGBA(0f, 0f, 0f, 0f);
            blendAdditive = true;
            return this;
        }

        /// <inheritdoc />
        public override void InitParticle(ref Particle particle)
        {
            base.InitParticle(ref particle);
            int sparklesParticle = RND_RANGE(6, 9);
            SetParticleQuad(sparklesParticle);
            Vector quadSize = Image.GetQuadSize(Resources.Img.ObjRocket, sparklesParticle);
            particle.width = quadSize.X;
            particle.height = quadSize.Y;
        }
    }
}
