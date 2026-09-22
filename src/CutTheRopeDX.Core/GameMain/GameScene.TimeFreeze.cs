using CutTheRopeDX.Framework.Core;
using CutTheRopeDX.Framework.Helpers;
using CutTheRopeDX.Framework.Media;
using CutTheRopeDX.Framework.Physics;
using CutTheRopeDX.Framework.Visual;
using CutTheRopeDX.GameMain.Tutorials;

namespace CutTheRopeDX.GameMain
{
    internal sealed partial class GameScene
    {
        /// <summary>One pointer's hold on a time-freeze button, from press to release.</summary>
        /// <param name="PointerIndex">Pointer that pressed the button.</param>
        /// <param name="Switcher">Button the pointer pressed.</param>
        private readonly record struct PauseSwitcherTouch(int PointerIndex, PauseSwitcher Switcher);

        /// <summary>Finds the switcher under a world point.</summary>
        /// <param name="worldX">World-space X.</param>
        /// <param name="worldY">World-space Y.</param>
        /// <returns>The switcher under the point, or <see langword="null"/>.</returns>
        private PauseSwitcher PauseSwitcherAt(float worldX, float worldY)
        {
            if (pauseSwitchers == null)
            {
                return null;
            }

            foreach (PauseSwitcher switcher in pauseSwitchers)
            {
                if (switcher != null && GameObject.PointInObject(new Vector(worldX, worldY), switcher))
                {
                    return switcher;
                }
            }

            return null;
        }

        /// <summary>
        /// Stops time if it is running, restarts it if it is stopped, and updates the button face.
        /// </summary>
        /// <param name="switcher">The switcher that was pressed.</param>
        private void ToggleTimeFreeze(PauseSwitcher switcher)
        {
            timeFrozen = !timeFrozen;
            particlesAniPool.updateable = !timeFrozen;
            foreach (Rocket rocket in rockets ?? [])
            {
                rocket?.SetExhaustHidden(timeFrozen);
            }
            if (timeFrozen)
            {
                switcher.ShowFrozen();
                pauseSwitcherWaves?.PlayFadeIn();
                StopLoopingMoverSounds();
                SoundMgr.PlaySound(Resources.Snd.PauseDown);
            }
            else
            {
                switcher.ShowRunning();
                pauseSwitcherWaves?.PlayFadeOut();
                RestartLoopingMoverSounds();
                SoundMgr.PlaySound(Resources.Snd.PauseUp);
            }

            tutorialDirector.Fire(timeFrozen ? TutorialEvent.TimeFreeze : TutorialEvent.TimeUnfreeze);
        }

        /// <summary>
        /// Stops or restarts every bubble overlay's animation to match the freeze, leaving the rest of
        /// each candy animating. Time Travel flips the same updateable flag on a bubbled candy's
        /// bubble when the pause switcher toggles; applying it every step also covers a body that
        /// splits or comes back from transport while time is stopped.
        /// </summary>
        private void SyncBubbleAnimationsToFreeze()
        {
            bool running = !timeFrozen;
            foreach (CandyContext ctx in candies)
            {
                SetBubbleAnimationsUpdateable(ctx.WholeBody, running);
                if (ctx.Lifecycle.Split is SplitCandyState split)
                {
                    foreach (CandyBody half in split.SurvivingBodies)
                    {
                        SetBubbleAnimationsUpdateable(half, running);
                    }
                }

                // A bulb draws its own bubble overlays rather than handing them to its body.
                if (ctx.LightBulb is LightBulb bulb)
                {
                    bulb.BubbleAnimation.updateable = running;
                    HoldGhostBubbleFrames(bulb.GhostBubbleAnimation, !running);
                }
            }
        }

        /// <summary>Sets whether a body's bubble and ghost-bubble animations advance.</summary>
        /// <param name="body">The body whose overlays to set, or <see langword="null"/>.</param>
        /// <param name="updateable">Whether the overlays advance.</param>
        private static void SetBubbleAnimationsUpdateable(CandyBody body, bool updateable)
        {
            if (body == null)
            {
                return;
            }

            _ = (body.BubbleAnimation?.updateable = updateable);
            HoldGhostBubbleFrames(body.GhostBubbleAnimation, !updateable);
        }

        /// <summary>
        /// Holds or resumes a ghost bubble's own frames while its drifting clouds keep animating.
        /// The clouds are children of the ghost bubble, so the overlay stays updateable and only
        /// its current timeline is paused.
        /// </summary>
        /// <param name="ghost">The ghost-bubble overlay, or <see langword="null"/>.</param>
        /// <param name="held">Whether the bubble frames hold still.</param>
        private static void HoldGhostBubbleFrames(CandyInGhostBubbleAnimation ghost, bool held)
        {
            if (ghost == null)
            {
                return;
            }

            ghost.updateable = true;
            Timeline frames = ghost.GetCurrentTimeline();
            if (held && frames?.state == Timeline.TimelineState.TIMELINE_PLAYING)
            {
                frames.PauseTimeline();
            }
            else if (!held && frames?.state == Timeline.TimelineState.TIMELINE_PAUSED)
            {
                // Resuming from a pause continues where the frames stopped; starting any other
                // state would rewind them.
                frames.PlayTimeline();
            }
        }

        /// <summary>Silences looping sounds whose gameplay sources stop when time is frozen.</summary>
        private void StopLoopingMoverSounds()
        {
            StopFlyingCandyFlapSounds();
            foreach (Spikes spike in spikes)
            {
                spike.SuspendElectricLoop();
            }

            if (rockets == null)
            {
                return;
            }

            foreach (Rocket rocket in rockets)
            {
                if (rocket?.flyLoopSound == null)
                {
                    continue;
                }

                SoundMgr.StopLoopedSound(rocket.flyLoopSound);
                rocket.flyLoopSound = null;
            }
        }

        /// <summary>Restarts looping sounds for sources that remain active when time resumes.</summary>
        private void RestartLoopingMoverSounds()
        {
            RestartFlyingCandyFlapSounds();
            foreach (Spikes spike in spikes)
            {
                spike.ResumeElectricLoop();
            }

            if (rockets == null)
            {
                return;
            }

            foreach (Rocket rocket in rockets)
            {
                if (rocket != null
                    && rocket.flyLoopSound == null
                    && RocketBoundCandy(rocket) != null)
                {
                    rocket.flyLoopSound = SoundMgr.PlaySoundLooped(Resources.Snd.ExpRocketFlyLooped);
                }
            }
        }

        /// <summary>
        /// Rewinds every candy point to its previous position and clears motion accumulated by
        /// constraints and other frozen-step interactions. Normal point integration is skipped
        /// earlier in the step; this final hold mirrors the iOS cleanup pass.
        /// </summary>
        private void HoldFrozenPoints()
        {
            foreach (CandyBody body in ActiveCandyBodies())
            {
                ConstrainedPoint point = body.Point;
                if (point == null)
                {
                    continue;
                }

                point.a = vectZero;
                point.v = vectZero;
                point.posDelta = vectZero;
                point.pos = point.prevPos;
            }
        }
    }
}
