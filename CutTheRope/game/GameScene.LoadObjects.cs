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
    /// <summary>
    /// GameScene.LoadObjects - Partial class containing all object loading logic
    /// Extracted from Show() method to improve maintainability
    /// </summary>
    internal sealed partial class GameScene
    {
        /// <summary>
        /// Loads all game objects from XML map data
        /// Coordinates loading of stars, bubbles, pumps, spikes, etc.
        /// </summary>
        private void LoadGameObjects(XMLNode map, float scale, float offsetX, float offsetY, int mapOffsetX, int mapOffsetY)
        {
            List<XMLNode> list = map.Childs();
            foreach (XMLNode xmlnode2 in list)
            {
                foreach (XMLNode item3 in xmlnode2.Childs())
                {
                    LoadGravitySwitch(item3, scale, offsetX, offsetY, mapOffsetX, mapOffsetY);
                    LoadStar(item3, scale, offsetX, offsetY, mapOffsetX, mapOffsetY);
                    LoadTutorialText(item3, scale, offsetX, offsetY, mapOffsetX, mapOffsetY);
                    LoadTutorialImage(item3, scale, offsetX, offsetY, mapOffsetX, mapOffsetY);
                    LoadBubble(item3, scale, offsetX, offsetY, mapOffsetX, mapOffsetY);
                    LoadPump(item3, scale, offsetX, offsetY, mapOffsetX, mapOffsetY);
                    LoadSock(item3, scale, offsetX, offsetY, mapOffsetX, mapOffsetY);
                    LoadSpikes(item3, scale, offsetX, offsetY, mapOffsetX, mapOffsetY);
                    LoadRotatedCircle(item3, scale, offsetX, offsetY, mapOffsetX, mapOffsetY);
                    LoadBouncer(item3, scale, offsetX, offsetY, mapOffsetX, mapOffsetY);
                    LoadGrab(item3, scale, offsetX, offsetY, mapOffsetX, mapOffsetY);
                    LoadTarget(item3, scale, offsetX, offsetY, mapOffsetX, mapOffsetY);
                }
            }
        }

        private void LoadGravitySwitch(XMLNode item, float scale, float offsetX, float offsetY, int mapOffsetX, int mapOffsetY)
        {
            if (item.Name != "gravitySwitch") return;

            gravityButton = CreateGravityButtonWithDelegate(this);
            gravityButton.visible = false;
            gravityButton.touchable = false;
            _ = AddChild(gravityButton);
            gravityButton.x = (item["x"].IntValue() * scale) + offsetX + mapOffsetX;
            gravityButton.y = (item["y"].IntValue() * scale) + offsetY + mapOffsetY;
            gravityButton.anchor = 18;
        }

        private void LoadStar(XMLNode item, float scale, float offsetX, float offsetY, int mapOffsetX, int mapOffsetY)
        {
            if (item.Name != "star") return;

            Star star = Star.Star_createWithResID(78);
            star.x = (item["x"].IntValue() * scale) + offsetX + mapOffsetX;
            star.y = (item["y"].IntValue() * scale) + offsetY + mapOffsetY;
            star.timeout = item["timeout"].FloatValue();
            star.CreateAnimations();
            star.bb = MakeRectangle(70.0, 64.0, 82.0, 82.0);
            star.ParseMover(item);
            star.Update(0f);
            _ = stars.AddObject(star);
        }

        private void LoadTutorialText(XMLNode item, float scale, float offsetX, float offsetY, int mapOffsetX, int mapOffsetY)
        {
            if (item.Name != "tutorialText") return;
            if (ShouldSkipTutorialElement(item)) return;

            TutorialText tutorialText = (TutorialText)new TutorialText().InitWithFont(Application.GetFont(4));
            tutorialText.color = RGBAColor.MakeRGBA(1.0, 1.0, 1.0, 0.9);
            tutorialText.x = (item["x"].IntValue() * scale) + offsetX + mapOffsetX;
            tutorialText.y = (item["y"].IntValue() * scale) + offsetY + mapOffsetY;
            tutorialText.special = item["special"].IntValue();
            tutorialText.SetAlignment(2);
            NSString newString = item["text"];
            tutorialText.SetStringandWidth(newString, item["width"].IntValue() * (int)scale);
            tutorialText.color = RGBAColor.transparentRGBA;

            float num6 = tutorialText.special == 3 ? 12f : 0f;
            Timeline timeline3 = new Timeline().InitWithMaxKeyFramesOnTrack(4);
            timeline3.AddKeyFrame(KeyFrame.MakeColor(RGBAColor.transparentRGBA, KeyFrame.TransitionType.FRAME_TRANSITION_LINEAR, num6));
            timeline3.AddKeyFrame(KeyFrame.MakeColor(RGBAColor.solidOpaqueRGBA, KeyFrame.TransitionType.FRAME_TRANSITION_LINEAR, 1.0));

            CTRRootController cTRRootController = (CTRRootController)Application.SharedRootController();
            if (cTRRootController.GetPack() == 0 && cTRRootController.GetLevel() == 0)
            {
                timeline3.AddKeyFrame(KeyFrame.MakeColor(RGBAColor.solidOpaqueRGBA, KeyFrame.TransitionType.FRAME_TRANSITION_LINEAR, 10.0));
            }
            else
            {
                timeline3.AddKeyFrame(KeyFrame.MakeColor(RGBAColor.solidOpaqueRGBA, KeyFrame.TransitionType.FRAME_TRANSITION_LINEAR, 5.0));
            }
            timeline3.AddKeyFrame(KeyFrame.MakeColor(RGBAColor.transparentRGBA, KeyFrame.TransitionType.FRAME_TRANSITION_LINEAR, 0.5));
            tutorialText.AddTimelinewithID(timeline3, 0);
            if (tutorialText.special is 0 or 3)
            {
                tutorialText.PlayTimeline(0);
            }
            _ = tutorials.AddObject(tutorialText);
        }

        private void LoadTutorialImage(XMLNode item, float scale, float offsetX, float offsetY, int mapOffsetX, int mapOffsetY)
        {
            if (!item.Name.StartsWith("tutorial", StringComparison.Ordinal)) return;
            if (ShouldSkipTutorialElement(item)) return;

            int q = new NSString(item.Name[8..]).IntValue() - 1;
            GameObjectSpecial gameObjectSpecial = GameObjectSpecial.GameObjectSpecial_createWithResIDQuad(84, q);
            gameObjectSpecial.color = RGBAColor.transparentRGBA;
            gameObjectSpecial.x = (item["x"].IntValue() * scale) + offsetX + mapOffsetX;
            gameObjectSpecial.y = (item["y"].IntValue() * scale) + offsetY + mapOffsetY;
            gameObjectSpecial.rotation = item["angle"].IntValue();
            gameObjectSpecial.special = item["special"].IntValue();
            gameObjectSpecial.ParseMover(item);

            float num7 = gameObjectSpecial.special is 3 or 4 ? 12f : 0f;
            Timeline timeline4 = new Timeline().InitWithMaxKeyFramesOnTrack(4);
            timeline4.AddKeyFrame(KeyFrame.MakeColor(RGBAColor.transparentRGBA, KeyFrame.TransitionType.FRAME_TRANSITION_LINEAR, num7));
            timeline4.AddKeyFrame(KeyFrame.MakeColor(RGBAColor.solidOpaqueRGBA, KeyFrame.TransitionType.FRAME_TRANSITION_LINEAR, 1.0));

            CTRRootController cTRRootController = (CTRRootController)Application.SharedRootController();
            if (cTRRootController.GetPack() == 0 && cTRRootController.GetLevel() == 0)
            {
                timeline4.AddKeyFrame(KeyFrame.MakeColor(RGBAColor.solidOpaqueRGBA, KeyFrame.TransitionType.FRAME_TRANSITION_LINEAR, 10.0));
            }
            else
            {
                timeline4.AddKeyFrame(KeyFrame.MakeColor(RGBAColor.solidOpaqueRGBA, KeyFrame.TransitionType.FRAME_TRANSITION_LINEAR, 5.2));
            }
            timeline4.AddKeyFrame(KeyFrame.MakeColor(RGBAColor.transparentRGBA, KeyFrame.TransitionType.FRAME_TRANSITION_LINEAR, 0.5));
            gameObjectSpecial.AddTimelinewithID(timeline4, 0);
            if (gameObjectSpecial.special is 0 or 3)
            {
                gameObjectSpecial.PlayTimeline(0);
            }
            if (gameObjectSpecial.special is 2 or 4)
            {
                Timeline timeline5 = new Timeline().InitWithMaxKeyFramesOnTrack(12);
                for (int j = 0; j < 2; j++)
                {
                    timeline5.AddKeyFrame(KeyFrame.MakeColor(RGBAColor.transparentRGBA, KeyFrame.TransitionType.FRAME_TRANSITION_IMMEDIATE, j == 1 ? 0f : num7));
                    timeline5.AddKeyFrame(KeyFrame.MakeColor(RGBAColor.solidOpaqueRGBA, KeyFrame.TransitionType.FRAME_TRANSITION_LINEAR, 0.5));
                    timeline5.AddKeyFrame(KeyFrame.MakeColor(RGBAColor.solidOpaqueRGBA, KeyFrame.TransitionType.FRAME_TRANSITION_LINEAR, 1.0));
                    timeline5.AddKeyFrame(KeyFrame.MakeColor(RGBAColor.solidOpaqueRGBA, KeyFrame.TransitionType.FRAME_TRANSITION_LINEAR, 1.1));
                    timeline5.AddKeyFrame(KeyFrame.MakeColor(RGBAColor.transparentRGBA, KeyFrame.TransitionType.FRAME_TRANSITION_LINEAR, 0.5));
                    timeline5.AddKeyFrame(KeyFrame.MakePos(gameObjectSpecial.x, gameObjectSpecial.y, KeyFrame.TransitionType.FRAME_TRANSITION_IMMEDIATE, (double)(j == 1 ? 0f : num7)));
                    timeline5.AddKeyFrame(KeyFrame.MakePos(gameObjectSpecial.x, gameObjectSpecial.y, KeyFrame.TransitionType.FRAME_TRANSITION_LINEAR, 0.5));
                    timeline5.AddKeyFrame(KeyFrame.MakePos(gameObjectSpecial.x, gameObjectSpecial.y, KeyFrame.TransitionType.FRAME_TRANSITION_LINEAR, 1.0));
                    timeline5.AddKeyFrame(KeyFrame.MakePos(gameObjectSpecial.x + 230.0, gameObjectSpecial.y, KeyFrame.TransitionType.FRAME_TRANSITION_EASE_IN, 0.5));
                    timeline5.AddKeyFrame(KeyFrame.MakePos(gameObjectSpecial.x + 440.0, gameObjectSpecial.y, KeyFrame.TransitionType.FRAME_TRANSITION_EASE_OUT, 0.5));
                    timeline5.AddKeyFrame(KeyFrame.MakePos(gameObjectSpecial.x + 440.0, gameObjectSpecial.y, KeyFrame.TransitionType.FRAME_TRANSITION_LINEAR, 0.6));
                }
                timeline5.SetTimelineLoopType(Timeline.LoopType.TIMELINE_NO_LOOP);
                gameObjectSpecial.AddTimelinewithID(timeline5, 1);
                gameObjectSpecial.PlayTimeline(1);
                gameObjectSpecial.rotation = 10f;
            }
            _ = tutorialImages.AddObject(gameObjectSpecial);
        }

        private void LoadBubble(XMLNode item, float scale, float offsetX, float offsetY, int mapOffsetX, int mapOffsetY)
        {
            if (item.Name != "bubble") return;

            int q2 = RND_RANGE(1, 3);
            Bubble bubble = Bubble.Bubble_createWithResIDQuad(75, q2);
            bubble.DoRestoreCutTransparency();
            bubble.bb = MakeRectangle(48.0, 48.0, 152.0, 152.0);
            bubble.initial_x = bubble.x = (item["x"].IntValue() * scale) + offsetX + mapOffsetX;
            bubble.initial_y = bubble.y = (item["y"].IntValue() * scale) + offsetY + mapOffsetY;
            bubble.initial_rotation = 0f;
            bubble.initial_rotatedCircle = null;
            bubble.anchor = 18;
            bubble.popped = false;
            Image image = Image.Image_createWithResIDQuad(75, 0);
            image.DoRestoreCutTransparency();
            image.parentAnchor = image.anchor = 18;
            _ = bubble.AddChild(image);
            _ = bubbles.AddObject(bubble);
        }

        private void LoadPump(XMLNode item, float scale, float offsetX, float offsetY, int mapOffsetX, int mapOffsetY)
        {
            if (item.Name != "pump") return;

            Pump pump = Pump.Pump_createWithResID(83);
            pump.DoRestoreCutTransparency();
            _ = pump.AddAnimationWithDelayLoopedCountSequence(0.05f, Timeline.LoopType.TIMELINE_NO_LOOP, 4, 1, [2, 3, 0]);
            pump.bb = MakeRectangle(300f, 300f, 175f, 175f);
            pump.initial_x = pump.x = (item["x"].IntValue() * scale) + offsetX + mapOffsetX;
            pump.initial_y = pump.y = (item["y"].IntValue() * scale) + offsetY + mapOffsetY;
            pump.initial_rotation = 0f;
            pump.initial_rotatedCircle = null;
            pump.rotation = item["angle"].FloatValue() + 90f;
            pump.UpdateRotation();
            pump.anchor = 18;
            _ = pumps.AddObject(pump);
        }

        private void LoadSock(XMLNode item, float scale, float offsetX, float offsetY, int mapOffsetX, int mapOffsetY)
        {
            if (item.Name != "sock") return;

            Sock sock = Sock.Sock_createWithResID(85);
            sock.CreateAnimations();
            sock.scaleX = sock.scaleY = 0.7f;
            sock.DoRestoreCutTransparency();
            sock.x = (item["x"].IntValue() * scale) + offsetX + mapOffsetX;
            sock.y = (item["y"].IntValue() * scale) + offsetY + mapOffsetY;
            sock.group = item["group"].IntValue();
            sock.anchor = 10;
            sock.rotationCenterY -= (sock.height / 2f) - 85f;
            if (sock.group == 0)
            {
                sock.SetDrawQuad(0);
            }
            else
            {
                sock.SetDrawQuad(1);
            }
            sock.state = Sock.SOCK_IDLE;
            sock.ParseMover(item);
            sock.rotation += 90f;
            if (sock.mover != null)
            {
                sock.mover.angle_ += 90.0;
                sock.mover.angle_initial = sock.mover.angle_;
                CTRRootController cTRRootController = (CTRRootController)Application.SharedRootController();
                if (cTRRootController.GetPack() == 3 && cTRRootController.GetLevel() == 24)
                {
                    sock.mover.use_angle_initial = true;
                }
            }
            sock.UpdateRotation();
            _ = socks.AddObject(sock);
        }

        private void LoadSpikes(XMLNode item, float scale, float offsetX, float offsetY, int mapOffsetX, int mapOffsetY)
        {
            if (!item.Name.Contains("spike") && item.Name != "electro") return;

            float px = (item["x"].IntValue() * scale) + offsetX + mapOffsetX;
            float py = (item["y"].IntValue() * scale) + offsetY + mapOffsetY;
            int w = item["size"].IntValue();
            double an = item["angle"].IntValue();
            NSString nSString2 = item["toggled"];
            int num8 = -1;
            if (nSString2.Length() > 0)
            {
                num8 = nSString2.IsEqualToString("false") ? -1 : nSString2.IntValue();
            }
            Spikes spikes = (Spikes)new Spikes().InitWithPosXYWidthAndAngleToggled(px, py, w, an, num8);
            spikes.ParseMover(item);
            if (num8 != 0)
            {
                spikes.delegateRotateAllSpikesWithID = new Spikes.rotateAllSpikesWithID(RotateAllSpikesWithID);
            }
            if (item.Name == "electro")
            {
                spikes.electro = true;
                spikes.initialDelay = item["initialDelay"].FloatValue();
                spikes.onTime = item["onTime"].FloatValue();
                spikes.offTime = item["offTime"].FloatValue();
                spikes.electroTimer = 0f;
                spikes.TurnElectroOff();
                spikes.electroTimer += spikes.initialDelay;
                spikes.UpdateRotation();
            }
            else
            {
                spikes.electro = false;
            }
            _ = this.spikes.AddObject(spikes);
        }

        private void LoadRotatedCircle(XMLNode item, float scale, float offsetX, float offsetY, int mapOffsetX, int mapOffsetY)
        {
            if (item.Name != "rotatedCircle") return;

            float num9 = (item["x"].IntValue() * scale) + offsetX + mapOffsetX;
            float num10 = (item["y"].IntValue() * scale) + offsetY + mapOffsetY;
            float num11 = item["size"].IntValue();
            float d = item["handleAngle"].IntValue();
            bool hasOneHandle = item["oneHandle"].BoolValue();
            RotatedCircle rotatedCircle = (RotatedCircle)new RotatedCircle().Init();
            rotatedCircle.anchor = 18;
            rotatedCircle.x = num9;
            rotatedCircle.y = num10;
            rotatedCircle.rotation = d;
            rotatedCircle.inithanlde1 = rotatedCircle.handle1 = Vect(rotatedCircle.x - (num11 * scale), rotatedCircle.y);
            rotatedCircle.inithanlde2 = rotatedCircle.handle2 = Vect(rotatedCircle.x + (num11 * scale), rotatedCircle.y);
            rotatedCircle.handle1 = VectRotateAround(rotatedCircle.handle1, (double)DEGREES_TO_RADIANS(d), rotatedCircle.x, rotatedCircle.y);
            rotatedCircle.handle2 = VectRotateAround(rotatedCircle.handle2, (double)DEGREES_TO_RADIANS(d), rotatedCircle.x, rotatedCircle.y);
            rotatedCircle.SetSize(num11);
            rotatedCircle.SetHasOneHandle(hasOneHandle);
            _ = rotatedCircles.AddObject(rotatedCircle);
        }

        private void LoadBouncer(XMLNode item, float scale, float offsetX, float offsetY, int mapOffsetX, int mapOffsetY)
        {
            if (!item.Name.Contains("bouncer")) return;

            float px2 = (item["x"].IntValue() * scale) + offsetX + mapOffsetX;
            float py2 = (item["y"].IntValue() * scale) + offsetY + mapOffsetY;
            int w2 = item["size"].IntValue();
            double an2 = item["angle"].IntValue();
            Bouncer bouncer = (Bouncer)new Bouncer().InitWithPosXYWidthAndAngle(px2, py2, w2, an2);
            bouncer.ParseMover(item);
            _ = bouncers.AddObject(bouncer);
        }

        private void LoadGrab(XMLNode item, float scale, float offsetX, float offsetY, int mapOffsetX, int mapOffsetY)
        {
            if (item.Name != "grab") return;

            float hx = (item["x"].IntValue() * scale) + offsetX + mapOffsetX;
            float hy = (item["y"].IntValue() * scale) + offsetY + mapOffsetY;
            float len = item["length"].IntValue() * scale;
            float num12 = item["radius"].FloatValue();
            bool wheel = item["wheel"].IsEqualToString("true");
            float k = item["moveLength"].FloatValue() * scale;
            bool v = item["moveVertical"].IsEqualToString("true");
            float o = item["moveOffset"].FloatValue() * scale;
            bool spider = item["spider"].IsEqualToString("true");
            bool flag = item["part"].IsEqualToString("L");
            bool flag2 = item["hidePath"].IsEqualToString("true");
            Grab grab = (Grab)new Grab().Init();
            grab.initial_x = grab.x = hx;
            grab.initial_y = grab.y = hy;
            grab.initial_rotation = 0f;
            grab.wheel = wheel;
            grab.SetSpider(spider);
            grab.ParseMover(item);
            if (grab.mover != null)
            {
                grab.SetBee();
                if (!flag2)
                {
                    int num13 = 3;
                    bool flag3 = item["path"].HasPrefix(NSS("R"));
                    for (int l = 0; l < grab.mover.pathLen - 1; l++)
                    {
                        if (!flag3 || l % num13 == 0)
                        {
                            pollenDrawer.FillWithPolenFromPathIndexToPathIndexGrab(l, l + 1, grab);
                        }
                    }
                    if (grab.mover.pathLen > 2)
                    {
                        pollenDrawer.FillWithPolenFromPathIndexToPathIndexGrab(0, grab.mover.pathLen - 1, grab);
                    }
                }
            }
            if (num12 != -1f)
            {
                num12 *= scale;
            }
            if (num12 == -1f)
            {
                ConstraintedPoint constraintedPoint = star;
                if (twoParts != 2)
                {
                    constraintedPoint = flag ? starL : starR;
                }
                Bungee bungee = (Bungee)new Bungee().InitWithHeadAtXYTailAtTXTYandLength(null, hx, hy, constraintedPoint, constraintedPoint.pos.x, constraintedPoint.pos.y, len);
                bungee.bungeeAnchor.pin = bungee.bungeeAnchor.pos;
                grab.SetRope(bungee);
            }
            grab.SetRadius(num12);
            grab.SetMoveLengthVerticalOffset(k, v, o);
            _ = bungees.AddObject(grab);
        }

        private void LoadTarget(XMLNode item, float scale, float offsetX, float offsetY, int mapOffsetX, int mapOffsetY)
        {
            if (item.Name != "target") return;

            int pack = ((CTRRootController)Application.SharedRootController()).GetPack();
            support = Image.Image_createWithResIDQuad(100, pack);
            support.Retain();
            support.DoRestoreCutTransparency();
            support.anchor = 18;
            target = CharAnimations.CharAnimations_createWithResID(80);
            target.DoRestoreCutTransparency();
            target.passColorToChilds = false;
            NSString nSString3 = item["x"];
            target.x = support.x = (nSString3.IntValue() * scale) + offsetX + mapOffsetX;
            NSString nSString4 = item["y"];
            target.y = support.y = (nSString4.IntValue() * scale) + offsetY + mapOffsetY;
            target.AddImage(101);
            target.AddImage(102);
            target.bb = MakeRectangle(264.0, 350.0, 108.0, 2.0);
            target.AddAnimationWithIDDelayLoopFirstLast(0, 0.05f, Timeline.LoopType.TIMELINE_REPLAY, 0, 18);
            target.AddAnimationWithIDDelayLoopFirstLast(1, 0.05f, Timeline.LoopType.TIMELINE_NO_LOOP, 43, 67);
            int num14 = 68;
            target.AddAnimationWithIDDelayLoopCountSequence(2, 0.05f, Timeline.LoopType.TIMELINE_NO_LOOP, 32, num14,
            [
                num14 + 1, num14 + 2, num14 + 3, num14 + 4, num14 + 5, num14 + 6, num14 + 7, num14 + 8,
                num14 + 9, num14 + 10, num14 + 11, num14 + 12, num14 + 13, num14 + 14, num14 + 15, num14,
                num14 + 1, num14 + 2, num14 + 3, num14 + 4, num14 + 5, num14 + 6, num14 + 7, num14 + 8,
                num14 + 9, num14 + 10, num14 + 11, num14 + 12, num14 + 13, num14 + 14, num14 + 15
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
