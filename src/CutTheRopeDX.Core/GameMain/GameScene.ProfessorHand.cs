using System;

using CutTheRopeDX.Framework.Core;
using CutTheRopeDX.Framework.Visual;

namespace CutTheRopeDX.GameMain
{
    /// <content>
    /// The Cut the Rope: Experiments professor's hand, which lowers the candy into place on the
    /// first level of a pack and lets go of it. The iOS HD <c>-[GameScene show]</c> hand block
    /// and its <c>handContainer</c> keyframe callback.
    /// </content>
    internal sealed partial class GameScene
    {
        /// <summary>Hand atlas quads: the hand, the marker where the candy sits, and the sleeve.</summary>
        private const int HandQuadHand = 0;
        private const int HandQuadCandyMarker = 1;
        private const int HandQuadSleeve = 2;

        /// <summary>Seconds the hand waits above the screen before lowering (iOS 0.5).</summary>
        private const float HandWaitSeconds = 0.5f;

        /// <summary>Seconds the hand takes to lower the candy, and to rise again after (iOS 0.7).</summary>
        private const float HandMoveSeconds = 0.7f;

        /// <summary>Seconds the hand stays down after letting go before rising (iOS 0.4).</summary>
        private const float HandLingerSeconds = 0.4f;

        /// <summary>Scale the candy is drawn at, as everywhere in play.</summary>
        private const float HandCandyScale = 0.71f;

        /// <summary>The hand and its sleeve, while it is on screen; <see langword="null"/> otherwise.</summary>
        private BaseElement professorHand;

        /// <summary>The copy of the candy the hand carries until it lets go.</summary>
        private BaseElement professorHandCandy;

        /// <summary>The arm above the hand, stretched every frame to the top of the view.</summary>
        private TiledImage professorHandSleeve;

        /// <summary>Height of one sleeve tile, so the arm grows by whole tiles.</summary>
        private float professorHandSleeveTile;

        /// <summary>
        /// Gets whether the hand is still carrying the candy, in which case the real candy is
        /// hidden and the hand draws its copy.
        /// </summary>
        internal bool ProfessorHandHolds { get; private set; }

        /// <summary>
        /// Gets whether the hand is on screen at all. Play is paused until it has risen away
        /// again, so nothing falls, swings or scores while it is still in view.
        /// </summary>
        internal bool ProfessorHandPausesPlay { get; private set; }

        /// <summary>
        /// Starts the hand if this level shows it: the Experiments menus, a level with one whole
        /// candy, the first level of its pack, opened fresh rather than restarted, and not from
        /// the level picker. iOS <c>-[GameScene shouldShowHandAnimation]</c>.
        /// </summary>
        private void StartProfessorHand()
        {
            professorHand = null;
            professorHandCandy = null;
            professorHandSleeve = null;
            ProfessorHandHolds = false;
            ProfessorHandPausesPlay = false;

            RootController root = Application.SharedRootController();
            if (!MenuTheme.IsExperiments
                || levelAuthorsSplitCandy
                || candies.Count != 1
                || root.Level != 0
                || root.IsPicker()
                || CustomLevelSession.IsActive
                || gameplayFlow.Phase != RestartPhase.Playing)
            {
                return;
            }

            string atlas = Resources.Img.ProfessorHand;
            Vector handOffset = Image.GetQuadOffset(atlas, HandQuadHand);
            Vector markerOffset = Image.GetQuadOffset(atlas, HandQuadCandyMarker);
            Vector markerSize = Image.GetQuadSize(atlas, HandQuadCandyMarker);
            Vector sleeveOffset = Image.GetQuadOffset(atlas, HandQuadSleeve);

            Image hand = Image.FromResource(atlas, HandQuadHand);
            hand.anchor = hand.parentAnchor = 9;
            BaseElement container = new()
            {
                width = hand.width,
                height = hand.height,
            };
            container.anchor = container.parentAnchor = 9;

            // The candy's center sits on the marker, measured from the hand quad's corner.
            Image candy = CreateProfessorHandCandy();
            candy.parentAnchor = 9;
            candy.anchor = 18;
            candy.x = markerOffset.X + (markerSize.X / 2f) - handOffset.X;
            candy.y = markerOffset.Y + (markerSize.Y / 2f) - handOffset.Y;
            _ = container.AddChild(candy);
            _ = container.AddChild(hand);

            // Placed so the marker lands on the candy's resting point, starting a hand's height
            // above whichever is higher: the top of the level, or the top of a viewport taller
            // than the level that shows world above it.
            Vector candyPoint = candies[0].WholeBody.Point.pos;
            float handX = candyPoint.X - (markerOffset.X - handOffset.X);
            float restY = candyPoint.Y - (markerOffset.Y - handOffset.Y);
            float aboveY = MathF.Min(0f, camera.RenderPos.Y) - container.height;

            // The sleeve fills from the top of the view down to the hand, so the arm never ends
            // in mid-air however low the candy starts or however tall the viewport is.
            TiledImage sleeve = Image.InitializeFromResource(new TiledImage(), atlas, HandQuadSleeve);
            sleeve.SetTile(HandQuadSleeve);
            sleeve.anchor = sleeve.parentAnchor = 9;
            sleeve.x = sleeveOffset.X - handOffset.X;
            _ = hand.AddChild(sleeve);
            professorHandSleeve = sleeve;
            professorHandSleeveTile = MathF.Max(1f, Image.GetQuadSize(atlas, HandQuadSleeve).Y);

            container.x = handX;
            container.y = aboveY;
            StretchProfessorHandSleeve();
            Timeline lower = new Timeline().InitWithMaxKeyFramesOnTrack(3);
            lower.AddKeyFrame(KeyFrame.MakePos(handX, aboveY, KeyFrame.TransitionType.FRAME_TRANSITION_LINEAR, 0));
            lower.AddKeyFrame(KeyFrame.MakePos(handX, aboveY, KeyFrame.TransitionType.FRAME_TRANSITION_LINEAR, HandWaitSeconds));
            lower.AddKeyFrame(KeyFrame.MakePos(handX, restY, KeyFrame.TransitionType.FRAME_TRANSITION_EASE_OUT, HandMoveSeconds));
            lower.delegateTimelineDelegate = this;
            container.AddTimelinewithID(lower, 0);

            Timeline rise = new Timeline().InitWithMaxKeyFramesOnTrack(3);
            rise.AddKeyFrame(KeyFrame.MakePos(handX, restY, KeyFrame.TransitionType.FRAME_TRANSITION_LINEAR, 0));
            rise.AddKeyFrame(KeyFrame.MakePos(handX, restY, KeyFrame.TransitionType.FRAME_TRANSITION_LINEAR, HandLingerSeconds));
            rise.AddKeyFrame(KeyFrame.MakePos(handX, aboveY, KeyFrame.TransitionType.FRAME_TRANSITION_EASE_IN, HandMoveSeconds));
            rise.delegateTimelineDelegate = this;
            container.AddTimelinewithID(rise, 1);

            _ = aniPool.AddChild(container);
            container.PlayTimeline(0);
            professorHand = container;
            professorHandCandy = candy;
            ProfessorHandHolds = true;
            ProfessorHandPausesPlay = true;
        }

        /// <summary>
        /// Stretches the sleeve up from the hand to the top of the visible world, which on a
        /// viewport taller than the level lies above the level's own top. Grows by whole tiles
        /// measured from the hand, so the tiling where the arm meets the hand never crawls as
        /// the hand moves or the viewport resizes.
        /// </summary>
        private void StretchProfessorHandSleeve()
        {
            if (professorHand == null || professorHandSleeve == null)
            {
                return;
            }

            float visibleTop = camera.RenderPos.Y;
            float reach = professorHand.y - visibleTop;
            int tiles = reach > 0f ? (int)MathF.Ceiling(reach / professorHandSleeveTile) : 0;
            professorHandSleeve.height = (int)(tiles * professorHandSleeveTile);
            professorHandSleeve.y = -professorHandSleeve.height;
            professorHandSleeve.SetEnabled(tiles > 0);
        }

        /// <summary>
        /// Builds the candy the hand carries, from the same skin and layers as the real one.
        /// </summary>
        /// <returns>The candy, centered on its own origin.</returns>
        private static Image CreateProfessorHandCandy()
        {
            string candyResource = CandySkinHelper.GetCandyResource(Preferences.GetIntForKey("PREFS_SELECTED_CANDY"));
            Image candy = Image.FromResource(candyResource, 0);
            candy.DoRestoreCutTransparency();
            candy.scaleX = candy.scaleY = HandCandyScale;
            candy.passTransformationsToChilds = false;
            for (int quad = 1; quad <= 2; quad++)
            {
                Image layer = Image.FromResource(candyResource, quad);
                layer.DoRestoreCutTransparency();
                layer.anchor = layer.parentAnchor = 18;
                layer.scaleX = layer.scaleY = HandCandyScale;
                _ = candy.AddChild(layer);
            }
            return candy;
        }

        /// <summary>
        /// Follows the hand's two timelines. When it has lowered the candy it lets go: the real
        /// candy takes over where the carried one was and the hand rises away. When it has risen
        /// off the screen it is removed and play resumes.
        /// </summary>
        /// <param name="t">The timeline that finished.</param>
        /// <returns><see langword="true"/> when <paramref name="t"/> belonged to the hand.</returns>
        private bool TryAdvanceProfessorHand(Timeline t)
        {
            if (professorHand == null || t.element != professorHand)
            {
                return false;
            }

            if (ProfessorHandHolds)
            {
                ProfessorHandHolds = false;
                professorHandCandy.SetEnabled(false);
                professorHand.PlayTimeline(1);
                return true;
            }

            ProfessorHandPausesPlay = false;
            aniPool.TimelineFinished(t);
            professorHand = null;
            professorHandCandy = null;
            professorHandSleeve = null;
            return true;
        }
    }
}
