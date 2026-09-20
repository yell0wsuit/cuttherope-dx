using System;
using System.Globalization;

using CutTheRopeDX.Framework;
using CutTheRopeDX.Framework.Core;
using CutTheRopeDX.Framework.Helpers;
using CutTheRopeDX.Framework.Platform;
using CutTheRopeDX.Framework.Visual;
using CutTheRopeDX.Helpers;

namespace CutTheRopeDX.GameMain
{
    /// <summary>
    /// Manages the level transition, result panel, score countdown, and confetti effects.
    /// </summary>
    internal sealed class BoxOpenClose : BaseElement, ITimelineDelegate
    {
        /// <inheritdoc />
        public override void Update(float delta)
        {
            base.Update(delta);
            if (boxAnim != 2)
            {
                return;
            }
            bool delayFinished = Mover.MoveVariableToTarget(ref raDelay, 0, 1, delta);
            switch (raState)
            {
                case -1:
                    {
                        cscore = 0;
                        ctime = ActiveResult.ElapsedTime;
                        cstarBonus = ActiveResult.StarBonus;
                        ((Text)result.GetChildWithName("scoreValue")).SetString(cscore.ToString(CultureInfo.InvariantCulture));
                        Text dataTitle = (Text)result.GetChildWithName("dataTitle");
                        Image.SetElementPositionWithQuadOffset(dataTitle, Resources.Img.MenuResults, 5);
                        dataTitle.SetString(Application.GetString("STAR_BONUS"));
                        ((Text)result.GetChildWithName("dataValue")).SetString(cstarBonus.ToString(CultureInfo.InvariantCulture));
                        raState = 1;
                        raDelay = 1f;
                        return;
                    }
                case 0:
                    if (delayFinished)
                    {
                        raState = 1;
                        raDelay = 0.2f;
                        return;
                    }
                    break;
                case 1:
                    {
                        Text dataTitle = (Text)result.GetChildWithName("dataTitle");
                        dataTitle.SetEnabled(true);
                        Text dataValue = (Text)result.GetChildWithName("dataValue");
                        dataValue.SetEnabled(true);
                        Text scoreValue = (Text)result.GetChildWithName("scoreValue");
                        scoreValue.SetEnabled(true);
                        dataTitle.color.AlphaChannel = dataValue.color.AlphaChannel = scoreValue.color.AlphaChannel = 1f - (raDelay / 0.2f);
                        if (delayFinished)
                        {
                            raState = 2;
                            raDelay = 1f;
                            return;
                        }
                        break;
                    }
                case 2:
                    {
                        cstarBonus = (int)(ActiveResult.StarBonus * raDelay);
                        cscore = (int)((1f - raDelay) * ActiveResult.StarBonus);
                        ((Text)result.GetChildWithName("dataValue")).SetString(cstarBonus.ToString(CultureInfo.InvariantCulture));
                        Text scoreValue = (Text)result.GetChildWithName("scoreValue");
                        scoreValue.SetEnabled(true);
                        scoreValue.SetString(cscore.ToString(CultureInfo.InvariantCulture));
                        if (delayFinished)
                        {
                            raState = 3;
                            raDelay = 0.2f;
                            return;
                        }
                        break;
                    }
                case 3:
                    {
                        BaseElement dataTitle = (Text)result.GetChildWithName("dataTitle");
                        Text dataValue = (Text)result.GetChildWithName("dataValue");
                        dataTitle.color.AlphaChannel = dataValue.color.AlphaChannel = raDelay / 0.2f;
                        if (delayFinished)
                        {
                            raState = 4;
                            raDelay = 0.2f;
                            int minutes = (int)MathF.Floor(Round(ActiveResult.ElapsedTime) / 60f);
                            int seconds = (int)(Round(ActiveResult.ElapsedTime) - (minutes * 60f));
                            ((Text)result.GetChildWithName("dataTitle")).SetString(Application.GetString("TIME"));
                            ((Text)result.GetChildWithName("dataValue")).SetString(minutes.ToString(CultureInfo.InvariantCulture) + ":" + seconds.ToString("D2", CultureInfo.InvariantCulture));
                            return;
                        }
                        break;
                    }
                case 4:
                    {
                        BaseElement dataTitle = (Text)result.GetChildWithName("dataTitle");
                        Text dataValue = (Text)result.GetChildWithName("dataValue");
                        dataTitle.color.AlphaChannel = dataValue.color.AlphaChannel = 1f - (raDelay / 0.2f);
                        if (delayFinished)
                        {
                            raState = 5;
                            raDelay = 1f;
                            return;
                        }
                        break;
                    }
                case 5:
                    {
                        ctime = ActiveResult.ElapsedTime * raDelay;
                        cscore = (int)(ActiveResult.StarBonus + ((1f - raDelay) * ActiveResult.TimeBonus));
                        int minutes = (int)MathF.Floor(Round(ctime) / 60);
                        int seconds = (int)(Round(ctime) - (minutes * 60));
                        ((Text)result.GetChildWithName("dataValue")).SetString(minutes.ToString(CultureInfo.InvariantCulture) + ":" + seconds.ToString("D2", CultureInfo.InvariantCulture));
                        ((Text)result.GetChildWithName("scoreValue")).SetString(cscore.ToString(CultureInfo.InvariantCulture));
                        if (delayFinished)
                        {
                            cscore = ActiveResult.FinalScore;
                            ((Text)result.GetChildWithName("scoreValue")).SetString(cscore.ToString(CultureInfo.InvariantCulture));
                            raState = 6;
                            raDelay = 0.2f;
                            return;
                        }
                        break;
                    }
                case 6:
                    {
                        BaseElement dataTitle = (Text)result.GetChildWithName("dataTitle");
                        Text dataValue = (Text)result.GetChildWithName("dataValue");
                        dataTitle.color.AlphaChannel = dataValue.color.AlphaChannel = raDelay / 0.2f;
                        if (delayFinished)
                        {
                            raState = 7;
                            raDelay = 0.2f;
                            Text finalScoreTitle = (Text)result.GetChildWithName("dataTitle");
                            Image.SetElementPositionWithQuadOffset(finalScoreTitle, Resources.Img.MenuResults, 7);
                            finalScoreTitle.SetString(Application.GetString("FINAL_SCORE"));
                            ((Text)result.GetChildWithName("dataValue")).SetString("");
                            return;
                        }
                        break;
                    }
                case 7:
                    {
                        BaseElement dataTitle = (Text)result.GetChildWithName("dataTitle");
                        Text dataValue = (Text)result.GetChildWithName("dataValue");
                        dataTitle.color.AlphaChannel = dataValue.color.AlphaChannel = 1f - (raDelay / 0.2f);
                        if (delayFinished)
                        {
                            raState = 8;
                            if (shouldShowImprovedResult)
                            {
                                stamp.SetEnabled(true);
                                stamp.PlayTimeline(0);
                            }
                        }
                        break;
                    }
                default:
                    return;
            }
        }

        /// <summary>
        /// Spans the transition box across the viewport and refits the box cover to it.
        /// </summary>
        /// <remarks>
        /// The result panel is not placed here. It is a design-space composition like a menu's,
        /// so the controller fits it the same way it fits those, and all this has to guarantee is
        /// that the panel's group hangs from the viewport's own origin.
        /// </remarks>
        /// <param name="visible">The logical region the viewport exposes.</param>
        public void RelayoutBox(Rectangle visible)
        {
            width = (int)visible.w;
            height = (int)visible.h;
            CoverFitAnimations(visible);
        }

        /// <summary>
        /// Scales the transition animation so the box covers every edge of the viewport, and
        /// centers what overhangs.
        /// </summary>
        /// <remarks>
        /// The covers are one fixed-size piece of art each, sized to meet in the middle of the
        /// design box, and their open and close positions are all derived from that. Cover-fitting
        /// the group that holds them is what lets the pair still reach both edges of a viewport
        /// the design box does not fill, without every placement inside needing to know about it.
        /// The group scales about its own origin, so the fit is a scale and a centering offset.
        /// </remarks>
        /// <param name="visible">The logical region the viewport exposes.</param>
        private void CoverFitAnimations(Rectangle visible)
        {
            if (openCloseAnims == null)
            {
                return;
            }

            Rectangle covered = LayoutMath.CoverInside(
                ViewportLayout.DesignWidth, ViewportLayout.DesignHeight, visible);
            float scale = covered.w / ViewportLayout.DesignWidth;
            openCloseAnims.scaleX = openCloseAnims.scaleY = scale;

            // Centered through the renderer's translation rather than this element's position. The
            // pieces inside are placed absolutely, which means they resolve their own position
            // without consulting this one - moving the group would scale them about a new point
            // and leave them exactly where they were. The translation is divided by the scale
            // because it is applied in the space the scale has already been taken into.
            openCloseAnims.translateX = covered.x / scale;
            openCloseAnims.translateY = covered.y / scale;
        }

        /// <summary>
        /// Index of the last anchor marker that belongs to the result panel's body.
        /// </summary>
        /// <remarks>
        /// The marker after it places the improved-result stamp, which is decorative, appears on
        /// some results and not others, and is authored well off to one side. Letting it into the
        /// measurement would pull the panel sideways to balance something most results never draw.
        /// </remarks>
        private const int LastPanelBodyQuad = 11;

        /// <summary>
        /// The offset, in design units, that brings the authored result composition onto the
        /// center of the design box it is fitted inside.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Every piece of the panel is placed from an anchor marker authored on the
        /// <c>menu_results</c> art canvas, which is 2560x1597 - taller than the 2560x1440 design
        /// box the group is fitted to. Centering that box therefore does not center what it holds:
        /// the composition is authored low in its own canvas and is drawn low on every screen. It
        /// reads as centered at the design shape only because the whole panel is small there;
        /// a phone-shaped viewport draws it at up to 1.55x and the gap above it opens up.
        /// </para>
        /// <para>
        /// Measured from the markers rather than from what the panel paints. The score counts up
        /// while the panel is on screen and the pass text differs with the stars earned, so the
        /// painted extent changes width under its own animation - centering on that would slide
        /// the panel sideways as the digits climb. The markers are fixed data and answer the same
        /// way on every frame.
        /// </para>
        /// </remarks>
        /// <returns>The design-space offset to apply to the placed panel group.</returns>
        public static Vector PanelCenteringOffset()
        {
            float left = float.MaxValue;
            float top = float.MaxValue;
            float right = float.MinValue;
            float bottom = float.MinValue;
            for (int quad = 0; quad <= LastPanelBodyQuad; quad++)
            {
                Vector marker = Image.GetQuadOffset(Resources.Img.MenuResults, quad);
                left = MathF.Min(left, marker.X);
                top = MathF.Min(top, marker.Y);
                right = MathF.Max(right, marker.X);
                bottom = MathF.Max(bottom, marker.Y);
            }

            return Vect(
                (ViewportLayout.DesignWidth / 2f) - ((left + right) / 2f),
                (ViewportLayout.DesignHeight / 2f) - ((top + bottom) / 2f));
        }

        /// <summary>
        /// Hangs one authored piece of the result panel from the panel's group.
        /// </summary>
        /// <remarks>
        /// Anchored to the group's own corner, which is what makes the design-space position each
        /// piece was authored with be read from where the fit put the group rather than from the
        /// corner of the screen. A piece left resolving against the screen stays where the design
        /// size put it while the rest of the panel moves.
        /// </remarks>
        /// <param name="piece">Panel piece to add.</param>
        private void AddPanelPiece(BaseElement piece)
        {
            piece.parentAnchor = 9;
            _ = result.AddChild(piece);
        }

        /// <summary>
        /// Initializes the transition box UI, result panel, buttons, and score labels.
        /// </summary>
        /// <param name="b">Button delegate that receives result-panel button events.</param>
        /// <returns>The initialized transition box instance.</returns>
        public BoxOpenClose InitWithButtonDelegate(IButtonDelegation b)
        {
            // Every piece of the result panel is authored in design coordinates, so they hang from
            // a group the layout pass fits to the viewport and the whole panel follows it. Sitting
            // at the viewport's own origin, rather than centered inside a parent, is what lets the
            // group's fitted position mean what it says.
            result = new FittedGroup { anchor = 9, parentAnchor = 9 };
            _ = AddChildwithID(result, 1);
            anchor = 9;
            parentAnchor = -1;
            x = 0f;
            y = 0f;
            RelayoutBox(VisibleBounds);
            result.SetEnabled(false);
            Timeline timeline = new Timeline().InitWithMaxKeyFramesOnTrack(2);
            timeline.AddKeyFrame(KeyFrame.MakeColor(RGBAColor.transparentRGBA, KeyFrame.TransitionType.FRAME_TRANSITION_LINEAR, 0));
            timeline.AddKeyFrame(KeyFrame.MakeColor(RGBAColor.solidOpaqueRGBA, KeyFrame.TransitionType.FRAME_TRANSITION_LINEAR, 0.5f));
            result.AddTimelinewithID(timeline, 0);
            timeline = new Timeline().InitWithMaxKeyFramesOnTrack(2);
            timeline.AddKeyFrame(KeyFrame.MakeColor(RGBAColor.solidOpaqueRGBA, KeyFrame.TransitionType.FRAME_TRANSITION_LINEAR, 0));
            timeline.AddKeyFrame(KeyFrame.MakeColor(RGBAColor.transparentRGBA, KeyFrame.TransitionType.FRAME_TRANSITION_LINEAR, 0.5f));
            result.AddTimelinewithID(timeline, 1);
            Image star1 = Image.Image_createWithResIDQuad(Resources.Img.MenuResults, 14);
            star1.anchor = 18;
            star1.SetName("star1");
            Image.SetElementPositionWithQuadOffset(star1, Resources.Img.MenuResults, 0);
            AddPanelPiece(star1);
            Image star2 = Image.Image_createWithResIDQuad(Resources.Img.MenuResults, 14);
            star2.anchor = 18;
            star2.SetName("star2");
            Image.SetElementPositionWithQuadOffset(star2, Resources.Img.MenuResults, 1);
            AddPanelPiece(star2);
            Image star3 = Image.Image_createWithResIDQuad(Resources.Img.MenuResults, 14);
            star3.anchor = 18;
            star3.SetName("star3");
            Image.SetElementPositionWithQuadOffset(star3, Resources.Img.MenuResults, 2);
            AddPanelPiece(star3);
            Text passText = new Text().InitWithFont(Application.GetFont(Resources.Fnt.BigFont));
            passText.SetString(Application.GetString("LEVEL_CLEARED1"));
            Image.SetElementPositionWithQuadOffset(passText, Resources.Img.MenuResults, 3);
            passText.anchor = 18;
            passText.SetName("passText");
            AddPanelPiece(passText);
            Image dataPlate = Image.Image_createWithResIDQuad(Resources.Img.MenuResults, 15);
            dataPlate.anchor = 18;
            Image.SetElementPositionWithQuadOffset(dataPlate, Resources.Img.MenuResults, 4);
            AddPanelPiece(dataPlate);
            stamp = Image.Image_createWithResIDQuad(Resources.Img.MenuResults, ResourceMgr.GetResultStampQuad());
            Timeline stampTimeline = new Timeline().InitWithMaxKeyFramesOnTrack(7);
            stampTimeline.AddKeyFrame(KeyFrame.MakeScale(3, 3, KeyFrame.TransitionType.FRAME_TRANSITION_LINEAR, 0));
            stampTimeline.AddKeyFrame(KeyFrame.MakeScale(1, 1, KeyFrame.TransitionType.FRAME_TRANSITION_EASE_IN, 0.5f));
            stampTimeline.AddKeyFrame(KeyFrame.MakeColor(RGBAColor.transparentRGBA, KeyFrame.TransitionType.FRAME_TRANSITION_LINEAR, 0));
            stampTimeline.AddKeyFrame(KeyFrame.MakeColor(RGBAColor.solidOpaqueRGBA, KeyFrame.TransitionType.FRAME_TRANSITION_EASE_IN, 0.5f));
            _ = stamp.AddTimeline(stampTimeline);
            stamp.anchor = 18;
            stamp.SetEnabled(false);
            Image.SetElementPositionWithQuadOffset(stamp, Resources.Img.MenuResults, 12);
            AddPanelPiece(stamp);
            Button replayButton = MenuController.CreateShortButtonWithTextIDDelegate(Application.GetString("REPLAY"), 8, b);
            replayButton.anchor = 18;
            // Custom levels hide the NEXT/MENU buttons, so replay takes the centered menu slot instead.
            Image.SetElementPositionWithQuadOffset(replayButton, Resources.Img.MenuResults, CustomLevelSession.IsActive ? 9 : 11);
            AddPanelPiece(replayButton);
            if (!CustomLevelSession.IsActive)
            {
                Button nextButton = MenuController.CreateShortButtonWithTextIDDelegate(Application.GetString("NEXT"), 9, b);
                nextButton.anchor = 18;
                Image.SetElementPositionWithQuadOffset(nextButton, Resources.Img.MenuResults, 10);
                AddPanelPiece(nextButton);
                Button menuButton = MenuController.CreateShortButtonWithTextIDDelegate(Application.GetString("MENU"), 5, b);
                menuButton.anchor = 18;
                Image.SetElementPositionWithQuadOffset(menuButton, Resources.Img.MenuResults, 9);
                AddPanelPiece(menuButton);
            }
            Text dataTitle = new Text().InitWithFont(Application.GetFont(Resources.Fnt.SmallFont));
            dataTitle.SetName("dataTitle");
            dataTitle.anchor = 18;
            Image.SetElementPositionWithQuadOffset(dataTitle, Resources.Img.MenuResults, 5);
            AddPanelPiece(dataTitle);
            Text dataValue = new Text().InitWithFont(Application.GetFont(Resources.Fnt.SmallFont));
            dataValue.SetName("dataValue");
            dataValue.anchor = 18;
            Image.SetElementPositionWithQuadOffset(dataValue, Resources.Img.MenuResults, 6);
            AddPanelPiece(dataValue);
            Text scoreValue = new Text().InitWithFont(Application.GetFont(Resources.Fnt.FontNumbersBig));
            scoreValue.SetName("scoreValue");
            scoreValue.anchor = 18;
            Image.SetElementPositionWithQuadOffset(scoreValue, Resources.Img.MenuResults, 8);
            AddPanelPiece(scoreValue);
            confettiAnims = new BaseElement();
            AddPanelPiece(confettiAnims);
            openCloseAnims = null;
            boxAnim = -1;
            delegateboxClosed = null;
            return this;
        }

        /// <summary>
        /// Creates one randomized confetti particle near the top of the screen.
        /// </summary>
        /// <returns>The configured confetti particle element.</returns>
        public static BaseElement CreateConfettiParticleNear()
        {
            Confetti confetti = Confetti.Confetti_createWithResID(Resources.Img.ConfettiParticles);

            // Spawned across the design box and animated in design coordinates, so it travels with
            // the panel it bursts over instead of falling where the design size alone would put it.
            confetti.parentAnchor = 9;
            confetti.DoRestoreCutTransparency();
            int confettiVariant = RND_RANGE(0, 2);
            int firstFrame = 18;
            int lastFrame = 26;
            if (confettiVariant != 1)
            {
                if (confettiVariant == 2)
                {
                    firstFrame = 0;
                    lastFrame = 8;
                }
            }
            else
            {
                firstFrame = 9;
                lastFrame = 17;
            }
            float spawnX = RND_RANGE((int)RTPD(-100), (int)ViewportLayout.DesignWidth);
            float spawnY = RND_RANGE((int)RTPD(-40), (int)RTPD(100));
            float fadeDuration = FLOAT_RND_RANGE(2, 5);
            int i = confetti.AddAnimationDelayLoopFirstLast(0.05f, Timeline.LoopType.TIMELINE_REPLAY, firstFrame, lastFrame);
            confetti.ani = confetti.GetTimeline(i);
            confetti.ani.PlayTimeline();
            confetti.ani.JumpToTrackKeyFrame((int)Track.TrackType.TRACK_ACTION, RND_RANGE(0, lastFrame - firstFrame - 1));
            Timeline timeline = new Timeline().InitWithMaxKeyFramesOnTrack(2);
            timeline.AddKeyFrame(KeyFrame.MakeColor(RGBAColor.solidOpaqueRGBA, KeyFrame.TransitionType.FRAME_TRANSITION_LINEAR, 0));
            timeline.AddKeyFrame(KeyFrame.MakeColor(RGBAColor.transparentRGBA, KeyFrame.TransitionType.FRAME_TRANSITION_LINEAR, fadeDuration));
            timeline.AddKeyFrame(KeyFrame.MakePos((int)spawnX, (int)spawnY, KeyFrame.TransitionType.FRAME_TRANSITION_LINEAR, 0));
            timeline.AddKeyFrame(KeyFrame.MakePos((int)spawnX, (int)(spawnY + FLOAT_RND_RANGE((int)RTPD(150), (int)RTPD(400))), KeyFrame.TransitionType.FRAME_TRANSITION_LINEAR, fadeDuration));
            timeline.AddKeyFrame(KeyFrame.MakeScale(0, 0, KeyFrame.TransitionType.FRAME_TRANSITION_LINEAR, 0));
            timeline.AddKeyFrame(KeyFrame.MakeScale(1, 1, KeyFrame.TransitionType.FRAME_TRANSITION_LINEAR, 0.3f));
            timeline.AddKeyFrame(KeyFrame.MakeRotation(RND_RANGE(-360, 360), KeyFrame.TransitionType.FRAME_TRANSITION_LINEAR, 0));
            timeline.AddKeyFrame(KeyFrame.MakeRotation(RND_RANGE(-360, 360), KeyFrame.TransitionType.FRAME_TRANSITION_LINEAR, fadeDuration));
            _ = confetti.AddTimeline(timeline);
            confetti.PlayTimeline(1);
            return confetti;
        }

        /// <summary>
        /// Starts the first-level opening transition and hides any visible result panel.
        /// </summary>
        public void LevelFirstStart()
        {
            boxAnim = 0;
            RemoveOpenCloseAnims();
            ShowOpenAnim();
            if (result.IsEnabled())
            {
                result.PlayTimeline(1);
            }
        }

        /// <summary>
        /// Starts the normal level opening transition and hides any visible result panel.
        /// </summary>
        public void LevelStart()
        {
            boxAnim = 1;
            RemoveOpenCloseAnims();
            ShowOpenAnim();
            if (result.IsEnabled())
            {
                result.PlayTimeline(1);
            }
        }

        /// <summary>
        /// Starts the closing transition for a won level and prepares the result panel countdown.
        /// </summary>
        /// <param name="levelResult">The immutable result that drives the presentation.</param>
        public void LevelWon(LevelResult levelResult)
        {
            ActiveResult = levelResult;
            boxAnim = 2;
            raState = -1;
            RemoveOpenCloseAnims();
            ShowCloseAnim();
            ((Text)result.GetChildWithName("scoreValue")).SetEnabled(false);
            Text text = (Text)result.GetChildWithName("dataTitle");
            text.SetEnabled(false);
            Image.SetElementPositionWithQuadOffset(text, Resources.Img.MenuResults, 5);
            ((Text)result.GetChildWithName("dataValue")).SetEnabled(false);
            result.PlayTimeline(0);
            result.SetEnabled(true);
            stamp.SetEnabled(false);
        }

        /// <summary>
        /// Starts the closing transition for a lost level.
        /// </summary>
        public void LevelLost()
        {
            boxAnim = 3;
            RemoveOpenCloseAnims();
            ShowCloseAnim();
        }

        /// <summary>
        /// Starts the closing transition for quitting a level.
        /// </summary>
        public void LevelQuit()
        {
            boxAnim = 4;
            result.SetEnabled(false);
            RemoveOpenCloseAnims();
            ShowCloseAnim();
        }

        /// <summary>
        /// Shows the box opening animation.
        /// </summary>
        public void ShowOpenAnim()
        {
            ShowOpenCloseAnim(true);
        }

        /// <summary>
        /// Shows the box closing animation.
        /// </summary>
        public void ShowCloseAnim()
        {
            ShowOpenCloseAnim(false);
        }

        /// <summary>
        /// Adds a burst of confetti particles to the result panel.
        /// </summary>
        public void ShowConfetti()
        {
            for (int i = 0; i < 70; i++)
            {
                _ = confettiAnims.AddChild(CreateConfettiParticleNear());
            }
        }

        /// <summary>
        /// Builds and plays the animated box cover used for opening and closing transitions.
        /// </summary>
        /// <param name="open"><see langword="true"/> to play the opening animation; <see langword="false"/> to play the closing animation.</param>
        public void ShowOpenCloseAnim(bool open)
        {
            CreateOpenCloseAnims();
            RootController root = Application.SharedRootController();
            string boxCover = PackConfig.GetBoxCoverOrDefault(root.GetPack());
            Image image = Image.Image_createWithResIDQuad(Resources.Img.MenuResults, 16);
            image.rotationCenterX = (-image.width / 2f) + 1f;
            image.rotationCenterY = (-image.height / 2f) + 1f;
            image.scaleX = image.scaleY = 4f;
            Timeline timeline = new Timeline().InitWithMaxKeyFramesOnTrack(2);
            if (open)
            {
                timeline.AddKeyFrame(KeyFrame.MakePos(0, 0, KeyFrame.TransitionType.FRAME_TRANSITION_LINEAR, 0));
                timeline.AddKeyFrame(KeyFrame.MakePos(-image.width * 4, 0, KeyFrame.TransitionType.FRAME_TRANSITION_LINEAR, 0.5f));
            }
            else
            {
                timeline.AddKeyFrame(KeyFrame.MakePos(-image.width * 4, 0, KeyFrame.TransitionType.FRAME_TRANSITION_LINEAR, 0));
                timeline.AddKeyFrame(KeyFrame.MakePos(0, 0, KeyFrame.TransitionType.FRAME_TRANSITION_LINEAR, 0.5f));
            }
            image.AddTimelinewithID(timeline, 0);
            image.PlayTimeline(0);
            timeline.delegateTimelineDelegate = this;
            _ = openCloseAnims.AddChild(image);
            Vector quadSize = Image.GetQuadSize(boxCover, 0);

            // The whole animation is authored against the design size and cover-fitted to the
            // viewport by the group that holds it, the way a menu backdrop is. Measuring the
            // pieces against the viewport instead would move them relative to art that had not
            // moved with them.
            float boxWidth = ViewportLayout.DesignWidth;

            // Where the two halves of the cover meet. The flaps and the loading piece are placed
            // against this rather than against a cover's own width: the two are only the same
            // thing while one cover is exactly half the box.
            float seamX = boxWidth / 2f;
            float leftCoverX = seamX - quadSize.X;
            Image coverBackgroundLeft = Image.Image_createWithResIDQuad(boxCover, 0);
            Image coverBackgroundRight = Image.Image_createWithResIDQuad(boxCover, 0);
            coverBackgroundLeft.x = leftCoverX;
            coverBackgroundLeft.rotationCenterX = -coverBackgroundLeft.width / 2f;
            coverBackgroundRight.rotationCenterX = coverBackgroundLeft.rotationCenterX;
            coverBackgroundRight.rotation = 180f;
            coverBackgroundRight.x = seamX + coverBackgroundLeft.width;
            coverBackgroundRight.y = -0.5f;
            timeline = new Timeline().InitWithMaxKeyFramesOnTrack(2);
            if (open)
            {
                timeline.AddKeyFrame(KeyFrame.MakeScale(1, 1, KeyFrame.TransitionType.FRAME_TRANSITION_LINEAR, 0));
                timeline.AddKeyFrame(KeyFrame.MakeScale(0, 1.1f, KeyFrame.TransitionType.FRAME_TRANSITION_LINEAR, 0.5f));
                timeline.AddKeyFrame(KeyFrame.MakeColor(RGBAColor.MakeRGBA(0.85f, 0.85f, 0.85f, 1), KeyFrame.TransitionType.FRAME_TRANSITION_LINEAR, 0));
                timeline.AddKeyFrame(KeyFrame.MakeColor(RGBAColor.whiteRGBA, KeyFrame.TransitionType.FRAME_TRANSITION_LINEAR, 0.5f));
            }
            else
            {
                timeline.AddKeyFrame(KeyFrame.MakeScale(0, 1.1f, KeyFrame.TransitionType.FRAME_TRANSITION_LINEAR, 0));
                timeline.AddKeyFrame(KeyFrame.MakeScale(1, 1, KeyFrame.TransitionType.FRAME_TRANSITION_LINEAR, 0.5f));
                timeline.AddKeyFrame(KeyFrame.MakeColor(RGBAColor.whiteRGBA, KeyFrame.TransitionType.FRAME_TRANSITION_LINEAR, 0));
                timeline.AddKeyFrame(KeyFrame.MakeColor(RGBAColor.MakeRGBA(0.85f, 0.85f, 0.85f, 1), KeyFrame.TransitionType.FRAME_TRANSITION_LINEAR, 0.5f));
            }
            coverBackgroundLeft.AddTimelinewithID(timeline, 0);
            coverBackgroundLeft.PlayTimeline(0);
            timeline = new Timeline().InitWithMaxKeyFramesOnTrack(2);
            if (open)
            {
                timeline.AddKeyFrame(KeyFrame.MakeScale(1, 1, KeyFrame.TransitionType.FRAME_TRANSITION_LINEAR, 0));
                timeline.AddKeyFrame(KeyFrame.MakeScale(0, 1.1f, KeyFrame.TransitionType.FRAME_TRANSITION_LINEAR, 0.5f));
                timeline.AddKeyFrame(KeyFrame.MakeColor(RGBAColor.MakeRGBA(0.85f, 0.85f, 0.85f, 1), KeyFrame.TransitionType.FRAME_TRANSITION_LINEAR, 0));
                timeline.AddKeyFrame(KeyFrame.MakeColor(RGBAColor.MakeRGBA(0.4f, 0.4f, 0.4f, 1), KeyFrame.TransitionType.FRAME_TRANSITION_LINEAR, 0.5f));
            }
            else
            {
                timeline.AddKeyFrame(KeyFrame.MakeScale(0, 1.1f, KeyFrame.TransitionType.FRAME_TRANSITION_LINEAR, 0));
                timeline.AddKeyFrame(KeyFrame.MakeScale(1, 1, KeyFrame.TransitionType.FRAME_TRANSITION_LINEAR, 0.5f));
                timeline.AddKeyFrame(KeyFrame.MakeColor(RGBAColor.MakeRGBA(0.4f, 0.4f, 0.4f, 1), KeyFrame.TransitionType.FRAME_TRANSITION_LINEAR, 0));
                timeline.AddKeyFrame(KeyFrame.MakeColor(RGBAColor.MakeRGBA(0.85f, 0.85f, 0.85f, 1), KeyFrame.TransitionType.FRAME_TRANSITION_LINEAR, 0.5f));
            }
            coverBackgroundRight.AddTimelinewithID(timeline, 0);
            coverBackgroundRight.PlayTimeline(0);
            Image spineLeft = Image.Image_createWithResIDQuad(Resources.Img.MenuLevelUi, 6);
            Image spineRight = Image.Image_createWithResIDQuad(Resources.Img.MenuLevelUi, 7);
            float loadingY = 80f;
            float leftOpenOffset = 50f;
            float rightRestInset = 10f;
            float leftClosedX = -40f;
            float rightClosedX = 25f;
            timeline = new Timeline().InitWithMaxKeyFramesOnTrack(2);
            if (open)
            {
                timeline.AddKeyFrame(KeyFrame.MakePos((int)(coverBackgroundLeft.width - leftOpenOffset), (int)loadingY, KeyFrame.TransitionType.FRAME_TRANSITION_LINEAR, 0));
                timeline.AddKeyFrame(KeyFrame.MakePos((int)leftClosedX, (int)loadingY, KeyFrame.TransitionType.FRAME_TRANSITION_LINEAR, 0.5f));
                timeline.AddKeyFrame(KeyFrame.MakeScale(1, 1, KeyFrame.TransitionType.FRAME_TRANSITION_LINEAR, 0));
                timeline.AddKeyFrame(KeyFrame.MakeScale(0, 1.3f, KeyFrame.TransitionType.FRAME_TRANSITION_LINEAR, 0.5f));
            }
            else
            {
                timeline.AddKeyFrame(KeyFrame.MakePos((int)RTD(-15), (int)loadingY, KeyFrame.TransitionType.FRAME_TRANSITION_LINEAR, 0));
                timeline.AddKeyFrame(KeyFrame.MakePos((int)(coverBackgroundLeft.width - leftOpenOffset), (int)loadingY, KeyFrame.TransitionType.FRAME_TRANSITION_LINEAR, 0.5f));
                timeline.AddKeyFrame(KeyFrame.MakeScale(0, 1.3f, KeyFrame.TransitionType.FRAME_TRANSITION_LINEAR, 0));
                timeline.AddKeyFrame(KeyFrame.MakeScale(1, 1, KeyFrame.TransitionType.FRAME_TRANSITION_LINEAR, 0.5f));
            }
            spineLeft.AddTimelinewithID(timeline, 0);
            spineLeft.PlayTimeline(0);
            timeline = new Timeline().InitWithMaxKeyFramesOnTrack(2);
            if (open)
            {
                timeline.AddKeyFrame(KeyFrame.MakePos((int)(seamX + rightRestInset), (int)loadingY, KeyFrame.TransitionType.FRAME_TRANSITION_LINEAR, 0));
                timeline.AddKeyFrame(KeyFrame.MakePos((int)(boxWidth + rightClosedX), (int)loadingY, KeyFrame.TransitionType.FRAME_TRANSITION_LINEAR, 0.5f));
                timeline.AddKeyFrame(KeyFrame.MakeScale(1, 1, KeyFrame.TransitionType.FRAME_TRANSITION_LINEAR, 0));
                timeline.AddKeyFrame(KeyFrame.MakeScale(0, 1.3f, KeyFrame.TransitionType.FRAME_TRANSITION_LINEAR, 0.5f));
            }
            else
            {
                timeline.AddKeyFrame(KeyFrame.MakePos((int)(boxWidth - RTD(9)), (int)loadingY, KeyFrame.TransitionType.FRAME_TRANSITION_LINEAR, 0));
                timeline.AddKeyFrame(KeyFrame.MakePos((int)(seamX + rightRestInset), (int)loadingY, KeyFrame.TransitionType.FRAME_TRANSITION_LINEAR, 0.5f));
                timeline.AddKeyFrame(KeyFrame.MakeScale(0, 1.3f, KeyFrame.TransitionType.FRAME_TRANSITION_LINEAR, 0));
                timeline.AddKeyFrame(KeyFrame.MakeScale(1, 1, KeyFrame.TransitionType.FRAME_TRANSITION_LINEAR, 0.5f));
            }
            spineRight.AddTimelinewithID(timeline, 0);
            spineRight.PlayTimeline(0);
            Image coverSideLeft = Image.Image_createWithResIDQuad(boxCover, 1);
            Image coverSideRight = Image.Image_createWithResIDQuad(boxCover, 1);
            coverSideLeft.rotationCenterX = -coverSideLeft.width / 2f;
            coverSideRight.rotationCenterX = coverSideLeft.rotationCenterX;
            timeline = new Timeline().InitWithMaxKeyFramesOnTrack(2);
            if (open)
            {
                timeline.AddKeyFrame(KeyFrame.MakePos((int)(coverBackgroundLeft.x + coverBackgroundLeft.width - RTD(6)), 0, KeyFrame.TransitionType.FRAME_TRANSITION_LINEAR, 0));
                timeline.AddKeyFrame(KeyFrame.MakePos(-25, 0, KeyFrame.TransitionType.FRAME_TRANSITION_LINEAR, 0.5f));
                timeline.AddKeyFrame(KeyFrame.MakeScale(0, 1.3f, KeyFrame.TransitionType.FRAME_TRANSITION_LINEAR, 0));
                timeline.AddKeyFrame(KeyFrame.MakeScale(1, 1, KeyFrame.TransitionType.FRAME_TRANSITION_LINEAR, 0.5f));
            }
            else
            {
                timeline.AddKeyFrame(KeyFrame.MakePos((int)coverBackgroundLeft.x, 0, KeyFrame.TransitionType.FRAME_TRANSITION_LINEAR, 0));
                timeline.AddKeyFrame(KeyFrame.MakePos((int)(coverBackgroundLeft.width - 16f), 0, KeyFrame.TransitionType.FRAME_TRANSITION_LINEAR, 0.5f));
                timeline.AddKeyFrame(KeyFrame.MakeScale(1, 1, KeyFrame.TransitionType.FRAME_TRANSITION_LINEAR, 0));
                timeline.AddKeyFrame(KeyFrame.MakeScale(0, 1.3f, KeyFrame.TransitionType.FRAME_TRANSITION_LINEAR, 0.5f));
            }
            coverSideLeft.AddTimelinewithID(timeline, 0);
            coverSideLeft.PlayTimeline(0);
            _ = openCloseAnims.AddChild(coverSideLeft);
            timeline = new Timeline().InitWithMaxKeyFramesOnTrack(2);
            if (open)
            {
                timeline.AddKeyFrame(KeyFrame.MakePos((int)(seamX + RTD(7)), 0, KeyFrame.TransitionType.FRAME_TRANSITION_LINEAR, 0));
                timeline.AddKeyFrame(KeyFrame.MakePos((int)boxWidth, 0, KeyFrame.TransitionType.FRAME_TRANSITION_LINEAR, 0.5f));
                timeline.AddKeyFrame(KeyFrame.MakeScale(0, 1.3f, KeyFrame.TransitionType.FRAME_TRANSITION_LINEAR, 0));
                timeline.AddKeyFrame(KeyFrame.MakeScale(1, 1, KeyFrame.TransitionType.FRAME_TRANSITION_LINEAR, 0.5f));
            }
            else
            {
                timeline.AddKeyFrame(KeyFrame.MakePos((int)(boxWidth - 40f), 0, KeyFrame.TransitionType.FRAME_TRANSITION_LINEAR, 0));
                timeline.AddKeyFrame(KeyFrame.MakePos((int)(seamX + 20f), 0, KeyFrame.TransitionType.FRAME_TRANSITION_LINEAR, 0.5f));
                timeline.AddKeyFrame(KeyFrame.MakeScale(1, 1, KeyFrame.TransitionType.FRAME_TRANSITION_LINEAR, 0));
                timeline.AddKeyFrame(KeyFrame.MakeScale(0, 1.3f, KeyFrame.TransitionType.FRAME_TRANSITION_LINEAR, 0.5f));
            }
            coverSideRight.AddTimelinewithID(timeline, 0);
            coverSideRight.PlayTimeline(0);
            _ = openCloseAnims.AddChild(coverSideRight);
            _ = openCloseAnims.AddChild(coverBackgroundLeft);
            _ = openCloseAnims.AddChild(coverBackgroundRight);
            if (boxAnim == 0)
            {
                _ = openCloseAnims.AddChild(spineLeft);
                _ = openCloseAnims.AddChild(spineRight);
            }
        }

        /// <inheritdoc />
        public void TimelinereachedKeyFramewithIndex(Timeline t, KeyFrame k, int i)
        {
        }

        /// <inheritdoc />
        public void TimelineFinished(Timeline t)
        {
            switch (boxAnim)
            {
                case 0:
                case 1:
                    {
                        DelayedDispatcher.DispatchFunc dispatchFunc = new(Selector_removeOpenCloseAnims);
                        TimerManager.RegisterDelayedObjectCall(dispatchFunc, this, 0.001f);
                        if (result.IsEnabled())
                        {
                            confettiAnims.RemoveAllChilds();
                            result.SetEnabled(false);
                            return;
                        }
                        break;
                    }
                case 2:
                    {
                        DelayedDispatcher.DispatchFunc postBoxClosedCall = new(Selector_postBoxClosed);
                        TimerManager.RegisterDelayedObjectCall(postBoxClosedCall, this, 0.001f);
                        break;
                    }
                case 3:
                    break;
                case 4:
                    Application.SharedRootController().GetCurrentController().Deactivate();
                    return;
                default:
                    return;
            }
        }

        /// <summary>
        /// Invokes the box-closed callback and optionally starts the confetti burst.
        /// </summary>
        public void PostBoxClosed()
        {
            delegateboxClosed?.Invoke();
            if (shouldShowConfetti)
            {
                ShowConfetti();
            }
        }

        /// <summary>
        /// Removes active box transition animation elements and restores result label opacity.
        /// </summary>
        public void RemoveOpenCloseAnims()
        {
            if (GetChild(0) != null)
            {
                RemoveChild(openCloseAnims);
                openCloseAnims = null;
            }
            BaseElement dataTitle = (Text)result.GetChildWithName("dataTitle");
            Text dataValue = (Text)result.GetChildWithName("dataValue");
            Text scoreValue = (Text)result.GetChildWithName("scoreValue");
            dataTitle.color.AlphaChannel = dataValue.color.AlphaChannel = scoreValue.color.AlphaChannel = 1f;
        }

        /// <summary>
        /// Creates the container used to hold box transition animation elements.
        /// </summary>
        public void CreateOpenCloseAnims()
        {
            openCloseAnims = new BaseElement();
            _ = AddChildwithID(openCloseAnims, 0);
            CoverFitAnimations(VisibleBounds);
        }

        /// <summary>
        /// Delayed-dispatcher callback that removes box open/close animation elements.
        /// </summary>
        /// <param name="obj">Box transition instance to update.</param>
        private static void Selector_removeOpenCloseAnims(FrameworkTypes obj)
        {
            ((BoxOpenClose)obj).RemoveOpenCloseAnims();
        }

        /// <summary>
        /// Delayed-dispatcher callback that finalizes the closed-box state.
        /// </summary>
        /// <param name="obj">Box transition instance to update.</param>
        private static void Selector_postBoxClosed(FrameworkTypes obj)
        {
            ((BoxOpenClose)obj).PostBoxClosed();
        }

        /// <summary>Box animation state for the first level start transition.</summary>
        public const int BOX_ANIM_LEVEL_FIRST_START = 0;

        /// <summary>Box animation state for a normal level start transition.</summary>
        public const int BOX_ANIM_LEVEL_START = 1;

        /// <summary>Box animation state for a won level transition.</summary>
        public const int BOX_ANIM_LEVEL_WON = 2;

        /// <summary>Box animation state for a lost level transition.</summary>
        public const int BOX_ANIM_LEVEL_LOST = 3;

        /// <summary>Box animation state for a quit-level transition.</summary>
        public const int BOX_ANIM_LEVEL_QUIT = 4;

        /// <summary>Result panel state before the countdown sequence begins.</summary>
        public const int RESULT_STATE_WAIT = 0;

        /// <summary>Result panel state that fades in the star bonus row.</summary>
        public const int RESULT_STATE_SHOW_STAR_BONUS = 1;

        /// <summary>Result panel state that counts star bonus points into the score.</summary>
        public const int RESULT_STATE_COUNTDOWN_STAR_BONUS = 2;

        /// <summary>Result panel state that fades out the star bonus row.</summary>
        public const int RESULT_STATE_HIDE_STAR_BONUS = 3;

        /// <summary>Result panel state that fades in the time bonus row.</summary>
        public const int RESULT_STATE_SHOW_TIME_BONUS = 4;

        /// <summary>Result panel state that counts time bonus points into the score.</summary>
        public const int RESULT_STATE_COUNTDOWN_TIME_BONUS = 5;

        /// <summary>Result panel state that fades out the time bonus row.</summary>
        public const int RESULT_STATE_HIDE_TIME_BONUS = 6;

        /// <summary>Result panel state that shows the final score label.</summary>
        public const int RESULT_STATE_SHOW_FINAL_SCORE = 7;

        /// <summary>Result panel timeline ID for showing the panel.</summary>
        public const int RESULTS_SHOW_ANIM = 0;

        /// <summary>Result panel timeline ID for hiding the panel.</summary>
        public const int RESULTS_HIDE_ANIM = 1;

        /// <summary>Container for active box cover transition elements.</summary>
        public BaseElement openCloseAnims;

        /// <summary>Container for active confetti particle elements.</summary>
        public BaseElement confettiAnims;

        /// <summary>Result panel root element.</summary>
        public BaseElement result;

        /// <summary>Current box animation state.</summary>
        public int boxAnim;

        /// <summary>Whether confetti should be shown after the box closes.</summary>
        public bool shouldShowConfetti;

        /// <summary>Whether the improved-result stamp should be shown.</summary>
        public bool shouldShowImprovedResult;

        /// <summary>Improved-result stamp image.</summary>
        public Image stamp;

        /// <summary>Current result panel countdown state.</summary>
        public int raState;

        /// <summary>The immutable result driving the active result presentation.</summary>
        internal LevelResult ActiveResult { get; private set; }

        /// <summary>Displayed countdown time value.</summary>
        public float ctime;

        /// <summary>Displayed countdown star bonus value.</summary>
        public int cstarBonus;

        /// <summary>Displayed countdown score value.</summary>
        public int cscore;

        /// <summary>Delay timer used by the result panel countdown state machine.</summary>
        public float raDelay;

        /// <summary>Callback invoked after the box closing transition completes.</summary>
        public boxClosed delegateboxClosed;

        /// <summary>
        /// Callback invoked after the box closing transition completes.
        /// </summary>
        public delegate void boxClosed();

        /// <summary>
        /// Confetti particle animation that advances its sprite animation timeline during updates.
        /// </summary>
        private sealed class Confetti : Animation
        {
            /// <summary>
            /// Creates a confetti particle from a texture resource name.
            /// </summary>
            /// <param name="resourceName">Texture resource name to load.</param>
            /// <returns>The initialized confetti particle.</returns>
            public static Confetti Confetti_createWithResID(string resourceName)
            {
                return Confetti_create(Application.GetTexture(resourceName));
            }

            /// <summary>
            /// Creates a confetti particle from a texture.
            /// </summary>
            /// <param name="t">Texture used by the confetti particle.</param>
            /// <returns>The initialized confetti particle.</returns>
            public static Confetti Confetti_create(Texture2D t)
            {
                return (Confetti)new Confetti().InitWithTexture(t);
            }

            /// <inheritdoc />
            public override void Update(float delta)
            {
                base.Update(delta);
                Timeline.UpdateTimeline(ani, delta);
            }

            /// <summary>Sprite animation timeline used by this confetti particle.</summary>
            public Timeline ani;
        }
    }
}
