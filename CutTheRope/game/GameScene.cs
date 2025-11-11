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
