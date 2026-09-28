using System.Xml.Linq;

using CutTheRopeDX.Framework;
using CutTheRopeDX.Framework.Core;
using CutTheRopeDX.Framework.Helpers;
using CutTheRopeDX.Framework.Visual;

using static CutTheRopeDX.Helpers.ParsingHelpers;

namespace CutTheRopeDX.GameMain
{
    internal sealed partial class GameScene
    {
        /// <summary>Quad holding Paddington's suitcase on the Christmas support sheet.</summary>
        private const int PaddingtonSupportQuad = 1;

        /// <summary>
        /// Downward nudge applied to the suitcase so Om Nom sits on its lid rather than inside it.
        /// The iOS release offsets by 32/64/128 px across its resolution tiers, all of which are
        /// 32 px in its 205 px quad space; scaled onto this port's 640 px quads that is 100.
        /// </summary>
        private const float PaddingtonSupportOffsetY = 100f;

        /// <summary>Number of platforms on the Experiments support sheet, one per Experiments pack.</summary>
        private const int ExperimentsSupportCount = 8;

        /// <summary>
        /// Loads Om Nom from XML node data
        /// Sets up Om Nom animations, blink animation, and greeting if needed
        /// </summary>
        /// <param name="xmlNode">The XML node describing Om Nom.</param>
        /// <param name="scale">The level scale factor applied to object coordinates.</param>
        /// <param name="offsetX">The base X offset applied to loaded objects.</param>
        /// <param name="offsetY">The base Y offset applied to loaded objects.</param>
        /// <param name="mapOffsetX">The additional map X offset applied during loading.</param>
        /// <param name="mapOffsetY">The additional map Y offset applied during loading.</param>
        private void LoadTarget(XElement xmlNode, float scale, float offsetX, float offsetY, int mapOffsetX, int mapOffsetY)
        {
            int pack = Application.SharedRootController().Pack;

            int targetType = ParseIntOrZero(xmlNode.Attribute("targetType")?.Value ?? string.Empty);

            bool isClassicSkin = OmNomSkinRegistry.IsClassicSkin(
                OmNomSkinRegistry.ResolveTargetSkinIndex(
                    targetType,
                    OmNomSkinRegistry.GetSelectedSkinIndex(),
                    OmNomSkinRegistry.TotalSkinCount));
            bool isPaddington = SpecialEvents.IsJanuary && isClassicSkin;

            bool isPrimaryTarget = targets.Count == 0;
            bool paddingtonGreetingPending =
                isPaddington && isPrimaryTarget && !nightLevel && RootController.IsShowGreeting();

            (string supportResource, int requestedQuad) = ResolveSupport(pack, isPaddington, MenuTheme.IsExperiments);

            // Clamp quad index to valid range; fall back to first quad for invalid values.
            Texture2D supportTexture = Application.GetTexture(supportResource);
            int quadIndex = (requestedQuad >= 0 && requestedQuad < supportTexture.quadRects.Length) ? requestedQuad : 0;

            support = Image.FromResource(supportResource, quadIndex);
            support.DoRestoreCutTransparency();
            support.anchor = 18;

            ITargetAnimationBackend animation = TargetAnimationBackendFactory.CreateForTarget(
                targetType, nightLevel, SpecialEvents.IsXmas, isPaddington, paddingtonGreetingPending, MenuTheme.IsExperiments);
            GameObject targetObj = animation.TargetObject;
            targetBaseScaleX = animation.GetTargetBaseScaleX();
            targetBaseScaleY = animation.GetTargetBaseScaleY();
            targetObj.scaleX = targetBaseScaleX;
            targetObj.scaleY = targetBaseScaleY;

            string xAttribute = xmlNode.Attribute("x")?.Value ?? string.Empty;
            int sourceX = ParseCoordinateIntOrZero(xAttribute);
            float transformedX = (sourceX * scale) + offsetX + mapOffsetX;
            targetObj.x = support.x = transformedX;

            string yAttribute = xmlNode.Attribute("y")?.Value ?? string.Empty;
            int sourceY = ParseCoordinateIntOrZero(yAttribute);
            float transformedY = (sourceY * scale) + offsetY + mapOffsetY;
            targetObj.y = support.y = transformedY;
            if (isPaddington)
            {
                support.y += PaddingtonSupportOffsetY;
            }

            // Mouth hitbox, center-relative so skins of any size keep the same mouth line.
            // Desktop: derived from classic char_animations (640x640): bb = (264, 350, 108, 2).
            // Mobile: WP7 bb (90, 110, 25, 1) scaled x3 onto the same 640x640 sheet = (270, 330, 75, 3).
            targetObj.bb = ActivePhysicsConstants.UseMobilePhysicsModel
                ? MakeRectangle((targetObj.width >> 1) - 50f, (targetObj.height >> 1) + 10f, 75f, 3f)
                : MakeRectangle((targetObj.width >> 1) - 56f, (targetObj.height >> 1) + 30f, 108f, 2f);

            animation.Initialize(this);

            // Register this Om Nom as an independent target. targets[0] stays the primary.
            targets.Add(new TargetContext(BLINK_SKIP, RND_RANGE(5, 20))
            {
                animation = animation,
                targetObject = targetObj,
                support = support,
                baseScaleX = targetBaseScaleX,
                baseScaleY = targetBaseScaleY,
            });

            // Show greeting if needed (skip for night levels).
            // Skins with startWithGreeting already play greeting on init, so skip the delayed call.
            if (RootController.IsShowGreeting())
            {
                if (!nightLevel && !animation.StartsWithGreeting)
                {
                    dd.CallObjectSelectorParamafterDelay(new DelayedDispatcher.DispatchFunc(Selector_showGreeting), null, 1.3f);
                }

                RootController.SetShowGreeting(false);
            }

            support = targets[0].support;
            targetBaseScaleX = targets[0].baseScaleX;
            targetBaseScaleY = targets[0].baseScaleY;
        }

        /// <summary>
        /// Picks the platform Om Nom sits on. Paddington seats him on the bear's suitcase. Otherwise
        /// the pack's platform set decides: the Experiments set takes the platform the pack's entry
        /// names or, when it names none that exists, the next in turn by the pack's position.
        /// </summary>
        /// <param name="pack">Pack being played.</param>
        /// <param name="isPaddington">Whether the Paddington greeting is in play.</param>
        /// <param name="isExperiments">Whether the Experiments menus are active.</param>
        /// <returns>The support sheet and the quad to draw from it.</returns>
        internal static (string Resource, int Quad) ResolveSupport(int pack, bool isPaddington, bool isExperiments)
        {
            if (isPaddington)
            {
                return (Resources.Img.CharSupportsXmas, PaddingtonSupportQuad);
            }
            if (ResolveSupportTheme(PackConfig.GetSittingPlatformTheme(pack), isExperiments) == SittingPlatformTheme.Experiments)
            {
                int quad = PackConfig.GetExpSittingPlatform(pack);
                return (Resources.Img.CharSupportExperiments,
                    quad is >= 0 and < ExperimentsSupportCount ? quad : pack % ExperimentsSupportCount);
            }
            return (Resources.Img.CharSupports, PackConfig.GetSittingPlatform(pack));
        }

        /// <summary>
        /// Picks the platform set: the one the pack asks for, else the one matching the menus.
        /// </summary>
        /// <param name="configured">Platform set from the pack's entry, or <see langword="null"/> when unset.</param>
        /// <param name="isExperiments">Whether the Experiments menus are active.</param>
        /// <returns>The platform set to draw from.</returns>
        internal static SittingPlatformTheme ResolveSupportTheme(SittingPlatformTheme? configured, bool isExperiments)
        {
            return configured ?? (isExperiments ? SittingPlatformTheme.Experiments : SittingPlatformTheme.Original);
        }
    }
}
