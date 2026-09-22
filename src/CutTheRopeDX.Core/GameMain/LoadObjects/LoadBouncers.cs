using System.Xml.Linq;

using static CutTheRopeDX.Helpers.ParsingHelpers;

namespace CutTheRopeDX.GameMain
{
    internal sealed partial class GameScene
    {
        /// <summary>
        /// Loads a bouncer object from XML node data
        /// </summary>
        /// <param name="xmlNode">The XML node describing the bouncer.</param>
        /// <param name="scale">The level scale factor applied to object coordinates.</param>
        /// <param name="offsetX">The base X offset applied to loaded objects.</param>
        /// <param name="offsetY">The base Y offset applied to loaded objects.</param>
        /// <param name="mapOffsetX">The additional map X offset applied during loading.</param>
        /// <param name="mapOffsetY">The additional map Y offset applied during loading.</param>
        private void LoadBouncer(XElement xmlNode, float scale, float offsetX, float offsetY, int mapOffsetX, int mapOffsetY)
        {
            float x = (ParseCoordinateIntOrZero(xmlNode.Attribute("x")?.Value) * scale) + offsetX + mapOffsetX;
            float y = (ParseCoordinateIntOrZero(xmlNode.Attribute("y")?.Value) * scale) + offsetY + mapOffsetY;
            int size = ParseIntOrZero(xmlNode.Attribute("size")?.Value);
            float angle = ParseIntOrZero(xmlNode.Attribute("angle")?.Value);
            Bouncer bouncer = new Bouncer().InitWithPosXYWidthAndAngle(x, y, size, angle);
            bouncer.ParseMover(xmlNode);
            bouncers.Add(bouncer);
        }
    }
}
