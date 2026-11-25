using System;

using CutTheRopeDX.Framework;
using CutTheRopeDX.Framework.Platform;
using CutTheRopeDX.Framework.Visual;

namespace CutTheRopeDX.GameMain
{
    /// <summary>
    /// The iOS LockParticles burst, emitted 0.2 seconds into a pack's unlock animation.
    /// Positions and physics are in the pack scene's asset pixels.
    /// </summary>
    internal sealed class TimeTravelLockParticles : RotateableMultiParticles
    {
        private float delay = 0.2f;
        private bool started;

        /// <summary>Initializes the original 30-particle effect on the pack-selection sheet.</summary>
        /// <returns>The initialized effect.</returns>
        public TimeTravelLockParticles Init()
        {
            _ = InitWithTotalParticlesandImageGrid(30, Image.FromResource(Resources.Img.MenuPackSelectionTimeTravel));
            SetName("ttLockParticles");
            width = TimeTravelSceneGroup.Width;
            height = TimeTravelSceneGroup.Height;
            anchor = parentAnchor = 9;
            touchable = false;
            float units = FlashXmlScale.AtlasToFlashPointScale;
            duration = 3.5f;
            gravity.Y = 150f * units;
            angle = -90f;
            angleVar = 160f;
            speed = 280f * units;
            speedVar = 200f * units;
            rotateSpeedVar = 600f;
            life = 4f;
            lifeVar = 0.5f;
            size = 1f;
            sizeVar = 0.3f;
            emissionRate = 100f;
            startColor = RGBAColor.solidOpaqueRGBA;
            endColor = RGBAColor.transparentRGBA;
            blendAdditive = true;
            blendingMode = 2;
            return this;
        }

        /// <inheritdoc />
        public override void InitParticle(ref Particle particle)
        {
            base.InitParticle(ref particle);
            SetParticleQuad(ref particle, TimeTravelArt.LockParticle, particle.size);
            particle.deltaSize = -particle.size / particle.life;
        }

        /// <inheritdoc />
        public override void UpdateParticle(ref Particle particle, float delta)
        {
            particle.size = MathF.Max(0f, particle.size + (particle.deltaSize * delta));
            SetParticleSize(ref particle);
            base.UpdateParticle(ref particle, delta);
        }

        private void SetParticleSize(ref Particle particle)
        {
            particle.width = imageGrid.texture.quadRects[TimeTravelArt.LockParticle].w * particle.size;
            particle.height = imageGrid.texture.quadRects[TimeTravelArt.LockParticle].h * particle.size;
        }

        /// <inheritdoc />
        public override void Update(float delta)
        {
            if (!started)
            {
                delay -= delta;
                if (delay > 0f)
                {
                    return;
                }
                started = true;
                StartSystem(30);
                delta = -delay;
            }
            // RotateableMultiParticles.Update also runs MultiParticles.Update, and both
            // simulate particles. This effect follows the iOS single update per frame.
            if (active)
            {
                emitCounter += delta;
                float rate = 1f / emissionRate;
                while (particleCount < totalParticles && emitCounter > rate)
                {
                    _ = AddParticle();
                    emitCounter -= rate;
                }
                elapsed += delta;
                if (elapsed > duration)
                {
                    StopSystem();
                }
            }
            particleIdx = 0;
            while (particleIdx < particleCount)
            {
                UpdateParticle(ref particles[particleIdx], delta);
            }
            if (!active && particleCount == 0)
            {
                parent?.RemoveChild(this);
                Dispose();
            }
        }

        /// <inheritdoc />
        public override void Draw()
        {
            // MultiParticles vertices are in the parent's local space rather than drawX/drawY.
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
