using CutTheRopeDX.Framework;
using CutTheRopeDX.Framework.Helpers;
using CutTheRopeDX.Framework.Visual;

namespace CutTheRopeDX.GameMain
{
    /// <summary>
    /// Rotatable debris particle system used for bamboo tube exit effects.
    /// </summary>
    internal sealed class LeafParticles : RotateableMultiParticles
    {
        /// <summary>
        /// Initialises the particle system.
        /// </summary>
        /// <param name="totalParticles">Maximum live particle count.</param>
        /// <param name="angle">Emission direction in degrees.</param>
        /// <param name="grid">Texture atlas containing the debris quad at index 3.</param>
        /// <param name="bgx">
        /// Optional horizontal gravity bias. The magnitude is stored in <c>baseGravityX</c>
        /// and is smoothly reduced to zero over the system lifetime by <see cref="Update"/>.
        /// </param>
        /// <returns>The initialized particle system instance, or <see langword="null"/> if initialization fails.</returns>
        public LeafParticles Init(int totalParticles, float angle, Image grid, float bgx = 0f)
        {
            if (InitWithTotalParticlesandImageGrid(totalParticles, grid) == null)
            {
                return null;
            }

            duration = 5f;
            gravity.X = bgx;
            gravity.Y = 75f;
            baseGravityX = bgx < 0f ? 0f - bgx : bgx;
            this.angle = angle;
            angleVar = 45f;
            speed = 100f;
            speedVar = 10f;
            radialAccel = 0f;
            radialAccelVar = 1f;
            tangentialAccel = 0f;
            tangentialAccelVar = 1f;
            posVar.X = 0f;
            posVar.Y = 0f;
            life = 5f;
            lifeVar = 0f;
            size = 1f;
            sizeVar = 0f;
            emissionRate = 100f;
            startColor = RGBAColor.MakeRGBA(1f, 1f, 1f, 1f);
            startColorVar = RGBAColor.MakeRGBA(0f, 0f, 0f, 0f);
            endColor = RGBAColor.MakeRGBA(0f, 0f, 0f, 0f);
            endColorVar = RGBAColor.MakeRGBA(0f, 0f, 0f, 0f);
            rotateSpeed = 0f;
            rotateSpeedVar = 600f;
            blendAdditive = false;
            return this;
        }

        /// <inheritdoc />
        public override void Update(float delta)
        {
            base.Update(delta);
            float gravitySpeed = life <= 0f ? 0f : baseGravityX / life;
            float gravityX = gravity.X;
            _ = Mover.MoveVariableToTarget(ref gravityX, 0f, gravitySpeed, delta);
            gravity.X = gravityX;
        }

        /// <inheritdoc />
        public override void InitParticle(ref Particle particle)
        {
            base.InitParticle(ref particle);

            SetParticleQuad(3);

            particle.width = ((RND_MINUS1_1 * 4f) + 12f) * 3;
            particle.height = ((RND_MINUS1_1 * 4f) + 22f) * 3;
        }

        /// <summary>
        /// Absolute value of the initial horizontal gravity bias, used to compute the decay rate per frame.
        /// </summary>
        private float baseGravityX;
    }
}
