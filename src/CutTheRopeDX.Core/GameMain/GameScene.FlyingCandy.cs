using System;
using System.Collections.Generic;

using CutTheRopeDX.Framework;
using CutTheRopeDX.Framework.Core;
using CutTheRopeDX.Framework.Helpers;
using CutTheRopeDX.Framework.Media;
using CutTheRopeDX.Framework.Physics;
using CutTheRopeDX.Framework.Visual;

namespace CutTheRopeDX.GameMain
{
    /// <summary>
    /// Time Travel's flying candy: a candy authored with <c>isDriven="true"</c> grows wings and
    /// repeats every movement of the other candy instead of being simulated on its own. Its ropes
    /// can still hold it back, bouncers stop it rather than bounce it, and it loses its wings -
    /// becoming an ordinary falling candy - the moment any candy leaves play.
    /// </summary>
    internal sealed partial class GameScene
    {
        /// <summary>Candies whose <c>isDriven</c> flag was read during the parse, waiting for a leader.</summary>
        private readonly List<CandyContext> pendingFlyingCandies = [];

        /// <summary>
        /// Gives every candy authored with <c>isDriven</c> its wings and its leader, once all the
        /// level's candies exist. The leader is the first other edible candy; the original only
        /// ever pairs its first and second candy, which is the same pair on every authored level.
        /// </summary>
        private void InstallFlyingCandies()
        {
            foreach (CandyContext flier in pendingFlyingCandies)
            {
                CandyContext leader = FlyingCandyLeaderFor(flier);
                if (leader == null || flier.Lifecycle.Split != null || flier.WholeBody.Visual == null)
                {
                    continue;
                }

                Vector offset = VectSub(flier.WholeBody.Point.pos, leader.WholeBody.Point.pos);
                flier.Flight = new CandyFlight(leader, offset, CreateFlyingCandyWings(flier.WholeBody.Visual))
                {
                    FlapSound = SoundMgr.PlaySoundLooped(Resources.Snd.TTSynchroIdle),
                };
            }

            pendingFlyingCandies.Clear();
        }

        private CandyContext FlyingCandyLeaderFor(CandyContext flier)
        {
            foreach (CandyContext candidate in candies)
            {
                if (candidate != flier && candidate.Capabilities.CanBeEaten)
                {
                    return candidate;
                }
            }

            return null;
        }

        /// <summary>Builds the flapping wings on a flying candy's visual.</summary>
        /// <param name="candyVisual">The candy's root visual.</param>
        /// <returns>The wing animation, already playing.</returns>
        private static Animation CreateFlyingCandyWings(GameObject candyVisual)
        {
            Animation wings = Image.InitializeFromResource(new Animation(), Resources.Img.ObjCandyTimeTravel);
            wings.DoRestoreCutTransparency();
            wings.anchor = wings.parentAnchor = 18;
            wings.scaleX = 0.8f;
            wings.scaleY = 0.8f;
            wings.AddAnimationWithIDDelayLoopFirstLast(
                0,
                CandyFlightDefinition.FlapFrameDelay,
                Timeline.LoopType.TIMELINE_REPLAY,
                CandyFlightDefinition.FirstFlapQuad,
                CandyFlightDefinition.LastFlapQuad);

            Image wingRoot = Image.InitializeFromResource(new Image(), Resources.Img.ObjCandyTimeTravel, CandyFlightDefinition.WingRootQuad);
            wingRoot.DoRestoreCutTransparency();
            wingRoot.anchor = wingRoot.parentAnchor = 18;
            _ = wings.AddChild(wingRoot);

            wings.PlayTimeline(0);
            _ = candyVisual.AddChild(wings);
            return wings;
        }

        /// <summary>
        /// Places every flying candy at its leader's position plus its offset, ahead of candy
        /// integration. Runs whether or not time is frozen, as in the original.
        /// </summary>
        /// <remarks>
        /// Two things stop a flying candy following. A rope that would be pulled past
        /// <see cref="CandyFlightDefinition.RopeStretchAllowance"/> of its natural length holds it
        /// where it is - neither moved nor simulated this step. A bouncer in the way leaves it to
        /// ordinary simulation for the step. Either way the offset is re-read from the two candies,
        /// so when the way clears the flying candy resumes the chase from where it stopped.
        /// </remarks>
        private void StepFlyingCandies()
        {
            foreach (CandyContext flier in candies)
            {
                CandyFlight flight = flier.Flight;
                if (flight == null || !flight.IsFlying)
                {
                    continue;
                }

                flight.Hovering = false;
                if (flier.HasNoWholeBodyInPlay)
                {
                    // Inside a sock or tube itself. Wherever it comes out, the next step pulls it
                    // straight back beside its leader.
                    flight.RejoinPending = true;
                    continue;
                }

                ConstrainedPoint point = flier.WholeBody.Point;
                if (flight.Leader.HasNoWholeBodyInPlay)
                {
                    // The leader is inside a sock or a tube. Hold still, keeping the offset, and
                    // rejoin it wherever it comes out.
                    flight.Hovering = true;
                    flight.RejoinPending = true;
                    continue;
                }

                ConstrainedPoint leaderPoint = flight.Leader.WholeBody.Point;
                Vector target = VectAdd(leaderPoint.pos, flight.Offset);
                if (FlyingCandyRopeWouldOverstretch(point, target))
                {
                    flight.Hovering = true;
                    flight.Offset = VectSub(point.pos, leaderPoint.pos);
                    continue;
                }

                if (PointIntersectsBouncer(target))
                {
                    flight.Offset = VectSub(point.pos, leaderPoint.pos);
                    continue;
                }

                if (flight.RejoinPending)
                {
                    // Rejoining the leader is a jump across the level. Verlet would read the jump as
                    // velocity and throw the candy the same distance again past its leader for a
                    // frame, so the candy arrives at rest instead.
                    flight.RejoinPending = false;
                    point.prevPos = target;
                }

                point.pos = target;
            }
        }

        /// <summary>
        /// Whether a flying candy is out of reach of <paramref name="interaction"/>. Its position
        /// belongs to its leader, so nothing that blows it about can move it. A sock or tube still
        /// catches it, as Time Travel's sock does: its ropes drop and it is thrown to the far end,
        /// where the next step pulls it straight back beside its leader, wings intact. A hand, a
        /// mouse, ants or a lantern can take it too, and doing so breaks its wings.
        /// </summary>
        /// <param name="body">Body the scene system is asking about.</param>
        /// <param name="interaction">The scene system asking.</param>
        /// <returns><see langword="true"/> when the system must leave the body alone.</returns>
        private static bool FlyingCandyIgnores(CandyBody body, CandyInteraction interaction)
        {
            return body.Role == CandyBodyRole.Whole
                && body.Owner?.IsFlying == true
                && interaction is CandyInteraction.Pump
                    or CandyInteraction.Steam;
        }

        /// <summary>
        /// Whether moving a flying candy to <paramref name="target"/> would stretch one of its
        /// intact ropes past its allowance.
        /// </summary>
        private bool FlyingCandyRopeWouldOverstretch(ConstrainedPoint point, Vector target)
        {
            foreach (Grab grab in bungees)
            {
                Bungee rope = grab?.Rope;
                if (rope == null || rope.cut != -1 || rope.tail != point)
                {
                    continue;
                }

                float allowance = (rope.parts.Count - 1)
                    * ActivePhysicsConstants.BungeeRestLength
                    * CandyFlightDefinition.RopeStretchAllowance;
                if (VectDistance(rope.bungeeAnchor.pos, target) > allowance)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Whether either edge of any bouncer crosses the probe square around a point.</summary>
        private bool PointIntersectsBouncer(Vector position)
        {
            float half = CandyFlightDefinition.BouncerProbeHalfExtent;
            float x = position.X - half;
            float y = position.Y - half;
            float size = half * 2f;
            foreach (Bouncer bouncer in bouncers)
            {
                if (LineInRect(bouncer.t1.X, bouncer.t1.Y, bouncer.t2.X, bouncer.t2.Y, x, y, size, size)
                    || LineInRect(bouncer.b1.X, bouncer.b1.Y, bouncer.b2.X, bouncer.b2.Y, x, y, size, size))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Whether a body is a flying candy hanging still this step.</summary>
        private static bool IsHoveringFlyingCandy(CandyBody body)
        {
            return body.Role == CandyBodyRole.Whole && body.Owner?.Flight?.Hovering == true;
        }

        /// <summary>
        /// Sets every flying candy that ended the step across a bouncer edge back to
        /// <see cref="CandyFlightDefinition.BouncerStandOff"/> in front of it, at rest. A flying
        /// candy is never bounced; the bouncer only blocks it.
        /// </summary>
        /// <remarks>
        /// Time Travel updates every bouncer again inside this pass, once per flying candy, so a
        /// bouncer on a path travels an extra step each frame while a candy flies. That is kept:
        /// its level 9_13 is timed around a moving bouncer running at that speed.
        /// </remarks>
        /// <param name="delta">Elapsed time in seconds since the last update.</param>
        private void PushFlyingCandiesOutOfBouncers(float delta)
        {
            float half = CandyFlightDefinition.BouncerProbeHalfExtent;
            foreach (CandyContext flier in candies)
            {
                if (!flier.IsFlying || flier.HasNoWholeBodyInPlay)
                {
                    continue;
                }

                ConstrainedPoint point = flier.WholeBody.Point;
                foreach (Bouncer bouncer in bouncers)
                {
                    bouncer.Update(delta, timeFrozen);
                    float x = point.pos.X - half;
                    float y = point.pos.Y - half;
                    float size = half * 2f;
                    if (LineInRect(bouncer.t1.X, bouncer.t1.Y, bouncer.t2.X, bouncer.t2.Y, x, y, size, size))
                    {
                        PushOffBouncerEdge(point, bouncer.t1, bouncer.t2, VectRotate(Vect(0f, -1f), float.DegreesToRadians(bouncer.rotation)));
                    }
                    else if (LineInRect(bouncer.b1.X, bouncer.b1.Y, bouncer.b2.X, bouncer.b2.Y, x, y, size, size))
                    {
                        PushOffBouncerEdge(point, bouncer.b1, bouncer.b2, VectRotate(Vect(0f, 1f), float.DegreesToRadians(bouncer.rotation)));
                    }
                }
            }
        }

        /// <summary>
        /// Moves a point to the stand-off distance from an edge, on the side
        /// <paramref name="facing"/> points to, when its projection lands on the edge.
        /// </summary>
        private static void PushOffBouncerEdge(ConstrainedPoint point, Vector from, Vector to, Vector facing)
        {
            Vector edge = VectSub(to, from);
            float along = VectDot(edge, VectSub(point.pos, from));
            float t = along / VectDot(edge, edge);
            if (t > 1f || along < 0f)
            {
                return;
            }

            Vector foot = VectAdd(from, VectMult(edge, t));
            Vector away = VectSub(point.pos, foot);
            float length = VectLength(away);
            if (length <= 0f)
            {
                return;
            }

            // The side comes from the edge's own facing; only the lean comes from the candy.
            float nx = MathF.Abs(away.X / length);
            float ny = MathF.Abs(away.Y / length);
            nx = facing.X < 0f ? -nx : nx;
            ny = facing.Y < 0f ? -ny : ny;
            float standOff = CandyFlightDefinition.BouncerStandOff;
            point.pos = Vect(foot.X + (nx * standOff), foot.Y + (ny * standOff));
            point.prevPos = point.pos;
        }

        /// <summary>
        /// Breaks the wings of every flying candy. Any candy leaving play ends the flight of all of
        /// them, since the pair it depends on is gone.
        /// </summary>
        /// <param name="animate">
        /// Whether the wings shatter with sound and feathers. A candy lost off screen takes them
        /// down silently.
        /// </param>
        private void BreakAllFlyingCandyWings(bool animate)
        {
            foreach (CandyContext flier in candies)
            {
                BreakFlyingCandyWings(flier, animate);
            }
        }

        /// <summary>Breaks one candy's wings, turning it back into an ordinary candy.</summary>
        /// <param name="flier">The candy to ground.</param>
        /// <param name="animate">Whether the wings shatter with sound and feathers.</param>
        private void BreakFlyingCandyWings(CandyContext flier, bool animate)
        {
            CandyFlight flight = flier.Flight;
            if (flight == null || !flight.TryBreak())
            {
                return;
            }

            SoundMgr.StopLoopedSound(flight.FlapSound);
            flight.FlapSound = null;
            flight.Wings.visible = false;

            ConstrainedPoint point = flier.WholeBody.Point;
            if (animate)
            {
                SoundMgr.PlaySound(Resources.Snd.TTWingsBomb);
                GameObject visual = flier.WholeBody.Visual;
                float drift = point.v.X / 5f;
                SpawnWingsBreak(visual.x - CandyFlightDefinition.WingBreakOffsetX, visual.y - CandyFlightDefinition.WingBreakOffsetY, -135f, drift);
                SpawnWingsBreak(visual.x + CandyFlightDefinition.WingBreakOffsetX, visual.y - CandyFlightDefinition.WingBreakOffsetY, -45f, drift);
            }

            // A rocket-bound candy starts its flight under the rocket from rest.
            if (flier.Lifecycle.Attachments.HasActiveRocket)
            {
                point.v = vectZero;
                point.a = vectZero;
                point.posDelta = vectZero;
                point.prevPos = point.pos;
            }
        }

        private void SpawnWingsBreak(float x, float y, float angle, float drift)
        {
            Image grid = Image.FromResource(Resources.Img.ObjCandyTimeTravel);
            grid.DoRestoreCutTransparency();
            if (new WingsBreak().Init(grid, angle, drift) is not WingsBreak burst)
            {
                return;
            }

            burst.particlesDelegate = new Particles.ParticlesFinished(aniPool.ParticlesFinished);
            burst.x = x;
            burst.y = y;
            burst.StartSystem(WingsBreak.FeatherCount);
            _ = aniPool.AddChild(burst);
        }

        /// <summary>Stops or restarts every flying candy's wing flap to match the freeze.</summary>
        private void SyncFlyingCandyWingsToFreeze()
        {
            foreach (CandyContext flier in candies)
            {
                if (flier.IsFlying)
                {
                    flier.Flight.Wings.updateable = !timeFrozen;
                }
            }
        }

        /// <summary>Silences every flying candy's wing flap while time is frozen.</summary>
        private void StopFlyingCandyFlapSounds()
        {
            foreach (CandyContext flier in candies)
            {
                if (flier.Flight?.FlapSound is ISoundInstance sound)
                {
                    SoundMgr.StopLoopedSound(sound);
                    flier.Flight.FlapSound = null;
                }
            }
        }

        /// <summary>Restarts the wing flap of every candy still flying when time resumes.</summary>
        private void RestartFlyingCandyFlapSounds()
        {
            foreach (CandyContext flier in candies)
            {
                if (flier.IsFlying && flier.Flight.FlapSound == null)
                {
                    flier.Flight.FlapSound = SoundMgr.PlaySoundLooped(Resources.Snd.TTSynchroIdle);
                }
            }
        }
    }
}
