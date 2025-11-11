using CutTheRope.desktop;
using CutTheRope.iframework;
using CutTheRope.iframework.core;
using CutTheRope.iframework.helpers;
using CutTheRope.iframework.sfe;
using CutTheRope.iframework.visual;
using CutTheRope.ios;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace CutTheRope.game
{
    internal sealed partial class GameScene : BaseElement, ITimelineDelegate, IButtonDelegation
    {
        private static void DrawCut(Vector fls, Vector frs, Vector start, Vector end, float startSize, float endSize, RGBAColor c, ref Vector le, ref Vector re)
        {
            Vector vector5 = VectNormalize(VectSub(end, start));
            Vector v3 = VectRperp(vector5);
            Vector v4 = VectPerp(vector5);
            Vector vector = VectEqual(frs, vectUndefined) ? VectAdd(start, VectMult(v3, startSize)) : frs;
            Vector vector2 = VectEqual(fls, vectUndefined) ? VectAdd(start, VectMult(v4, startSize)) : fls;
            Vector vector3 = VectAdd(end, VectMult(v3, endSize));
            Vector vector4 = VectAdd(end, VectMult(v4, endSize));
            GLDrawer.DrawSolidPolygonWOBorder([vector2.x, vector2.y, vector.x, vector.y, vector3.x, vector3.y, vector4.x, vector4.y], 4, c);
            le = vector4;
            re = vector3;
        }

        private static float MaxOf4(float v1, float v2, float v3, float v4)
        {
            return v1 >= v2 && v1 >= v3 && v1 >= v4
                ? v1
                : v2 >= v1 && v2 >= v3 && v2 >= v4 ? v2 : v3 >= v2 && v3 >= v1 && v3 >= v4 ? v3 : v4 >= v2 && v4 >= v3 && v4 >= v1 ? v4 : -1f;
        }

        private static float MinOf4(float v1, float v2, float v3, float v4)
        {
            return v1 <= v2 && v1 <= v3 && v1 <= v4
                ? v1
                : v2 <= v1 && v2 <= v3 && v2 <= v4 ? v2 : v3 <= v2 && v3 <= v1 && v3 <= v4 ? v3 : v4 <= v2 && v4 <= v3 && v4 <= v1 ? v4 : -1f;
        }

        public bool PointOutOfScreen(ConstraintedPoint p)
        {
            return p.pos.y > mapHeight + 400f || p.pos.y < -400f;
        }

        public void XmlLoaderFinishedWithfromwithSuccess(XMLNode rootNode, NSString url, bool success)
        {
            ((CTRRootController)Application.SharedRootController()).SetMap(rootNode);
            if (animateRestartDim)
            {
                AnimateLevelRestart();
                return;
            }
            Restart();
        }

        public static bool ShouldSkipTutorialElement(XMLNode c)
        {
            CTRRootController cTRRootController = (CTRRootController)Application.SharedRootController();
            if (cTRRootController.GetPack() == 0 && cTRRootController.GetLevel() == 1)
            {
                return true;
            }
            NSString @string = Application.SharedAppSettings().GetString(8);
            NSString nSString = c["locale"];
            if (@string.IsEqualToString("en") || @string.IsEqualToString("ru") || @string.IsEqualToString("de") || @string.IsEqualToString("fr"))
            {
                if (!nSString.IsEqualToString(@string))
                {
                    return true;
                }
            }
            else if (!nSString.IsEqualToString("en"))
            {
                return true;
            }
            return false;
        }

        public void ShowGreeting()
        {
            target.PlayAnimationtimeline(101, 10);
        }


        public override void Hide()
        {
            if (gravityButton != null)
            {
                RemoveChild(gravityButton);
            }
            pollenDrawer.Release();
            earthAnims?.Release();
            candy.Release();
            star.Release();
            candyL?.Release();
            candyR?.Release();
            starL.Release();
            starR.Release();
            razors.Release();
            spikes.Release();
            bungees.Release();
            stars.Release();
            bubbles.Release();
            pumps.Release();
            socks.Release();
            bouncers.Release();
            rotatedCircles.Release();
            target.Release();
            support.Release();
            tutorialImages.Release();
            tutorials.Release();
            candyL = null;
            candyR = null;
            starL = null;
            starR = null;
        }

        public override void Update(float delta)
        {
            delta = 0.016f;
            base.Update(delta);
            dd.Update(delta);
            pollenDrawer.Update(delta);
            for (int i = 0; i < 5; i++)
            {
                for (int j = 0; j < fingerCuts[i].Count(); j++)
                {
                    FingerCut fingerCut = (FingerCut)fingerCuts[i].ObjectAtIndex(j);
                    float alpha = fingerCut.c.a;
                    if (Mover.MoveVariableToTarget(ref alpha, 0.0f, 10.0f, (float)delta))
                    {
                        fingerCuts[i].RemoveObject(fingerCut);
                        j--;
                    }
                    else
                    {
                        fingerCut.c.a = alpha;
                    }
                }
            }
            if (earthAnims != null)
            {
                foreach (object obj in earthAnims)
                {
                    ((Image)obj).Update(delta);
                }
            }
            _ = Mover.MoveVariableToTarget(ref ropeAtOnceTimer, 0.0, 1.0, (double)delta);
            ConstraintedPoint constraintedPoint4 = twoParts != 2 ? starL : star;
            float num = constraintedPoint4.pos.x - (SCREEN_WIDTH / 2f);
            double num19 = (double)(constraintedPoint4.pos.y - (SCREEN_HEIGHT / 2f));
            float num2 = FIT_TO_BOUNDARIES((double)num, 0.0, (double)(mapWidth - SCREEN_WIDTH));
            float num3 = FIT_TO_BOUNDARIES(num19, 0.0, (double)(mapHeight - SCREEN_HEIGHT));
            camera.MoveToXYImmediate(num2, num3, false);
            if (!freezeCamera || camera.type != CAMERATYPE.CAMERASPEEDDELAY)
            {
                camera.Update(delta);
            }
            if (camera.type == CAMERATYPE.CAMERASPEEDPIXELS)
            {
                float num4 = 100f;
                float num5 = 800f;
                float num6 = 400f;
                float a = 1000f;
                float a2 = 300f;
                float num7 = VectDistance(camera.pos, Vect(num2, num3));
                if (num7 < num4)
                {
                    ignoreTouches = false;
                }
                if (fastenCamera)
                {
                    if (camera.speed < 5500f)
                    {
                        camera.speed *= 1.5f;
                    }
                }
                else if ((double)num7 > initialCameraToStarDistance / 2.0)
                {
                    camera.speed += delta * num5;
                    camera.speed = MIN(a, camera.speed);
                }
                else
                {
                    camera.speed -= delta * num6;
                    camera.speed = MAX(a2, camera.speed);
                }
                if ((double)Math.Abs(camera.pos.x - num2) < 1.0 && (double)Math.Abs(camera.pos.y - num3) < 1.0)
                {
                    camera.type = CAMERATYPE.CAMERASPEEDDELAY;
                    camera.speed = 14f;
                }
            }
            else
            {
                time += delta;
            }
            if (bungees.Count() > 0)
            {
                bool flag = false;
                bool flag2 = false;
                bool flag3 = false;
                int num8 = bungees.Count();
                int k = 0;
                while (k < num8)
                {
                    Grab grab = (Grab)bungees.ObjectAtIndex(k);
                    grab.Update(delta);
                    Bungee rope = grab.rope;
                    if (grab.mover != null)
                    {
                        if (grab.rope != null)
                        {
                            grab.rope.bungeeAnchor.pos = Vect(grab.x, grab.y);
                            grab.rope.bungeeAnchor.pin = grab.rope.bungeeAnchor.pos;
                        }
                        if (grab.radius != -1f)
                        {
                            grab.ReCalcCircle();
                        }
                    }
                    if (rope == null)
                    {
                        goto IL_0478;
                    }
                    if (rope.cut == -1 || rope.cutTime != 0.0)
                    {
                        rope?.Update(delta * ropePhysicsSpeed);
                        if (!grab.hasSpider)
                        {
                            goto IL_0478;
                        }
                        if (camera.type != CAMERATYPE.CAMERASPEEDPIXELS || !ignoreTouches)
                        {
                            grab.UpdateSpider(delta);
                        }
                        if (grab.spiderPos == -1f)
                        {
                            SpiderWon(grab);
                            break;
                        }
                        goto IL_0478;
                    }
                IL_08D4:
                    k++;
                    continue;
                IL_0478:
                    if (grab.radius != -1f && grab.rope == null)
                    {
                        if (twoParts != 2)
                        {
                            if (!noCandyL && VectDistance(Vect(grab.x, grab.y), starL.pos) <= grab.radius + 42f)
                            {
                                Bungee bungee = (Bungee)new Bungee().InitWithHeadAtXYTailAtTXTYandLength(null, grab.x, grab.y, starL, starL.pos.x, starL.pos.y, grab.radius + 42f);
                                bungee.bungeeAnchor.pin = bungee.bungeeAnchor.pos;
                                grab.hideRadius = true;
                                grab.SetRope(bungee);
                                CTRSoundMgr.PlaySound(24);
                                if (grab.mover != null)
                                {
                                    CTRSoundMgr.PlaySound(44);
                                }
                            }
                            if (!noCandyR && grab.rope == null && VectDistance(Vect(grab.x, grab.y), starR.pos) <= grab.radius + 42f)
                            {
                                Bungee bungee2 = (Bungee)new Bungee().InitWithHeadAtXYTailAtTXTYandLength(null, grab.x, grab.y, starR, starR.pos.x, starR.pos.y, grab.radius + 42f);
                                bungee2.bungeeAnchor.pin = bungee2.bungeeAnchor.pos;
                                grab.hideRadius = true;
                                grab.SetRope(bungee2);
                                CTRSoundMgr.PlaySound(24);
                                if (grab.mover != null)
                                {
                                    CTRSoundMgr.PlaySound(44);
                                }
                            }
                        }
                        else if (VectDistance(Vect(grab.x, grab.y), star.pos) <= grab.radius + 42f)
                        {
                            Bungee bungee3 = (Bungee)new Bungee().InitWithHeadAtXYTailAtTXTYandLength(null, grab.x, grab.y, star, star.pos.x, star.pos.y, grab.radius + 42f);
                            bungee3.bungeeAnchor.pin = bungee3.bungeeAnchor.pos;
                            grab.hideRadius = true;
                            grab.SetRope(bungee3);
                            CTRSoundMgr.PlaySound(24);
                            if (grab.mover != null)
                            {
                                CTRSoundMgr.PlaySound(44);
                            }
                        }
                    }
                    if (rope == null)
                    {
                        goto IL_08D4;
                    }
                    MaterialPoint bungeeAnchor = rope.bungeeAnchor;
                    ConstraintedPoint constraintedPoint2 = rope.parts[^1];
                    Vector v = VectSub(bungeeAnchor.pos, constraintedPoint2.pos);
                    bool flag4 = false;
                    if (twoParts != 2)
                    {
                        if (constraintedPoint2 == starL && !noCandyL && !flag2)
                        {
                            flag4 = true;
                        }
                        if (constraintedPoint2 == starR && !noCandyR && !flag3)
                        {
                            flag4 = true;
                        }
                    }
                    else if (!noCandy && !flag)
                    {
                        flag4 = true;
                    }
                    if (rope.relaxed != 0 && rope.cut == -1 && flag4)
                    {
                        float num9 = RADIANS_TO_DEGREES(VectAngleNormalized(v));
                        if (twoParts != 2)
                        {
                            GameObject gameObject = constraintedPoint2 == starL ? candyL : candyR;
                            if (!rope.chosenOne)
                            {
                                rope.initialCandleAngle = gameObject.rotation - num9;
                            }
                            if (constraintedPoint2 == starL)
                            {
                                lastCandyRotateDeltaL = num9 + rope.initialCandleAngle - gameObject.rotation;
                                flag2 = true;
                            }
                            else
                            {
                                lastCandyRotateDeltaR = num9 + rope.initialCandleAngle - gameObject.rotation;
                                flag3 = true;
                            }
                            gameObject.rotation = num9 + rope.initialCandleAngle;
                        }
                        else
                        {
                            if (!rope.chosenOne)
                            {
                                rope.initialCandleAngle = candyMain.rotation - num9;
                            }
                            lastCandyRotateDelta = num9 + rope.initialCandleAngle - candyMain.rotation;
                            candyMain.rotation = num9 + rope.initialCandleAngle;
                            flag = true;
                        }
                        rope.chosenOne = true;
                        goto IL_08D4;
                    }
                    rope.chosenOne = false;
                    goto IL_08D4;
                }
                if (twoParts != 2)
                {
                    if (!flag2 && !noCandyL)
                    {
                        candyL.rotation += MIN(5.0, lastCandyRotateDeltaL);
                        lastCandyRotateDeltaL *= 0.98f;
                    }
                    if (!flag3 && !noCandyR)
                    {
                        candyR.rotation += MIN(5.0, lastCandyRotateDeltaR);
                        lastCandyRotateDeltaR *= 0.98f;
                    }
                }
                else if (!flag && !noCandy)
                {
                    candyMain.rotation += MIN(5.0, lastCandyRotateDelta);
                    lastCandyRotateDelta *= 0.98f;
                }
            }
            if (!noCandy)
            {
                star.Update(delta * ropePhysicsSpeed);
                candy.x = star.pos.x;
                candy.y = star.pos.y;
                candy.Update(delta);
                CalculateTopLeft(candy);
            }
            if (twoParts != 2)
            {
                candyL.Update(delta);
                starL.Update(delta * ropePhysicsSpeed);
                candyR.Update(delta);
                starR.Update(delta * ropePhysicsSpeed);
                if (twoParts == 1)
                {
                    for (int l = 0; l < 30; l++)
                    {
                        ConstraintedPoint.SatisfyConstraints(starL);
                        ConstraintedPoint.SatisfyConstraints(starR);
                    }
                }
                if (partsDist > 0.0)
                {
                    if (Mover.MoveVariableToTarget(ref partsDist, 0.0, 200.0, (double)delta))
                    {
                        CTRSoundMgr.PlaySound(40);
                        twoParts = 2;
                        noCandy = false;
                        noCandyL = true;
                        noCandyR = true;
                        int num20 = Preferences._getIntForKey("PREFS_CANDIES_UNITED") + 1;
                        Preferences._setIntforKey(num20, "PREFS_CANDIES_UNITED", false);
                        if (num20 == 100)
                        {
                            CTRRootController.PostAchievementName("1432722351", ACHIEVEMENT_STRING("\"Romantic Soul\""));
                        }
                        if (candyBubbleL != null || candyBubbleR != null)
                        {
                            candyBubble = candyBubbleL ?? candyBubbleR;
                            candyBubbleAnimation.visible = true;
                        }
                        lastCandyRotateDelta = 0f;
                        lastCandyRotateDeltaL = 0f;
                        lastCandyRotateDeltaR = 0f;
                        star.pos.x = starL.pos.x;
                        star.pos.y = starL.pos.y;
                        candy.x = star.pos.x;
                        candy.y = star.pos.y;
                        CalculateTopLeft(candy);
                        Vector vector = VectSub(starL.pos, starL.prevPos);
                        Vector vector2 = VectSub(starR.pos, starR.prevPos);
                        Vector v2 = Vect((vector.x + vector2.x) / 2f, (vector.y + vector2.y) / 2f);
                        star.prevPos = VectSub(star.pos, v2);
                        int num10 = bungees.Count();
                        for (int m = 0; m < num10; m++)
                        {
                            Bungee rope2 = ((Grab)bungees.ObjectAtIndex(m)).rope;
                            if (rope2 != null && rope2.cut != rope2.parts.Count - 3 && (rope2.tail == starL || rope2.tail == starR))
                            {
                                ConstraintedPoint constraintedPoint3 = rope2.parts[^2];
                                int num11 = (int)rope2.tail.RestLengthFor(constraintedPoint3);
                                star.AddConstraintwithRestLengthofType(constraintedPoint3, num11, Constraint.CONSTRAINT.DISTANCE);
                                rope2.tail = star;
                                rope2.parts[^1] = star;
                                rope2.initialCandleAngle = 0f;
                                rope2.chosenOne = false;
                            }
                        }
                        Animation animation = Animation.Animation_createWithResID(63);
                        animation.DoRestoreCutTransparency();
                        animation.x = candy.x;
                        animation.y = candy.y;
                        animation.anchor = 18;
                        int n = animation.AddAnimationDelayLoopFirstLast(0.05, Timeline.LoopType.TIMELINE_NO_LOOP, 21, 25);
                        animation.GetTimeline(n).delegateTimelineDelegate = aniPool;
                        animation.PlayTimeline(0);
                        _ = aniPool.AddChild(animation);
                    }
                    else
                    {
                        starL.ChangeRestLengthToFor(partsDist, starR);
                        starR.ChangeRestLengthToFor(partsDist, starL);
                    }
                }
                if (!noCandyL && !noCandyR && GameObject.ObjectsIntersect(candyL, candyR) && twoParts == 0)
                {
                    twoParts = 1;
                    partsDist = VectDistance(starL.pos, starR.pos);
                    starL.AddConstraintwithRestLengthofType(starR, partsDist, Constraint.CONSTRAINT.NOT_MORE_THAN);
                    starR.AddConstraintwithRestLengthofType(starL, partsDist, Constraint.CONSTRAINT.NOT_MORE_THAN);
                }
            }
            target.Update(delta);
            if (camera.type != CAMERATYPE.CAMERASPEEDPIXELS || !ignoreTouches)
            {
                foreach (object obj2 in stars)
                {
                    Star star = (Star)obj2;
                    star.Update(delta);
                    if (star.timeout > 0.0 && star.time == 0.0)
                    {
                        star.GetTimeline(1).delegateTimelineDelegate = aniPool;
                        _ = aniPool.AddChild(star);
                        stars.RemoveObject(star);
                        star.timedAnim.PlayTimeline(1);
                        star.PlayTimeline(1);
                        break;
                    }
                    if (twoParts == 2 ? GameObject.ObjectsIntersect(candy, star) && !noCandy : (GameObject.ObjectsIntersect(candyL, star) && !noCandyL) || (GameObject.ObjectsIntersect(candyR, star) && !noCandyR))
                    {
                        candyBlink.PlayTimeline(1);
                        starsCollected++;
                        hudStar[starsCollected - 1].PlayTimeline(0);
                        Animation animation2 = Animation.Animation_createWithResID(71);
                        animation2.DoRestoreCutTransparency();
                        animation2.x = star.x;
                        animation2.y = star.y;
                        animation2.anchor = 18;
                        int n2 = animation2.AddAnimationDelayLoopFirstLast(0.05, Timeline.LoopType.TIMELINE_NO_LOOP, 0, 12);
                        animation2.GetTimeline(n2).delegateTimelineDelegate = aniPool;
                        animation2.PlayTimeline(0);
                        _ = aniPool.AddChild(animation2);
                        stars.RemoveObject(star);
                        CTRSoundMgr.PlaySound(25 + starsCollected - 1);
                        if (target.GetCurrentTimelineIndex() == 0)
                        {
                            target.PlayAnimationtimeline(101, 3);
                            break;
                        }
                        break;
                    }
                }
            }
            foreach (object obj3 in bubbles)
            {
                Bubble bubble3 = (Bubble)obj3;
                bubble3.Update(delta);
                float num12 = 85f;
                if (twoParts != 2)
                {
                    if (!noCandyL && !bubble3.popped && PointInRect(candyL.x, candyL.y, bubble3.x - num12, bubble3.y - num12, num12 * 2f, num12 * 2f))
                    {
                        if (candyBubbleL != null)
                        {
                            PopBubbleAtXY(bubble3.x, bubble3.y);
                        }
                        candyBubbleL = bubble3;
                        candyBubbleAnimationL.visible = true;
                        CTRSoundMgr.PlaySound(13);
                        bubble3.popped = true;
                        bubble3.RemoveChildWithID(0);
                        break;
                    }
                    if (!noCandyR && !bubble3.popped && PointInRect(candyR.x, candyR.y, bubble3.x - num12, bubble3.y - num12, num12 * 2f, num12 * 2f))
                    {
                        if (candyBubbleR != null)
                        {
                            PopBubbleAtXY(bubble3.x, bubble3.y);
                        }
                        candyBubbleR = bubble3;
                        candyBubbleAnimationR.visible = true;
                        CTRSoundMgr.PlaySound(13);
                        bubble3.popped = true;
                        bubble3.RemoveChildWithID(0);
                        break;
                    }
                }
                else if (!noCandy && !bubble3.popped && PointInRect(candy.x, candy.y, bubble3.x - num12, bubble3.y - num12, num12 * 2f, num12 * 2f))
                {
                    if (candyBubble != null)
                    {
                        PopBubbleAtXY(bubble3.x, bubble3.y);
                    }
                    candyBubble = bubble3;
                    candyBubbleAnimation.visible = true;
                    CTRSoundMgr.PlaySound(13);
                    bubble3.popped = true;
                    bubble3.RemoveChildWithID(0);
                    break;
                }
                if (!bubble3.withoutShadow)
                {
                    foreach (object obj4 in rotatedCircles)
                    {
                        RotatedCircle rotatedCircle5 = (RotatedCircle)obj4;
                        if (VectDistance(Vect(bubble3.x, bubble3.y), Vect(rotatedCircle5.x, rotatedCircle5.y)) < rotatedCircle5.sizeInPixels)
                        {
                            bubble3.withoutShadow = true;
                        }
                    }
                }
            }
            foreach (object obj5 in tutorials)
            {
                ((Text)obj5).Update(delta);
            }
            foreach (object obj6 in tutorialImages)
            {
                ((GameObject)obj6).Update(delta);
            }
            foreach (object obj7 in pumps)
            {
                Pump pump = (Pump)obj7;
                pump.Update(delta);
                if (Mover.MoveVariableToTarget(ref pump.pumpTouchTimer, 0.0, 1.0, (double)delta))
                {
                    OperatePump(pump);
                }
            }
            RotatedCircle rotatedCircle6 = null;
            foreach (object obj8 in rotatedCircles)
            {
                RotatedCircle rotatedCircle7 = (RotatedCircle)obj8;
                foreach (object obj9 in bungees)
                {
                    Grab bungee4 = (Grab)obj9;
                    if (VectDistance(Vect(bungee4.x, bungee4.y), Vect(rotatedCircle7.x, rotatedCircle7.y)) <= rotatedCircle7.sizeInPixels + (RTPD(5.0) * 3f))
                    {
                        if (rotatedCircle7.containedObjects.GetObjectIndex(bungee4) == -1)
                        {
                            _ = rotatedCircle7.containedObjects.AddObject(bungee4);
                        }
                    }
                    else if (rotatedCircle7.containedObjects.GetObjectIndex(bungee4) != -1)
                    {
                        rotatedCircle7.containedObjects.RemoveObject(bungee4);
                    }
                }
                foreach (object obj10 in bubbles)
                {
                    Bubble bubble4 = (Bubble)obj10;
                    if (VectDistance(Vect(bubble4.x, bubble4.y), Vect(rotatedCircle7.x, rotatedCircle7.y)) <= rotatedCircle7.sizeInPixels + (RTPD(10.0) * 3f))
                    {
                        if (rotatedCircle7.containedObjects.GetObjectIndex(bubble4) == -1)
                        {
                            _ = rotatedCircle7.containedObjects.AddObject(bubble4);
                        }
                    }
                    else if (rotatedCircle7.containedObjects.GetObjectIndex(bubble4) != -1)
                    {
                        rotatedCircle7.containedObjects.RemoveObject(bubble4);
                    }
                }
                if (rotatedCircle7.removeOnNextUpdate)
                {
                    rotatedCircle6 = rotatedCircle7;
                }
                rotatedCircle7.Update(delta);
            }
            if (rotatedCircle6 != null)
            {
                rotatedCircles.RemoveObject(rotatedCircle6);
            }
            float num13 = RTPD(20.0);
            foreach (object obj11 in socks)
            {
                Sock sock3 = (Sock)obj11;
                sock3.Update(delta);
                if (Mover.MoveVariableToTarget(ref sock3.idleTimeout, 0.0, 1.0, (double)delta))
                {
                    sock3.state = Sock.SOCK_IDLE;
                }
                float num14 = sock3.rotation;
                sock3.rotation = 0f;
                sock3.UpdateRotation();
                Vector ptr = VectRotate(star.posDelta, (double)DEGREES_TO_RADIANS(0f - num14));
                sock3.rotation = num14;
                sock3.UpdateRotation();
                if (ptr.y >= 0.0 && (LineInRect(sock3.t1.x, sock3.t1.y, sock3.t2.x, sock3.t2.y, star.pos.x - num13, star.pos.y - num13, num13 * 2f, num13 * 2f) || LineInRect(sock3.b1.x, sock3.b1.y, sock3.b2.x, sock3.b2.y, star.pos.x - num13, star.pos.y - num13, num13 * 2f, num13 * 2f)))
                {
                    if (sock3.state != Sock.SOCK_IDLE)
                    {
                        continue;
                    }

                    foreach (Sock sock4 in socks)
                    {
                        if (sock4 != sock3 && sock4.group == sock3.group)
                        {
                            sock3.state = Sock.SOCK_RECEIVING;
                            sock4.state = Sock.SOCK_THROWING;
                            ReleaseAllRopes(false);
                            savedSockSpeed = 0.9f * VectLength(star.v);
                            savedSockSpeed *= 1.4f;
                            targetSock = sock4;
                            sock3.light.PlayTimeline(0);
                            sock3.light.visible = true;
                            CTRSoundMgr.PlaySound(45);
                            dd.CallObjectSelectorParamafterDelay(new DelayedDispatcher.DispatchFunc(Selector_teleport), null, 0.1);
                            break;
                        }
                    }
                }
                if (sock3.state != Sock.SOCK_IDLE && sock3.idleTimeout == 0f)
                {
                    sock3.idleTimeout = 0.8f;
                }
            }
            foreach (object obj13 in razors)
            {
                Razor razor = (Razor)obj13;
                razor.Update(delta);
                _ = CutWithRazorOrLine1Line2Immediate(razor, vectZero, vectZero, false);
            }
            foreach (object obj14 in spikes)
            {
                Spikes spike = (Spikes)obj14;
                spike.Update(delta);
                float num15 = 15f;
                if (!spike.electro || (spike.electro && spike.electroOn))
                {
                    bool flag5 = false;
                    bool flag6;
                    if (twoParts != 2)
                    {
                        flag6 = (LineInRect(spike.t1.x, spike.t1.y, spike.t2.x, spike.t2.y, starL.pos.x - num15, starL.pos.y - num15, num15 * 2f, num15 * 2f) || LineInRect(spike.b1.x, spike.b1.y, spike.b2.x, spike.b2.y, starL.pos.x - num15, starL.pos.y - num15, num15 * 2f, num15 * 2f)) && !noCandyL;
                        if (flag6)
                        {
                            flag5 = true;
                        }
                        else
                        {
                            flag6 = (LineInRect(spike.t1.x, spike.t1.y, spike.t2.x, spike.t2.y, starR.pos.x - num15, starR.pos.y - num15, num15 * 2f, num15 * 2f) || LineInRect(spike.b1.x, spike.b1.y, spike.b2.x, spike.b2.y, starR.pos.x - num15, starR.pos.y - num15, num15 * 2f, num15 * 2f)) && !noCandyR;
                        }
                    }
                    else
                    {
                        flag6 = (LineInRect(spike.t1.x, spike.t1.y, spike.t2.x, spike.t2.y, star.pos.x - num15, star.pos.y - num15, num15 * 2f, num15 * 2f) || LineInRect(spike.b1.x, spike.b1.y, spike.b2.x, spike.b2.y, star.pos.x - num15, star.pos.y - num15, num15 * 2f, num15 * 2f)) && !noCandy;
                    }
                    if (flag6)
                    {
                        if (twoParts != 2)
                        {
                            if (flag5)
                            {
                                if (candyBubbleL != null)
                                {
                                    PopCandyBubble(true);
                                }
                            }
                            else if (candyBubbleR != null)
                            {
                                PopCandyBubble(false);
                            }
                        }
                        else if (candyBubble != null)
                        {
                            PopCandyBubble(false);
                        }
                        Image image2 = Image.Image_createWithResID(63);
                        image2.DoRestoreCutTransparency();
                        CandyBreak candyBreak = (CandyBreak)new CandyBreak().InitWithTotalParticlesandImageGrid(5, image2);
                        if (gravityButton != null && !gravityNormal)
                        {
                            candyBreak.gravity.y = -500f;
                            candyBreak.angle = 90f;
                        }
                        candyBreak.particlesDelegate = new Particles.ParticlesFinished(aniPool.ParticlesFinished);
                        if (twoParts != 2)
                        {
                            if (flag5)
                            {
                                candyBreak.x = candyL.x;
                                candyBreak.y = candyL.y;
                                noCandyL = true;
                            }
                            else
                            {
                                candyBreak.x = candyR.x;
                                candyBreak.y = candyR.y;
                                noCandyR = true;
                            }
                        }
                        else
                        {
                            candyBreak.x = candy.x;
                            candyBreak.y = candy.y;
                            noCandy = true;
                        }
                        candyBreak.StartSystem(5);
                        _ = aniPool.AddChild(candyBreak);
                        CTRSoundMgr.PlaySound(14);
                        ReleaseAllRopes(flag5);
                        if (restartState != 0 && (twoParts == 2 || !noCandyL || !noCandyR))
                        {
                            dd.CallObjectSelectorParamafterDelay(new DelayedDispatcher.DispatchFunc(Selector_gameLost), null, 0.3);
                        }
                        return;
                    }
                }
            }
            foreach (object obj15 in bouncers)
            {
                Bouncer bouncer = (Bouncer)obj15;
                bouncer.Update(delta);
                float num16 = 40f;
                bool flag7 = false;
                bool flag8;
                if (twoParts != 2)
                {
                    flag8 = (LineInRect(bouncer.t1.x, bouncer.t1.y, bouncer.t2.x, bouncer.t2.y, starL.pos.x - num16, starL.pos.y - num16, num16 * 2f, num16 * 2f) || LineInRect(bouncer.b1.x, bouncer.b1.y, bouncer.b2.x, bouncer.b2.y, starL.pos.x - num16, starL.pos.y - num16, num16 * 2f, num16 * 2f)) && !noCandyL;
                    if (flag8)
                    {
                        flag7 = true;
                    }
                    else
                    {
                        flag8 = (LineInRect(bouncer.t1.x, bouncer.t1.y, bouncer.t2.x, bouncer.t2.y, starR.pos.x - num16, starR.pos.y - num16, num16 * 2f, num16 * 2f) || LineInRect(bouncer.b1.x, bouncer.b1.y, bouncer.b2.x, bouncer.b2.y, starR.pos.x - num16, starR.pos.y - num16, num16 * 2f, num16 * 2f)) && !noCandyR;
                    }
                }
                else
                {
                    flag8 = (LineInRect(bouncer.t1.x, bouncer.t1.y, bouncer.t2.x, bouncer.t2.y, star.pos.x - num16, star.pos.y - num16, num16 * 2f, num16 * 2f) || LineInRect(bouncer.b1.x, bouncer.b1.y, bouncer.b2.x, bouncer.b2.y, star.pos.x - num16, star.pos.y - num16, num16 * 2f, num16 * 2f)) && !noCandy;
                }
                if (flag8)
                {
                    if (twoParts != 2)
                    {
                        if (flag7)
                        {
                            HandleBouncePtDelta(bouncer, starL, delta);
                        }
                        else
                        {
                            HandleBouncePtDelta(bouncer, starR, delta);
                        }
                    }
                    else
                    {
                        HandleBouncePtDelta(bouncer, star, delta);
                    }
                }
                else
                {
                    bouncer.skip = false;
                }
            }
            float num17 = -40f;
            float num18 = 14f;
            if (twoParts == 0)
            {
                if (candyBubbleL != null)
                {
                    if (gravityButton != null && !gravityNormal)
                    {
                        starL.ApplyImpulseDelta(Vect((0f - starL.v.x) / num18, ((0f - starL.v.y) / num18) - num17), delta);
                    }
                    else
                    {
                        starL.ApplyImpulseDelta(Vect((0f - starL.v.x) / num18, ((0f - starL.v.y) / num18) + num17), delta);
                    }
                }
                if (candyBubbleR != null)
                {
                    if (gravityButton != null && !gravityNormal)
                    {
                        starR.ApplyImpulseDelta(Vect((0f - starR.v.x) / num18, ((0f - starR.v.y) / num18) - num17), delta);
                    }
                    else
                    {
                        starR.ApplyImpulseDelta(Vect((0f - starR.v.x) / num18, ((0f - starR.v.y) / num18) + num17), delta);
                    }
                }
            }
            if (twoParts == 1)
            {
                if (candyBubbleR != null || candyBubbleL != null)
                {
                    if (gravityButton != null && !gravityNormal)
                    {
                        starL.ApplyImpulseDelta(Vect((0f - starL.v.x) / num18, ((0f - starL.v.y) / num18) - num17), delta);
                        starR.ApplyImpulseDelta(Vect((0f - starR.v.x) / num18, ((0f - starR.v.y) / num18) - num17), delta);
                    }
                    else
                    {
                        starL.ApplyImpulseDelta(Vect((0f - starL.v.x) / num18, ((0f - starL.v.y) / num18) + num17), delta);
                        starR.ApplyImpulseDelta(Vect((0f - starR.v.x) / num18, ((0f - starR.v.y) / num18) + num17), delta);
                    }
                }
            }
            else if (candyBubble != null)
            {
                if (gravityButton != null && !gravityNormal)
                {
                    star.ApplyImpulseDelta(Vect((0f - star.v.x) / num18, ((0f - star.v.y) / num18) - num17), delta);
                }
                else
                {
                    star.ApplyImpulseDelta(Vect((0f - star.v.x) / num18, ((0f - star.v.y) / num18) + num17), delta);
                }
            }
            if (!noCandy)
            {
                if (!mouthOpen)
                {
                    if (VectDistance(star.pos, Vect(target.x, target.y)) < 200f)
                    {
                        mouthOpen = true;
                        target.PlayTimeline(7);
                        CTRSoundMgr.PlaySound(17);
                        mouthCloseTimer = 1f;
                    }
                }
                else if (mouthCloseTimer > 0.0)
                {
                    _ = Mover.MoveVariableToTarget(ref mouthCloseTimer, 0.0, 1.0, (double)delta);
                    if (mouthCloseTimer <= 0.0)
                    {
                        if (VectDistance(star.pos, Vect(target.x, target.y)) > 200f)
                        {
                            mouthOpen = false;
                            target.PlayTimeline(8);
                            CTRSoundMgr.PlaySound(16);
                            tummyTeasers++;
                            if (tummyTeasers >= 10)
                            {
                                CTRRootController.PostAchievementName("1058281905", ACHIEVEMENT_STRING("\"Tummy Teaser\""));
                            }
                        }
                        else
                        {
                            mouthCloseTimer = 1f;
                        }
                    }
                }
                if (restartState != 0 && GameObject.ObjectsIntersect(candy, target))
                {
                    GameWon();
                    return;
                }
            }
            bool flag9 = twoParts == 2 && PointOutOfScreen(star) && !noCandy;
            bool flag10 = twoParts != 2 && PointOutOfScreen(starL) && !noCandyL;
            bool flag11 = twoParts != 2 && PointOutOfScreen(starR) && !noCandyR;
            if (flag10 || flag11 || flag9)
            {
                if (flag9)
                {
                    noCandy = true;
                }
                if (flag10)
                {
                    noCandyL = true;
                }
                if (flag11)
                {
                    noCandyR = true;
                }
                if (restartState != 0)
                {
                    int num21 = Preferences._getIntForKey("PREFS_CANDIES_LOST") + 1;
                    Preferences._setIntforKey(num21, "PREFS_CANDIES_LOST", false);
                    if (num21 == 50)
                    {
                        CTRRootController.PostAchievementName("681497443", ACHIEVEMENT_STRING("\"Weight Loser\""));
                    }
                    if (num21 == 200)
                    {
                        CTRRootController.PostAchievementName("1058341297", ACHIEVEMENT_STRING("\"Calorie Minimizer\""));
                    }
                    if (twoParts == 2 || !noCandyL || !noCandyR)
                    {
                        GameLost();
                    }
                    return;
                }
            }
            if (special != 0 && special == 1 && !noCandy && candyBubble != null && candy.y < 400f && candy.x > 1200f)
            {
                special = 0;
                foreach (object obj16 in tutorials)
                {
                    TutorialText tutorial2 = (TutorialText)obj16;
                    if (tutorial2.special == 1)
                    {
                        tutorial2.PlayTimeline(0);
                    }
                }
                foreach (object obj17 in tutorialImages)
                {
                    GameObjectSpecial tutorialImage2 = (GameObjectSpecial)obj17;
                    if (tutorialImage2.special == 1)
                    {
                        tutorialImage2.PlayTimeline(0);
                    }
                }
            }
            if (clickToCut && !ignoreTouches)
            {
                ResetBungeeHighlight();
                bool flag12 = false;
                Vector p = VectAdd(slastTouch, camera.pos);
                if (gravityButton != null && ((Button)gravityButton.GetChild(gravityButton.On() ? 1 : 0)).IsInTouchZoneXYforTouchDown(p.x, p.y, true))
                {
                    flag12 = true;
                }
                if (candyBubble != null || (twoParts != 2 && (candyBubbleL != null || candyBubbleR != null)))
                {
                    foreach (object obj18 in bubbles)
                    {
                        Bubble bubble5 = (Bubble)obj18;
                        if (candyBubble != null && PointInRect(p.x, p.y, star.pos.x - 60f, star.pos.y - 60f, 120f, 120f))
                        {
                            flag12 = true;
                            break;
                        }
                        if (candyBubbleL != null && PointInRect(p.x, p.y, starL.pos.x - 60f, starL.pos.y - 60f, 120f, 120f))
                        {
                            flag12 = true;
                            break;
                        }
                        if (candyBubbleR != null && PointInRect(p.x, p.y, starR.pos.x - 60f, starR.pos.y - 60f, 120f, 120f))
                        {
                            flag12 = true;
                            break;
                        }
                    }
                }
                foreach (object obj19 in spikes)
                {
                    Spikes spike2 = (Spikes)obj19;
                    if (spike2.rotateButton != null && spike2.rotateButton.IsInTouchZoneXYforTouchDown(p.x, p.y, true))
                    {
                        flag12 = true;
                    }
                }
                foreach (object obj20 in pumps)
                {
                    Pump pump2 = (Pump)obj20;
                    if (GameObject.PointInObject(p, pump2))
                    {
                        flag12 = true;
                        break;
                    }
                }
                foreach (object obj21 in rotatedCircles)
                {
                    RotatedCircle rotatedCircle8 = (RotatedCircle)obj21;
                    if (rotatedCircle8.IsLeftControllerActive() || rotatedCircle8.IsRightControllerActive())
                    {
                        flag12 = true;
                        break;
                    }
                    if (VectDistance(Vect(p.x, p.y), Vect(rotatedCircle8.handle1.x, rotatedCircle8.handle1.y)) <= 90f || VectDistance(Vect(p.x, p.y), Vect(rotatedCircle8.handle2.x, rotatedCircle8.handle2.y)) <= 90f)
                    {
                        flag12 = true;
                        break;
                    }
                }
                foreach (object obj22 in bungees)
                {
                    Grab bungee5 = (Grab)obj22;
                    if (bungee5.wheel && PointInRect(p.x, p.y, bungee5.x - 110f, bungee5.y - 110f, 220f, 220f))
                    {
                        flag12 = true;
                        break;
                    }
                    if (bungee5.moveLength > 0.0 && (PointInRect(p.x, p.y, bungee5.x - 65f, bungee5.y - 65f, 130f, 130f) || bungee5.moverDragging != -1))
                    {
                        flag12 = true;
                        break;
                    }
                }
                if (!flag12)
                {
                    Vector s = default;
                    Grab grab2 = null;
                    Bungee nearestBungeeSegmentByBeziersPointsatXYgrab = GetNearestBungeeSegmentByBeziersPointsatXYgrab(ref s, slastTouch.x + camera.pos.x, slastTouch.y + camera.pos.y, ref grab2);
                    if (nearestBungeeSegmentByBeziersPointsatXYgrab != null)
                    {
                        nearestBungeeSegmentByBeziersPointsatXYgrab.highlighted = true;
                    }
                }
            }
            if (Mover.MoveVariableToTarget(ref dimTime, 0.0, 1.0, (double)delta))
            {
                if (restartState == 0)
                {
                    restartState = 1;
                    Hide();
                    Show();
                    dimTime = 0.15f;
                    return;
                }
                restartState = -1;
            }
        }

        public void Teleport()
        {
            if (targetSock != null)
            {
                targetSock.light.PlayTimeline(0);
                targetSock.light.visible = true;
                Vector v = Vect(0f, -16f);
                v = VectRotate(v, (double)DEGREES_TO_RADIANS(targetSock.rotation));
                star.pos.x = targetSock.x;
                star.pos.y = targetSock.y;
                star.pos = VectAdd(star.pos, v);
                star.prevPos.x = star.pos.x;
                star.prevPos.y = star.pos.y;
                star.v = VectMult(VectRotate(Vect(0f, -1f), (double)DEGREES_TO_RADIANS(targetSock.rotation)), savedSockSpeed);
                star.posDelta = VectDiv(star.v, 60f);
                star.prevPos = VectSub(star.pos, star.posDelta);
                targetSock = null;
            }
        }

        public void AnimateLevelRestart()
        {
            restartState = 0;
            dimTime = 0.15f;
        }

        public void ReleaseAllRopes(bool left)
        {
            int num = bungees.Count();
            for (int i = 0; i < num; i++)
            {
                Grab grab = (Grab)bungees.ObjectAtIndex(i);
                Bungee rope = grab.rope;
                if (rope != null && (rope.tail == star || (rope.tail == starL && left) || (rope.tail == starR && !left)))
                {
                    if (rope.cut == -1)
                    {
                        rope.SetCut(rope.parts.Count - 2);
                    }
                    else
                    {
                        rope.hideTailParts = true;
                    }
                    if (grab.hasSpider && grab.spiderActive)
                    {
                        SpiderBusted(grab);
                    }
                }
            }
        }

        public void CalculateScore()
        {
            timeBonus = (int)MAX(0f, 30f - time) * 100;
            timeBonus /= 10;
            timeBonus *= 10;
            starBonus = 1000 * starsCollected;
            score = (int)Ceil(timeBonus + starBonus);
        }

        public void GameWon()
        {
            dd.CancelAllDispatches();
            target.PlayTimeline(6);
            CTRSoundMgr.PlaySound(15);
            if (candyBubble != null)
            {
                PopCandyBubble(false);
            }
            noCandy = true;
            candy.passTransformationsToChilds = true;
            candyMain.scaleX = candyMain.scaleY = 1f;
            candyTop.scaleX = candyTop.scaleY = 1f;
            Timeline timeline = new Timeline().InitWithMaxKeyFramesOnTrack(2);
            timeline.AddKeyFrame(KeyFrame.MakePos(candy.x, candy.y, KeyFrame.TransitionType.FRAME_TRANSITION_LINEAR, 0.0));
            timeline.AddKeyFrame(KeyFrame.MakePos(target.x, target.y + 10.0, KeyFrame.TransitionType.FRAME_TRANSITION_LINEAR, 0.1));
            timeline.AddKeyFrame(KeyFrame.MakeScale(0.71, 0.71, KeyFrame.TransitionType.FRAME_TRANSITION_LINEAR, 0.0));
            timeline.AddKeyFrame(KeyFrame.MakeScale(0.0, 0.0, KeyFrame.TransitionType.FRAME_TRANSITION_LINEAR, 0.1));
            timeline.AddKeyFrame(KeyFrame.MakeColor(RGBAColor.solidOpaqueRGBA, KeyFrame.TransitionType.FRAME_TRANSITION_LINEAR, 0.0));
            timeline.AddKeyFrame(KeyFrame.MakeColor(RGBAColor.transparentRGBA, KeyFrame.TransitionType.FRAME_TRANSITION_LINEAR, 0.1));
            candy.AddTimelinewithID(timeline, 0);
            candy.PlayTimeline(0);
            timeline.delegateTimelineDelegate = aniPool;
            _ = aniPool.AddChild(candy);
            dd.CallObjectSelectorParamafterDelay(new DelayedDispatcher.DispatchFunc(Selector_gameWon), null, 2.0);
            CalculateScore();
            ReleaseAllRopes(false);
        }

        public void GameLost()
        {
            dd.CancelAllDispatches();
            target.PlayAnimationtimeline(102, 5);
            CTRSoundMgr.PlaySound(18);
            dd.CallObjectSelectorParamafterDelay(new DelayedDispatcher.DispatchFunc(Selector_animateLevelRestart), null, 1.0);
            gameSceneDelegate.GameLost();
        }

        public override void Draw()
        {
            OpenGL.GlClear(0);
            base.PreDraw();
            camera.ApplyCameraTransformation();
            OpenGL.GlEnable(0);
            OpenGL.GlDisable(1);
            Vector pos = VectDiv(camera.pos, 1.25f);
            back.UpdateWithCameraPos(pos);
            float num = Canvas.xOffsetScaled;
            float num2 = 0f;
            OpenGL.GlPushMatrix();
            OpenGL.GlTranslatef((double)num, (double)num2, 0.0);
            OpenGL.GlScalef(back.scaleX, back.scaleY, 1.0);
            OpenGL.GlTranslatef((double)(0f - num), (double)(0f - num2), 0.0);
            OpenGL.GlTranslatef(Canvas.xOffsetScaled, 0.0, 0.0);
            back.Draw();
            if (mapHeight > SCREEN_HEIGHT)
            {
                float num3 = RTD(2.0);
                int pack = ((CTRRootController)Application.SharedRootController()).GetPack();
                CTRTexture2D texture = Application.GetTexture(105 + (pack * 2));
                int num4 = 0;
                float num5 = texture.quadOffsets[num4].y;
                CTRRectangle r = texture.quadRects[num4];
                r.y += num3;
                r.h -= num3 * 2f;
                GLDrawer.DrawImagePart(texture, r, 0.0, (double)(num5 + num3));
            }
            OpenGL.GlEnable(1);
            OpenGL.GlBlendFunc(BlendingFactor.GLONE, BlendingFactor.GLONEMINUSSRCALPHA);
            if (earthAnims != null)
            {
                foreach (object obj in earthAnims)
                {
                    ((Image)obj).Draw();
                }
            }
            OpenGL.GlTranslatef((double)-(double)Canvas.xOffsetScaled, 0.0, 0.0);
            OpenGL.GlPopMatrix();
            OpenGL.GlEnable(1);
            OpenGL.GlBlendFunc(BlendingFactor.GLONE, BlendingFactor.GLONEMINUSSRCALPHA);
            pollenDrawer.Draw();
            gravityButton?.Draw();
            OpenGL.GlColor4f(Color.White);
            OpenGL.GlEnable(0);
            OpenGL.GlBlendFunc(BlendingFactor.GLONE, BlendingFactor.GLONEMINUSSRCALPHA);
            support.Draw();
            target.Draw();
            foreach (object obj2 in tutorials)
            {
                ((Text)obj2).Draw();
            }
            foreach (object obj3 in tutorialImages)
            {
                ((GameObject)obj3).Draw();
            }
            foreach (object obj4 in razors)
            {
                ((Razor)obj4).Draw();
            }
            foreach (object obj5 in rotatedCircles)
            {
                ((RotatedCircle)obj5).Draw();
            }
            foreach (object obj6 in bubbles)
            {
                ((GameObject)obj6).Draw();
            }
            foreach (object obj7 in pumps)
            {
                ((GameObject)obj7).Draw();
            }
            foreach (object obj8 in spikes)
            {
                ((Spikes)obj8).Draw();
            }
            foreach (object obj9 in bouncers)
            {
                ((Bouncer)obj9).Draw();
            }
            foreach (object obj10 in socks)
            {
                Sock sock = (Sock)obj10;
                sock.y -= 85f;
                sock.Draw();
                sock.y += 85f;
            }
            OpenGL.GlBlendFunc(BlendingFactor.GLSRCALPHA, BlendingFactor.GLONEMINUSSRCALPHA);
            foreach (object obj11 in bungees)
            {
                ((Grab)obj11).DrawBack();
            }
            foreach (object obj12 in bungees)
            {
                ((Grab)obj12).Draw();
            }
            OpenGL.GlBlendFunc(BlendingFactor.GLONE, BlendingFactor.GLONEMINUSSRCALPHA);
            foreach (object obj13 in stars)
            {
                ((GameObject)obj13).Draw();
            }
            if (!noCandy && targetSock == null)
            {
                candy.x = star.pos.x;
                candy.y = star.pos.y;
                candy.Draw();
                if (candyBlink.GetCurrentTimeline() != null)
                {
                    OpenGL.GlBlendFunc(BlendingFactor.GLSRCALPHA, BlendingFactor.GLONE);
                    candyBlink.Draw();
                    OpenGL.GlBlendFunc(BlendingFactor.GLONE, BlendingFactor.GLONEMINUSSRCALPHA);
                }
            }
            if (twoParts != 2)
            {
                if (!noCandyL)
                {
                    candyL.x = starL.pos.x;
                    candyL.y = starL.pos.y;
                    candyL.Draw();
                }
                if (!noCandyR)
                {
                    candyR.x = starR.pos.x;
                    candyR.y = starR.pos.y;
                    candyR.Draw();
                }
            }
            foreach (object obj14 in bungees)
            {
                Grab bungee3 = (Grab)obj14;
                if (bungee3.hasSpider)
                {
                    bungee3.DrawSpider();
                }
            }
            aniPool.Draw();
            OpenGL.GlBlendFunc(BlendingFactor.GLSRCALPHA, BlendingFactor.GLONEMINUSSRCALPHA);
            OpenGL.GlDisable(0);
            OpenGL.GlColor4f(Color.White);
            DrawCuts();
            OpenGL.GlEnable(0);
            OpenGL.GlBlendFunc(BlendingFactor.GLONE, BlendingFactor.GLONEMINUSSRCALPHA);
            camera.CancelCameraTransformation();
            staticAniPool.Draw();
            if (nightLevel)
            {
                OpenGL.GlDisable(4);
            }
            base.PostDraw();
        }

        public void DrawCuts()
        {
            for (int i = 0; i < 5; i++)
            {
                int num = fingerCuts[i].Count();
                if (num > 0)
                {
                    float num2 = RTD(6.0);
                    float num3 = 1f;
                    int num4 = 0;
                    int j = 0;
                    Vector[] array = new Vector[num + 1];
                    int num5 = 0;
                    while (j < num)
                    {
                        FingerCut fingerCut = (FingerCut)fingerCuts[i].ObjectAtIndex(j);
                        if (j == 0)
                        {
                            array[num5++] = fingerCut.start;
                        }
                        array[num5++] = fingerCut.end;
                        j++;
                    }
                    List<Vector> list = [];
                    Vector vector = default;
                    bool flag = true;
                    for (int k = 0; k < array.Length; k++)
                    {
                        if (k == 0)
                        {
                            list.Add(array[k]);
                        }
                        else if (array[k].x != vector.x || array[k].y != vector.y)
                        {
                            list.Add(array[k]);
                            flag = false;
                        }
                        vector = array[k];
                    }
                    if (!flag)
                    {
                        array = [.. list];
                        num = array.Length - 1;
                        int num6 = num * 2;
                        float[] array2 = new float[num6 * 2];
                        float num7 = 1f / num6;
                        float num8 = 0f;
                        int num9 = 0;
                        for (; ; )
                        {
                            if ((double)num8 > 1.0)
                            {
                                num8 = 1f;
                            }
                            Vector vector2 = GLDrawer.CalcPathBezier(array, num + 1, num8);
                            if (num9 > array2.Length - 2)
                            {
                                break;
                            }
                            array2[num9++] = vector2.x;
                            array2[num9++] = vector2.y;
                            if ((double)num8 == 1.0)
                            {
                                break;
                            }
                            num8 += num7;
                        }
                        float num10 = num2 / num6;
                        float[] array3 = new float[num6 * 4];
                        for (int l = 0; l < num6 - 1; l++)
                        {
                            float s = num3;
                            float s2 = l == num6 - 2 ? 1f : num3 + num10;
                            Vector vector3 = Vect(array2[l * 2], array2[(l * 2) + 1]);
                            Vector vector8 = Vect(array2[(l + 1) * 2], array2[((l + 1) * 2) + 1]);
                            Vector vector9 = VectNormalize(VectSub(vector8, vector3));
                            Vector v4 = VectRperp(vector9);
                            Vector v5 = VectPerp(vector9);
                            if (num4 == 0)
                            {
                                Vector vector4 = VectAdd(vector3, VectMult(v4, s));
                                Vector vector5 = VectAdd(vector3, VectMult(v5, s));
                                array3[num4++] = vector5.x;
                                array3[num4++] = vector5.y;
                                array3[num4++] = vector4.x;
                                array3[num4++] = vector4.y;
                            }
                            Vector vector6 = VectAdd(vector8, VectMult(v4, s2));
                            Vector vector7 = VectAdd(vector8, VectMult(v5, s2));
                            array3[num4++] = vector7.x;
                            array3[num4++] = vector7.y;
                            array3[num4++] = vector6.x;
                            array3[num4++] = vector6.y;
                            num3 += num10;
                        }
                        OpenGL.GlColor4f(Color.White);
                        OpenGL.GlVertexPointer(2, 5, 0, array3);
                        OpenGL.GlDrawArrays(8, 0, num4 / 2);
                    }
                }
            }
        }

        public static void HandlePumpFlowPtSkin(Pump p, ConstraintedPoint s, GameObject c)
        {
            float num = 624f;
            if (GameObject.RectInObject(p.x - num, p.y - num, p.x + num, p.y + num, c))
            {
                Vector v = Vect(c.x, c.y);
                Vector vector = default;
                vector.x = p.x - (p.bb.w / 2f);
                Vector vector2 = default;
                vector2.x = p.x + (p.bb.w / 2f);
                vector.y = vector2.y = p.y;
                if (p.angle != 0.0)
                {
                    v = VectRotateAround(v, 0.0 - p.angle, p.x, p.y);
                }
                if (v.y < vector.y && RectInRect((float)(v.x - (c.bb.w / 2.0)), (float)(v.y - (c.bb.h / 2.0)), (float)(v.x + (c.bb.w / 2.0)), (float)(v.y + (c.bb.h / 2.0)), vector.x, vector.y - num, vector2.x, vector2.y))
                {
                    float num2 = num * 2f * (num - (vector.y - v.y)) / num;
                    Vector v2 = Vect(0f, 0f - num2);
                    v2 = VectRotate(v2, p.angle);
                    s.ApplyImpulseDelta(v2, 0.016f);
                }
            }
        }

        public static void HandleBouncePtDelta(Bouncer b, ConstraintedPoint s, float delta)
        {
            if (!b.skip)
            {
                b.skip = true;
                Vector vector = VectSub(s.prevPos, s.pos);
                int num = VectRotateAround(s.prevPos, (double)(0f - b.angle), b.x, b.y).y >= b.y ? 1 : -1;
                float s2 = MAX((double)(VectLength(vector) * 40f), 840.0) * num;
                Vector impulse = VectMult(VectPerp(VectForAngle(b.angle)), s2);
                s.pos = VectRotateAround(s.pos, (double)(0f - b.angle), b.x, b.y);
                s.prevPos = VectRotateAround(s.prevPos, (double)(0f - b.angle), b.x, b.y);
                s.prevPos.y = s.pos.y;
                s.pos = VectRotateAround(s.pos, b.angle, b.x, b.y);
                s.prevPos = VectRotateAround(s.prevPos, b.angle, b.x, b.y);
                s.ApplyImpulseDelta(impulse, delta);
                b.PlayTimeline(0);
                CTRSoundMgr.PlaySound(41);
            }
        }

        public void OperatePump(Pump p)
        {
            p.PlayTimeline(0);
            CTRSoundMgr.PlaySound(RND_RANGE(29, 32));
            Image grid = Image.Image_createWithResID(83);
            PumpDirt pumpDirt = new PumpDirt().InitWithTotalParticlesAngleandImageGrid(5, RADIANS_TO_DEGREES((float)p.angle) - 90f, grid);
            pumpDirt.particlesDelegate = new Particles.ParticlesFinished(aniPool.ParticlesFinished);
            Vector v = Vect(p.x + 80f, p.y);
            v = VectRotateAround(v, p.angle - 1.5707963267948966, p.x, p.y);
            pumpDirt.x = v.x;
            pumpDirt.y = v.y;
            pumpDirt.StartSystem(5);
            _ = aniPool.AddChild(pumpDirt);
            if (!noCandy)
            {
                HandlePumpFlowPtSkin(p, star, candy);
            }
            if (twoParts != 2)
            {
                if (!noCandyL)
                {
                    HandlePumpFlowPtSkin(p, starL, candyL);
                }
                if (!noCandyR)
                {
                    HandlePumpFlowPtSkin(p, starR, candyR);
                }
            }
        }

        public int CutWithRazorOrLine1Line2Immediate(Razor r, Vector v1, Vector v2, bool im)
        {
            int num = 0;
            for (int i = 0; i < bungees.Count(); i++)
            {
                Grab grab = (Grab)bungees.ObjectAtIndex(i);
                Bungee rope = grab.rope;
                if (rope != null && rope.cut == -1)
                {
                    for (int j = 0; j < rope.parts.Count - 1; j++)
                    {
                        ConstraintedPoint constraintedPoint = rope.parts[j];
                        ConstraintedPoint constraintedPoint2 = rope.parts[j + 1];
                        bool flag = false;
                        if (r == null)
                        {
                            flag = (!grab.wheel || !LineInRect(v1.x, v1.y, v2.x, v2.y, grab.x - 110f, grab.y - 110f, 220f, 220f)) && LineInLine(v1.x, v1.y, v2.x, v2.y, constraintedPoint.pos.x, constraintedPoint.pos.y, constraintedPoint2.pos.x, constraintedPoint2.pos.y);
                        }
                        else if (constraintedPoint.prevPos.x != 2.1474836E+09f)
                        {
                            float num2 = MinOf4(constraintedPoint.pos.x, constraintedPoint.prevPos.x, constraintedPoint2.pos.x, constraintedPoint2.prevPos.x);
                            float y1t = MinOf4(constraintedPoint.pos.y, constraintedPoint.prevPos.y, constraintedPoint2.pos.y, constraintedPoint2.prevPos.y);
                            float x1r = MaxOf4(constraintedPoint.pos.x, constraintedPoint.prevPos.x, constraintedPoint2.pos.x, constraintedPoint2.prevPos.x);
                            float y1b = MaxOf4(constraintedPoint.pos.y, constraintedPoint.prevPos.y, constraintedPoint2.pos.y, constraintedPoint2.prevPos.y);
                            flag = RectInRect(num2, y1t, x1r, y1b, r.drawX, r.drawY, r.drawX + r.width, r.drawY + r.height);
                        }
                        if (flag)
                        {
                            num++;
                            if (grab.hasSpider && grab.spiderActive)
                            {
                                SpiderBusted(grab);
                            }
                            CTRSoundMgr.PlaySound(20 + rope.relaxed);
                            rope.SetCut(j);
                            if (im)
                            {
                                rope.cutTime = 0f;
                                rope.RemovePart(j);
                            }
                            return num;
                        }
                    }
                }
            }
            return num;
        }

        public void SpiderBusted(Grab g)
        {
            int num = Preferences._getIntForKey("PREFS_SPIDERS_BUSTED") + 1;
            Preferences._setIntforKey(num, "PREFS_SPIDERS_BUSTED", false);
            if (num == 40)
            {
                CTRRootController.PostAchievementName("681486608", ACHIEVEMENT_STRING("\"Spider Busted\""));
            }
            if (num == 200)
            {
                CTRRootController.PostAchievementName("1058341284", ACHIEVEMENT_STRING("\"Spider Tammer\""));
            }
            CTRSoundMgr.PlaySound(34);
            g.hasSpider = false;
            Image image = Image.Image_createWithResIDQuad(64, 11);
            image.DoRestoreCutTransparency();
            Timeline timeline = new Timeline().InitWithMaxKeyFramesOnTrack(3);
            if (gravityButton != null && !gravityNormal)
            {
                timeline.AddKeyFrame(KeyFrame.MakePos(g.spider.x, g.spider.y, KeyFrame.TransitionType.FRAME_TRANSITION_EASE_OUT, 0.0));
                timeline.AddKeyFrame(KeyFrame.MakePos(g.spider.x, g.spider.y + 50.0, KeyFrame.TransitionType.FRAME_TRANSITION_EASE_OUT, 0.3));
                timeline.AddKeyFrame(KeyFrame.MakePos(g.spider.x, (double)(g.spider.y - SCREEN_HEIGHT), KeyFrame.TransitionType.FRAME_TRANSITION_EASE_IN, 1.0));
            }
            else
            {
                timeline.AddKeyFrame(KeyFrame.MakePos(g.spider.x, g.spider.y, KeyFrame.TransitionType.FRAME_TRANSITION_EASE_OUT, 0.0));
                timeline.AddKeyFrame(KeyFrame.MakePos(g.spider.x, g.spider.y - 50.0, KeyFrame.TransitionType.FRAME_TRANSITION_EASE_OUT, 0.3));
                timeline.AddKeyFrame(KeyFrame.MakePos(g.spider.x, (double)(g.spider.y + SCREEN_HEIGHT), KeyFrame.TransitionType.FRAME_TRANSITION_EASE_IN, 1.0));
            }
            timeline.AddKeyFrame(KeyFrame.MakeRotation(0.0, KeyFrame.TransitionType.FRAME_TRANSITION_LINEAR, 0.0));
            timeline.AddKeyFrame(KeyFrame.MakeRotation(RND_RANGE(-120, 120), KeyFrame.TransitionType.FRAME_TRANSITION_LINEAR, 1.0));
            image.AddTimelinewithID(timeline, 0);
            image.PlayTimeline(0);
            image.x = g.spider.x;
            image.y = g.spider.y;
            image.anchor = 18;
            timeline.delegateTimelineDelegate = aniPool;
            _ = aniPool.AddChild(image);
        }

        public void SpiderWon(Grab sg)
        {
            CTRSoundMgr.PlaySound(35);
            int num = bungees.Count();
            for (int i = 0; i < num; i++)
            {
                Grab grab = (Grab)bungees.ObjectAtIndex(i);
                Bungee rope = grab.rope;
                if (rope != null && rope.tail == star)
                {
                    if (rope.cut == -1)
                    {
                        rope.SetCut(rope.parts.Count - 2);
                        rope.forceWhite = false;
                    }
                    if (grab.hasSpider && grab.spiderActive && sg != grab)
                    {
                        SpiderBusted(grab);
                    }
                }
            }
            sg.hasSpider = false;
            spiderTookCandy = true;
            noCandy = true;
            Image image = Image.Image_createWithResIDQuad(64, 12);
            image.DoRestoreCutTransparency();
            candy.anchor = candy.parentAnchor = 18;
            candy.x = 0f;
            candy.y = -5f;
            _ = image.AddChild(candy);
            Timeline timeline = new Timeline().InitWithMaxKeyFramesOnTrack(3);
            if (gravityButton != null && !gravityNormal)
            {
                timeline.AddKeyFrame(KeyFrame.MakePos(sg.spider.x, sg.spider.y - 10.0, KeyFrame.TransitionType.FRAME_TRANSITION_EASE_OUT, 0.0));
                timeline.AddKeyFrame(KeyFrame.MakePos(sg.spider.x, sg.spider.y + 70.0, KeyFrame.TransitionType.FRAME_TRANSITION_EASE_OUT, 0.3));
                timeline.AddKeyFrame(KeyFrame.MakePos(sg.spider.x, (double)(sg.spider.y - SCREEN_HEIGHT), KeyFrame.TransitionType.FRAME_TRANSITION_EASE_IN, 1.0));
            }
            else
            {
                timeline.AddKeyFrame(KeyFrame.MakePos(sg.spider.x, sg.spider.y - 10.0, KeyFrame.TransitionType.FRAME_TRANSITION_EASE_OUT, 0.0));
                timeline.AddKeyFrame(KeyFrame.MakePos(sg.spider.x, sg.spider.y - 70.0, KeyFrame.TransitionType.FRAME_TRANSITION_EASE_OUT, 0.3));
                timeline.AddKeyFrame(KeyFrame.MakePos(sg.spider.x, (double)(sg.spider.y + SCREEN_HEIGHT), KeyFrame.TransitionType.FRAME_TRANSITION_EASE_IN, 1.0));
            }
            image.AddTimelinewithID(timeline, 0);
            image.PlayTimeline(0);
            image.x = sg.spider.x;
            image.y = sg.spider.y - 10f;
            image.anchor = 18;
            timeline.delegateTimelineDelegate = aniPool;
            _ = aniPool.AddChild(image);
            if (restartState != 0)
            {
                dd.CallObjectSelectorParamafterDelay(new DelayedDispatcher.DispatchFunc(Selector_gameLost), null, 2.0);
            }
        }

        public void PopCandyBubble(bool left)
        {
            if (twoParts == 2)
            {
                candyBubble = null;
                candyBubbleAnimation.visible = false;
                PopBubbleAtXY(candy.x, candy.y);
                return;
            }
            if (left)
            {
                candyBubbleL = null;
                candyBubbleAnimationL.visible = false;
                PopBubbleAtXY(candyL.x, candyL.y);
                return;
            }
            candyBubbleR = null;
            candyBubbleAnimationR.visible = false;
            PopBubbleAtXY(candyR.x, candyR.y);
        }

        public void PopBubbleAtXY(float bx, float by)
        {
            CTRSoundMgr.PlaySound(12);
            Animation animation = Animation.Animation_createWithResID(73);
            animation.DoRestoreCutTransparency();
            animation.x = bx;
            animation.y = by;
            animation.anchor = 18;
            int i = animation.AddAnimationDelayLoopFirstLast(0.05, Timeline.LoopType.TIMELINE_NO_LOOP, 0, 11);
            animation.GetTimeline(i).delegateTimelineDelegate = aniPool;
            animation.PlayTimeline(0);
            _ = aniPool.AddChild(animation);
        }

        public bool HandleBubbleTouchXY(ConstraintedPoint s, float tx, float ty)
        {
            if (PointInRect(tx + camera.pos.x, ty + camera.pos.y, s.pos.x - 60f, s.pos.y - 60f, 120f, 120f))
            {
                PopCandyBubble(s == starL);
                int num = Preferences._getIntForKey("PREFS_BUBBLES_POPPED") + 1;
                Preferences._setIntforKey(num, "PREFS_BUBBLES_POPPED", false);
                if (num == 50)
                {
                    CTRRootController.PostAchievementName("681513183", ACHIEVEMENT_STRING("\"Bubble Popper\""));
                }
                if (num == 300)
                {
                    CTRRootController.PostAchievementName("1058345234", ACHIEVEMENT_STRING("\"Bubble Master\""));
                }
                return true;
            }
            return false;
        }

        public void ResetBungeeHighlight()
        {
            for (int i = 0; i < bungees.Count(); i++)
            {
                Bungee rope = ((Grab)bungees.ObjectAtIndex(i)).rope;
                if (rope != null && rope.cut == -1)
                {
                    rope.highlighted = false;
                }
            }
        }

        public Bungee GetNearestBungeeSegmentByBeziersPointsatXYgrab(ref Vector s, float tx, float ty, ref Grab grab)
        {
            float num = 60f;
            Bungee result = null;
            float num2 = num;
            Vector v = Vect(tx, ty);
            for (int i = 0; i < bungees.Count(); i++)
            {
                Grab grab2 = (Grab)bungees.ObjectAtIndex(i);
                Bungee rope = grab2.rope;
                if (rope != null)
                {
                    for (int j = 0; j < rope.drawPtsCount; j += 2)
                    {
                        Vector vector = Vect(rope.drawPts[j], rope.drawPts[j + 1]);
                        float num3 = VectDistance(vector, v);
                        if (num3 < num && num3 < num2)
                        {
                            num2 = num3;
                            result = rope;
                            s = vector;
                            grab = grab2;
                        }
                    }
                }
            }
            return result;
        }

        public static Bungee GetNearestBungeeSegmentByConstraintsforGrab(ref Vector s, Grab g)
        {
            float num4 = 2.1474836E+09f;
            Bungee result = null;
            float num2 = num4;
            Vector v = s;
            Bungee rope = g.rope;
            if (rope == null || rope.cut != -1)
            {
                return null;
            }
            for (int i = 0; i < rope.parts.Count - 1; i++)
            {
                ConstraintedPoint constraintedPoint = rope.parts[i];
                float num3 = VectDistance(constraintedPoint.pos, v);
                if (num3 < num2 && (!g.wheel || !PointInRect(constraintedPoint.pos.x, constraintedPoint.pos.y, g.x - 110f, g.y - 110f, 220f, 220f)))
                {
                    num2 = num3;
                    result = rope;
                    s = constraintedPoint.pos;
                }
            }
            return result;
        }

        public bool TouchDownXYIndex(float tx, float ty, int ti)
        {
            if (ignoreTouches)
            {
                if (camera.type == CAMERATYPE.CAMERASPEEDPIXELS)
                {
                    fastenCamera = true;
                }
                return true;
            }
            if (ti >= 5)
            {
                return true;
            }
            if (gravityButton != null && ((Button)gravityButton.GetChild(gravityButton.On() ? 1 : 0)).IsInTouchZoneXYforTouchDown(tx + camera.pos.x, ty + camera.pos.y, true))
            {
                gravityTouchDown = ti;
            }
            Vector vector = Vect(tx, ty);
            if (candyBubble != null && HandleBubbleTouchXY(star, tx, ty))
            {
                return true;
            }
            if (twoParts != 2)
            {
                if (candyBubbleL != null && HandleBubbleTouchXY(starL, tx, ty))
                {
                    return true;
                }
                if (candyBubbleR != null && HandleBubbleTouchXY(starR, tx, ty))
                {
                    return true;
                }
            }
            if (!dragging[ti])
            {
                dragging[ti] = true;
                prevStartPos[ti] = startPos[ti] = vector;
            }
            foreach (object obj in spikes)
            {
                Spikes spike = (Spikes)obj;
                if (spike.rotateButton != null && spike.touchIndex == -1 && spike.rotateButton.OnTouchDownXY(tx + camera.pos.x, ty + camera.pos.y))
                {
                    spike.touchIndex = ti;
                    return true;
                }
            }
            int num = pumps.Count();
            for (int i = 0; i < num; i++)
            {
                Pump pump = (Pump)pumps.ObjectAtIndex(i);
                if (GameObject.PointInObject(Vect(tx + camera.pos.x, ty + camera.pos.y), pump))
                {
                    pump.pumpTouchTimer = 0.05f;
                    pump.pumpTouch = ti;
                    return true;
                }
            }
            RotatedCircle rotatedCircle = null;
            bool flag = false;
            bool flag2 = false;
            foreach (object obj2 in rotatedCircles)
            {
                RotatedCircle rotatedCircle2 = (RotatedCircle)obj2;
                float num2 = VectDistance(Vect(tx + camera.pos.x, ty + camera.pos.y), rotatedCircle2.handle1);
                float num3 = VectDistance(Vect(tx + camera.pos.x, ty + camera.pos.y), rotatedCircle2.handle2);
                if ((num2 < 90f && !rotatedCircle2.HasOneHandle()) || num3 < 90f)
                {
                    foreach (object obj3 in rotatedCircles)
                    {
                        RotatedCircle rotatedCircle3 = (RotatedCircle)obj3;
                        if (rotatedCircles.GetObjectIndex(rotatedCircle3) > rotatedCircles.GetObjectIndex(rotatedCircle2))
                        {
                            float num4 = VectDistance(Vect(rotatedCircle3.x, rotatedCircle3.y), Vect(rotatedCircle2.x, rotatedCircle2.y));
                            if (num4 + rotatedCircle3.sizeInPixels <= rotatedCircle2.sizeInPixels)
                            {
                                flag = true;
                            }
                            if (num4 <= rotatedCircle2.sizeInPixels + rotatedCircle3.sizeInPixels)
                            {
                                flag2 = true;
                            }
                        }
                    }
                    rotatedCircle2.lastTouch = Vect(tx + camera.pos.x, ty + camera.pos.y);
                    rotatedCircle2.operating = ti;
                    if (num2 < 90f)
                    {
                        rotatedCircle2.SetIsLeftControllerActive(true);
                    }
                    if (num3 < 90f)
                    {
                        rotatedCircle2.SetIsRightControllerActive(true);
                    }
                    rotatedCircle = rotatedCircle2;
                    break;
                }
            }
            if (rotatedCircles.GetObjectIndex(rotatedCircle) != rotatedCircles.Count() - 1 && flag2 && !flag)
            {
                Timeline timeline = new Timeline().InitWithMaxKeyFramesOnTrack(2);
                timeline.AddKeyFrame(KeyFrame.MakeColor(RGBAColor.transparentRGBA, KeyFrame.TransitionType.FRAME_TRANSITION_LINEAR, 0.0));
                timeline.AddKeyFrame(KeyFrame.MakeColor(RGBAColor.solidOpaqueRGBA, KeyFrame.TransitionType.FRAME_TRANSITION_LINEAR, 0.2));
                Timeline timeline2 = new Timeline().InitWithMaxKeyFramesOnTrack(1);
                timeline2.AddKeyFrame(KeyFrame.MakeColor(RGBAColor.solidOpaqueRGBA, KeyFrame.TransitionType.FRAME_TRANSITION_LINEAR, 0.2));
                timeline2.delegateTimelineDelegate = this;
                RotatedCircle rotatedCircle4 = (RotatedCircle)rotatedCircle.Copy();
                _ = rotatedCircle4.AddTimeline(timeline2);
                rotatedCircle4.PlayTimeline(0);
                _ = rotatedCircle.AddTimeline(timeline);
                rotatedCircle.PlayTimeline(0);
                rotatedCircle.Retain();
                rotatedCircles.SetObjectAt(rotatedCircle4, rotatedCircles.GetObjectIndex(rotatedCircle));
                _ = rotatedCircles.AddObject(rotatedCircle);
                rotatedCircle.Release();
            }
            foreach (object obj4 in bungees)
            {
                Grab bungee = (Grab)obj4;
                if (bungee.wheel && PointInRect(tx + camera.pos.x, ty + camera.pos.y, bungee.x - 110f, bungee.y - 110f, 220f, 220f))
                {
                    bungee.HandleWheelTouch(Vect(tx + camera.pos.x, ty + camera.pos.y));
                    bungee.wheelOperating = ti;
                }
                if (bungee.moveLength > 0.0 && PointInRect(tx + camera.pos.x, ty + camera.pos.y, bungee.x - 65f, bungee.y - 65f, 130f, 130f))
                {
                    bungee.moverDragging = ti;
                    return true;
                }
            }
            if (clickToCut && !ignoreTouches)
            {
                Vector s = default;
                Grab grab2 = null;
                Bungee nearestBungeeSegmentByBeziersPointsatXYgrab = GetNearestBungeeSegmentByBeziersPointsatXYgrab(ref s, tx + camera.pos.x, ty + camera.pos.y, ref grab2);
                if (nearestBungeeSegmentByBeziersPointsatXYgrab != null && nearestBungeeSegmentByBeziersPointsatXYgrab.highlighted && GetNearestBungeeSegmentByConstraintsforGrab(ref s, grab2) != null)
                {
                    _ = CutWithRazorOrLine1Line2Immediate(null, s, s, false);
                }
            }
            return true;
        }

        public bool TouchUpXYIndex(float tx, float ty, int ti)
        {
            if (ignoreTouches)
            {
                return true;
            }
            dragging[ti] = false;
            if (ti >= 5)
            {
                return true;
            }
            if (gravityButton != null && gravityTouchDown == ti)
            {
                if (((Button)gravityButton.GetChild(gravityButton.On() ? 1 : 0)).IsInTouchZoneXYforTouchDown(tx + camera.pos.x, ty + camera.pos.y, true))
                {
                    gravityButton.Toggle();
                    OnButtonPressed(0);
                }
                gravityTouchDown = -1;
            }
            foreach (object obj in spikes)
            {
                Spikes spike = (Spikes)obj;
                if (spike.rotateButton != null && spike.touchIndex == ti)
                {
                    spike.touchIndex = -1;
                    if (spike.rotateButton.OnTouchUpXY(tx + camera.pos.x, ty + camera.pos.y))
                    {
                        return true;
                    }
                }
            }
            foreach (object obj2 in rotatedCircles)
            {
                RotatedCircle rotatedCircle = (RotatedCircle)obj2;
                if (rotatedCircle.operating == ti)
                {
                    rotatedCircle.operating = -1;
                    rotatedCircle.soundPlaying = -1;
                    rotatedCircle.SetIsLeftControllerActive(false);
                    rotatedCircle.SetIsRightControllerActive(false);
                }
            }
            foreach (object obj3 in bungees)
            {
                Grab bungee = (Grab)obj3;
                if (bungee.wheel && bungee.wheelOperating == ti)
                {
                    bungee.wheelOperating = -1;
                }
                if (bungee.moveLength > 0.0 && bungee.moverDragging == ti)
                {
                    bungee.moverDragging = -1;
                }
            }
            return true;
        }

        public bool TouchMoveXYIndex(float tx, float ty, int ti)
        {
            if (ignoreTouches)
            {
                return true;
            }
            Vector vector = Vect(tx, ty);
            if (ti >= 5)
            {
                return true;
            }
            foreach (object obj in pumps)
            {
                Pump pump3 = (Pump)obj;
                if (pump3.pumpTouch == ti && pump3.pumpTouchTimer != 0.0 && (double)VectDistance(startPos[ti], vector) > 10.0)
                {
                    pump3.pumpTouchTimer = 0f;
                }
            }
            if (rotatedCircles != null)
            {
                for (int i = 0; i < rotatedCircles.Count(); i++)
                {
                    RotatedCircle rotatedCircle = (RotatedCircle)rotatedCircles[i];
                    if (rotatedCircle != null && rotatedCircle.operating == ti)
                    {
                        Vector v = Vect(rotatedCircle.x, rotatedCircle.y);
                        Vector vector2 = Vect(tx + camera.pos.x, ty + camera.pos.y);
                        Vector v2 = VectSub(rotatedCircle.lastTouch, v);
                        float num = VectAngleNormalized(VectSub(vector2, v)) - VectAngleNormalized(v2);
                        float initial_rotation = DEGREES_TO_RADIANS(rotatedCircle.rotation);
                        rotatedCircle.rotation += RADIANS_TO_DEGREES(num);
                        float a = DEGREES_TO_RADIANS(rotatedCircle.rotation);
                        a = FBOUND_PI(a);
                        rotatedCircle.handle1 = VectRotateAround(rotatedCircle.inithanlde1, (double)a, rotatedCircle.x, rotatedCircle.y);
                        rotatedCircle.handle2 = VectRotateAround(rotatedCircle.inithanlde2, (double)a, rotatedCircle.x, rotatedCircle.y);
                        int num2 = num > 0f ? 46 : 47;
                        if ((double)Math.Abs(num) < 0.07)
                        {
                            num2 = -1;
                        }
                        if (rotatedCircle.soundPlaying != num2 && num2 != -1)
                        {
                            CTRSoundMgr.PlaySound(num2);
                            rotatedCircle.soundPlaying = num2;
                        }
                        for (int j = 0; j < bungees.Count(); j++)
                        {
                            Grab grab = (Grab)bungees[j];
                            if (VectDistance(Vect(grab.x, grab.y), Vect(rotatedCircle.x, rotatedCircle.y)) <= rotatedCircle.sizeInPixels + 5f)
                            {
                                if (grab.initial_rotatedCircle != rotatedCircle)
                                {
                                    grab.initial_x = grab.x;
                                    grab.initial_y = grab.y;
                                    grab.initial_rotatedCircle = rotatedCircle;
                                    grab.initial_rotation = initial_rotation;
                                }
                                float a2 = DEGREES_TO_RADIANS(rotatedCircle.rotation) - grab.initial_rotation;
                                a2 = FBOUND_PI(a2);
                                Vector vector3 = VectRotateAround(Vect(grab.initial_x, grab.initial_y), (double)a2, rotatedCircle.x, rotatedCircle.y);
                                grab.x = vector3.x;
                                grab.y = vector3.y;
                                if (grab.rope != null)
                                {
                                    grab.rope.bungeeAnchor.pos = Vect(grab.x, grab.y);
                                    grab.rope.bungeeAnchor.pin = grab.rope.bungeeAnchor.pos;
                                }
                                if (grab.radius != -1f)
                                {
                                    grab.ReCalcCircle();
                                }
                            }
                        }
                        for (int k = 0; k < pumps.Count(); k++)
                        {
                            Pump pump4 = (Pump)pumps[k];
                            if (VectDistance(Vect(pump4.x, pump4.y), Vect(rotatedCircle.x, rotatedCircle.y)) <= rotatedCircle.sizeInPixels + 5f)
                            {
                                if (pump4.initial_rotatedCircle != rotatedCircle)
                                {
                                    pump4.initial_x = pump4.x;
                                    pump4.initial_y = pump4.y;
                                    pump4.initial_rotatedCircle = rotatedCircle;
                                    pump4.initial_rotation = initial_rotation;
                                }
                                float a3 = DEGREES_TO_RADIANS(rotatedCircle.rotation) - pump4.initial_rotation;
                                a3 = FBOUND_PI(a3);
                                Vector vector4 = VectRotateAround(Vect(pump4.initial_x, pump4.initial_y), (double)a3, rotatedCircle.x, rotatedCircle.y);
                                pump4.x = vector4.x;
                                pump4.y = vector4.y;
                                pump4.rotation += RADIANS_TO_DEGREES(num);
                                pump4.UpdateRotation();
                            }
                        }
                        for (int l = 0; l < bubbles.Count(); l++)
                        {
                            Bubble bubble = (Bubble)bubbles[l];
                            if (VectDistance(Vect(bubble.x, bubble.y), Vect(rotatedCircle.x, rotatedCircle.y)) <= rotatedCircle.sizeInPixels + 10f && bubble != candyBubble && bubble != candyBubbleR && bubble != candyBubbleL)
                            {
                                if (bubble.initial_rotatedCircle != rotatedCircle)
                                {
                                    bubble.initial_x = bubble.x;
                                    bubble.initial_y = bubble.y;
                                    bubble.initial_rotatedCircle = rotatedCircle;
                                    bubble.initial_rotation = initial_rotation;
                                }
                                float a4 = DEGREES_TO_RADIANS(rotatedCircle.rotation) - bubble.initial_rotation;
                                a4 = FBOUND_PI(a4);
                                Vector vector5 = VectRotateAround(Vect(bubble.initial_x, bubble.initial_y), (double)a4, rotatedCircle.x, rotatedCircle.y);
                                bubble.x = vector5.x;
                                bubble.y = vector5.y;
                            }
                        }
                        if (PointInRect(target.x, target.y, rotatedCircle.x - rotatedCircle.size, rotatedCircle.y - rotatedCircle.size, 2f * rotatedCircle.size, 2f * rotatedCircle.size))
                        {
                            Vector vector6 = VectRotateAround(Vect(target.x, target.y), (double)num, rotatedCircle.x, rotatedCircle.y);
                            target.x = vector6.x;
                            target.y = vector6.y;
                        }
                        rotatedCircle.lastTouch = vector2;
                        return true;
                    }
                }
            }
            int num3 = bungees.Count();
            for (int m = 0; m < num3; m++)
            {
                Grab grab2 = (Grab)bungees.ObjectAtIndex(m);
                if (grab2 != null)
                {
                    if (grab2.wheel && grab2.wheelOperating == ti)
                    {
                        grab2.HandleWheelRotate(Vect(tx + camera.pos.x, ty + camera.pos.y));
                        return true;
                    }
                    if (grab2.moveLength > 0.0 && grab2.moverDragging == ti)
                    {
                        if (grab2.moveVertical)
                        {
                            grab2.y = FIT_TO_BOUNDARIES(ty + camera.pos.y, grab2.minMoveValue, grab2.maxMoveValue);
                        }
                        else
                        {
                            grab2.x = FIT_TO_BOUNDARIES(tx + camera.pos.x, grab2.minMoveValue, grab2.maxMoveValue);
                        }
                        if (grab2.rope != null)
                        {
                            grab2.rope.bungeeAnchor.pos = Vect(grab2.x, grab2.y);
                            grab2.rope.bungeeAnchor.pin = grab2.rope.bungeeAnchor.pos;
                        }
                        if (grab2.radius != -1f)
                        {
                            grab2.ReCalcCircle();
                        }
                        return true;
                    }
                }
            }
            if (dragging[ti])
            {
                Vector start = VectAdd(startPos[ti], camera.pos);
                Vector end = VectAdd(Vect(tx, ty), camera.pos);
                FingerCut fingerCut = (FingerCut)new FingerCut().Init();
                fingerCut.start = start;
                fingerCut.end = end;
                fingerCut.startSize = 5f;
                fingerCut.endSize = 5f;
                fingerCut.c = RGBAColor.whiteRGBA;
                _ = fingerCuts[ti].AddObject(fingerCut);
                int num4 = 0;
                foreach (object obj2 in fingerCuts[ti])
                {
                    FingerCut item = (FingerCut)obj2;
                    num4 += CutWithRazorOrLine1Line2Immediate(null, item.start, item.end, false);
                }
                if (num4 > 0)
                {
                    freezeCamera = false;
                    if (ropesCutAtOnce > 0 && ropeAtOnceTimer > 0.0)
                    {
                        ropesCutAtOnce += num4;
                    }
                    else
                    {
                        ropesCutAtOnce = num4;
                    }
                    ropeAtOnceTimer = 0.1f;
                    int num5 = Preferences._getIntForKey("PREFS_ROPES_CUT") + 1;
                    Preferences._setIntforKey(num5, "PREFS_ROPES_CUT", false);
                    if (num5 == 100)
                    {
                        CTRRootController.PostAchievementName("681461850", ACHIEVEMENT_STRING("\"Rope Cutter\""));
                    }
                    if (ropesCutAtOnce is >= 3 and < 5)
                    {
                        CTRRootController.PostAchievementName("681464917", ACHIEVEMENT_STRING("\"Quick Finger\""));
                    }
                    if (ropesCutAtOnce >= 5)
                    {
                        CTRRootController.PostAchievementName("681508316", ACHIEVEMENT_STRING("\"Master Finger\""));
                    }
                    if (num5 == 800)
                    {
                        CTRRootController.PostAchievementName("681457931", ACHIEVEMENT_STRING("\"Rope Cutter Maniac\""));
                    }
                    if (num5 == 2000)
                    {
                        CTRRootController.PostAchievementName("1058248892", ACHIEVEMENT_STRING("\"Ultimate Rope Cutter\""));
                    }
                }
                prevStartPos[ti] = startPos[ti];
                startPos[ti] = vector;
            }
            return true;
        }

        public bool TouchDraggedXYIndex(float tx, float ty, int index)
        {
            if (index > 5)
            {
                return false;
            }
            slastTouch = Vect(tx, ty);
            return true;
        }

        public void OnButtonPressed(int n)
        {
            if (MaterialPoint.globalGravity.y == 784.0)
            {
                MaterialPoint.globalGravity.y = -784f;
                gravityNormal = false;
                CTRSoundMgr.PlaySound(39);
            }
            else
            {
                MaterialPoint.globalGravity.y = 784f;
                gravityNormal = true;
                CTRSoundMgr.PlaySound(38);
            }
            if (earthAnims == null)
            {
                return;
            }
            foreach (object obj in earthAnims)
            {
                Image earthAnim = (Image)obj;
                if (gravityNormal)
                {
                    earthAnim.PlayTimeline(0);
                }
                else
                {
                    earthAnim.PlayTimeline(1);
                }
            }
        }

        public void RotateAllSpikesWithID(int sid)
        {
            foreach (object obj in spikes)
            {
                Spikes spike = (Spikes)obj;
                if (spike.GetToggled() == sid)
                {
                    spike.RotateSpikes();
                }
            }
        }

        public override void Dealloc()
        {
            for (int i = 0; i < 5; i++)
            {
                fingerCuts[i].Release();
            }
            dd.Release();
            camera.Release();
            back.Release();
            base.Dealloc();
        }

        public void FullscreenToggled(bool isFullscreen)
        {
            BaseElement childWithName = staticAniPool.GetChildWithName("levelLabel");
            if (childWithName != null)
            {
                childWithName.x = 15f + Canvas.xOffsetScaled;
            }
            for (int i = 0; i < 3; i++)
            {
                hudStar[i].x = (hudStar[i].width * i) + Canvas.xOffsetScaled;
            }
            if (isFullscreen)
            {
                float num = Global.ScreenSizeManager.ScreenWidth;
                back.scaleX = num / Canvas.backingWidth * 1.25f;
                return;
            }
            back.scaleX = 1.25f;
        }

        private void Selector_gameLost(NSObject param)
        {
            GameLost();
        }

        private void Selector_gameWon(NSObject param)
        {
            CTRSoundMgr.EnableLoopedSounds(false);
            gameSceneDelegate?.GameWon();
        }

        private void Selector_animateLevelRestart(NSObject param)
        {
            AnimateLevelRestart();
        }

        private void Selector_showGreeting(NSObject param)
        {
            ShowGreeting();
        }

        private void Selector_doCandyBlink(NSObject param)
        {
            DoCandyBlink();
        }

        private void Selector_teleport(NSObject param)
        {
            Teleport();
        }

        public static float FBOUND_PI(float a)
        {
            return (float)((double)a > 3.141592653589793 ? (double)a - 6.283185307179586 : (double)a < -3.141592653589793 ? (double)a + 6.283185307179586 : (double)a);
        }

        public const int MAX_TOUCHES = 5;

        public const float DIM_TIMEOUT = 0.15f;

        public const int RESTART_STATE_FADE_IN = 0;

        public const int RESTART_STATE_FADE_OUT = 1;

        public const int S_MOVE_DOWN = 0;

        public const int S_WAIT = 1;

        public const int S_MOVE_UP = 2;

        public const int CAMERA_MOVE_TO_CANDY_PART = 0;

        public const int CAMERA_MOVE_TO_CANDY = 1;

        public const int BUTTON_GRAVITY = 0;

        public const int PARTS_SEPARATE = 0;

        public const int PARTS_DIST = 1;

        public const int PARTS_NONE = 2;

        public const float SCOMBO_TIMEOUT = 0.2f;

        public const int SCUT_SCORE = 10;

        public const int MAX_LOST_CANDIES = 3;

        public const float ROPE_CUT_AT_ONCE_TIMEOUT = 0.1f;

        public const int STAR_RADIUS = 42;

        public const float MOUTH_OPEN_RADIUS = 200f;

        public const int BLINK_SKIP = 3;

        public const float MOUTH_OPEN_TIME = 1f;

        public const float PUMP_TIMEOUT = 0.05f;

        public const int CAMERA_SPEED = 14;

        public const float SOCK_SPEED_K = 0.9f;

        public const int SOCK_COLLISION_Y_OFFSET = 85;

        public const int BUBBLE_RADIUS = 60;

        public const int WHEEL_RADIUS = 110;

        public const int GRAB_MOVE_RADIUS = 65;

        public const int RC_CONTROLLER_RADIUS = 90;

        public const int CANDY_BLINK_INITIAL = 0;

        public const int CANDY_BLINK_STAR = 1;

        public const int TUTORIAL_SHOW_ANIM = 0;

        public const int TUTORIAL_HIDE_ANIM = 1;

        public const int EARTH_NORMAL_ANIM = 0;

        public const int EARTH_UPSIDEDOWN_ANIM = 1;

        private const int CHAR_ANIMATION_IDLE = 0;

        private const int CHAR_ANIMATION_IDLE2 = 1;

        private const int CHAR_ANIMATION_IDLE3 = 2;

        private const int CHAR_ANIMATION_EXCITED = 3;

        private const int CHAR_ANIMATION_PUZZLED = 4;

        private const int CHAR_ANIMATION_FAIL = 5;

        private const int CHAR_ANIMATION_WIN = 6;

        private const int CHAR_ANIMATION_MOUTH_OPEN = 7;

        private const int CHAR_ANIMATION_MOUTH_CLOSE = 8;

        private const int CHAR_ANIMATION_CHEW = 9;

        private const int CHAR_ANIMATION_GREETING = 10;

        private DelayedDispatcher dd;

        public IGameSceneDelegate gameSceneDelegate;

        private AnimationsPool aniPool;

        private AnimationsPool staticAniPool;

        private PollenDrawer pollenDrawer;

        private TileMap back;

        private CharAnimations target;

        private Image support;

        private GameObject candy;

        private GameObject candyMain;

        private GameObject candyTop;

        private Animation candyBlink;

        private Animation candyBubbleAnimation;

        private Animation candyBubbleAnimationL;

        private Animation candyBubbleAnimationR;

        private ConstraintedPoint star;

        private DynamicArray bungees;

        private DynamicArray razors;

        private DynamicArray spikes;

        private DynamicArray stars;

        private DynamicArray bubbles;

        private DynamicArray pumps;

        private DynamicArray socks;

        private DynamicArray bouncers;

        private DynamicArray rotatedCircles;

        private DynamicArray tutorialImages;

        private DynamicArray tutorials;

        private GameObject candyL;

        private GameObject candyR;

        private ConstraintedPoint starL;

        private ConstraintedPoint starR;

        private Animation blink;

        private readonly bool[] dragging = new bool[5];

        private readonly Vector[] startPos = new Vector[5];

        private readonly Vector[] prevStartPos = new Vector[5];

        private float ropePhysicsSpeed;

        private GameObject candyBubble;

        private GameObject candyBubbleL;

        private GameObject candyBubbleR;

        private readonly Animation[] hudStar = new Animation[3];

        private Camera2D camera;

        private float mapWidth;

        private float mapHeight;

        private bool mouthOpen;

        private bool noCandy;

        private int blinkTimer;

        private int idlesTimer;

        private float mouthCloseTimer;

        private float lastCandyRotateDelta;

        private float lastCandyRotateDeltaL;

        private float lastCandyRotateDeltaR;

        private bool spiderTookCandy;

        private int special;

        private bool fastenCamera;

        private float savedSockSpeed;

        private Sock targetSock;

        private int ropesCutAtOnce;

        private float ropeAtOnceTimer;

        private bool clickToCut;

        public int starsCollected;

        public int starBonus;

        public int timeBonus;

        public int score;

        public float time;

        public float initialCameraToStarDistance;

        public float dimTime;

        public int restartState;

        public bool animateRestartDim;

        public bool freezeCamera;

        public int cameraMoveMode;

        public bool ignoreTouches;

        public bool nightLevel;

        public bool gravityNormal;

        public ToggleButton gravityButton;

        public int gravityTouchDown;

        public int twoParts;

        public bool noCandyL;

        public bool noCandyR;

        public float partsDist;

        public DynamicArray earthAnims;

        public int tummyTeasers;

        public Vector slastTouch;

        public DynamicArray[] fingerCuts = new DynamicArray[5];

        private sealed class FingerCut : NSObject
        {
            public Vector start;

            public Vector end;

            public float startSize;

            public float endSize;

            public RGBAColor c;
        }

        private sealed class SCandy : ConstraintedPoint
        {
            public bool good;

            public float speed;

            public float angle;

            public float lastAngleChange;
        }

        private sealed class TutorialText : Text
        {
            public int special;
        }

        private sealed class GameObjectSpecial : CTRGameObject
        {
            private static GameObjectSpecial GameObjectSpecial_create(CTRTexture2D t)
            {
                GameObjectSpecial gameObjectSpecial = new();
                _ = gameObjectSpecial.InitWithTexture(t);
                return gameObjectSpecial;
            }

            public static GameObjectSpecial GameObjectSpecial_createWithResIDQuad(int r, int q)
            {
                GameObjectSpecial gameObjectSpecial = GameObjectSpecial_create(Application.GetTexture(r));
                gameObjectSpecial.SetDrawQuad(q);
                return gameObjectSpecial;
            }

            public int special;
        }
    }
}
