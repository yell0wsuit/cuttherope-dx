using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

using CutTheRopeDX.Framework;
using CutTheRopeDX.Framework.Core;
using CutTheRopeDX.Framework.Media;
using CutTheRopeDX.Framework.Visual;

namespace CutTheRopeDX.GameMain
{
    /// <summary>
    /// The Time Travel result screen (iOS HD 1.5.0 <c>ResultScreen</c>): two halves of a blue
    /// blind fold shut over the level, stretched across the whole screen; the yellow plates and
    /// the dark displays pop in over them; the stars fill one by one; and the star bonus, time and
    /// final score count up as the classic result box counts them. Its buttons are round icons.
    /// </summary>
    internal sealed class TimeTravelResultScreen : BaseElement, ITimelineDelegate
    {
        /// <summary>Timeline that shuts the blind and pops the panel in.</summary>
        private const int ShowTimeline = 0;

        /// <summary>Timeline that shows the improved-result banner.</summary>
        private const int ImprovedTimeline = 1;

        /// <summary>Timeline that fills a star.</summary>
        private const int StarTimeline = 2;

        /// <summary>Seconds between one star filling and the next (iOS 0.4).</summary>
        private const float StarStep = 0.4f;

        /// <summary>Seconds a line of the count fades in or out over (iOS 0.2).</summary>
        private const float CountFade = 0.2f;

        /// <summary>Seconds a figure counts for (iOS 1).</summary>
        private const float CountRun = 1f;

        /// <summary>Room between the count's title and its figure, in asset pixels.</summary>
        private const float CountGap = 40f;

        /// <summary>
        /// How far the display the buttons, stars and title hang from comes to rest from its quad's
        /// canvas place, in canvas pixels: its timeline sets it a little lower.
        /// </summary>
        private static readonly Vector DisplayRest = new(-2.3f, 46f);

        /// <summary>
        /// The stars' places with no blue star (iOS q37 to q39): an arc centered on the screen.
        /// </summary>
        private static readonly Rectangle[] StarSlots =
        [
            new(408f, 522f, 10f, 8f),
            new(634f, 502f, 10f, 10f),
            new(864f, 526f, 10f, 10f),
        ];

        /// <summary>How the display pops in (iOS): its scale at each step, and when.</summary>
        private static readonly (float Seconds, float Scale, bool Shown, KeyFrame.TransitionType Step)[] DisplayPop =
        [
            (0f, 0f, false, KeyFrame.TransitionType.FRAME_TRANSITION_IMMEDIATE),
            (0.4f, 2.04724f, true, KeyFrame.TransitionType.FRAME_TRANSITION_IMMEDIATE),
            (0.2f, 0.968475f, true, KeyFrame.TransitionType.FRAME_TRANSITION_LINEAR),
            (0.2f, 1f, true, KeyFrame.TransitionType.FRAME_TRANSITION_LINEAR),
        ];

        private readonly IButtonDelegation buttons;
        private readonly FlashXmlAnimationDefinition definition;
        private readonly BaseElement popContent;
        private readonly BaseElement countContent;
        private readonly Text passText;
        private readonly Text dataValue;
        private readonly List<TimeTravelFlashStage> stars = [];
        private TimeTravelFlashStage improvedBanner;
        private TimeTravelFlashStage chapterCapsule;
        private int completedPack;
        private bool allowChapterUnlock;
        private LevelResult result;
        private bool shown;
        private bool improved;
        private float shownFor;
        private int starsFilled;
        private int starBursts;
        private bool counting;
        private int countState;
        private float countDelay;

        /// <summary>Initializes a new instance of the <see cref="TimeTravelResultScreen"/> class.</summary>
        /// <param name="buttons">Delegate that receives the buttons' presses.</param>
        public TimeTravelResultScreen(IButtonDelegation buttons)
        {
            this.buttons = buttons;
            definition = TimeTravelFlashStage.Load(TimeTravelArt.ResultAnimationXml);
            anchor = parentAnchor = 9;

            Overlay = new TimeTravelSceneGroup();
            Overlay.SetName("ttResultOverlay");
            popContent = new BaseElement { width = TimeTravelSceneGroup.Width, height = TimeTravelSceneGroup.Height };
            popContent.anchor = popContent.parentAnchor = 9;
            countContent = new BaseElement { width = TimeTravelSceneGroup.Width, height = TimeTravelSceneGroup.Height };
            countContent.anchor = countContent.parentAnchor = 9;
            _ = Overlay.AddChild(popContent);
            _ = Overlay.AddChild(countContent);
            AddPopTimeline();

            passText = new Text().InitWithFont(Application.GetFont(Resources.Fnt.BigFont));
            passText.colorOverride = RGBAColor.solidOpaqueRGBA;
            passText.anchor = 18;
            passText.parentAnchor = 9;
            Vector title = AtRest(Middle(TimeTravelArt.ResultMarkers.Title));
            passText.x = title.X;
            passText.y = title.Y;
            _ = popContent.AddChild(passText);

            CountTitle = CreateCountText(Resources.Fnt.SmallFont, 17);
            dataValue = CreateCountText(Resources.Fnt.SmallFont, 20);
            Score = CreateCountText(Resources.Fnt.FontNumbersBig, 18);
            Vector score = ToAsset(Middle(TimeTravelArt.ResultMarkers.Score));
            Score.x = score.X;
            Score.y = score.Y;

            AddButtons();
            SetName("ttResultScreen");
            SetEnabled(false);
        }

        /// <summary>Gets or sets what runs once the blind has shut over the level.</summary>
        public Action Shut { get; set; }

        /// <summary>Gets the blind's halves.</summary>
        internal TimeTravelFlashStage Folds { get; private set; }

        /// <summary>Gets the plates and displays.</summary>
        internal TimeTravelFlashStage Panel { get; private set; }

        /// <summary>Gets the stars, left to right, for tests.</summary>
        internal IReadOnlyList<TimeTravelFlashStage> Stars => stars;

        /// <summary>Gets the scene the buttons, stars, title and count are in.</summary>
        internal TimeTravelSceneGroup Overlay { get; }

        /// <summary>Gets the score.</summary>
        internal Text Score { get; }

        /// <summary>Gets the title of the line the star bonus and time are counted on.</summary>
        internal Text CountTitle { get; }

        /// <summary>Gets whether the count has finished, for tests.</summary>
        internal bool CountFinished => counting && countState == 10;

        /// <summary>Shows the result of a level that was just won.</summary>
        /// <param name="levelResult">The level's result.</param>
        /// <param name="improvedResult">Whether it beat the level's best.</param>
        public void Show(LevelResult levelResult, bool improvedResult)
        {
            result = levelResult;
            shown = true;
            improved = improvedResult;
            completedPack = Application.SharedRootController().Pack;
            allowChapterUnlock = !CustomLevelSession.IsActive;
            shownFor = 0f;
            starsFilled = 0;
            starBursts = 0;
            counting = false;
            countState = -1;
            countDelay = 0f;
            RemoveAllChilds();

            Folds = TimeTravelFlashStage.Create(definition, Resources.Img.ResultScreenTimeTravel, IsFold);
            Folds.Root.SetName("ttResultFolds");
            Panel = TimeTravelFlashStage.Create(definition, Resources.Img.ResultScreenTimeTravel,
                part => !IsFold(part) && !IsStarPart(part) && part.Name != "Layer 2", withRootTimelines: false);
            Panel.Root.SetName("ttResultPanel");
            improvedBanner = TimeTravelFlashStage.Create(definition, Resources.Img.ResultScreenTimeTravel, part => part.Name == "Layer 2", withRootTimelines: false);
            improvedBanner.Root.SetName("ttResultImproved");
            improvedBanner.Root.visible = false;
            AddNoticeText(improvedBanner.Part("Layer 2"), TimeTravelArt.ResultMarkers.ImprovedBanner,
                TimeTravelArt.ResultMarkers.ImprovedText, "IMPROVED_RESULT", "ttResultImprovedText");
            chapterCapsule = TimeTravelFlashStage.Create(TimeTravelArt.ResultUnlockedAnimationXml, Resources.Img.ResultScreenTimeTravel);
            chapterCapsule.Root.SetName("ttResultNewChapter");
            chapterCapsule.Root.visible = false;
            chapterCapsule.RootTimeline(0).delegateTimelineDelegate = this;
            chapterCapsule.RootTimeline(1).SetTimelineLoopType(Timeline.LoopType.TIMELINE_REPLAY);
            chapterCapsule.Part("lamp").GetTimeline(1).SetTimelineLoopType(Timeline.LoopType.TIMELINE_REPLAY);
            AddNoticeText(chapterCapsule.Part("lamp"), TimeTravelArt.ResultMarkers.UnlockedCapsule,
                TimeTravelArt.ResultMarkers.UnlockedText, "NEW_CHAPTER_AVAILABLE", "ttResultNewChapterText");
            _ = AddChild(Folds.Root);
            _ = AddChild(Panel.Root);
            _ = AddChild(Overlay);
            _ = AddChild(improvedBanner.Root);
            _ = AddChild(chapterCapsule.Root);

            foreach (TimeTravelFlashStage star in stars)
            {
                popContent.RemoveChild(star.Root);
            }
            stars.Clear();
            foreach (TimeTravelStarParticles burst in popContent.GetChilds().Values.OfType<TimeTravelStarParticles>().ToArray())
            {
                popContent.RemoveChild(burst);
                burst.Dispose();
            }
            for (int i = 0; i < StarSlots.Length; i++)
            {
                TimeTravelFlashStage star = TimeTravelFlashStage.Create(definition, Resources.Img.ResultScreenTimeTravel, IsStarPart, withRootTimelines: false);
                star.Root.SetName("ttResultStar" + i);
                Vector at = AtRest(new Vector(StarSlots[i].x, StarSlots[i].y));
                star.Place(FlashXmlScale.AtlasToFlashPointScale, 0f, 0f, at.X, at.Y);

                // Posed empty, and held there until it fills.
                star.Play(StarTimeline);
                star.Root.updateable = false;
                stars.Add(star);
                _ = popContent.AddChild(star.Root);
            }

            passText.SetString(Application.GetString(levelResult.StarsCollected switch
            {
                1 => "LEVEL_CLEARED2",
                2 => "LEVEL_CLEARED3",
                3 => "LEVEL_CLEARED4",
                _ => "LEVEL_CLEARED1",
            }));
            CountTitle.SetString(string.Empty);
            dataValue.SetString(string.Empty);
            Score.SetString(string.Empty);
            SetCountAlpha(0f, score: true);

            Timeline shut = Folds.RootTimeline(ShowTimeline);
            shut?.delegateTimelineDelegate = this;
            Folds.Play(ShowTimeline);
            Panel.Play(ShowTimeline);
            popContent.PlayTimeline(0);
            SetEnabled(true);
            Layout(VisibleBounds);
            SoundMgr.PlaySound(Resources.Snd.ResultOpenTimeTravel);
            if (shut == null)
            {
                TimelineFinished(null);
            }
        }

        /// <summary>Takes the screen away at once, as iOS does.</summary>
        public void Dismiss()
        {
            SetEnabled(false);
            counting = false;
        }

        /// <summary>Lays the screen out for the current viewport.</summary>
        /// <param name="visible">The logical region the viewport exposes.</param>
        public void Layout(Rectangle visible)
        {
            width = (int)MathF.Ceiling(visible.w);
            height = (int)MathF.Ceiling(visible.h);
            TimeTravelScreen screen = new(visible);
            Overlay.Layout(screen);
            if (Folds == null)
            {
                return;
            }

            // The blind is stretched across the whole screen, as iOS stretched it.
            float stretchX = visible.w / definition.StageWidth;
            float stretchY = visible.h / definition.StageHeight;
            Folds.Root.scaleX = stretchX;
            Folds.Root.scaleY = stretchY;
            Folds.Root.x = -(Folds.Root.width >> 1) * (1f - stretchX);
            Folds.Root.y = -(Folds.Root.height >> 1) * (1f - stretchY);

            Vector origin = screen.ToDesign(0f, 0f);
            Panel.Place(screen.Scale, 0f, 0f, origin.X, origin.Y);
            improvedBanner.Place(screen.Scale, 0f, 0f, origin.X, origin.Y);
            chapterCapsule.Place(screen.Scale, 0f, 0f, origin.X, origin.Y);
        }

        /// <inheritdoc />
        public override void Update(float delta)
        {
            base.Update(delta);
            if (!visible || !shown)
            {
                return;
            }
            shownFor += delta;
            float showLength = Folds?.RootTimeline(ShowTimeline)?.Duration ?? 0f;
            while (starsFilled < result.StarsCollected && starsFilled < stars.Count
                && shownFor >= showLength + (starsFilled * StarStep))
            {
                FillStar(starsFilled);
                starsFilled++;
            }
            while (starBursts < starsFilled && shownFor >= showLength + (starBursts * StarStep) + 0.3f)
            {
                Vector at = AtRest(new Vector(StarSlots[starBursts].x, StarSlots[starBursts].y));
                TimeTravelStarParticles burst = new TimeTravelStarParticles().Init();
                burst.x = at.X;
                burst.y = at.Y;
                _ = popContent.AddChild(burst);
                burst.StartSystem(15);
                burst.StopSystem();
                burst.Update(0f);
                starBursts++;
            }
            if (counting)
            {
                AdvanceCount(delta);
            }
        }

        /// <inheritdoc />
        public void TimelinereachedKeyFramewithIndex(Timeline t, KeyFrame k, int i)
        {
        }

        /// <inheritdoc />
        public void TimelineFinished(Timeline t)
        {
            if (t == chapterCapsule?.RootTimeline(0))
            {
                chapterCapsule.Play(1);
                return;
            }
            counting = true;
            countState = -1;
            Shut?.Invoke();
        }

        /// <summary>Whether a part is one of the blind's halves.</summary>
        /// <param name="part">The part.</param>
        /// <returns><see langword="true"/> for the blind.</returns>
        private static bool IsFold(FlashXmlPartDefinition part)
        {
            return part.Name is "Fold 1" or "Fold 2";
        }

        /// <summary>Whether a part belongs to a star.</summary>
        /// <param name="part">The part.</param>
        /// <returns><see langword="true"/> for a star's parts.</returns>
        private static bool IsStarPart(FlashXmlPartDefinition part)
        {
            return part.Name is "Star" or "Star On" or "circle";
        }

        /// <summary>The middle of a canvas rect.</summary>
        /// <param name="box">The rect.</param>
        /// <returns>Its middle.</returns>
        private static Vector Middle(Rectangle box)
        {
            return new Vector(box.x + (box.w / 2f), box.y + (box.h / 2f));
        }

        /// <summary>A canvas point in asset pixels.</summary>
        /// <param name="canvas">The point, in canvas pixels.</param>
        /// <returns>The point in asset pixels.</returns>
        private static Vector ToAsset(Vector canvas)
        {
            return new Vector(canvas.X * TimeTravelArt.CanvasToAsset, canvas.Y * TimeTravelArt.CanvasToAsset);
        }

        /// <summary>A canvas point on the display, moved to where the display comes to rest.</summary>
        /// <param name="canvas">The point, in canvas pixels.</param>
        /// <returns>The point in asset pixels.</returns>
        private static Vector AtRest(Vector canvas)
        {
            return ToAsset(new Vector(canvas.X + DisplayRest.X, canvas.Y + DisplayRest.Y));
        }

        /// <summary>Makes one of the count's lines.</summary>
        /// <param name="font">Its font.</param>
        /// <param name="textAnchor">Its anchor.</param>
        /// <returns>The line.</returns>
        private Text CreateCountText(string font, sbyte textAnchor)
        {
            Text text = new Text().InitWithFont(Application.GetFont(font));
            text.colorOverride = RGBAColor.solidOpaqueRGBA;
            text.anchor = textAnchor;
            text.parentAnchor = 9;
            _ = countContent.AddChild(text);
            return text;
        }

        /// <summary>Adds fitted black text directly on the bright notification artwork.</summary>
        private static void AddNoticeText(Image art, Rectangle artBox, Rectangle textBox, string key, string name)
        {
            float unit = 0.5f;
            float boxWidth = textBox.w * unit;
            float boxHeight = textBox.h * unit;
            BaseElement notice = new()
            {
                width = (int)boxWidth,
                height = (int)boxHeight,
                anchor = 18,
                parentAnchor = 9,
                x = (textBox.x + (textBox.w / 2f) - artBox.x) * unit,
                y = (textBox.y + (textBox.h / 2f) - artBox.y) * unit,
            };
            FontGeneric font = Application.GetFont(Resources.Fnt.SmallFont);
            Text text = new Text().InitWithFont(font);
            text.SetName(name);
            text.colorOverride = RGBAColor.MakeRGBA(0f, 0f, 0f, 1f);
            text.anchor = text.parentAnchor = 18;
            string message = Application.GetString(key);
            text.SetAlignment(2);
            text.SetStringandWidth(message, MathF.Max(boxWidth - 16f, font.StringWidth(message) * 0.65f));
            float renderedWidth = MathF.Max(text.width, text.Lines.Max(line => line.width));
            float scale = MathF.Min(1f, MathF.Min((boxWidth - 16f) / MathF.Max(1f, renderedWidth), (boxHeight - 32f) / MathF.Max(1f, text.height)));
            text.scaleX = text.scaleY = scale;
            // SmallFont's leading padding shifts the visible lettering below its centered box.
            // Remove that scaled padding so the lettering sits in the artwork's text marker.
            text.y = -font.GetTopSpacing() * scale;
            _ = notice.AddChild(text);
            _ = art.AddChild(notice);
        }

        /// <summary>
        /// The level grid, replay and next buttons on their iOS markers, where the display comes
        /// to rest. A custom level keeps only replay.
        /// </summary>
        private void AddButtons()
        {
            List<(string Sheet, int Icon, GameControllerButtonId Id, Rectangle Marker)> row = CustomLevelSession.IsActive
                ? [(Resources.Img.ResultScreenTimeTravel, TimeTravelArt.ResultRestartIcon, GameControllerButtonId.ExitFromLose, TimeTravelArt.ResultMarkers.Restart)]
                :
                [
                    (Resources.Img.ResultScreenTimeTravel, TimeTravelArt.ResultMenuIcon, GameControllerButtonId.ExitFromWin, TimeTravelArt.ResultMarkers.Menu),
                    (Resources.Img.ResultScreenTimeTravel, TimeTravelArt.ResultRestartIcon, GameControllerButtonId.ExitFromLose, TimeTravelArt.ResultMarkers.Restart),
                    (Resources.Img.ResultScreenTimeTravel, TimeTravelArt.ResultNextIcon, GameControllerButtonId.NextLevel, TimeTravelArt.ResultMarkers.Next),
                ];
            foreach ((string sheet, int icon, GameControllerButtonId id, Rectangle marker) in row)
            {
                Button button = GameController.CreateTimeTravelRoundButton(sheet, icon, id, buttons);
                Vector at = AtRest(new Vector(marker.x, marker.y));
                button.x = at.X;
                button.y = at.Y;
                _ = popContent.AddChild(button);
            }
        }

        /// <summary>Pops the display's buttons, stars and title in as the display pops.</summary>
        private void AddPopTimeline()
        {
            Timeline pop = new Timeline().InitWithMaxKeyFramesOnTrack(DisplayPop.Length);
            foreach ((float seconds, float scale, bool shown, KeyFrame.TransitionType step) in DisplayPop)
            {
                pop.AddKeyFrame(KeyFrame.MakeScale(scale, scale, step, seconds));
                pop.AddKeyFrame(KeyFrame.MakeColor(shown ? RGBAColor.solidOpaqueRGBA : RGBAColor.transparentRGBA, KeyFrame.TransitionType.FRAME_TRANSITION_IMMEDIATE, seconds));
            }
            popContent.AddTimelinewithID(pop, 0);
        }

        /// <summary>Fills a star, with its chime.</summary>
        /// <param name="index">Which star, from the left.</param>
        private void FillStar(int index)
        {
            TimeTravelFlashStage star = stars[index];
            star.Root.updateable = true;
            star.Play(StarTimeline);
            SoundMgr.PlaySound(index switch
            {
                0 => Resources.Snd.ResultStar1TimeTravel,
                1 => Resources.Snd.ResultStar2TimeTravel,
                _ => Resources.Snd.ResultStar3TimeTravel,
            });
        }

        /// <summary>Sets how opaque the count's lines are.</summary>
        /// <param name="alpha">Opacity.</param>
        /// <param name="score">Whether the score follows too.</param>
        private void SetCountAlpha(float alpha, bool score)
        {
            CountTitle.color.AlphaChannel = alpha;
            dataValue.color.AlphaChannel = alpha;
            if (score)
            {
                Score.color.AlphaChannel = alpha;
            }
        }

        /// <summary>Sets the count's title and figure, centered together on their marker.</summary>
        /// <param name="title">The title.</param>
        /// <param name="value">The figure.</param>
        private void SetCountLine(string title, string value)
        {
            CountTitle.SetString(title);
            dataValue.SetString(value);
            float gap = string.IsNullOrEmpty(value) ? 0f : CountGap;
            float rowWidth = CountTitle.width + gap + dataValue.width;
            Vector middle = ToAsset(Middle(TimeTravelArt.ResultMarkers.Data));
            CountTitle.x = middle.X - (rowWidth / 2f);
            CountTitle.y = middle.Y;
            dataValue.x = middle.X + (rowWidth / 2f);
            dataValue.y = middle.Y;
        }

        /// <summary>Formats seconds as minutes and seconds.</summary>
        /// <param name="seconds">Seconds.</param>
        /// <returns>The time.</returns>
        private static string FormatTime(float seconds)
        {
            int rounded = (int)MathF.Round(seconds);
            return (rounded / 60).ToString(CultureInfo.InvariantCulture) + ":" + (rounded % 60).ToString("D2", CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Counts the star bonus into the score, then the time bonus, then shows the final score,
        /// fading each line in and out as iOS does.
        /// </summary>
        /// <param name="delta">Seconds since the last update.</param>
        private void AdvanceCount(float delta)
        {
            countDelay = MathF.Max(0f, countDelay - delta);
            bool done = countDelay <= 0f;
            float faded = countDelay / CountFade;
            switch (countState)
            {
                case -1:
                    SetCountLine(Application.GetString("STAR_BONUS"), result.StarBonus.ToString(CultureInfo.InvariantCulture));
                    Score.SetString("0");
                    countState = 1;
                    countDelay = CountFade;
                    break;
                case 1:
                    SetCountAlpha(1f - faded, score: true);
                    if (done)
                    {
                        countState = 2;
                        countDelay = CountRun;
                    }
                    break;
                case 2:
                    {
                        float left = countDelay / CountRun;
                        dataValue.SetString(((int)(result.StarBonus * left)).ToString(CultureInfo.InvariantCulture));
                        Score.SetString(((int)((1f - left) * result.StarBonus)).ToString(CultureInfo.InvariantCulture));
                        if (done)
                        {
                            countState = 3;
                            countDelay = CountFade;
                        }
                        break;
                    }
                case 3:
                    SetCountAlpha(faded, score: false);
                    if (done)
                    {
                        SetCountLine(Application.GetString("TIME"), FormatTime(result.ElapsedTime));
                        countState = 4;
                        countDelay = CountFade;
                    }
                    break;
                case 4:
                    SetCountAlpha(1f - faded, score: false);
                    if (done)
                    {
                        countState = 5;
                        countDelay = CountRun;
                    }
                    break;
                case 5:
                    {
                        float left = countDelay / CountRun;
                        dataValue.SetString(FormatTime(result.ElapsedTime * left));
                        Score.SetString(((int)(result.StarBonus + ((1f - left) * result.TimeBonus))).ToString(CultureInfo.InvariantCulture));
                        if (done)
                        {
                            Score.SetString(result.FinalScore.ToString(CultureInfo.InvariantCulture));
                            countState = 6;
                            countDelay = CountFade;
                        }
                        break;
                    }
                case 6:
                    SetCountAlpha(faded, score: false);
                    if (done)
                    {
                        SetCountLine(Application.GetString("FINAL_SCORE"), string.Empty);
                        countState = 7;
                        countDelay = CountFade;
                    }
                    break;
                case 7:
                    SetCountAlpha(1f - faded, score: false);
                    if (done)
                    {
                        countState = 10;
                        if (improved)
                        {
                            improvedBanner.Root.visible = true;
                            improvedBanner.Play(ImprovedTimeline);
                        }
                        ShowNextChapterIfUnlocked();
                    }
                    break;
                default:
                    break;
            }
        }

        /// <summary>Unlocks the next chapter once, after the score count, as native ResultScreen does.</summary>
        private void ShowNextChapterIfUnlocked()
        {
            int next = completedPack + 1;
            if (!allowChapterUnlock || next >= Preferences.GetPacksCount()
                || Preferences.GetUnlockedForPackLevel(next, 0) != UNLOCKEDSTATE.LOCKED
                || Preferences.GetTotalStarsInBox(PackConfig.GetSaveSlot(next)) < PackConfig.GetUnlockStars(next))
            {
                return;
            }
            // Leave the menu's first-visit unlock animation pending.
            Preferences.SetUnlockedForPackLevel(UNLOCKEDSTATE.JUSTUNLOCKED, next, 0);
            chapterCapsule.Root.visible = true;
            chapterCapsule.Play(0);
        }
    }
}
