using System.Collections.Generic;
using System.Globalization;

using CutTheRope.desktop;
using CutTheRope.iframework;
using CutTheRope.iframework.core;
using CutTheRope.iframework.helpers;
using CutTheRope.iframework.sfe;
using CutTheRope.iframework.visual;
using CutTheRope.ios;

namespace CutTheRope.game
{
    internal sealed partial class GameScene
    {
        public override void Show()
        {
            // Initialize game state and load level data
            InitializeGameState();
            InitializeCandyObjects();
            InitializeHUDStars();

            CTRRootController cTRRootController = (CTRRootController)Application.SharedRootController();
            XMLNode map = cTRRootController.GetMap();

            float num = 3f;
            float num2 = 0f;

            // Load level metadata (map dimensions, game design settings, candy positions)
            LoadAllLevelMetadata(map, num, num2, out float num3, out int num4, out int num5);

            List<XMLNode> list = map.Childs();
            foreach (XMLNode xmlnode2 in list)
            {
                foreach (XMLNode item3 in xmlnode2.Childs())
                {
                    if (item3.Name == "gravitySwitch")
                    {
                        gravityButton = CreateGravityButtonWithDelegate(this);
                        gravityButton.visible = false;
                        gravityButton.touchable = false;
                        _ = AddChild(gravityButton);
                        gravityButton.x = (item3["x"].IntValue() * num) + num3 + num4;
                        gravityButton.y = (item3["y"].IntValue() * num) + num2 + num5;
                        gravityButton.anchor = 18;
                    }
                    else if (item3.Name == "star")
                    {
                        LoadStar(item3, num, num3 + num4, num2 + num5, 0, 0);
                    }
                    else if (item3.Name == "tutorialText")
                    {
                        LoadTutorialText(item3, num, num3 + num4, num2 + num5, 0, 0);
                    }
                    else if (item3.Name is "tutorial01" or "tutorial02" or "tutorial03" or "tutorial04" or "tutorial05" or "tutorial06" or "tutorial07" or "tutorial08" or "tutorial09" or "tutorial10" or "tutorial11")
                    {
                        LoadTutorialImage(item3, num, num3 + num4, num2 + num5, 0, 0);
                    }
                    else if (item3.Name == "bubble")
                    {
                        LoadBubble(item3, num, num3 + num4, num2 + num5, 0, 0);
                    }
                    else if (item3.Name == "pump")
                    {
                        LoadPump(item3, num, num3 + num4, num2 + num5, 0, 0);
                    }
                    else if (item3.Name == "sock")
                    {
                        LoadSock(item3, num, num3 + num4, num2 + num5, 0, 0);
                    }
                    else if (item3.Name is "spike1" or "spike2" or "spike3" or "spike4" or "electro")
                    {
                        LoadSpike(item3, num, num3 + num4, num2 + num5, 0, 0);
                    }
                    else if (item3.Name == "rotatedCircle")
                    {
                        LoadRotatedCircle(item3, num, num3 + num4, num2 + num5, 0, 0);
                    }
                    else if (item3.Name is "bouncer1" or "bouncer2")
                    {
                        LoadBouncer(item3, num, num3 + num4, num2 + num5, 0, 0);
                    }
                    else if (item3.Name == "grab")
                    {
                        LoadGrab(item3, num, num3 + num4, num2 + num5, 0, 0);
                    }
                    else if (item3.Name == "target")
                    {
                        int pack = ((CTRRootController)Application.SharedRootController()).GetPack();
                        support = Image.Image_createWithResIDQuad(100, pack);
                        support.Retain();
                        support.DoRestoreCutTransparency();
                        support.anchor = 18;
                        target = CharAnimations.CharAnimations_createWithResID(80);
                        target.DoRestoreCutTransparency();
                        target.passColorToChilds = false;
                        NSString nSString3 = item3["x"];
                        target.x = support.x = (nSString3.IntValue() * num) + num3 + num4;
                        NSString nSString4 = item3["y"];
                        target.y = support.y = (nSString4.IntValue() * num) + num2 + num5;
                        target.AddImage(101);
                        target.AddImage(102);
                        target.bb = MakeRectangle(264.0, 350.0, 108.0, 2.0);
                        target.AddAnimationWithIDDelayLoopFirstLast(0, 0.05f, Timeline.LoopType.TIMELINE_REPLAY, 0, 18);
                        target.AddAnimationWithIDDelayLoopFirstLast(1, 0.05f, Timeline.LoopType.TIMELINE_NO_LOOP, 43, 67);
                        int num14 = 68;
                        target.AddAnimationWithIDDelayLoopCountSequence(2, 0.05f, Timeline.LoopType.TIMELINE_NO_LOOP, 32, num14,
                        [
                            num14 + 1,
                            num14 + 2,
                            num14 + 3,
                            num14 + 4,
                            num14 + 5,
                            num14 + 6,
                            num14 + 7,
                            num14 + 8,
                            num14 + 9,
                            num14 + 10,
                            num14 + 11,
                            num14 + 12,
                            num14 + 13,
                            num14 + 14,
                            num14 + 15,
                            num14,
                            num14 + 1,
                            num14 + 2,
                            num14 + 3,
                            num14 + 4,
                            num14 + 5,
                            num14 + 6,
                            num14 + 7,
                            num14 + 8,
                            num14 + 9,
                            num14 + 10,
                            num14 + 11,
                            num14 + 12,
                            num14 + 13,
                            num14 + 14,
                            num14 + 15
                        ]);
                        target.AddAnimationWithIDDelayLoopFirstLast(7, 0.05f, Timeline.LoopType.TIMELINE_NO_LOOP, 19, 27);
                        target.AddAnimationWithIDDelayLoopFirstLast(8, 0.05f, Timeline.LoopType.TIMELINE_NO_LOOP, 28, 31);
                        target.AddAnimationWithIDDelayLoopFirstLast(9, 0.05f, Timeline.LoopType.TIMELINE_REPLAY, 32, 40);
                        target.AddAnimationWithIDDelayLoopFirstLast(6, 0.05f, Timeline.LoopType.TIMELINE_NO_LOOP, 28, 31);
                        target.AddAnimationWithIDDelayLoopFirstLast(101, 10, 0.05f, Timeline.LoopType.TIMELINE_NO_LOOP, 47, 76);
                        target.AddAnimationWithIDDelayLoopFirstLast(101, 3, 0.05f, Timeline.LoopType.TIMELINE_NO_LOOP, 0, 19);
                        target.AddAnimationWithIDDelayLoopFirstLast(101, 4, 0.05f, Timeline.LoopType.TIMELINE_NO_LOOP, 20, 46);
                        target.AddAnimationWithIDDelayLoopFirstLast(102, 5, 0.05f, Timeline.LoopType.TIMELINE_NO_LOOP, 0, 12);
                        target.SwitchToAnimationatEndOfAnimationDelay(9, 6, 0.05f);
                        target.SwitchToAnimationatEndOfAnimationDelay(101, 4, 80, 8, 0.05f);
                        target.SwitchToAnimationatEndOfAnimationDelay(80, 0, 101, 10, 0.05f);
                        target.SwitchToAnimationatEndOfAnimationDelay(80, 0, 80, 1, 0.05f);
                        target.SwitchToAnimationatEndOfAnimationDelay(80, 0, 80, 2, 0.05f);
                        target.SwitchToAnimationatEndOfAnimationDelay(80, 0, 101, 3, 0.05f);
                        target.SwitchToAnimationatEndOfAnimationDelay(80, 0, 101, 4, 0.05f);
                        target.Retain();
                        if (CTRRootController.IsShowGreeting())
                        {
                            dd.CallObjectSelectorParamafterDelay(new DelayedDispatcher.DispatchFunc(Selector_showGreeting), null, 1.3f);
                            CTRRootController.SetShowGreeting(false);
                        }
                        target.PlayTimeline(0);
                        target.GetTimeline(0).delegateTimelineDelegate = this;
                        target.SetPauseAtIndexforAnimation(8, 7);
                        blink = Animation.Animation_createWithResID(80);
                        blink.parentAnchor = 9;
                        blink.visible = false;
                        blink.AddAnimationWithIDDelayLoopCountSequence(0, 0.05f, Timeline.LoopType.TIMELINE_NO_LOOP, 4, 41, [41, 42, 42, 42]);
                        blink.SetActionTargetParamSubParamAtIndexforAnimation("ACTION_SET_VISIBLE", blink, 0, 0, 2, 0);
                        blinkTimer = 3;
                        blink.DoRestoreCutTransparency();
                        _ = target.AddChild(blink);
                        idlesTimer = RND_RANGE(5, 20);
                    }
                }
            }
            if (twoParts != 2)
            {
                candyBubbleAnimationL = Animation.Animation_createWithResID(72);
                candyBubbleAnimationL.parentAnchor = candyBubbleAnimationL.anchor = 18;
                _ = candyBubbleAnimationL.AddAnimationDelayLoopFirstLast(0.05, Timeline.LoopType.TIMELINE_REPLAY, 0, 12);
                candyBubbleAnimationL.PlayTimeline(0);
                _ = candyL.AddChild(candyBubbleAnimationL);
                candyBubbleAnimationL.visible = false;
                candyBubbleAnimationR = Animation.Animation_createWithResID(72);
                candyBubbleAnimationR.parentAnchor = candyBubbleAnimationR.anchor = 18;
                _ = candyBubbleAnimationR.AddAnimationDelayLoopFirstLast(0.05, Timeline.LoopType.TIMELINE_REPLAY, 0, 12);
                candyBubbleAnimationR.PlayTimeline(0);
                _ = candyR.AddChild(candyBubbleAnimationR);
                candyBubbleAnimationR.visible = false;
            }
            foreach (object obj in rotatedCircles)
            {
                RotatedCircle rotatedCircle2 = (RotatedCircle)obj;
                rotatedCircle2.operating = -1;
                rotatedCircle2.circlesArray = rotatedCircles;
            }
            StartCamera();
            tummyTeasers = 0;
            starsCollected = 0;
            candyBubble = null;
            candyBubbleL = null;
            candyBubbleR = null;
            mouthOpen = false;
            noCandy = twoParts != 2;
            noCandyL = false;
            noCandyR = false;
            blink.PlayTimeline(0);
            spiderTookCandy = false;
            time = 0f;
            score = 0;
            gravityNormal = true;
            MaterialPoint.globalGravity = Vect(0f, 784f);
            dimTime = 0f;
            ropesCutAtOnce = 0;
            ropeAtOnceTimer = 0f;
            dd.CallObjectSelectorParamafterDelay(new DelayedDispatcher.DispatchFunc(Selector_doCandyBlink), null, 1.0);
            Text text = Text.CreateWithFontandString(3, (cTRRootController.GetPack() + 1).ToString(CultureInfo.InvariantCulture) + " - " + (cTRRootController.GetLevel() + 1).ToString(CultureInfo.InvariantCulture));
            text.anchor = 33;
            Text text2 = Text.CreateWithFontandString(3, Application.GetString(655376));
            text2.anchor = 33;
            text2.parentAnchor = 9;
            text.SetName("levelLabel");
            text.x = 15f + Canvas.xOffsetScaled;
            text.y = SCREEN_HEIGHT + 15f;
            text2.y = 60f;
            text2.rotationCenterX -= text2.width / 2f;
            text2.scaleX = text2.scaleY = 0.7f;
            _ = text.AddChild(text2);
            Timeline timeline6 = new Timeline().InitWithMaxKeyFramesOnTrack(5);
            timeline6.AddKeyFrame(KeyFrame.MakeColor(RGBAColor.transparentRGBA, KeyFrame.TransitionType.FRAME_TRANSITION_LINEAR, 0.0));
            timeline6.AddKeyFrame(KeyFrame.MakeColor(RGBAColor.transparentRGBA, KeyFrame.TransitionType.FRAME_TRANSITION_LINEAR, 0.5));
            timeline6.AddKeyFrame(KeyFrame.MakeColor(RGBAColor.solidOpaqueRGBA, KeyFrame.TransitionType.FRAME_TRANSITION_LINEAR, 0.5));
            timeline6.AddKeyFrame(KeyFrame.MakeColor(RGBAColor.solidOpaqueRGBA, KeyFrame.TransitionType.FRAME_TRANSITION_LINEAR, 1.0));
            timeline6.AddKeyFrame(KeyFrame.MakeColor(RGBAColor.transparentRGBA, KeyFrame.TransitionType.FRAME_TRANSITION_LINEAR, 0.5));
            text.AddTimelinewithID(timeline6, 0);
            text.PlayTimeline(0);
            timeline6.delegateTimelineDelegate = staticAniPool;
            _ = staticAniPool.AddChild(text);
            for (int m = 0; m < 5; m++)
            {
                dragging[m] = false;
                startPos[m] = prevStartPos[m] = vectZero;
            }
            if (clickToCut)
            {
                ResetBungeeHighlight();
            }
            Global.MouseCursor.ReleaseButtons();
            CTRRootController.LogEvent("IG_SHOWN");
        }

        public void StartCamera()
        {
            if (mapWidth > SCREEN_WIDTH || mapHeight > SCREEN_HEIGHT)
            {
                ignoreTouches = true;
                fastenCamera = false;
                camera.type = CAMERATYPE.CAMERASPEEDPIXELS;
                camera.speed = 20f;
                cameraMoveMode = 0;
                ConstraintedPoint constraintedPoint = twoParts != 2 ? starL : star;
                float num;
                float num2;
                if (mapWidth > SCREEN_WIDTH)
                {
                    if (constraintedPoint.pos.x > mapWidth / 2.0)
                    {
                        num = 0f;
                        num2 = 0f;
                    }
                    else
                    {
                        num = mapWidth - SCREEN_WIDTH;
                        num2 = 0f;
                    }
                }
                else if (constraintedPoint.pos.y > mapHeight / 2.0)
                {
                    num = 0f;
                    num2 = 0f;
                }
                else
                {
                    num = 0f;
                    num2 = mapHeight - SCREEN_HEIGHT;
                }
                double num6 = (double)(constraintedPoint.pos.x - (SCREEN_WIDTH / 2f));
                float num3 = constraintedPoint.pos.y - (SCREEN_HEIGHT / 2f);
                float num4 = FIT_TO_BOUNDARIES(num6, 0.0, (double)(mapWidth - SCREEN_WIDTH));
                float num5 = FIT_TO_BOUNDARIES((double)num3, 0.0, (double)(mapHeight - SCREEN_HEIGHT));
                camera.MoveToXYImmediate(num, num2, true);
                initialCameraToStarDistance = VectDistance(camera.pos, Vect(num4, num5));
                return;
            }
            ignoreTouches = false;
            camera.MoveToXYImmediate(0f, 0f, true);
        }

        public void DoCandyBlink()
        {
            candyBlink.PlayTimeline(0);
        }

        public void TimelinereachedKeyFramewithIndex(Timeline t, KeyFrame k, int i)
        {
            if (rotatedCircles.GetObjectIndex(t.element) != -1 || i != 1)
            {
                return;
            }
            blinkTimer--;
            if (blinkTimer == 0)
            {
                blink.visible = true;
                blink.PlayTimeline(0);
                blinkTimer = 3;
            }
            idlesTimer--;
            if (idlesTimer == 0)
            {
                if (RND_RANGE(0, 1) == 1)
                {
                    target.PlayTimeline(1);
                }
                else
                {
                    target.PlayTimeline(2);
                }
                idlesTimer = RND_RANGE(5, 20);
            }
        }

        public void TimelineFinished(Timeline t)
        {
            if (rotatedCircles.GetObjectIndex(t.element) != -1)
            {
                ((RotatedCircle)t.element).removeOnNextUpdate = true;
            }
            foreach (object obj in tutorials)
            {
            }
        }
    }
}
