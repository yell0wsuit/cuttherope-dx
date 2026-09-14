using System;

using CutTheRopeDX.Framework.Visual;

namespace CutTheRopeDX.GameMain
{
    /// <summary>
    /// One frame of the easter egg: where Om Nom sits, how big he is, where his eyes point, and
    /// how far the overlay has faded.
    /// </summary>
    /// <param name="ScaleX">Horizontal scale.</param>
    /// <param name="ScaleY">Vertical scale, squash included.</param>
    /// <param name="EyeOffset">Horizontal pupil offset, in path units.</param>
    /// <param name="X">Horizontal placement, in design units.</param>
    /// <param name="Y">Vertical placement, in design units.</param>
    /// <param name="Alpha">Overlay opacity, 0 to 1, applied to the dim and to Om Nom alike.</param>
    /// <param name="ShowsOmNom">Whether Om Nom is drawn, or only the dim behind him.</param>
    internal readonly record struct EasterEggOmNomFrame(
        float ScaleX, float ScaleY, float EyeOffset, float X, float Y, float Alpha, bool ShowsOmNom);

    /// <summary>
    /// Drives the easter egg's timeline. A dim fades up over the level, then Om Nom springs up,
    /// glances left, right and back, holds, and sinks away. The level stays frozen until the
    /// overlay starts fading out, and the whole overlay can be dismissed early.
    /// </summary>
    internal sealed class EasterEggOmNomAnimation
    {
        private const float FadeInMs = 200f;
        private const float FadeOutMs = 200f;

        private const float RiseEndMs = 600f;
        private const float LookLeftEndMs = 1000f;
        private const float LookRightEndMs = 1600f;
        private const float LookBackEndMs = 2300f;
        private const float HoldEndMs = 2800f;
        private const float SinkEndMs = 3600f;

        /// <summary>How long each eye movement waits before it starts.</summary>
        private const float EyeDelayMs = 100f;

        private const float StartScale = 0.1f;

        /// <summary>
        /// Full size. The web edition rounds a 2.2 scale through a helper meant for pixel counts,
        /// which lands on 6 in this design space.
        /// </summary>
        private const float FullScale = 6f;

        private const float EyeTravel = 25f;
        private const float SinkDistance = 750f;
        private const float SquashDepth = 0.1f;

        private float elapsedMs;
        private bool running;
        private bool dismissing;
        private float dismissElapsedMs;
        private EasterEggOmNomFrame dismissedFrame;

        /// <summary>Gets a value indicating whether anything still needs drawing.</summary>
        public bool IsActive => running;

        /// <summary>
        /// Gets a value indicating whether the level should stay frozen. This drops as soon as the
        /// overlay starts fading out, so the fade plays over a level that is running again.
        /// </summary>
        public bool FreezesGameplay => running && !dismissing && elapsedMs < FadeInMs + SinkEndMs;

        /// <summary>Gets the current frame.</summary>
        public EasterEggOmNomFrame CurrentFrame { get; private set; }

        /// <summary>Restarts the timeline from the beginning.</summary>
        public void Start()
        {
            elapsedMs = 0f;
            running = true;
            dismissing = false;
            CurrentFrame = Evaluate(0f);
        }

        /// <summary>
        /// Freezes Om Nom where he is and fades the overlay out. Does nothing once the overlay is
        /// already fading out, whether from an earlier dismissal or the end of the timeline.
        /// </summary>
        /// <returns><see langword="true"/> when this call started the dismissal.</returns>
        public bool Cancel()
        {
            if (!FreezesGameplay)
            {
                return false;
            }

            dismissing = true;
            dismissElapsedMs = 0f;
            dismissedFrame = CurrentFrame;
            return true;
        }

        /// <summary>Advances the timeline.</summary>
        /// <param name="deltaSeconds">Seconds since the previous update.</param>
        public void Update(float deltaSeconds)
        {
            if (!running)
            {
                return;
            }

            if (dismissing)
            {
                dismissElapsedMs += deltaSeconds * 1000f;
                float remaining = 1f - (dismissElapsedMs / FadeOutMs);
                if (remaining <= 0f)
                {
                    Stop();
                    return;
                }
                CurrentFrame = dismissedFrame with { Alpha = dismissedFrame.Alpha * remaining };
                return;
            }

            elapsedMs += deltaSeconds * 1000f;
            if (elapsedMs >= FadeInMs + SinkEndMs + FadeOutMs)
            {
                Stop();
                return;
            }

            CurrentFrame = Evaluate(elapsedMs);
        }

        /// <summary>Ends the timeline at once, with no closing fade.</summary>
        public void Stop()
        {
            running = false;
            dismissing = false;
            CurrentFrame = default;
        }

        private static EasterEggOmNomFrame Evaluate(float elapsedMs)
        {
            float t = elapsedMs - FadeInMs;
            if (t < 0f)
            {
                // Only the dim is up while it fades in; Om Nom starts once it is fully opaque.
                return new EasterEggOmNomFrame(
                    StartScale, StartScale, 0f, 0f, 0f, elapsedMs / FadeInMs, false);
            }

            float scale = Scale(t);
            float sink = t >= HoldEndMs
                ? Easing.OutExpo(t - HoldEndMs, 0f, SinkDistance, SinkEndMs - HoldEndMs)
                : 0f;

            float x = 1250f - (scale / FullScale * 500f);
            float y = 1500f - (scale / FullScale * 1000f) + sink;

            float alpha = t > SinkEndMs
                ? Math.Clamp(1f - ((t - SinkEndMs) / FadeOutMs), 0f, 1f)
                : 1f;

            return new EasterEggOmNomFrame(
                scale, scale + Squash(t), EyeOffset(t), x, y, alpha, true);
        }

        private static float Scale(float t)
        {
            if (t < RiseEndMs)
            {
                return Easing.OutBack(t, StartScale, FullScale - StartScale, RiseEndMs, 1.5f);
            }
            if (t < HoldEndMs)
            {
                return FullScale;
            }
            float shrink = Easing.OutExpo(
                t - HoldEndMs, 0f, FullScale - StartScale, SinkEndMs - HoldEndMs);
            return Math.Max(FullScale - shrink, StartScale);
        }

        private static float EyeOffset(float t)
        {
            if (t < RiseEndMs)
            {
                return 0f;
            }
            if (t < LookLeftEndMs)
            {
                float start = RiseEndMs + EyeDelayMs;
                return t <= start
                    ? 0f
                    : -Easing.OutExpo(t - start, 0f, EyeTravel, LookLeftEndMs - start);
            }
            if (t < LookRightEndMs)
            {
                return -EyeTravel + Easing.InOutExpo(
                    t - LookLeftEndMs, 0f, EyeTravel * 2f, LookRightEndMs - LookLeftEndMs);
            }
            if (t < LookBackEndMs)
            {
                float start = LookRightEndMs + EyeDelayMs;
                return t <= start
                    ? EyeTravel
                    : EyeTravel - Easing.InOutExpo(t - start, 0f, EyeTravel, LookBackEndMs - start);
            }
            return 0f;
        }

        private static float Squash(float t)
        {
            if (t > RiseEndMs && t < LookRightEndMs)
            {
                return Easing.InOutBack(
                    t - RiseEndMs, 0f, SquashDepth, LookRightEndMs - RiseEndMs, 6f);
            }
            if (t >= LookRightEndMs && t < HoldEndMs)
            {
                return SquashDepth - Easing.InOutBack(
                    t - LookRightEndMs, 0f, SquashDepth, HoldEndMs - LookRightEndMs, 2f);
            }
            return 0f;
        }
    }
}
