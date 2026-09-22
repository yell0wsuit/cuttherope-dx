using System.Collections.Generic;
using System.Xml.Linq;

namespace CutTheRopeDX.GameMain
{
    internal sealed partial class GameScene
    {
        /// <summary>
        /// Loads all game objects from XML map data using a dispatch switch
        /// Iterates through XML nodes and calls appropriate Load* method for each object type
        /// </summary>
        /// <param name="map">The XML map node containing object definitions.</param>
        /// <param name="scale">The level scale factor applied to object coordinates.</param>
        /// <param name="offsetX">The base X offset applied to loaded objects.</param>
        /// <param name="offsetY">The base Y offset applied to loaded objects.</param>
        /// <param name="mapOffsetX">The additional map X offset applied during loading.</param>
        /// <param name="mapOffsetY">The additional map Y offset applied during loading.</param>
        private void LoadObjectsFromMap(XElement map, float scale, float offsetX, float offsetY, int mapOffsetX, int mapOffsetY)
        {
            List<XElement> layers = [.. map.Elements()];
            List<XElement> tutorialNodes = [];
            // Establish captured state before grabs are loaded so XML object order cannot attach a
            // fixed rope to candy that starts inside a lantern.
            if (NormalRopeLoad.CandyStartsInLantern(map))
            {
                _ = candies[0].Lifecycle.Attachments.CaptureInLantern();
            }

            // Preload candy-like auxiliary bodies (light bulbs, axes, bombs) so grabs can resolve them
            // regardless of XML order.
            foreach (XElement layer in layers)
            {
                foreach (XElement node in layer.Elements())
                {
                    switch (node.Name.LocalName)
                    {
                        case "lightBulb":
                        case "lightbulb":
                            LoadLightBulb(node, scale, offsetX + mapOffsetX, offsetY + mapOffsetY, 0, 0);
                            break;
                        case "axe":
                            LoadAxe(node, scale, offsetX + mapOffsetX, offsetY + mapOffsetY, 0, 0);
                            break;
                        case "bomb":
                            LoadBomb(node, scale, offsetX + mapOffsetX, offsetY + mapOffsetY, 0, 0);
                            break;
                        default:
                            break;
                    }
                }
            }
            foreach (XElement layer in layers)
            {
                foreach (XElement node in layer.Elements())
                {
                    switch (node.Name.LocalName)
                    {
                        case "gravitySwitch":
                            LoadGravityButton(node, scale, offsetX + mapOffsetX, offsetY + mapOffsetY, 0, 0);
                            break;
                        case "pauseSwitcher":
                            LoadPauseSwitcher(node, scale, offsetX + mapOffsetX, offsetY + mapOffsetY, 0, 0);
                            break;
                        case "star":
                            LoadStar(node, scale, offsetX + mapOffsetX, offsetY + mapOffsetY, 0, 0);
                            break;
                        case "tutorialText":
                            tutorialNodes.Add(node);
                            break;
                        case "tutorial01":
                        case "tutorial02":
                        case "tutorial03":
                        case "tutorial04":
                        case "tutorial05":
                        case "tutorial06":
                        case "tutorial07":
                        case "tutorial08":
                        case "tutorial09":
                        case "tutorial10":
                        case "tutorial11":
                            tutorialNodes.Add(node);
                            break;
                        case "bubble":
                            LoadBubble(node, scale, offsetX + mapOffsetX, offsetY + mapOffsetY, 0, 0);
                            break;
                        case "pump":
                            LoadPump(node, scale, offsetX + mapOffsetX, offsetY + mapOffsetY, 0, 0);
                            break;
                        case "sock":
                            LoadSock(node, scale, offsetX + mapOffsetX, offsetY + mapOffsetY, 0, 0);
                            break;
                        case "spike1":
                        case "spike2":
                        case "spike3":
                        case "spike4":
                        case "electro":
                            LoadSpike(node, scale, offsetX + mapOffsetX, offsetY + mapOffsetY, 0, 0);
                            break;
                        case "rotatedCircle":
                            LoadRotatedCircle(node, scale, offsetX + mapOffsetX, offsetY + mapOffsetY, 0, 0);
                            break;
                        case "bouncer1":
                        case "bouncer2":
                            LoadBouncer(node, scale, offsetX + mapOffsetX, offsetY + mapOffsetY, 0, 0);
                            break;
                        case "load":
                            LoadSnail(node, scale, offsetX + mapOffsetX, offsetY + mapOffsetY, 0, 0);
                            break;
                        case "grab":
                            LoadGrab(node, scale, offsetX + mapOffsetX, offsetY + mapOffsetY, 0, 0);
                            break;
                        case "target":
                            LoadTarget(node, scale, offsetX + mapOffsetX, offsetY + mapOffsetY, 0, 0);
                            break;
                        case "steamTube":
                            LoadSteamTube(node, scale, offsetX + mapOffsetX, offsetY + mapOffsetY, 0, 0);
                            break;
                        case "pipe":
                            LoadBambooTube(node, scale, offsetX + mapOffsetX, offsetY + mapOffsetY, 0, 0);
                            break;
                        case "ants":
                            LoadAnts(node, scale, offsetX + mapOffsetX, offsetY + mapOffsetY, 0, 0);
                            break;
                        case "ghost":
                            LoadGhost(node, scale, offsetX + mapOffsetX, offsetY + mapOffsetY, 0, 0);
                            break;
                        case "rocket":
                            LoadRocket(node, scale, offsetX + mapOffsetX, offsetY + mapOffsetY, 0, 0);
                            break;
                        case "hand":
                            LoadHand(node, scale, offsetX + mapOffsetX, offsetY + mapOffsetY, 0, 0);
                            break;
                        case "conveyorBelt":
                        case "transporter":
                            LoadConveyorBelt(node, scale, offsetX + mapOffsetX, offsetY + mapOffsetY, 0, 0);
                            break;
                        case "lantern":
                            LoadLantern(node, scale, offsetX + mapOffsetX, offsetY + mapOffsetY, 0, 0);
                            break;
                        case "lightBulb":
                        case "lightbulb":
                        case "axe":
                        case "bomb":
                            // Preloaded above.
                            break;
                        case "gap":
                        case "mouse":
                            LoadMouse(node, scale, offsetX + mapOffsetX, offsetY + mapOffsetY, 0, 0);
                            break;
                        default:
                            break;
                    }
                }
            }
            LoadTutorials(tutorialNodes, scale, offsetX, offsetY, mapOffsetX, mapOffsetY);
        }
    }
}
