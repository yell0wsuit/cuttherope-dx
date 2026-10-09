using System;
using System.Collections.Generic;

using CutTheRopeDX.Framework;
using CutTheRopeDX.Framework.Core;
using CutTheRopeDX.Framework.Helpers;
using CutTheRopeDX.Framework.Media;
using CutTheRopeDX.Framework.Visual;

namespace CutTheRopeDX.GameMain
{
    /// <content>
    /// The Time Travel game screen's effects (iOS HD 1.5.0 <c>GameScene</c>): the HUD's star
    /// counter, the lightning that strikes as a level opens, and the time spiral the original Om
    /// Nom is drawn out of as a pack begins and back into once its last level is eaten.
    /// </content>
    internal sealed partial class GameScene
    {
        /// <summary>Seconds into an arrival that the screen flashes and the spiral sounds (iOS 0.3).</summary>
        private const float TimeTravelArrivalFlash = 0.3f;

        /// <summary>Seconds into an arrival or departure that the spiral opens (iOS 0.6333).</summary>
        private const float TimeTravelSpiralOpens = 0.63333f;

        /// <summary>Seconds into an arrival that Om Nom steps out (iOS 1.3667).</summary>
        private const float TimeTravelArrivalStep = 1.3667f;

        /// <summary>Seconds into a departure that Om Nom is drawn in and the screen flashes (iOS 0.3).</summary>
        private const float TimeTravelDepartureStep = 0.3f;

        /// <summary>Seconds the screen flash waits, and stays white, for (iOS 0.1).</summary>
        private const float TimeTravelFlashBeat = 0.1f;

        /// <summary>Where the spiral opens from Om Nom's middle, in iOS logical units.</summary>
        private static readonly Vector TimeTravelSpiralOffset = new(-15f, -120f);

        /// <summary>
        /// How the spiral stretches as it opens, holds and closes (iOS): each step's scale and the
        /// seconds it takes to reach it from the one before.
        /// </summary>
        private static readonly (float Seconds, float X, float Y)[] TimeTravelSpiralSteps =
        [
            (0f, 0.45f, 0.09f),
            (0.13333f, 0.6f, 0.05f),
            (0.1f, 1.82f, 0.1f),
            (0.16667f, 1.4f, 0.41f),
            (2f, 1.4f, 0.41f),
            (0.16667f, 1.63f, 0.48f),
            (0.13333f, 3.29f, 0.08f),
            (0.13333f, 0.28f, 0.08f),
        ];

        /// <summary>The HUD star animation, read once.</summary>
        private FlashXmlAnimationDefinition timeTravelHudStarDefinition;

        /// <summary>Top-left of the star's empty pose, held fixed while its fill animates.</summary>
        private Vector timeTravelHudStarOrigin;

        /// <summary>Seconds the open spiral has left.</summary>
        private float timeTravelSpiralLeft;

        /// <summary>
        /// Whether this show is the one a pack begins with, so the original Om Nom arrives
        /// through the spiral instead of greeting.
        /// </summary>
        private bool timeTravelArrivalPending;

        private bool timeTravelArrivalActive;

        private TimeTravelBlackout timeTravelBlackout;

        /// <summary>Gets the HUD's stars; <see langword="null"/> outside Time Travel.</summary>
        internal TimeTravelFlashStage[] TimeTravelHudStars { get; private set; }

        /// <summary>Gets the time spiral while it is open, drawn behind <see cref="TimeTravelSpiralTarget"/>.</summary>
        internal TimeTravelFlashStage TimeTravelSpiral { get; private set; }

        /// <summary>Gets the Om Nom passing through the spiral, as an index into the targets; -1 when none.</summary>
        internal int TimeTravelSpiralTarget { get; private set; } = -1;

        /// <summary>Gets the level's Om Noms, the primary first.</summary>
        internal IReadOnlyList<TargetContext> Targets => targets;

        /// <summary>Builds the Time Travel HUD stars in place of the classic ones.</summary>
        private void CreateTimeTravelHudStars()
        {
            foreach (Animation star in hudStar)
            {
                star.visible = false;
            }
            timeTravelHudStarDefinition = TimeTravelFlashStage.Load(TimeTravelArt.HudStarAnimationXml);
            TimeTravelHudStars = new TimeTravelFlashStage[hudStar.Length];
            ResetTimeTravelHudStars();
        }

        /// <summary>Puts every HUD star back to empty, rebuilding each at its first pose.</summary>
        private void ResetTimeTravelHudStars()
        {
            if (TimeTravelHudStars == null)
            {
                return;
            }
            for (int i = 0; i < TimeTravelHudStars.Length; i++)
            {
                if (TimeTravelHudStars[i] != null)
                {
                    RemoveChild(TimeTravelHudStars[i].Root);
                }
                TimeTravelFlashStage star = TimeTravelFlashStage.Create(timeTravelHudStarDefinition, Resources.Img.HudTimeTravel);
                star.Root.SetName("ttHudStar" + i);
                TimeTravelHudStars[i] = star;
                _ = AddChild(star.Root);

                // Posed empty, and held there until it is filled.
                star.Play(0);
                star.Root.updateable = false;
            }
            Image empty = TimeTravelHudStars[0].Part("star_empty");
            timeTravelHudStarOrigin = new Vector(empty.x, empty.y);
            LayOutTimeTravelHudStars();
        }

        /// <summary>
        /// Places the first HUD star flush with the visible top-left corner, like the classic
        /// HUD, keeping the Time Travel spacing between stars.
        /// </summary>
        private void LayOutTimeTravelHudStars()
        {
            if (TimeTravelHudStars == null)
            {
                return;
            }
            TimeTravelScreen screen = new(VisibleBounds);
            for (int i = 0; i < TimeTravelHudStars.Length; i++)
            {
                float x = i * TimeTravelArt.HudMarkers.StarStep / 2f * screen.Scale;
                TimeTravelHudStars[i].Place(screen.Scale, timeTravelHudStarOrigin.X, timeTravelHudStarOrigin.Y, x, 0f);
            }
        }

        /// <summary>Fills a HUD star.</summary>
        /// <param name="index">Which star, from the left.</param>
        private void FillTimeTravelHudStar(int index)
        {
            if (TimeTravelHudStars != null && index >= 0 && index < TimeTravelHudStars.Length)
            {
                TimeTravelHudStars[index].Root.updateable = true;
                TimeTravelHudStars[index].Play(0);
            }
        }

        /// <summary>
        /// Strikes the level-opening lightning from the visible top right and bottom left, as iOS
        /// does on every level but a pack's first, which opens with the spiral instead.
        /// </summary>
        private void StrikeTimeTravelLightning()
        {
            if (!MenuTheme.IsTimeTravel || Application.SharedRootController().Level == 0)
            {
                return;
            }
            Rectangle visible = VisibleBounds;
            TimeTravelScreen screen = new(visible);
            FlashXmlAnimationDefinition definition = TimeTravelFlashStage.Load(TimeTravelArt.LightningAnimationXml);

            TimeTravelFlashStage topRight = TimeTravelFlashStage.Create(definition, Resources.Img.FxRestartTimeTravel);
            topRight.Root.SetName("ttLightningRT");
            topRight.Place(screen.Scale, definition.StageWidth, 0f, visible.w, 0f);
            StartTimeTravelLightning(topRight);

            // The same bolt turned half about, its top right corner on the bottom left one.
            TimeTravelFlashStage bottomLeft = TimeTravelFlashStage.Create(definition, Resources.Img.FxRestartTimeTravel);
            bottomLeft.Root.SetName("ttLightningBL");
            bottomLeft.Root.rotation = 180f;
            bottomLeft.Root.scaleX = bottomLeft.Root.scaleY = screen.Scale;
            float halfWidth = bottomLeft.Root.width >> 1;
            float halfHeight = bottomLeft.Root.height >> 1;
            bottomLeft.Root.x = 0f + ((definition.StageWidth - halfWidth) * screen.Scale) - halfWidth;
            bottomLeft.Root.y = visible.h + ((0f - halfHeight) * screen.Scale) - halfHeight;
            StartTimeTravelLightning(bottomLeft);
        }

        /// <summary>Plays a lightning bolt over the level until it ends.</summary>
        /// <param name="bolt">The bolt.</param>
        private void StartTimeTravelLightning(TimeTravelFlashStage bolt)
        {
            Timeline played = bolt.RootTimeline(0);
            played?.delegateTimelineDelegate = staticAniPool;
            _ = staticAniPool.AddChild(bolt.Root);
            bolt.Play(0);
        }

        /// <summary>
        /// Whether a target is the original Om Nom, the one the spiral carries.
        /// </summary>
        /// <param name="target">The target.</param>
        /// <returns><see langword="true"/> when it wears the original Flash skin or either seasonal hat.</returns>
        private static bool IsTimeTravelSpiralTarget(TargetContext target)
        {
            return target.animation is FlashXmlTargetAnimationBackend flash
                && flash.SkinDefinition?.Id is "OM_NOM_ORIGINAL_FLASH" or "OM_NOM_HALLOWEEN" or "OM_NOM_XMAS";
        }

        /// <summary>Finds the original Om Nom among the targets.</summary>
        /// <returns>Its index, or -1 when it is not in the level.</returns>
        private int FindTimeTravelSpiralTarget()
        {
            for (int i = 0; i < targets.Count; i++)
            {
                if (IsTimeTravelSpiralTarget(targets[i]))
                {
                    return i;
                }
            }
            return -1;
        }

        /// <summary>
        /// Begins a pack's first level the iOS way: the original Om Nom is away, the screen
        /// flashes and the spiral sounds, the spiral opens, and he steps out of it, after which he
        /// greets as he would have. A level without him greets as usual.
        /// </summary>
        private void BeginTimeTravelArrival()
        {
            timeTravelArrivalPending = false;
            int index = FindTimeTravelSpiralTarget();
            if (index < 0 || nightLevel)
            {
                dd.CallObjectSelectorParamafterDelay(new DelayedDispatcher.DispatchFunc(Selector_showGreeting), null, 1.3f);
                return;
            }
            TimeTravelSpiralTarget = index;
            BeginTimeTravelBlackout(fadeOut: true);
            timeTravelArrivalActive = true;
            tutorialDirector.PresentationPaused = true;
            ((FlashXmlTargetAnimationBackend)targets[index].animation).LevelIntroFinished = () =>
            {
                timeTravelArrivalActive = false;
                tutorialDirector.PresentationPaused = false;
                ShowGreeting();
            };
            targets[index].targetObject.visible = false;
            dd.CallObjectSelectorParamafterDelay(new DelayedDispatcher.DispatchFunc(Selector_timeTravelArrivalFlash), null, TimeTravelArrivalFlash);
            dd.CallObjectSelectorParamafterDelay(new DelayedDispatcher.DispatchFunc(Selector_openTimeTravelSpiral), null, TimeTravelSpiralOpens);
            dd.CallObjectSelectorParamafterDelay(new DelayedDispatcher.DispatchFunc(Selector_timeTravelStepOut), null, TimeTravelArrivalStep);
        }

        /// <summary>
        /// Ends a pack's last level the iOS way, once the original Om Nom has finished chewing: the
        /// spiral sounds, he is drawn into it as the screen flashes, and it closes after him.
        /// </summary>
        private bool ScheduleTimeTravelDeparture()
        {
            RootController root = Application.SharedRootController();
            if (!MenuTheme.IsTimeTravel
                || CustomLevelSession.IsActive
                || root.IsPicker()
                || root.Level != Preferences.GetLevelsInPackCount(root.Pack) - 1)
            {
                return false;
            }
            int index = FindTimeTravelSpiralTarget();
            if (index < 0 || !targets[index].Feeding.IsFed || targets[index].Feeding.IsAsleep)
            {
                return false;
            }
            TimeTravelSpiralTarget = index;
            FlashXmlTargetAnimationBackend animation = (FlashXmlTargetAnimationBackend)targets[index].animation;
            animation.LevelOutroFinished = () => dd.CallObjectSelectorParamafterDelay(
                new DelayedDispatcher.DispatchFunc(Selector_gameWon), null, 2f);
            float chewing = animation.GetPlaybackSeconds(TargetAnimationState.Chewing);
            dd.CallObjectSelectorParamafterDelay(new DelayedDispatcher.DispatchFunc(Selector_timeTravelDeparture), null, chewing);
            return true;
        }

        /// <summary>Starts the departure's own beats.</summary>
        /// <param name="param">Unused.</param>
        private void Selector_timeTravelDeparture(FrameworkTypes param)
        {
            BeginTimeTravelBlackout(fadeOut: false);
            SoundMgr.PlaySound(Resources.Snd.TimeSpiralSuckInTimeTravel);
            dd.CallObjectSelectorParamafterDelay(new DelayedDispatcher.DispatchFunc(Selector_timeTravelDrawIn), null, TimeTravelDepartureStep);
            dd.CallObjectSelectorParamafterDelay(new DelayedDispatcher.DispatchFunc(Selector_openTimeTravelSpiral), null, TimeTravelSpiralOpens);
        }

        /// <summary>Draws the original Om Nom into the spiral, under a flash.</summary>
        /// <param name="param">Unused.</param>
        private void Selector_timeTravelDrawIn(FrameworkTypes param)
        {
            FlashTimeTravelScreen();
            if (TimeTravelSpiralTarget >= 0 && TimeTravelSpiralTarget < targets.Count)
            {
                targets[TimeTravelSpiralTarget].animation?.Play(TargetAnimationState.LevelOutro);
            }
        }

        /// <summary>Flashes the screen and sounds the spiral as Om Nom arrives.</summary>
        /// <param name="param">Unused.</param>
        private void Selector_timeTravelArrivalFlash(FrameworkTypes param)
        {
            FlashTimeTravelScreen();
            SoundMgr.PlaySound(Resources.Snd.TimeSpiralSuckOutTimeTravel);
        }

        /// <summary>Steps the original Om Nom out of the spiral.</summary>
        /// <param name="param">Unused.</param>
        private void Selector_timeTravelStepOut(FrameworkTypes param)
        {
            if (TimeTravelSpiralTarget < 0 || TimeTravelSpiralTarget >= targets.Count)
            {
                return;
            }
            TargetContext target = targets[TimeTravelSpiralTarget];
            target.targetObject.visible = true;
            target.animation?.Play(TargetAnimationState.LevelIntro);
        }

        /// <summary>Opens the spiral behind the original Om Nom.</summary>
        /// <param name="param">Unused.</param>
        private void Selector_openTimeTravelSpiral(FrameworkTypes param)
        {
            if (TimeTravelSpiralTarget < 0 || TimeTravelSpiralTarget >= targets.Count)
            {
                return;
            }
            TargetContext target = targets[TimeTravelSpiralTarget];
            TimeTravelFlashStage spiral = TimeTravelFlashStage.Create(TimeTravelArt.SpiralAnimationXml, Resources.Img.FxSpiralTimeTravel);
            spiral.Root.SetName("ttSpiral");
            foreach (Image part in spiral.Parts)
            {
                part.GetTimeline(0)?.SetTimelineLoopType(Timeline.LoopType.TIMELINE_REPLAY);
            }

            // In the world, at the scale the Om Nom it opens behind is drawn at.
            float unit = target.baseScaleX;
            float halfWidth = spiral.Root.width >> 1;
            float halfHeight = spiral.Root.height >> 1;
            spiral.Root.x = target.targetObject.x + (TimeTravelSpiralOffset.X * unit) - halfWidth;
            spiral.Root.y = target.targetObject.y + (TimeTravelSpiralOffset.Y * unit) - halfHeight;
            Timeline stretch = new Timeline().InitWithMaxKeyFramesOnTrack(TimeTravelSpiralSteps.Length);
            float length = 0f;
            foreach ((float seconds, float x, float y) in TimeTravelSpiralSteps)
            {
                stretch.AddKeyFrame(KeyFrame.MakeScale(x * unit, y * unit, KeyFrame.TransitionType.FRAME_TRANSITION_LINEAR, seconds));
                length += seconds;
            }
            spiral.Root.AddTimelinewithID(stretch, 1);
            spiral.Play(0);
            spiral.Root.PlayTimeline(1);
            TimeTravelSpiral = spiral;
            timeTravelSpiralLeft = length;
        }

        /// <summary>Flashes the whole screen white for a beat, after a beat.</summary>
        private void FlashTimeTravelScreen()
        {
            Rectangle visible = VisibleBounds;
            RectangleElement flash = new()
            {
                width = (int)MathF.Ceiling(visible.w),
                height = (int)MathF.Ceiling(visible.h),
                color = RGBAColor.transparentRGBA,
            };
            flash.SetName("ttScreenFlash");
            flash.anchor = flash.parentAnchor = 9;
            Timeline beat = new Timeline().InitWithMaxKeyFramesOnTrack(3);
            beat.AddKeyFrame(KeyFrame.MakeColor(RGBAColor.transparentRGBA, KeyFrame.TransitionType.FRAME_TRANSITION_IMMEDIATE, 0f));
            beat.AddKeyFrame(KeyFrame.MakeColor(RGBAColor.solidOpaqueRGBA, KeyFrame.TransitionType.FRAME_TRANSITION_IMMEDIATE, TimeTravelFlashBeat));
            beat.AddKeyFrame(KeyFrame.MakeColor(RGBAColor.transparentRGBA, KeyFrame.TransitionType.FRAME_TRANSITION_IMMEDIATE, TimeTravelFlashBeat));
            beat.delegateTimelineDelegate = staticAniPool;
            _ = flash.AddTimeline(beat);
            flash.PlayTimeline(0);
            _ = staticAniPool.AddChild(flash);
        }

        /// <summary>Runs the open spiral, and closes it once it has shrunk away.</summary>
        /// <param name="delta">Seconds since the last update.</param>
        private void UpdateTimeTravelSpiral(float delta)
        {
            if (TimeTravelSpiral == null)
            {
                return;
            }
            TimeTravelSpiral.Root.Update(delta);
            timeTravelSpiralLeft -= delta;
            if (timeTravelSpiralLeft <= 0f)
            {
                TimeTravelSpiral = null;
            }
        }

        /// <summary>Draws the open spiral, if it opens behind this Om Nom.</summary>
        /// <param name="targetIndex">The Om Nom about to be drawn.</param>
        private void DrawTimeTravelSpiral(int targetIndex)
        {
            if (TimeTravelSpiral != null && targetIndex == TimeTravelSpiralTarget)
            {
                TimeTravelSpiral.Root.Draw();
            }
        }

        /// <summary>Clears the spiral and its Om Nom as a level is torn down.</summary>
        private void ResetTimeTravelSpiral()
        {
            timeTravelArrivalActive = false;
            timeTravelBlackout = null;
            TimeTravelSpiral = null;
            TimeTravelSpiralTarget = -1;
        }

        /// <summary>Dims the background around the traveling Om Nom with the native fade beats.</summary>
        private void BeginTimeTravelBlackout(bool fadeOut)
        {
            if (timeTravelBlackout?.parent != null)
            {
                timeTravelBlackout.parent.RemoveChild(timeTravelBlackout);
                timeTravelBlackout.Dispose();
            }
            timeTravelBlackout = new TimeTravelBlackout();
            Timeline fade = new Timeline().InitWithMaxKeyFramesOnTrack(5);
            fade.AddKeyFrame(KeyFrame.MakeColor(RGBAColor.transparentRGBA, KeyFrame.TransitionType.FRAME_TRANSITION_IMMEDIATE, 0f));
            RGBAColor dim = RGBAColor.MakeRGBA(1f, 1f, 1f, 200f / 255f);
            fade.AddKeyFrame(KeyFrame.MakeColor(dim, KeyFrame.TransitionType.FRAME_TRANSITION_LINEAR, 1.1f));
            fade.AddKeyFrame(KeyFrame.MakeColor(dim, KeyFrame.TransitionType.FRAME_TRANSITION_LINEAR, 1.9f));
            if (fadeOut)
            {
                fade.AddKeyFrame(KeyFrame.MakeColor(RGBAColor.transparentRGBA, KeyFrame.TransitionType.FRAME_TRANSITION_LINEAR, 1f));
                fade.delegateTimelineDelegate = staticAniPool;
            }
            _ = timeTravelBlackout.AddTimeline(fade);
            _ = staticAniPool.AddChild(timeTravelBlackout);
            timeTravelBlackout.PlayTimeline(0);
        }

        /// <summary>Keeps the spotlight on Om Nom as the gameplay camera moves or resizes.</summary>
        private void LayoutTimeTravelBlackout()
        {
            if (timeTravelBlackout?.parent != null && TimeTravelSpiralTarget >= 0 && TimeTravelSpiralTarget < targets.Count)
            {
                GameObject target = targets[TimeTravelSpiralTarget].targetObject;
                timeTravelBlackout.Place(VisibleBounds,
                    new Vector((target.x - camera.RenderPos.X) * camera.Scale, (target.y - camera.RenderPos.Y) * camera.Scale),
                    3f * camera.Scale);
            }
        }
    }
}
