using System;
using System.Collections.Generic;
using System.Linq;

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
        private readonly Dictionary<string, Image> partsByName = new(StringComparer.Ordinal);

        private TimeTravelFlashStage(FlashXmlStageRoot root)
        {
            Root = root;
        }

        /// <summary>Gets the stage, sized to the animation's stage in its units.</summary>
        public FlashXmlStageRoot Root { get; }

        /// <summary>Gets the animation's parts.</summary>
        public IReadOnlyList<Image> Parts => parts;

        /// <summary>Finds one of the animation's parts by its name.</summary>
        /// <param name="name">The part's name.</param>
        /// <returns>The part, or <see langword="null"/> when the stage has none by that name.</returns>
        public Image Part(string name)
        {
            return partsByName.GetValueOrDefault(name);
        }

        /// <summary>Reads an animation file.</summary>
        /// <param name="xmlFile">Animation file in the animations folder.</param>
        /// <returns>The parsed animation.</returns>
        public static FlashXmlAnimationDefinition Load(string xmlFile)
        {
            return FlashXmlImporter.ParseFile(ContentPaths.GetAnimationXmlAbsolutePath(xmlFile));
        }

        /// <summary>Builds an animation, its parts at their first pose and nothing playing.</summary>
        /// <param name="xmlFile">Animation file in the animations folder.</param>
        /// <param name="sheet">Sheet its parts draw from.</param>
        /// <returns>The stage.</returns>
        public static TimeTravelFlashStage Create(string xmlFile, string sheet)
        {
            return Create(Load(xmlFile), sheet);
        }

        /// <summary>
        /// Builds an animation, or some of its parts, its parts at their first pose and nothing
        /// playing.
        /// </summary>
        /// <param name="definition">The parsed animation.</param>
        /// <param name="sheet">Sheet its parts draw from.</param>
        /// <param name="keep">Which parts to build; every part when <see langword="null"/>.</param>
        /// <param name="withRootTimelines">
        /// Whether to give the stage the animation's own timelines, which time it and say when it
        /// ends; a stage that only follows another one needs none.
        /// </param>
        /// <returns>The stage.</returns>
        public static TimeTravelFlashStage Create(
            FlashXmlAnimationDefinition definition,
            string sheet,
            Func<FlashXmlPartDefinition, bool> keep = null,
            bool withRootTimelines = true)
        {
            FlashXmlAnimationDefinition built = keep == null ? definition : new FlashXmlAnimationDefinition
            {
                StageWidth = definition.StageWidth,
                StageHeight = definition.StageHeight,
                TextureResourceName = definition.TextureResourceName,
                Parts = [.. definition.Parts.Where(keep)],
                RootTimelines = definition.RootTimelines,
                RootTimelineDefinitions = definition.RootTimelineDefinitions,
            };
            FlashXmlStageRoot root = new();
            _ = root.InitWithTexture(Application.GetTexture(sheet));
            root.SetDrawQuad(0);
            root.color = RGBAColor.transparentRGBA;
            root.passColorToChilds = false;
            root.width = (int)MathF.Round(built.StageWidth);
            root.height = (int)MathF.Round(built.StageHeight);
            root.anchor = root.parentAnchor = 9;
            TimeTravelFlashStage stage = new(root);
            FlashXmlTargetAnimationBackend.BuildParts(built, root, stage.parts, -1, -1);
            for (int i = 0; i < built.Parts.Count; i++)
            {
                if (!string.IsNullOrEmpty(built.Parts[i].Name))
                {
                    _ = stage.partsByName.TryAdd(built.Parts[i].Name, stage.parts[i]);
                }
            }
            if (withRootTimelines)
            {
                FlashXmlTargetAnimationBackend.BuildRootTimelines(built, root, -1, -1);
            }
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
