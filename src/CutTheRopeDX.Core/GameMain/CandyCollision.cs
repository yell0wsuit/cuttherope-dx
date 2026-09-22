using System;

using CutTheRopeDX.Framework.Core;
using CutTheRopeDX.Framework.Physics;

using static CutTheRopeDX.Framework.Helpers.MathHelper;

namespace CutTheRopeDX.GameMain
{
    /// <summary>
    /// Candy-collision eligibility checks and the pairwise responses they select.
    /// </summary>
    internal static class CandyCollision
    {
        /// <summary>HTML nudge: X-axis reverse-velocity scale (engine constant).</summary>
        public const float HtmlNudgeScaleX = 62.5f;

        /// <summary>HTML nudge: Y-axis reverse-velocity scale (engine constant).</summary>
        public const float HtmlNudgeScaleY = 75f;

        /// <summary>
        /// HTML candy↔candy trigger: fraction of the candy's bounding-box WIDTH. The HTML engine
        /// uses the width of the candy bounds (112 wide), not the
        /// radius — so 0.9× of it is roughly the surface-touch distance (≈ the radius sum), not a
        /// near-center overlap.
        /// </summary>
        public const float HtmlTriggerWidthFactor = 0.9f;

        /// <summary>
        /// Whether a candy takes part in candy-to-candy collision: it needs a whole body in play and
        /// must not be held in a lantern.
        /// </summary>
        /// <param name="hasNoWholeBodyInPlay">Whether the candy's lifecycle leaves no whole body in play.</param>
        /// <param name="inLantern">Whether a lantern currently holds the candy.</param>
        /// <returns><see langword="true"/> when the candy can collide.</returns>
        public static bool ShouldParticipate(bool hasNoWholeBodyInPlay, bool inLantern)
        {
            return !hasNoWholeBodyInPlay && !inLantern;
        }

        /// <summary>
        /// Whether a candy-like body takes part in candy-to-candy collision: its lifecycle must leave
        /// a whole body in play, no lantern may hold it, and its capabilities must allow body collision
        /// (an axe, for instance, is a physical hazard that does not push candies around).
        /// </summary>
        public static bool ShouldParticipate(CandyContext ctx)
        {
            return ctx != null
                && ShouldParticipate(ctx.HasNoWholeBodyInPlay, ctx.Lifecycle.Attachments.InLantern)
                && ctx.Capabilities.CanCollideWithCandyBodies;
        }

        public static float PairDistance(CandyContext a, CandyContext b)
        {
            return a.collisionDistanceOverride.HasValue || b.collisionDistanceOverride.HasValue
                ? MathF.Max(a.collisionDistanceOverride ?? 0f, b.collisionDistanceOverride ?? 0f)
                : a.CollisionRadius + b.CollisionRadius;
        }

        /// <summary>
        /// Selects the HTML candy↔candy nudge model. Candy-like non-candy bodies keep the
        /// mobile overlap solver even when the level uses desktop physics tuning.
        /// </summary>
        public static bool ShouldUseHtmlModel(CandyContext a, CandyContext b, bool useMobilePhysicsModel)
        {
            return !useMobilePhysicsModel
                && a.Capabilities == CandyCapabilities.Candy
                && b.Capabilities == CandyCapabilities.Candy;
        }

        /// <summary>
        /// HTML-build candy↔candy trigger: fire only when the centers are within
        /// <see cref="HtmlTriggerWidthFactor"/> × the candy's bounding-box width AND still closing in (current distance below the previous frame's).
        /// </summary>
        public static bool ShouldHtmlNudge(float distance, float previousDistance, float candyBodyWidth)
        {
            return distance <= HtmlTriggerWidthFactor * candyBodyWidth && distance < previousDistance;
        }

        /// <summary>
        /// HTML-build candy↔candy nudge impulse for point <paramref name="a"/>.
        /// Each point contributes its reverse last-frame displacement (prevPos − pos) scaled by
        /// <see cref="HtmlNudgeScaleX"/>/<see cref="HtmlNudgeScaleY"/>; the impulse is their
        /// difference. Point <paramref name="b"/>'s impulse is the negation of this one.
        /// </summary>
        public static Vector HtmlNudgeImpulse(ConstrainedPoint a, ConstrainedPoint b)
        {
            float aReverseX = (a.prevPos.X - a.pos.X) * HtmlNudgeScaleX;
            float aReverseY = (a.prevPos.Y - a.pos.Y) * HtmlNudgeScaleY;
            float bReverseX = (b.prevPos.X - b.pos.X) * HtmlNudgeScaleX;
            float bReverseY = (b.prevPos.Y - b.pos.Y) * HtmlNudgeScaleY;
            return new Vector(aReverseX - bReverseX, aReverseY - bReverseY);
        }

        /// <summary>
        /// PC-model candy↔candy response: the HTML build's velocity-only nudge. Shifts each
        /// candy's position by the equal-and-opposite impulse via <see cref="MaterialPoint.ApplyImpulseDelta"/>
        /// (leaving prevPos, so it reads as injected velocity in the Verlet step). No de-penetration.
        /// </summary>
        internal static void ResolveCandyPairHtml(ConstrainedPoint a, ConstrainedPoint b, float delta)
        {
            Vector impulseA = HtmlNudgeImpulse(a, b);
            a.ApplyImpulseDelta(impulseA, delta);
            b.ApplyImpulseDelta(VectMult(impulseA, -1f), delta);
        }

        /// <summary>
        /// Resolves a single elastic body overlap. Ported from the engine's
        /// <c>GameScene::handleCandyIntersection</c>, which the engine uses for every elastic
        /// body collision (candy↔candy and light-bulb collisions both route through here).
        /// </summary>
        /// <param name="a">First body point.</param>
        /// <param name="b">Second body point.</param>
        /// <param name="collisionDist">
        /// Collision distance threshold (the engine's <c>a5</c>). For candy↔candy this is the sum
        /// of the two candy radii; for light bulbs it is the bulb collision distance.
        /// </param>
        internal static void HandleCandyIntersection(ConstrainedPoint a, ConstrainedPoint b, float collisionDist)
        {
            float dx = a.pos.X - b.pos.X;
            float dy = a.pos.Y - b.pos.Y;
            float distSq = (dx * dx) + (dy * dy);
            if (distSq >= (collisionDist * collisionDist))
            {
                return;
            }

            float dist = MathF.Sqrt(distSq);
            float penetration = collisionDist - dist;
            float half = penetration * 0.5f;

            // Unit normal pointing from b toward a (algebraic equivalent of the engine's
            // acos/sincos separation: each candy moves half the penetration along the normal).
            float invDist = dist > 0f ? 1f / dist : 0f;
            float nx = dx * invDist;
            float ny = dy * invDist;

            float speedA = MathF.Sqrt((a.v.X * a.v.X) + (a.v.Y * a.v.Y));
            float speedB = MathF.Sqrt((b.v.X * b.v.X) + (b.v.Y * b.v.Y));
            float combinedSpeed = speedA + speedB;

            // High closing speed -> full elastic response; low speed -> gentle separation only.
            // (Engine threshold: penetration >= 1000/speedA-ish + 1000/speedB-ish == 2000/combined.)
            bool fullResolve = combinedSpeed > 0f && penetration >= (2000f / combinedSpeed);

            if (fullResolve)
            {
                // Equal-mass elastic velocity exchange along the contact axis. The engine
                // normalizes the solve by collisionDist (the radius sum), not the live distance.
                float avX = a.v.X;
                float avY = a.v.Y;
                float bvX = b.v.X;

                float dxBA = -dx; // b.x - a.x
                float dyBA = -dy; // b.y - a.y
                float c = dxBA / collisionDist;
                float s = dyBA / collisionDist;

                float t28 = dxBA * avX;
                float t29 = (t28 + (dyBA * avY)) / collisionDist;
                float t30 = dyBA * avX;
                float t31 = (t30 + (dxBA * bvX)) / collisionDist;
                float t32 = ((dxBA * avY) - t30) / collisionDist;
                float t35 = (t28 - (bvX * dyBA)) / collisionDist;

                a.v.X = (t31 * c) - (t32 * s);
                a.v.Y = (t32 * c) + (t31 * s);
                b.v.X = (t29 * c) - (t35 * s);
                b.v.Y = (t35 * c) + (t29 * s);

                a.pos.X += nx * half;
                a.pos.Y += ny * half;
                b.pos.X -= nx * half;
                b.pos.Y -= ny * half;

                // Rebuild the verlet history from the new velocity (dt = 1/60s).
                a.posDelta.X = a.v.X / 60f;
                a.posDelta.Y = a.v.Y / 60f;
                a.prevPos.X = a.pos.X - a.posDelta.X;
                a.prevPos.Y = a.pos.Y - a.posDelta.Y;
                b.posDelta.X = b.v.X / 60f;
                b.posDelta.Y = b.v.Y / 60f;
                b.prevPos.X = b.pos.X - b.posDelta.X;
                b.prevPos.Y = b.pos.Y - b.posDelta.Y;
            }
            else
            {
                // Low closing speed: separate positions only, leave velocities untouched.
                a.pos.X += nx * half;
                a.pos.Y += ny * half;
                b.pos.X -= nx * half;
                b.pos.Y -= ny * half;
            }
        }
    }
}
