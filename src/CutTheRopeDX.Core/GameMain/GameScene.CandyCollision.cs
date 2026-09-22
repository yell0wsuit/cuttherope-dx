using System.Collections.Generic;

using CutTheRopeDX.Framework;
using CutTheRopeDX.Framework.Physics;

namespace CutTheRopeDX.GameMain
{
    internal sealed partial class GameScene
    {
        /// <summary>
        /// Previous-frame center distance per candy pair (keyed by ordered candy indices),
        /// used by the PC-model HTML nudge's closing-in guard. Cleared per level in InitializeCandyObjects.
        /// </summary>
        private readonly Dictionary<(int, int), float> candyPairPrevDistance = [];

        /// <summary>
        /// Resolves candy-to-candy collisions for all independent candies, matching the engine's
        /// pairwise <c>handleCandyIntersection</c> loop. No-ops for single-candy levels.
        /// Candies that are eaten, carried by a bubble, or captured in a lantern do not collide.
        /// </summary>
        private void ResolveCandyCollisions(float delta)
        {
            int count = candies.Count;
            for (int i = 0; i < count; i++)
            {
                CandyContext ca = candies[i];
                if (!CandyCollision.ShouldParticipate(ca))
                {
                    continue;
                }
                for (int j = i + 1; j < count; j++)
                {
                    CandyContext cb = candies[j];
                    if (!CandyCollision.ShouldParticipate(cb))
                    {
                        continue;
                    }
                    // Handclap exemption: two hand-held candies pass through each other (spec §4).
                    if (ca.Lifecycle.Attachments.Hand != null && cb.Lifecycle.Attachments.Hand != null)
                    {
                        continue;
                    }
                    // Only whole bodies collide, so each candy contributes exactly one point here.
                    ConstrainedPoint pa = ca.WholeBody.Point;
                    ConstrainedPoint pb = cb.WholeBody.Point;
                    if (!CandyCollision.ShouldUseHtmlModel(ca, cb, ActivePhysicsConstants.UseMobilePhysicsModel))
                    {
                        // Mobile-style: radius-sum trigger + de-penetration.
                        float collisionDist = CandyCollision.PairDistance(ca, cb);
                        float dx = pa.pos.X - pb.pos.X;
                        float dy = pa.pos.Y - pb.pos.Y;
                        if (((dx * dx) + (dy * dy)) < (collisionDist * collisionDist))
                        {
                            CandyCollision.HandleCandyIntersection(pa, pb, collisionDist);
                        }
                    }
                    else
                    {
                        // PC: 0.9 * candy body width trigger (≈ surface touch, + closing-in guard + velocity-only nudge.
                        (int, int) key = (i, j);
                        float distance = VectDistance(pa.pos, pb.pos);
                        float previousDistance = candyPairPrevDistance.GetValueOrDefault(key);
                        if (CandyCollision.ShouldHtmlNudge(distance, previousDistance, GetCandyBoundingBox().w))
                        {
                            CandyCollision.ResolveCandyPairHtml(pa, pb, delta);
                        }
                        candyPairPrevDistance[key] = distance;
                    }
                }
            }
        }
    }
}
