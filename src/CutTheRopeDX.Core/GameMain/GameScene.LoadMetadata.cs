using System;
using System.Collections.Generic;
using System.Xml.Linq;

using CutTheRopeDX.Framework;
using CutTheRopeDX.Framework.Core;
using CutTheRopeDX.Framework.Physics;

using static CutTheRopeDX.Helpers.ParsingHelpers;

namespace CutTheRopeDX.GameMain
{
    /// <summary>Selects the level layers consumed by <see cref="GameScene"/> metadata loading.</summary>
    internal static class LevelMetadataLayerSelection
    {
        /// <summary>
        /// Returns only the first case-insensitive <c>settings</c> layer while retaining every
        /// non-settings layer in document order.
        /// </summary>
        /// <param name="mapNode">Root XML node for the current map.</param>
        /// <returns>Layers to inspect for metadata.</returns>
        public static IEnumerable<XElement> SelectLayers(XElement mapNode)
        {
            bool settingsLayerSelected = false;
            foreach (XElement layer in mapNode.Elements())
            {
                bool isSettingsLayer = layer.Name.LocalName == "layer" && IsSettingsLayer(layer);
                if (isSettingsLayer)
                {
                    if (settingsLayerSelected)
                    {
                        continue;
                    }

                    settingsLayerSelected = true;
                }

                yield return layer;
            }
        }

        private static bool IsSettingsLayer(XElement layer)
        {
            return string.Equals(
                layer.Attribute("name")?.Value,
                "settings",
                StringComparison.OrdinalIgnoreCase);
        }
    }

    internal sealed partial class GameScene
    {
        /// <summary>
        /// Loads all level metadata from XML in a single pass
        /// Extracts map dimensions, Game design settings, and candy positions
        /// </summary>
        /// <param name="mapNode">Root XML node for the current map.</param>
        /// <param name="scale">Level scale factor.</param>
        /// <param name="offsetY">Vertical offset applied to map coordinates.</param>
        /// <param name="offsetX">Computed horizontal offset for map coordinates.</param>
        /// <param name="mapOffsetX">Computed integer map X offset.</param>
        /// <param name="mapOffsetY">Computed integer map Y offset.</param>
        private void LoadAllLevelMetadata(XElement mapNode, float scale, float offsetY, out float offsetX, out int mapOffsetX, out int mapOffsetY)
        {
            offsetX = 0f;
            mapOffsetX = 0;
            mapOffsetY = 0;
            ActivePhysicsConstants.UseMobilePhysicsModel = false;
            ActivePhysicsConstants.UseTimeTravelRocketModel = false;
            Bungee.BUNGEE_REST_LEN = ActivePhysicsConstants.BungeeRestLength;

            RootController rc = Application.SharedRootController();

            // Single pass through XML metadata nodes, ignoring duplicate settings layers.
            foreach (XElement layer in LevelMetadataLayerSelection.SelectLayers(mapNode))
            {
                foreach (XElement node in layer.Elements())
                {
                    switch (node.Name.LocalName)
                    {
                        case "map":
                            mapWidth = ParseFloatOrZero(node.Attribute("width")?.Value);
                            mapHeight = ParseFloatOrZero(node.Attribute("height")?.Value);
                            mapWidth *= scale;
                            mapHeight *= scale;

                            // The level sits centered in the space the camera can show. Deriving
                            // the offset from the camera window rather than the design width is
                            // what lets a wider window reveal more of the level instead of adding
                            // bars beside it.
                            cameraWindow = new Rectangle(
                                0f,
                                0f,
                                MathF.Min(mapWidth, SCREEN_WIDTH),
                                MathF.Min(mapHeight, SCREEN_HEIGHT));
                            offsetX = (SCREEN_WIDTH - mapWidth) / 2f;
                            cameraBounds = new Rectangle(offsetX, 0f, mapWidth, mapHeight);
                            levelName = node.Attribute("levelName")?.Value ?? null;

                            if (PackConfig.GetEarthBg(rc.Pack))
                            {
                                if (mapWidth > SCREEN_WIDTH)
                                {
                                    CreateEarthImageWithOffsetXY(back.width, 0f);
                                }
                                if (mapHeight > SCREEN_HEIGHT)
                                {
                                    CreateEarthImageWithOffsetXY(0f, back.height);
                                }
                                CreateEarthImageWithOffsetXY(0f, 0f);
                            }
                            break;
                        case "gameDesign":
                            mapOffsetX = ParseCoordinateIntOrZero(node.Attribute("mapOffsetX")?.Value);
                            mapOffsetY = ParseCoordinateIntOrZero(node.Attribute("mapOffsetY")?.Value);
                            ropePhysicsSpeed = ParseFloatOrZero(node.Attribute("ropePhysicsSpeed")?.Value);
                            _ = bool.TryParse(node.Attribute("useMobilePhysics")?.Value, out bool useMobilePhysics);
                            ActivePhysicsConstants.UseMobilePhysicsModel = useMobilePhysics;
                            // Time Travel's rocket tuning is a mode of the mobile model, not a peer of
                            // it: the game it comes from is a mobile one, and its authored values are in
                            // the same level coordinates the mobile model works in. A map that asks for
                            // it without mobile physics is ignored rather than half-honoured. Everything
                            // downstream keeps reading UseTimeTravelRocketModel on its own, so the
                            // per-constant gates are unchanged.
                            _ = bool.TryParse(node.Attribute("useTimeTravelRocketPhysics")?.Value, out bool useTimeTravelRocketPhysics);
                            ActivePhysicsConstants.UseTimeTravelRocketModel = useMobilePhysics && useTimeTravelRocketPhysics;
                            Bungee.BUNGEE_REST_LEN = ActivePhysicsConstants.BungeeRestLength;
                            _ = bool.TryParse(node.Attribute("nightLevel")?.Value, out nightLevel);
                            _ = bool.TryParse(node.Attribute("twoParts")?.Value, out bool twoPartsBool);
                            levelAuthorsSplitCandy = twoPartsBool;
                            if (levelAuthorsSplitCandy)
                            {
                                // A split level's primary candy is the split one, so candies[0] is
                                // reserved for the halves this pass is about to parse. Claiming it
                                // here - the settings layer is read before the object layers - is
                                // what lets a level author a split candy and ordinary candies at the
                                // same time: every <candy> element then builds its own whole context
                                // instead of the first one taking the split candy's place.
                                primaryCandyClaimed = true;
                            }
                            waterLevel = ParseFloatOrZero(node.Attribute("water")?.Value);
                            if (waterLevel != 0f)
                            {
                                waterLevel *= scale;
                            }
                            waterSpeed = ParseFloatOrZero(node.Attribute("waterSpeed")?.Value) * scale;
                            if (waterLevel > 0f)
                            {
                                float waterWorldX = offsetX + mapOffsetX;
                                float waterWorldWidth = mapWidth;
                                if (waterWorldWidth < SCREEN_WIDTH)
                                {
                                    waterWorldX = 0f;
                                    waterWorldWidth = SCREEN_WIDTH;
                                }

                                waterLayer = WaterElement.CreateWithWidthHeight(waterWorldWidth, waterLevel);
                                if (waterLayer != null)
                                {
                                    waterLayer.x = waterWorldX;
                                    waterLayer.y = offsetY + mapOffsetY + mapHeight - waterLevel;
                                }
                                else
                                {
                                    // Disable water behavior when the texture atlas is not available.
                                    waterLevel = 0f;
                                    waterSpeed = 0f;
                                }
                            }
                            ropePhysicsSpeed *= ActivePhysicsConstants.RopePhysicsSpeedMultiplier;
                            float globalGravityX = (node.Attribute("globalGravityX") != null) ? ParseFloatOrZero(node.Attribute("globalGravityX")?.Value) : 0f;
                            float globalGravityY = (node.Attribute("globalGravityY") != null) ? ParseFloatOrZero(node.Attribute("globalGravityY")?.Value) : ActivePhysicsConstants.GravityEarthY;
                            gravityState.ConfigureBase(new Vector(globalGravityX, globalGravityY));
                            _ = bool.TryParse(node.Attribute("candiesConnected")?.Value, out candiesConnected);
                            candiesConnectedLength = ParseFloatOrZero(node.Attribute("candiesConnectedLength")?.Value) * scale;
                            candiesConnectedBreakable = GetBoolAttribute(node, "candiesConnectedBreakable", defaultValue: true);
                            break;
                        case "candyL":
                            pendingLeftHalf = CreateSplitHalfBody(
                                CandyBodyRole.LeftHalf,
                                (ParseCoordinateIntOrZero(node.Attribute("x")?.Value) * scale) + offsetX + mapOffsetX,
                                (ParseCoordinateIntOrZero(node.Attribute("y")?.Value) * scale) + offsetY + mapOffsetY);
                            break;
                        case "candyR":
                            pendingRightHalf = CreateSplitHalfBody(
                                CandyBodyRole.RightHalf,
                                (ParseCoordinateIntOrZero(node.Attribute("x")?.Value) * scale) + offsetX + mapOffsetX,
                                (ParseCoordinateIntOrZero(node.Attribute("y")?.Value) * scale) + offsetY + mapOffsetY);
                            break;
                        case "candy":
                            {
                                float cx = (ParseCoordinateIntOrZero(node.Attribute("x")?.Value) * scale) + offsetX + mapOffsetX;
                                float cy = (ParseCoordinateIntOrZero(node.Attribute("y")?.Value) * scale) + offsetY + mapOffsetY;
                                // Key comes straight from XML; null for legacy single-candy packs (never matched).
                                string number = node.Attribute("candyNumber")?.Value;

                                // The first <candy> parsed claims the pre-built primary candy (candies[0])
                                // and takes its key from XML; later <candy> elements are built fresh.
                                CandyContext loaded;
                                if (!primaryCandyClaimed)
                                {
                                    primaryCandyClaimed = true;
                                    candies[0].candyNumber = number;
                                    CandyPoint.pos.X = cx;
                                    CandyPoint.pos.Y = cy;
                                    CandyPoint.prevPos = CandyPoint.pos;
                                    Candy.x = cx;
                                    Candy.y = cy;
                                    loaded = candies[0];
                                }
                                else
                                {
                                    loaded = CreateCandyContext(number, cx, cy);
                                }

                                // Time Travel's flying candy. Its leader is only known once every
                                // candy has loaded, so it is installed after the parse.
                                if (GetBoolAttribute(node, "isDriven", defaultValue: false))
                                {
                                    pendingFlyingCandies.Add(loaded);
                                }
                            }
                            break;
                        default:
                            break;
                    }
                }
            }

            InstallSplitCandyState();
            InstallFlyingCandies();

            // Re-apply per-level collision boxes after metadata is fully parsed, so XML order cannot leak stale mode.
            Candy.bb = GetCandyBoundingBox(Candy);
            foreach (CandyBody body in ActiveCandyBodies())
            {
                if (body.Role != CandyBodyRole.Whole)
                {
                    body.Visual.bb = GetSplitCandyBoundingBox();
                }
            }

            // candiesConnected: join the two candies with a mutual elastic. Both candy points are
            // passed directly as head/tail; Bungee preserves their weights and skips integrating
            // non-owned endpoints.
            if (candiesConnected && candies.Count >= 2)
            {
                ConstrainedPoint connectorHead = candies[0].WholeBody.Point;
                ConstrainedPoint connectorTail = candies[1].WholeBody.Point;
                candyConnector = new Bungee().InitWithHeadAtXYTailAtTXTYandLength(
                    connectorHead, connectorHead.pos.X, connectorHead.pos.Y,
                    connectorTail, connectorTail.pos.X, connectorTail.pos.Y,
                    candiesConnectedLength);
                if (!candiesConnectedBreakable)
                {
                    // The connecting elastic is a chain: renders as a chain and is not finger-cuttable.
                    candyConnector.SetCutOnlyByAxe();
                }
                ropes.RegisterConnector(candyConnector);
            }
        }
    }
}
