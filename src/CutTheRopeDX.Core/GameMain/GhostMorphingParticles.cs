using CutTheRopeDX.Framework;
using CutTheRopeDX.Framework.Visual;

namespace CutTheRopeDX.GameMain
{
    /// <summary>
    /// Rotating particle effect used around a ghost while it morphs between states.
    /// </summary>
    internal sealed class GhostMorphingParticles : RotateableMultiParticles
    {
        /// <inheritdoc />
        public override void InitParticle(ref Particle particle)
        {
            base.InitParticle(ref particle);
            // Integer division, as in the original: seven particles step 51 degrees each,
            // so a burst spans 357 and the next one starts three degrees around.
            angle += 360 / totalParticles;
            int quadIndex = RND_RANGE(4, 6);
            float scale = size + (RND_MINUS1_1 * sizeVar);
            SetParticleQuad(ref particle, quadIndex, scale);
            particle.deltaColor = RGBAColor.MakeRGBA(0f, 0f, 0f, 0f);
        }

        /// <inheritdoc />
        public override GhostMorphingParticles InitWithTotalParticles(int numberOfParticles)
        {
            if (InitWithTotalParticlesandImageGrid(numberOfParticles, Image.Image_createWithResID(Resources.Img.ObjGhost)) != null)
            {
                size = 0.6f;
                sizeVar = 0.2f;
                angle = RND_RANGE(0, 360);
                angleVar = 15f;
                rotateSpeedVar = 30f;
                life = 0.8f;
                lifeVar = 0.3f;
                duration = 1.5f;
                speed = 420f;
                speedVar = 105f;
                startColor = RGBAColor.solidOpaqueRGBA;
                endColor = RGBAColor.transparentRGBA;
            }
            return this;
        }

        /// <inheritdoc />
        public override void Update(float delta)
        {
            base.Update(delta);
            for (int i = 0; i < particleCount; i++)
            {
                ref Particle particle = ref particles[i];
                if (particle.life > 0f)
                {
                    float fadeThreshold = 0.7f * life;
                    if (particle.life < fadeThreshold)
                    {
                        particle.deltaColor.RedColor = (endColor.RedColor - startColor.RedColor) / fadeThreshold;
                        particle.deltaColor.GreenColor = (endColor.GreenColor - startColor.GreenColor) / fadeThreshold;
                        particle.deltaColor.BlueColor = (endColor.BlueColor - startColor.BlueColor) / fadeThreshold;
                        particle.deltaColor.AlphaChannel = (endColor.AlphaChannel - startColor.AlphaChannel) / fadeThreshold;
                    }
                    particle.dir = VectMult(particle.dir, 0.83f);
                    particle.width *= 1.015f;
                    particle.height *= 1.015f;
                }
            }
        }
    }
}
