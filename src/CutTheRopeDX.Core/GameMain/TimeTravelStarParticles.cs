using System;

using CutTheRopeDX.Framework;
using CutTheRopeDX.Framework.Platform;
using CutTheRopeDX.Framework.Visual;

namespace CutTheRopeDX.GameMain
{
    /// <summary>The native result-star burst, simulated in the overlay's asset pixels.</summary>
    internal sealed class TimeTravelStarParticles : RotateableMultiParticles
    {
        /// <summary>Starts the native fifteen-particle yellow-star burst.</summary>
        /// <returns>The initialized burst.</returns>
        public TimeTravelStarParticles Init()
        {
            _ = InitWithTotalParticlesandImageGrid(15, Image.FromResource(Resources.Img.ResultScreenTimeTravel));
            SetName("ttResultStarParticles");
            anchor = parentAnchor = 9;
            touchable = false;
            float units = FlashXmlScale.AtlasToFlashPointScale;
            duration = 4f;
            gravity.Y = 150f * units;
            angle = -90f;
            angleVar = 160f;
            speed = 300f * units;
            speedVar = 200f * units;
            rotateSpeedVar = 600f;
            life = 5f;
            size = 0.9f;
            sizeVar = 0.3f;
            startColor = RGBAColor.solidOpaqueRGBA;
            endColor = RGBAColor.solidOpaqueRGBA;
            blendAdditive = true;
            blendingMode = 2;
            return this;
        }

        /// <inheritdoc />
        public override void InitParticle(ref Particle particle)
        {
            base.InitParticle(ref particle);
            SetParticleQuad(ref particle, TimeTravelArt.ResultStarParticleFirst + RND(1), particle.size);
            particle.deltaSize = -particle.size / particle.life;
        }

        /// <inheritdoc />
        public override void UpdateParticle(ref Particle particle, float delta)
        {
            float previous = particle.size;
            particle.size = MathF.Max(0f, previous + (particle.deltaSize * delta));
            float ratio = previous > 0f ? particle.size / previous : 0f;
            particle.width *= ratio;
            particle.height *= ratio;
            base.UpdateParticle(ref particle, delta);
        }

        /// <inheritdoc />
        public override void Update(float delta)
        {
            // The generic rotating emitter simulates twice. Native StarParticles advances once.
            particleIdx = 0;
            while (particleIdx < particleCount)
            {
                UpdateParticle(ref particles[particleIdx], delta);
            }
            if (particleCount == 0)
            {
                parent?.RemoveChild(this);
                Dispose();
            }
        }

        /// <inheritdoc />
        public override void Draw()
        {
            float offsetX = parent?.drawX ?? 0f;
            float offsetY = parent?.drawY ?? 0f;
            Renderer.Translate(offsetX, offsetY, 0f);
            try
            {
                base.Draw();
            }
            finally
            {
                Renderer.Translate(-offsetX, -offsetY, 0f);
            }
        }
    }
}
