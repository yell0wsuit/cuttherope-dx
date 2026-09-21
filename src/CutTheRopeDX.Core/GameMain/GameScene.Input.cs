using System;

using CutTheRopeDX.Framework;
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
        /// <summary>
        /// Handles tapping a candy bubble and pops it when the touch is inside the bubble touch area.
        /// </summary>
        /// <param name="body">Candy body whose bubble the touch may pop.</param>
        /// <param name="tx">Touch x-coordinate in screen space.</param>
        /// <param name="ty">Touch y-coordinate in screen space.</param>
        /// <returns><see langword="true"/> when the bubble was touched and popped; otherwise, <see langword="false"/>.</returns>
        public bool HandleBubbleTouchXY(CandyBody body, float tx, float ty)
        {
            if (PointInRect(camera.ScreenToWorldX(tx), camera.ScreenToWorldY(ty), body.Point.pos.X - 60f, body.Point.pos.Y - 60f, 120f, 120f))
            {
                PopCandyBubble(body);
                RegisterBubblePopped();
                return true;
            }
            return false;
        }

        /// <summary>Bumps the bubbles-popped counter and posts the related achievements.</summary>
        private static void RegisterBubblePopped()
        {
            int bubblesPoppedCount = Preferences.GetIntForKey("PREFS_BUBBLES_POPPED") + 1;
            Preferences.SetIntForKey(bubblesPoppedCount, "PREFS_BUBBLES_POPPED", false);
            if (bubblesPoppedCount == 50)
            {
                Scorer.PostAchievementName("681513183", "\"Bubble Popper\"");
            }
            if (bubblesPoppedCount == 300)
            {
                Scorer.PostAchievementName("1058345234", "\"Bubble Master\"");
            }
        }

        /// <summary>
        /// Ends any in-progress rope-cut finger traces so they fade out instead of lingering
        /// (used when a win/loss transition interrupts an active drag). Mirrors the touch-up cleanup.
        /// </summary>
        public void EndActiveFingerTraces()
        {
            foreach (PointerGestureState gesture in pointerGestures)
            {
                gesture?.Cancel();
            }
        }

        /// <summary>Cancels belt dragging before synthetic touch releases can create inertia.</summary>
        public void CancelConveyorDrags()
        {
            conveyors?.CancelAllDrags();
        }

        /// <summary>Cancels held rocket taps so a synthetic release cannot turn them.</summary>
        public void CancelPendingRocketTaps()
        {
            if (rockets == null)
            {
                return;
            }

            foreach (Rocket rocket in rockets)
            {
                // Actual rotation drags still finish through the normal snap-to-angle path.
                if (rocket != null && !rocket.rotateHandled)
                {
                    rocket.isOperating = -1;
                }
            }
        }

        /// <summary>Releases captured controls when play ends without firing their release actions.</summary>
        private void CancelTouchesForLevelEnd()
        {
            EndActiveFingerTraces();
            CancelConveyorDrags();

            foreach (Spikes spike in spikes)
            {
                spike.touchIndex = -1;
                spike.rotateButton?.SetState(Button.BUTTON_STATE.BUTTON_UP);
            }

            foreach (Grab grab in bungees)
            {
                if (grab.Wheel is WheelControl wheel)
                {
                    wheel.EndOperating(wheel.OperatingTouch);
                }
                if (grab.Rail is RailMotion rail)
                {
                    rail.EndDrag(rail.DraggingTouch);
                }
            }
        }

        /// <summary>Resolves a supported pointer index before any gesture state is accessed.</summary>
        private bool TryGetPointerGesture(int pointerIndex, out PointerGestureState gesture)
        {
            if ((uint)pointerIndex < (uint)pointerGestures.Length)
            {
                gesture = pointerGestures[pointerIndex];
                return gesture != null;
            }

            gesture = null;
            return false;
        }

        /// <summary>
        /// Handles a touch-down event for gameplay objects and rope-cut gestures.
        /// </summary>
        /// <param name="tx">Touch x-coordinate in screen space.</param>
        /// <param name="ty">Touch y-coordinate in screen space.</param>
        /// <param name="ti">Touch index.</param>
        /// <returns><see langword="true"/> when the touch-down event was consumed; otherwise, <see langword="false"/>.</returns>
        public bool TouchDownXYIndex(float tx, float ty, int ti)
        {
            if (!TryGetPointerGesture(ti, out PointerGestureState gesture))
            {
                return true;
            }
            // Outcome input owns only the visual trace; it must not reach any gameplay object.
            if (AcceptsVisualOnlyPointerInput)
            {
                gesture.Begin(Vect(tx, ty), camera.ScreenToWorld(tx, ty));
                return true;
            }
            // Every press starts unlatched, so a press whose release another handler consumed
            // cannot leave the egg armed for a later release.
            overOmNom = false;
            // While the egg holds the level, a press anywhere goes to it and nowhere else.
            if (DismissEasterEgg())
            {
                return true;
            }
            if (ignoreTouches)
            {
                if (camera.type == CAMERATYPE.CAMERASPEEDPIXELS)
                {
                    fastenCamera = true;
                }
                return true;
            }
            if (gravityState.IsInToggleTouchZone(camera.ScreenToWorldX(tx), camera.ScreenToWorldY(ty)))
            {
                gravityState.CaptureToggleTouch(ti);
            }
            Vector world = camera.ScreenToWorld(tx, ty);
            float worldX = world.X;
            float worldY = world.Y;
            PauseSwitcher pressedSwitcher = PauseSwitcherAt(worldX, worldY);
            if (pressedSwitcher != null)
            {
                pauseSwitcherTouch = new PauseSwitcherTouch(ti, pressedSwitcher);
            }
            waterLayer?.AddParticlesAtXY(worldX, worldY);
            if (miceManager != null && miceManager.HandleClick(worldX, worldY, out ConstraintedPoint droppedMouseCandy))
            {
                CandyContext droppedCandy = CandyForPointOrNull(droppedMouseCandy);
                if (droppedCandy != null)
                {
                    droppedMouseCandy.disableGravity = IsCandyGravitySuppressed(droppedCandy);
                }
                return true;
            }
            Vector vector = Vect(tx, ty);
            if (rockets != null)
            {
                foreach (Rocket rocket in rockets)
                {
                    if (rocket != null && rocket.state == Rocket.STATE_ROCKET_IDLE && rocket.isRotatable && rocket.isOperating == -1 &&
                        VectLength(VectSub(world, Vect(rocket.x, rocket.y))) < 90f)
                    {
                        rocket.HandleTouch(world);
                        rocket.isOperating = ti;
                        return true;
                    }
                }
            }
            if (snailobjects != null)
            {
                // Shaking a snail off only ever applies to a whole candy, which the body-role table
                // enforces for the snail interaction.
                foreach (CandyBody body in ActiveCandyBodies(CandyInteraction.Snail))
                {
                    ConstraintedPoint p = body.Point;
                    if (PointInRect(worldX, worldY, p.pos.X - 30f, p.pos.Y - 30f, 60f, 60f) && p.weight > 1f)
                    {
                        p.SetWeight(p.weight - 3f);
                        if (p.weight <= 1f)
                        {
                            p.SetWeight(1f);
                            DetachSnailsForPoint(p);
                        }
                        return true;
                    }
                }
            }
            // Tapping a bubbled body pops its bubble, whether that body is a whole candy or a half.
            foreach (CandyBody body in ActiveCandyBodies(CandyInteraction.Bubble))
            {
                if (body.Bubble != null && HandleBubbleTouchXY(body, tx, ty))
                {
                    return true;
                }
            }
            gesture.Begin(vector, world);
            foreach (Spikes spike in spikes)
            {
                if (spike.rotateButton != null && spike.touchIndex == -1 && spike.rotateButton.OnTouchDownXY(camera.ScreenToWorldX(tx), camera.ScreenToWorldY(ty)))
                {
                    spike.touchIndex = ti;
                    return true;
                }
            }
            int pumpCount = pumps.Count;
            for (int i = 0; i < pumpCount; i++)
            {
                Pump pump = pumps[i];
                if (GameObject.PointInObject(camera.ScreenToWorld(tx, ty), pump))
                {
                    pump.pumpTouchTimer = 0.05f;
                    pump.pumpTouch = ti;
                    return true;
                }
            }
            // Handle gun tap
            bool primaryInPlay = !candies[0].HasNoWholeBodyInPlay;
            if (primaryInPlay)
            {
                foreach (Grab grab in bungees)
                {
                    GunSource gun = grab.GunSource;
                    if (gun != null && gun.CanFire(candies[0].Lifecycle.Attachments.InLantern, MouseCarries(candies[0])))
                    {
                        float mapLeftX = waterLayer?.x ?? 0f;
                        float mapRightX = waterLayer != null ? waterLayer.x + waterLayer.width : mapWidth;
                        bool candyInMapBounds = GameObject.RectInObject(mapLeftX, 0f, mapRightX, mapHeight, Candy);
                        bool canFireFromWaterState = waterLayer == null || candyInMapBounds || waterLayer.y > CandyPoint.pos.Y;
                        float tapRadius = Grab.GUN_TAP_RADIUS;
                        if (canFireFromWaterState && PointInRect(camera.ScreenToWorldX(tx), camera.ScreenToWorldY(ty), grab.x - tapRadius, grab.y - tapRadius, tapRadius * 2f, tapRadius * 2f))
                        {
                            gun.Fire(Vect(grab.x, grab.y), CandyPoint.pos, CandyMain.rotation);
                            gun.Cup.rotation = gun.InitialRotation;
                            gun.Front.SetDrawQuad(Grab.GunDisabledFrontQuad);
                            gun.Cup.PlayTimeline(Grab.GUN_CUP_SHOW);

                            // Fire the gun - create a rope to the candy
                            float gunToCandyDistance = VectDistance(Vect(grab.x, grab.y), CandyPoint.pos) - ActivePhysicsConstants.BungeeRestLength;
                            float ropeLength = Math.Max(gunToCandyDistance, ActivePhysicsConstants.BungeeRestLength);
                            Bungee bungee = new Bungee().InitWithHeadAtXYTailAtTXTYandLength(null, grab.x, grab.y, CandyPoint, CandyPoint.pos.X, CandyPoint.pos.Y, ropeLength);
                            bungee.bungeeAnchor.pin = bungee.bungeeAnchor.pos;
                            grab.SetRope(bungee);
                            ropes.Register(bungee, grab);
                            SoundMgr.PlaySound(Resources.Snd.ExpGun);

                            // Track achievement
                            int ropesShoot = Preferences.GetIntForKey("PREFS_ROPES_SHOOT") + 1;
                            Preferences.SetIntForKey(ropesShoot, "PREFS_ROPES_SHOOT", false);
                            if (ropesShoot >= 50)
                            {
                                Scorer.PostAchievementName("acRookieSniper", "\"Rookie Sniper\"");
                            }
                            if (ropesShoot >= 150)
                            {
                                Scorer.PostAchievementName("acSkilledSniper", "\"Skilled Sniper\"");
                            }
                            return true;
                        }
                    }
                }
            }
            // A frozen valve cannot turn: the steam it would change is frozen too, so the tap would
            // switch the level with nothing on screen to show it.
            foreach (SteamTube steamTube in tubes)
            {
                if (steamTube != null && !timeFrozen && steamTube.OnTouchDownXY(camera.ScreenToWorldX(tx), camera.ScreenToWorldY(ty)))
                {
                    tutorialDirector.Fire(TutorialEvent.SteamBurst);
                    return true;
                }
            }

            BambooTube nearestBambooTube = null;
            float nearestBambooDistance = float.PositiveInfinity;
            foreach (BambooTube bambooTube in bambooTubes)
            {
                if (bambooTube == null)
                {
                    continue;
                }

                float distance = VectDistance(world, Vect(bambooTube.x, bambooTube.y));
                if (distance < nearestBambooDistance)
                {
                    nearestBambooDistance = distance;
                    nearestBambooTube = bambooTube;
                }
            }

            if (nearestBambooTube != null && nearestBambooTube.HandleBambooTouchWithIndex(world, ti))
            {
                return true;
            }

            bool handledHandInput = false;
            if (hands != null)
            {
                foreach (MechanicalHand hand in hands)
                {
                    if (hand == null)
                    {
                        continue;
                    }

                    if (hand.State == MechanicalHandState.HoldingCandy && VectDistance(world, hand.ClawPosition()) < MechanicalHand.MH_CLAW_TOUCH_RADIUS)
                    {
                        CandyContext held = HandHeldCandy(hand);
                        hand.cPoint.RemoveConstraint(held?.WholeBody.Point ?? CandyPoint);
                        hand.ReleaseCandyAfterDropSound();
                        hand.AnimateReleaseWithAnimationsPool(aniPool);
                        _ = held?.Lifecycle.Attachments.TryReleaseHand(hand);
                        SoundMgr.PlaySound(Resources.Snd.ExpHandDrop);
                        return true;
                    }

                    if (hand.segments == null)
                    {
                        continue;
                    }

                    for (int i = hand.segments.Count - 1; i >= 0; i--)
                    {
                        MechanicalHandSegment segment = hand.SegmentAtIndex(i);
                        if (segment?.button != null && segment.button.OnTouchDownXY(world.X, world.Y))
                        {
                            segment.Rotate();
                            hand.rotatingSegment = segment;
                            hand.ArmClap();
                            handledHandInput = true;
                            SoundMgr.PlaySound(Resources.Snd.ExpHandRotate);
                            break;
                        }
                    }
                }
            }
            if (handledHandInput)
            {
                return true;
            }

            foreach (CandyBody body in ActiveCandyBodies())
            {
                if (body.Point != null && HandleConveyorTouchConstraintedPointXY(body.Point, tx, ty))
                {
                    return true;
                }
            }

            foreach (Lantern lantern in Lantern.GetAllLanterns())
            {
                if (lantern != null && lantern.OnTouchDown(camera.ScreenToWorldX(tx), camera.ScreenToWorldY(ty), out ConstraintedPoint releasedCandyPoint))
                {
                    dd.CallObjectSelectorParamafterDelay(new DelayedDispatcher.DispatchFunc(Selector_revealCandyFromLantern), releasedCandyPoint, Lantern.LanternCandyRevealTime);
                    return true;
                }
            }
            RotatedCircle rotatedCircle = null;
            bool hasContainedCircle = false;
            bool hasOverlappingCircle = false;
            foreach (RotatedCircle rotatedCircle2 in rotatedCircles)
            {
                float distanceToLeftHandle = VectDistance(camera.ScreenToWorld(tx, ty), rotatedCircle2.handle1);
                float distanceToRightHandle = VectDistance(camera.ScreenToWorld(tx, ty), rotatedCircle2.handle2);
                if ((distanceToLeftHandle < 90f && !rotatedCircle2.HasOneHandle()) || distanceToRightHandle < 90f)
                {
                    foreach (RotatedCircle rotatedCircle3 in rotatedCircles)
                    {
                        if (rotatedCircles.IndexOf(rotatedCircle3) > rotatedCircles.IndexOf(rotatedCircle2))
                        {
                            float circleDistance = VectDistance(Vect(rotatedCircle3.x, rotatedCircle3.y), Vect(rotatedCircle2.x, rotatedCircle2.y));
                            if (circleDistance + rotatedCircle3.sizeInPixels <= rotatedCircle2.sizeInPixels)
                            {
                                hasContainedCircle = true;
                            }
                            if (circleDistance <= rotatedCircle2.sizeInPixels + rotatedCircle3.sizeInPixels)
                            {
                                hasOverlappingCircle = true;
                            }
                        }
                    }
                    rotatedCircle2.lastTouch = camera.ScreenToWorld(tx, ty);
                    bool discWasIdle = rotatedCircle2.operating == -1;
                    rotatedCircle2.operating = ti;
                    if (discWasIdle)
                    {
                        tutorialDirector.Fire(TutorialEvent.DiscSpin);
                    }
                    if (distanceToLeftHandle < 90f)
                    {
                        rotatedCircle2.SetIsLeftControllerActive(true);
                    }
                    if (distanceToRightHandle < 90f)
                    {
                        rotatedCircle2.SetIsRightControllerActive(true);
                    }
                    rotatedCircle = rotatedCircle2;
                    break;
                }
            }
            if (rotatedCircle != null && rotatedCircles.IndexOf(rotatedCircle) != rotatedCircles.Count - 1 && hasOverlappingCircle && !hasContainedCircle)
            {
                Timeline fadeInTimeline = new Timeline().InitWithMaxKeyFramesOnTrack(2);
                fadeInTimeline.AddKeyFrame(KeyFrame.MakeColor(RGBAColor.transparentRGBA, KeyFrame.TransitionType.FRAME_TRANSITION_LINEAR, 0));
                fadeInTimeline.AddKeyFrame(KeyFrame.MakeColor(RGBAColor.solidOpaqueRGBA, KeyFrame.TransitionType.FRAME_TRANSITION_LINEAR, 0.2f));
                Timeline copyHoldTimeline = new Timeline().InitWithMaxKeyFramesOnTrack(1);
                copyHoldTimeline.AddKeyFrame(KeyFrame.MakeColor(RGBAColor.solidOpaqueRGBA, KeyFrame.TransitionType.FRAME_TRANSITION_LINEAR, 0.2f));
                copyHoldTimeline.delegateTimelineDelegate = this;
                RotatedCircle circleCopy = rotatedCircle.Copy();
                _ = circleCopy.AddTimeline(copyHoldTimeline);
                circleCopy.PlayTimeline(0);
                _ = rotatedCircle.AddTimeline(fadeInTimeline);
                rotatedCircle.PlayTimeline(0);
                rotatedCircles[rotatedCircles.IndexOf(rotatedCircle)] = circleCopy;
                rotatedCircles.Add(rotatedCircle);
            }
            if (ghosts != null)
            {
                foreach (Ghost ghost in ghosts)
                {
                    if (ghost != null && ghost.OnTouchDownXY(camera.ScreenToWorldX(tx), camera.ScreenToWorldY(ty)))
                    {
                        return true;
                    }
                }
            }
            // A tap that lands on a stuck cup claims the touch; taps that land anywhere else start
            // every detached cup trying to re-stick.
            bool touchedMountedCup = false;
            foreach (Grab bungee in bungees)
            {
                float tapRadius = Grab.KICK_TAP_RADIUS;
                if (bungee.Mount is SuctionMount tapped && tapped.IsMounted
                    && PointInRect(camera.ScreenToWorldX(tx), camera.ScreenToWorldY(ty), bungee.x - tapRadius, bungee.y - tapRadius, tapRadius * 2f, tapRadius * 2f))
                {
                    touchedMountedCup = true;
                    break;
                }
            }
            foreach (Grab bungee in bungees)
            {
                if (bungee.Mount is SuctionMount mount && !mount.IsMounted && bungee.Rope != null && !touchedMountedCup)
                {
                    mount.BeginSticking();
                }
            }
            foreach (Grab bungee in bungees)
            {
                if (bungee.Wheel?.TryBeginOperating(bungee, camera.ScreenToWorldX(tx), camera.ScreenToWorldY(ty), ti) == true)
                {
                    // A touch that lands on the wheel belongs to the wheel: without this, a wheel
                    // hook riding a manual belt let the same touch also start a belt drag.
                    return true;
                }
                if (bungee.Rail?.TryBeginDrag(bungee, camera.ScreenToWorldX(tx), camera.ScreenToWorldY(ty), ti) == true)
                {
                    return true;
                }
            }
            if (conveyors.OnPointerDown(worldX, worldY, ti))
            {
                gesture.Cancel();
                return true;
            }
            if (clickToCut && !ignoreTouches)
            {
                Vector cutPoint = default;
                Grab touchedGrab = null;
                Bungee nearestBungeeSegmentByBeziersPointsatXYgrab = GetNearestBungeeSegmentByBeziersPointsatXYgrab(ref cutPoint, camera.ScreenToWorldX(tx), camera.ScreenToWorldY(ty), ref touchedGrab);
                if (nearestBungeeSegmentByBeziersPointsatXYgrab != null && nearestBungeeSegmentByBeziersPointsatXYgrab.highlighted && GetNearestBungeeSegmentByConstraintsforGrab(ref cutPoint, touchedGrab) != null)
                {
                    _ = CutWithRazorOrLine1Line2Immediate(null, cutPoint, cutPoint, false);
                }
            }
            // Checked last so the egg can never shadow a rope cut or a grab.
            if (EasterEggMatchesTarget
                && TargetObject.PointInDrawQuad(camera.ScreenToWorldX(tx), camera.ScreenToWorldY(ty)))
            {
                overOmNom = true;
            }
            return true;
        }


        /// <summary>
        /// Handles a touch-up event for gameplay objects and active touch gestures.
        /// </summary>
        /// <param name="tx">Touch x-coordinate in screen space.</param>
        /// <param name="ty">Touch y-coordinate in screen space.</param>
        /// <param name="ti">Touch index.</param>
        /// <returns><see langword="true"/> when the touch-up event was consumed; otherwise, <see langword="false"/>.</returns>
        public bool TouchUpXYIndex(float tx, float ty, int ti)
        {
            if (!TryGetPointerGesture(ti, out PointerGestureState gesture))
            {
                return true;
            }
            // Any release ends the capture, so a second pointer letting go cancels the press.
            PauseSwitcherTouch? capturedTouch = pauseSwitcherTouch;
            pauseSwitcherTouch = null;
            // Outcome input ends the visual trace without reaching any gameplay object.
            if (AcceptsVisualOnlyPointerInput)
            {
                gesture.End();
                return true;
            }
            if (ignoreTouches)
            {
                return true;
            }
            gesture.End();
            if (rockets != null)
            {
                foreach (Rocket rocket in rockets)
                {
                    if (rocket == null || rocket.isOperating != ti)
                    {
                        continue;
                    }
                    if (!rocket.rotateHandled)
                    {
                        Timeline timeline = rocket.GetCurrentTimeline();
                        if (timeline != null && timeline.state == Timeline.TimelineState.TIMELINE_PLAYING)
                        {
                            timeline.JumpToTrackKeyFrame(2, 1);
                            timeline.StopTimeline();
                        }
                        rocket.PlayTimeline(0);
                        rocket.startRotation += DEG_45;
                    }
                    else
                    {
                        rocket.HandleRotateFinal();
                    }
                    rocket.rotateHandled = false;
                    rocket.isOperating = -1;
                    return true;
                }
            }
            if (gravityState.ReleaseToggleTouch(ti))
            {
                if (gravityState.IsInToggleTouchZone(camera.ScreenToWorldX(tx), camera.ScreenToWorldY(ty)))
                {
                    OnButtonPressed(0);
                }
            }

            if (capturedTouch is { } switcherTouch && switcherTouch.PointerIndex == ti)
            {
                PauseSwitcher releasedSwitcher = PauseSwitcherAt(camera.ScreenToWorldX(tx), camera.ScreenToWorldY(ty));
                if (ReferenceEquals(switcherTouch.Switcher, releasedSwitcher))
                {
                    ToggleTimeFreeze(switcherTouch.Switcher);
                }
            }

            foreach (BambooTube bambooTube in bambooTubes)
            {
                if (bambooTube != null && bambooTube.BambooTouchIndex == ti)
                {
                    bambooTube.HandleBambooCancel();
                    return true;
                }
            }

            if (hands != null)
            {
                foreach (MechanicalHand hand in hands)
                {
                    if (hand?.segments == null)
                    {
                        continue;
                    }

                    foreach (MechanicalHandSegment segment in hand.segments)
                    {
                        _ = (segment?.button?.OnTouchUpXY(camera.ScreenToWorldX(tx), camera.ScreenToWorldY(ty)));
                    }
                }
            }
            foreach (Spikes spike in spikes)
            {
                if (spike.rotateButton != null && spike.touchIndex == ti)
                {
                    spike.touchIndex = -1;
                    if (spike.rotateButton.OnTouchUpXY(camera.ScreenToWorldX(tx), camera.ScreenToWorldY(ty)))
                    {
                        return true;
                    }
                }
            }
            foreach (RotatedCircle rotatedCircle in rotatedCircles)
            {
                if (rotatedCircle.operating == ti)
                {
                    rotatedCircle.operating = -1;
                    rotatedCircle.soundPlaying = -1;
                    rotatedCircle.SetIsLeftControllerActive(false);
                    rotatedCircle.SetIsRightControllerActive(false);
                }
            }
            foreach (Grab bungee in bungees)
            {
                bungee.Wheel?.EndOperating(ti);
                bungee.Rail?.EndDrag(ti);
                if (bungee.Mount is SuctionMount mount && bungee.Rope != null)
                {
                    float tapRadius = Grab.KICK_TAP_RADIUS;
                    if (mount.IsMounted && bungee.Rope.cut == -1 &&
                        PointInRect(camera.ScreenToWorldX(tx), camera.ScreenToWorldY(ty), bungee.x - tapRadius, bungee.y - tapRadius, tapRadius * 2f, tapRadius * 2f))
                    {
                        if (mount.TakeStain(out float stainAlpha))
                        {
                            Image stain = Image.Image_createWithResIDQuad(Resources.Img.ObjSticker, 0);
                            stain.DoRestoreCutTransparency();
                            stain.x = bungee.Rope.bungeeAnchor.pos.X;
                            stain.y = bungee.Rope.bungeeAnchor.pos.Y;
                            stain.anchor = 18;
                            stain.color.AlphaChannel = stainAlpha;
                            _ = decalsLayer.AddChild(stain);
                        }
                        mount.Kick(bungee);
                        bungee.UpdateKickState();
                        SoundMgr.PlaySound(Resources.Snd.ExpSuckerDrop);
                        int wallClimberCount = Preferences.GetIntForKey("PREFS_WALL_CLIMBER") + 1;
                        Preferences.SetIntForKey(wallClimberCount, "PREFS_WALL_CLIMBER", false);
                        if (wallClimberCount >= 50)
                        {
                            Scorer.PostAchievementName("acRookieWallClimber", "\"Rookie Wall Climber\"");
                        }
                        if (wallClimberCount >= 400)
                        {
                            Scorer.PostAchievementName("acVeteranWallClimber", "\"Veteran Wall Climber\"");
                        }
                    }
                }
            }
            _ = conveyors.OnPointerUp(camera.ScreenToWorldX(tx), camera.ScreenToWorldY(ty), ti);
            if (overOmNom)
            {
                overOmNom = false;
                if (EasterEggMatchesTarget
                    && TargetObject.PointInDrawQuad(camera.ScreenToWorldX(tx), camera.ScreenToWorldY(ty)))
                {
                    _ = easterEgg.TryTrigger();
                }
            }
            return true;
        }


        /// <summary>
        /// Handles a touch-move event for active gameplay gestures, draggable objects, and rope cutting.
        /// </summary>
        /// <param name="tx">Touch x-coordinate in screen space.</param>
        /// <param name="ty">Touch y-coordinate in screen space.</param>
        /// <param name="ti">Touch index.</param>
        /// <returns><see langword="true"/> when the touch-move event was consumed; otherwise, <see langword="false"/>.</returns>
        public bool TouchMoveXYIndex(float tx, float ty, int ti)
        {
            if (!TryGetPointerGesture(ti, out PointerGestureState gesture))
            {
                return true;
            }
            Vector vector = Vect(tx, ty);
            Vector world = camera.ScreenToWorld(tx, ty);
            // Outcome input advances only the visual trace; no cuts or object handlers run.
            if (AcceptsVisualOnlyPointerInput)
            {
                _ = gesture.Move(vector, world, out _);
                return true;
            }
            if (ignoreTouches)
            {
                return true;
            }
            if (rockets != null)
            {
                foreach (Rocket rocket in rockets)
                {
                    if (rocket != null && rocket.isOperating == ti)
                    {
                        rocket.HandleRotate(world);
                        return true;
                    }
                }
            }
            foreach (Pump pump3 in pumps)
            {
                if (pump3.pumpTouch == ti && pump3.pumpTouchTimer != 0 && VectDistance(gesture.StartPosition, vector) > 10)
                {
                    pump3.pumpTouchTimer = 0f;
                }
            }

            foreach (BambooTube bambooTube in bambooTubes)
            {
                if (bambooTube != null && bambooTube.BambooTouchIndex == ti)
                {
                    bambooTube.HandleBambooRotate(world);
                }
            }

            if (hands != null)
            {
                foreach (MechanicalHand hand in hands)
                {
                    if (hand?.segments == null)
                    {
                        continue;
                    }

                    foreach (MechanicalHandSegment segment in hand.segments)
                    {
                        _ = (segment?.button?.OnTouchMoveXY(world.X, world.Y));
                    }
                }
            }
            if (rotatedCircles != null)
            {
                for (int i = 0; i < rotatedCircles.Count; i++)
                {
                    RotatedCircle rotatedCircle = rotatedCircles[i];
                    if (rotatedCircle != null && rotatedCircle.operating == ti)
                    {
                        Vector circleCenter = Vect(rotatedCircle.x, rotatedCircle.y);
                        Vector touchWorld = camera.ScreenToWorld(tx, ty);
                        Vector lastTouchOffset = VectSub(rotatedCircle.lastTouch, circleCenter);
                        float rotationDelta = VectAngleNormalized(VectSub(touchWorld, circleCenter)) - VectAngleNormalized(lastTouchOffset);
                        float initial_rotation = float.DegreesToRadians(rotatedCircle.rotation);
                        rotatedCircle.rotation += float.RadiansToDegrees(rotationDelta);
                        float circleAngle = float.DegreesToRadians(rotatedCircle.rotation);
                        circleAngle = FBOUND_PI(circleAngle);
                        rotatedCircle.handle1 = VectRotateAround(rotatedCircle.inithanlde1, circleAngle, rotatedCircle.x, rotatedCircle.y);
                        rotatedCircle.handle2 = VectRotateAround(rotatedCircle.inithanlde2, circleAngle, rotatedCircle.x, rotatedCircle.y);
                        int scratchSoundState = rotationDelta > 0f ? 1 : 2;
                        if (MathF.Abs(rotationDelta) < 0.07f)
                        {
                            scratchSoundState = -1;
                        }
                        if (rotatedCircle.soundPlaying != scratchSoundState && scratchSoundState != -1)
                        {
                            SoundMgr.PlaySound(scratchSoundState == 1 ? Resources.Snd.ScratchOut : Resources.Snd.ScratchIn);
                            rotatedCircle.soundPlaying = scratchSoundState;
                        }
                        for (int j = 0; j < bungees.Count; j++)
                        {
                            Grab grab = bungees[j];
                            // A grab that carries its own movement is not the disc's to move: a path
                            // mover drives itself, and a drag rail belongs to the player. Scratching
                            // the disc would otherwise sweep such a hook off its rail, leaving the
                            // rail drawn where it was. Same rule the disc-capture test in the update
                            // loop applies, so both agree on what this disc owns.
                            if (!(grab.Mount?.FollowsPlatform ?? grab.Motion.FollowsPlatform)
                                || grab is IGhostApparition)
                            {
                                continue;
                            }
                            if (VectDistance(Vect(grab.x, grab.y), Vect(rotatedCircle.x, rotatedCircle.y)) <= rotatedCircle.sizeInPixels + 5f)
                            {
                                if (grab.initial_rotatedCircle != rotatedCircle)
                                {
                                    grab.initial_x = grab.x;
                                    grab.initial_y = grab.y;
                                    grab.initial_rotatedCircle = rotatedCircle;
                                    grab.initial_rotation = initial_rotation;
                                }
                                float grabAngle = float.DegreesToRadians(rotatedCircle.rotation) - grab.initial_rotation;
                                grabAngle = FBOUND_PI(grabAngle);
                                Vector rotatedGrabPos = VectRotateAround(Vect(grab.initial_x, grab.initial_y), grabAngle, rotatedCircle.x, rotatedCircle.y);
                                grab.x = rotatedGrabPos.X;
                                grab.y = rotatedGrabPos.Y;
                                grab.SyncRopeAnchor();
                                grab.ReCalcCircle();
                            }
                        }
                        for (int k = 0; k < pumps.Count; k++)
                        {
                            Pump pump = pumps[k];
                            if (VectDistance(Vect(pump.x, pump.y), Vect(rotatedCircle.x, rotatedCircle.y)) <= rotatedCircle.sizeInPixels + 5f)
                            {
                                if (pump.initial_rotatedCircle != rotatedCircle)
                                {
                                    pump.initial_x = pump.x;
                                    pump.initial_y = pump.y;
                                    pump.initial_rotatedCircle = rotatedCircle;
                                    pump.initial_rotation = initial_rotation;
                                }
                                float pumpAngle = float.DegreesToRadians(rotatedCircle.rotation) - pump.initial_rotation;
                                pumpAngle = FBOUND_PI(pumpAngle);
                                Vector rotatedPumpPos = VectRotateAround(Vect(pump.initial_x, pump.initial_y), pumpAngle, rotatedCircle.x, rotatedCircle.y);
                                pump.x = rotatedPumpPos.X;
                                pump.y = rotatedPumpPos.Y;
                                pump.rotation += float.RadiansToDegrees(rotationDelta);
                                pump.UpdateRotation();
                            }
                        }
                        for (int l = 0; l < bubbles.Count; l++)
                        {
                            Bubble bubble = bubbles[l];
                            // A ghost's bubble belongs to the ghost, not the disc: its morph clouds
                            // stay at the ghost's spot, so rotating the bubble alone would strand them.
                            if (bubble is IGhostApparition)
                            {
                                continue;
                            }
                            if (VectDistance(Vect(bubble.x, bubble.y), Vect(rotatedCircle.x, rotatedCircle.y)) <= rotatedCircle.sizeInPixels + 10f && CandyBodyForBubbleOrNull(bubble) == null)
                            {
                                if (bubble.initial_rotatedCircle != rotatedCircle)
                                {
                                    bubble.initial_x = bubble.x;
                                    bubble.initial_y = bubble.y;
                                    bubble.initial_rotatedCircle = rotatedCircle;
                                    bubble.initial_rotation = initial_rotation;
                                }
                                float bubbleAngle = float.DegreesToRadians(rotatedCircle.rotation) - bubble.initial_rotation;
                                bubbleAngle = FBOUND_PI(bubbleAngle);
                                Vector rotatedBubblePos = VectRotateAround(Vect(bubble.initial_x, bubble.initial_y), bubbleAngle, rotatedCircle.x, rotatedCircle.y);
                                bubble.x = rotatedBubblePos.X;
                                bubble.y = rotatedBubblePos.Y;
                            }
                        }
                        for (int targetIndex = 0; targetIndex < targets.Count; targetIndex++)
                        {
                            GameObject targetObject = targets[targetIndex].targetObject;
                            if (targetObject != null && PointInRect(targetObject.x, targetObject.y, rotatedCircle.x - rotatedCircle.size, rotatedCircle.y - rotatedCircle.size, 2f * rotatedCircle.size, 2f * rotatedCircle.size))
                            {
                                Vector rotatedTargetPos = VectRotateAround(Vect(targetObject.x, targetObject.y), rotationDelta, rotatedCircle.x, rotatedCircle.y);
                                targetObject.x = rotatedTargetPos.X;
                                targetObject.y = rotatedTargetPos.Y;
                            }
                        }
                        rotatedCircle.lastTouch = touchWorld;
                        return true;
                    }
                }
            }
            int grabCount = bungees.Count;
            for (int m = 0; m < grabCount; m++)
            {
                Grab grab = bungees[m];
                if (grab != null)
                {
                    if (grab.Wheel is WheelControl wheel && wheel.OperatingTouch == ti)
                    {
                        wheel.HandleRotate(grab, camera.ScreenToWorld(tx, ty));
                        return true;
                    }
                    if (grab.Rail is RailMotion rail && rail.DraggingTouch == ti)
                    {
                        rail.DragTo(grab, camera.ScreenToWorldX(tx), camera.ScreenToWorldY(ty));
                        grab.SyncRopeAnchor();
                        grab.ReCalcCircle();
                        return true;
                    }
                    // Cancel stick timer if moved too much (kickable grabs)
                    if (grab.Mount is SuctionMount dragMount && !dragMount.IsMounted && grab.Rope != null &&
                        VectLength(VectSub(gesture.StartPosition, vector)) > Grab.KICK_MOVE_LENGTH)
                    {
                        dragMount.CancelSticking();
                    }
                }
            }
            if (conveyors.OnPointerMove(camera.ScreenToWorldX(tx), camera.ScreenToWorldY(ty), ti))
            {
                return true;
            }
            if (gesture.Move(vector, world, out Vector segmentStart))
            {
                Vector start = camera.ScreenToWorld(segmentStart.X, segmentStart.Y);
                Vector end = camera.ScreenToWorld(tx, ty);
                FingerCut fingerCut = new()
                {
                    start = start,
                    end = end,
                    startSize = 5f,
                    endSize = 5f,
                    c = RGBAColor.whiteRGBA
                };
                gesture.Cuts.Add(fingerCut);
                int ropesCutThisFrame = 0;
                foreach (FingerCut item in gesture.Cuts)
                {
                    ropesCutThisFrame += CutWithRazorOrLine1Line2Immediate(null, item.start, item.end, false);
                }
                if (ropesCutThisFrame > 0)
                {
                    freezeCamera = false;
                    if (ropesCutAtOnce > 0 && ropeAtOnceTimer > 0)
                    {
                        ropesCutAtOnce += ropesCutThisFrame;
                    }
                    else
                    {
                        ropesCutAtOnce = ropesCutThisFrame;
                    }
                    ropeAtOnceTimer = 0.1f;
                    int ropesCutTotal = Preferences.GetIntForKey("PREFS_ROPES_CUT") + 1;
                    Preferences.SetIntForKey(ropesCutTotal, "PREFS_ROPES_CUT", false);
                    if (ropesCutTotal == 100)
                    {
                        Scorer.PostAchievementName("681461850", "\"Rope Cutter\"");
                    }
                    if (ropesCutAtOnce is >= 3 and < 5)
                    {
                        Scorer.PostAchievementName("681464917", "\"Quick Finger\"");
                    }
                    if (ropesCutAtOnce >= 5)
                    {
                        Scorer.PostAchievementName("681508316", "\"Master Finger\"");
                    }
                    if (ropesCutTotal == 800)
                    {
                        Scorer.PostAchievementName("681457931", "\"Rope Cutter Maniac\"");
                    }
                    if (ropesCutTotal == 2000)
                    {
                        Scorer.PostAchievementName("1058248892", "\"Ultimate Rope Cutter\"");
                    }
                }
            }
            return true;
        }


        /// <summary>
        /// Records the latest dragged touch location.
        /// </summary>
        /// <param name="tx">Touch x-coordinate in screen space.</param>
        /// <param name="ty">Touch y-coordinate in screen space.</param>
        /// <param name="index">Touch index.</param>
        /// <returns><see langword="true"/> when the drag was recorded; otherwise, <see langword="false"/>.</returns>
        public bool TouchDraggedXYIndex(float tx, float ty, int index)
        {
            if (!TryGetPointerGesture(index, out _))
            {
                return false;
            }
            // Hover state is gameplay-only; outcome drawing is handled by the gesture lifecycle.
            if (AcceptsVisualOnlyPointerInput)
            {
                return true;
            }
            slastTouch = Vect(tx, ty);
            return true;
        }

    }
}
