using System;
using System.Collections.Generic;

using CutTheRopeDX.Framework;
using CutTheRopeDX.Framework.Core;
using CutTheRopeDX.Framework.Visual;
using CutTheRopeDX.Helpers;

namespace CutTheRopeDX.GameMain
{
    /// <summary>
    /// One of the Time Travel menus' Flash animations, built on its own stage. The stage is in the
    /// animation's own units, the iOS logical units, so its scale is what carries it into the
    /// space it is drawn in.
    /// </summary>
    internal sealed class TimeTravelFlashStage
    {
        private readonly List<Image> parts = [];

        private TimeTravelFlashStage(FlashXmlStageRoot root)
        {
            Root = root;
        }

        /// <summary>Gets the stage, sized to the animation's stage in its units.</summary>
        public FlashXmlStageRoot Root { get; }

        /// <summary>Gets the animation's parts.</summary>
        public IReadOnlyList<Image> Parts => parts;

        /// <summary>Builds an animation, its parts at their first pose and nothing playing.</summary>
        /// <param name="xmlFile">Animation file in the animations folder.</param>
        /// <param name="sheet">Sheet its parts draw from.</param>
        /// <returns>The stage.</returns>
        public static TimeTravelFlashStage Create(string xmlFile, string sheet)
        {
            FlashXmlAnimationDefinition definition = FlashXmlImporter.ParseFile(ContentPaths.GetAnimationXmlAbsolutePath(xmlFile));
            FlashXmlStageRoot root = new();
            _ = root.InitWithTexture(Application.GetTexture(sheet));
            root.SetDrawQuad(0);
            root.color = RGBAColor.transparentRGBA;
            root.passColorToChilds = false;
            root.width = (int)MathF.Round(definition.StageWidth);
            root.height = (int)MathF.Round(definition.StageHeight);
            root.anchor = root.parentAnchor = 9;
            TimeTravelFlashStage stage = new(root);
            FlashXmlTargetAnimationBackend.BuildParts(definition, root, stage.parts, -1, -1);
            FlashXmlTargetAnimationBackend.BuildRootTimelines(definition, root, -1, -1);
            return stage;
        }

        /// <summary>Gets one of the animation's own timelines, which sets its length.</summary>
        /// <param name="timelineId">Timeline to get.</param>
        /// <returns>The timeline, or <see langword="null"/> when the animation has none by that id.</returns>
        public Timeline RootTimeline(int timelineId)
        {
            return Root.GetTimeline(timelineId);
        }

        /// <summary>Starts one of the animation's timelines on every part.</summary>
        /// <param name="timelineId">Timeline to play.</param>
        public void Play(int timelineId)
        {
            FlashXmlTargetAnimationBackend.PlayTimeline(parts, timelineId);
            FlashXmlTargetAnimationBackend.PlayRootTimeline(Root, timelineId);
        }

        /// <summary>
        /// Scales the stage and places it so the stage point <paramref name="stageX"/>,
        /// <paramref name="stageY"/> lands at <paramref name="x"/>, <paramref name="y"/> of its
        /// parent's top left. The stage scales about its own center like every element, so its
        /// position is solved for that point.
        /// </summary>
        /// <param name="scale">Parent units per stage unit.</param>
        /// <param name="stageX">Stage point X.</param>
        /// <param name="stageY">Stage point Y.</param>
        /// <param name="x">Where it lands, X.</param>
        /// <param name="y">Where it lands, Y.</param>
        public void Place(float scale, float stageX, float stageY, float x, float y)
        {
            Root.scaleX = Root.scaleY = scale;
            float halfWidth = Root.width >> 1;
            float halfHeight = Root.height >> 1;
            Root.x = x - halfWidth - ((stageX - halfWidth) * scale);
            Root.y = y - halfHeight - ((stageY - halfHeight) * scale);
        }
    }
}
