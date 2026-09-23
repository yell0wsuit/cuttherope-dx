using CutTheRopeDX.Framework;
using CutTheRopeDX.Framework.Core;
using CutTheRopeDX.Framework.Platform;
using CutTheRopeDX.Framework.Visual;

namespace CutTheRopeDX.GameMain
{
    /// <content>
    /// The Cut the Rope: Experiments level transition: a blind, the loading screen's sheet with
    /// the scroll along its bottom edge, rolled up off the level when it opens and down over it
    /// when it closes. The iOS HD <c>-[BoxOpenClose showOpenCloseAnim:]</c>.
    /// </content>
    internal sealed partial class BoxOpenClose
    {
        /// <summary>Seconds the blind takes to roll up or down (iOS 0.65).</summary>
        private const float ExpBlindSeconds = 0.65f;

        /// <summary>
        /// Builds and plays the blind. Laid out in design units inside
        /// <see cref="openCloseAnims"/>, which is cover-fitted to the viewport exactly as the
        /// loading screen fits the same sheet.
        /// </summary>
        /// <param name="open">
        /// <see langword="true"/> to roll the blind up off the level; <see langword="false"/> to
        /// roll it down over it.
        /// </param>
        private void ShowExperimentsBlind(bool open)
        {
            // Coming out of the loading screen, the lit machine it ended on rides up with the sheet.
            BaseElement sheet = LoadingView.CreateExperimentsSheet(boxAnim == 0 ? CreateExperimentsMachine() : null);

            // Rolled up far enough that the pull ring hanging below the rod is gone too.
            float rolledUp = -(ViewportLayout.DesignHeight + LoadingView.ExperimentsSheetOverhang());
            Timeline timeline = new Timeline().InitWithMaxKeyFramesOnTrack(2);
            if (open)
            {
                timeline.AddKeyFrame(KeyFrame.MakePos(0, 0, KeyFrame.TransitionType.FRAME_TRANSITION_EASE_IN, 0));
                timeline.AddKeyFrame(KeyFrame.MakePos(0, rolledUp, KeyFrame.TransitionType.FRAME_TRANSITION_EASE_IN, ExpBlindSeconds));
            }
            else
            {
                timeline.AddKeyFrame(KeyFrame.MakePos(0, rolledUp, KeyFrame.TransitionType.FRAME_TRANSITION_EASE_OUT, 0));
                timeline.AddKeyFrame(KeyFrame.MakePos(0, 0, KeyFrame.TransitionType.FRAME_TRANSITION_EASE_OUT, ExpBlindSeconds));
            }
            timeline.delegateTimelineDelegate = this;
            sheet.AddTimelinewithID(timeline, 0);
            sheet.PlayTimeline(0);
            _ = openCloseAnims.AddChild(sheet);
        }

        /// <summary>
        /// Builds the porthole machine in the state the loading screen leaves it: lamps lit, the
        /// candy filled, the key still turning. Placed where the loading screen draws it, carried
        /// into the blind's cover-fitted space so the handover does not jump.
        /// </summary>
        /// <returns>The machine, in the sheet's coordinates.</returns>
        private static BaseElement CreateExperimentsMachine()
        {
            Rectangle visible = VisibleBounds;
            Rectangle covered = LayoutMath.CoverInside(ViewportLayout.DesignWidth, ViewportLayout.DesignHeight, visible);
            float coverScale = covered.w / ViewportLayout.DesignWidth;
            ExperimentsFramePlacement frame = LoadingView.ExperimentsFrame(visible);
            float scale = frame.Scale / coverScale;

            string atlas = Resources.Img.MenuExpLoading;
            Vector frameSize = Application.GetTexture(atlas).preCutSize;
            BaseElement machine = new()
            {
                width = (int)frameSize.X,
                height = (int)frameSize.Y,
                scaleX = scale,
                scaleY = scale,
            };
            machine.anchor = machine.parentAnchor = 9;

            // Scaled about its own center, so the drift that puts on its top left comes back out.
            machine.x = ((frame.X - covered.x) / coverScale) - (machine.width / 2f * (1f - scale));
            machine.y = ((frame.Y - covered.y) / coverScale) - (machine.height / 2f * (1f - scale));

            foreach (int quad in new[] { 2, 3, 7, 1, 5 })
            {
                _ = machine.AddChild(CreateMachinePart(Image.FromResource(atlas, quad)));
            }

            Vector fillOffset = Image.GetQuadOffset(atlas, 5);
            Vector fillSize = Image.GetQuadSize(atlas, 5);
            Image line = Image.FromResource(atlas, 4);
            line.anchor = line.parentAnchor = 9;
            line.x = fillOffset.X + ((fillSize.X - line.width) / 2f);
            line.y = fillOffset.Y - (line.height / 2f);
            _ = machine.AddChild(line);

            _ = machine.AddChild(CreateMachinePart(Image.FromResource(atlas, 6)));
            Animation key = CreateMachinePart(Image.InitializeFromResource(new Animation(), atlas, 8));
            int keyAnimation = key.AddAnimationDelayLoopFirstLast(0.05f, Timeline.LoopType.TIMELINE_REPLAY, 8, 16);
            key.PlayTimeline(keyAnimation);
            _ = machine.AddChild(key);
            return machine;
        }

        /// <summary>
        /// Readies a piece of the machine to draw at its place in the loading atlas's frame.
        /// </summary>
        /// <typeparam name="T">Image type.</typeparam>
        /// <param name="part">The piece.</param>
        /// <returns><paramref name="part"/>.</returns>
        private static T CreateMachinePart<T>(T part)
            where T : Image
        {
            part.DoRestoreCutTransparency();
            part.anchor = part.parentAnchor = 9;
            return part;
        }
    }
}
