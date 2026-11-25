using System.Collections.Generic;

using CutTheRopeDX.Framework.Core;
using CutTheRopeDX.Framework.Visual;

namespace CutTheRopeDX.GameMain
{
    /// <summary>
    /// One Time Travel scene, authored in asset pixels from the iOS canvas origin. Each layout pass
    /// contains it in the viewport and moves its attached children with the visible edges, the way
    /// <c>ScreenSizeMgr</c> did on iOS. Touches are mapped back through its scale as a
    /// <see cref="FittedGroup"/> maps them, so they land on what is drawn.
    /// </summary>
    internal sealed class TimeTravelSceneGroup : FittedGroup
    {
        /// <summary>Scene width in asset pixels.</summary>
        public const int Width = 998;

        /// <summary>Scene height in asset pixels.</summary>
        public const int Height = 1498;

        private readonly List<(BaseElement Child, TimeTravelAttach Flags, float X, float Y, float ScaleX, float ScaleY)> attached = [];

        /// <summary>Initializes a new instance of the <see cref="TimeTravelSceneGroup"/> class.</summary>
        public TimeTravelSceneGroup()
        {
            width = Width;
            height = Height;
            anchor = parentAnchor = 9;
        }

        /// <summary>
        /// Registers a child, already placed at its authored position, to follow the visible edges.
        /// </summary>
        /// <param name="child">A child of this group.</param>
        /// <param name="flags">Edges and full-screen scales it follows.</param>
        public void Attach(BaseElement child, TimeTravelAttach flags)
        {
            attached.Add((child, flags, child.x, child.y, child.scaleX, child.scaleY));
        }

        /// <summary>Contains the scene in the viewport and re-applies every attachment.</summary>
        /// <param name="screen">The screen model for the current viewport.</param>
        public void Layout(TimeTravelScreen screen)
        {
            float scale = screen.AssetScale;
            Vector origin = screen.ToDesign(0f, 0f);
            scaleX = scaleY = scale;

            // Scaled about its center like every element, so the center's travel is taken back out.
            x = origin.X - ((width >> 1) * (1f - scale));
            y = origin.Y - ((height >> 1) * (1f - scale));

            float assetPerLogical = FlashXmlScale.AtlasToFlashPointScale;
            foreach ((BaseElement child, TimeTravelAttach flags, float baseX, float baseY, float baseScaleX, float baseScaleY) in attached)
            {
                Vector pinned = screen.Pin(baseX / assetPerLogical, baseY / assetPerLogical, flags);
                child.x = pinned.X * assetPerLogical;
                child.y = pinned.Y * assetPerLogical;
                Vector full = screen.FullScale(flags);
                child.scaleX = baseScaleX * full.X;
                child.scaleY = baseScaleY * full.Y;
            }
        }
    }
}
