using System;

using CutTheRopeDX.Framework;
using CutTheRopeDX.Framework.Core;

namespace CutTheRopeDX.GameMain
{
    /// <summary>
    /// How a Time Travel element follows the screen, as the flags passed to
    /// <c>ScreenSizeMgr::attach</c>.
    /// </summary>
    [Flags]
    internal enum TimeTravelAttach
    {
        /// <summary>Stays where the scene puts it.</summary>
        None = 0,

        /// <summary>Follows the visible left edge.</summary>
        Left = 0x01,

        /// <summary>Follows the visible right edge.</summary>
        Right = 0x04,

        /// <summary>Follows the visible top edge.</summary>
        Top = 0x08,

        /// <summary>Follows the visible bottom edge.</summary>
        Bottom = 0x20,

        /// <summary>Widens by the visible width over the scene width.</summary>
        ScaleToFullX = 0x400,

        /// <summary>Heightens by the visible height over the scene height.</summary>
        ScaleToFullY = 0x800,
    }

    /// <summary>
    /// Time Travel's screen model: the 640 by 960 logical scene contained in the visible rect,
    /// with the margin either side of it exposed and reachable by pinned elements.
    /// </summary>
    /// <param name="visible">The logical region the viewport exposes, in design units.</param>
    internal readonly struct TimeTravelScreen(Rectangle visible)
    {
        /// <summary>Width of the logical scene.</summary>
        public const float SceneWidth = 640f;

        /// <summary>Height of the logical scene.</summary>
        public const float SceneHeight = 960f;

        private readonly Rectangle visible = visible;

        /// <summary>Gets the design units per logical unit.</summary>
        public float Scale => MathF.Min(visible.w / SceneWidth, visible.h / SceneHeight);

        /// <summary>Gets the horizontal margin each side of the scene, in logical units.</summary>
        public float OffsetX => ((visible.w / Scale) - SceneWidth) / 2f;

        /// <summary>Gets the vertical margin above and below the scene, in logical units.</summary>
        public float OffsetY => ((visible.h / Scale) - SceneHeight) / 2f;

        /// <summary>Gets the visible width in logical units.</summary>
        public float FullWidth => SceneWidth + (2f * OffsetX);

        /// <summary>Gets the visible height in logical units.</summary>
        public float FullHeight => SceneHeight + (2f * OffsetY);

        /// <summary>Gets the scale that puts one atlas pixel of a Time Travel sheet on the scene.</summary>
        public float AssetScale => Scale / FlashXmlScale.AtlasToFlashPointScale;

        /// <summary>Maps a logical point to design units.</summary>
        /// <param name="lx">Logical X.</param>
        /// <param name="ly">Logical Y.</param>
        /// <returns>The point in design units.</returns>
        public Vector ToDesign(float lx, float ly)
        {
            return new Vector(visible.x + ((OffsetX + lx) * Scale), visible.y + ((OffsetY + ly) * Scale));
        }

        /// <summary>Moves a logical point the way the edge flags move an attached element.</summary>
        /// <param name="lx">Logical X the element was authored at.</param>
        /// <param name="ly">Logical Y the element was authored at.</param>
        /// <param name="flags">Edges the element follows.</param>
        /// <returns>The pinned point, in logical units.</returns>
        public Vector Pin(float lx, float ly, TimeTravelAttach flags)
        {
            float x = lx;
            float y = ly;
            if ((flags & TimeTravelAttach.Left) != 0)
            {
                x -= OffsetX;
            }
            else if ((flags & TimeTravelAttach.Right) != 0)
            {
                x += OffsetX;
            }
            if ((flags & TimeTravelAttach.Top) != 0)
            {
                y -= OffsetY;
            }
            else if ((flags & TimeTravelAttach.Bottom) != 0)
            {
                y += OffsetY;
            }
            return new Vector(x, y);
        }

        /// <summary>Returns the per-axis scale the full-screen flags apply.</summary>
        /// <param name="flags">Flags the element was attached with.</param>
        /// <returns>X and Y factors; 1 on an axis the flags leave alone.</returns>
        public Vector FullScale(TimeTravelAttach flags)
        {
            return new Vector(
                (flags & TimeTravelAttach.ScaleToFullX) != 0 ? FullWidth / SceneWidth : 1f,
                (flags & TimeTravelAttach.ScaleToFullY) != 0 ? FullHeight / SceneHeight : 1f);
        }

        /// <summary>
        /// Returns the non-uniform scale that stretches an element across the visible rect, with
        /// its atlas edge bleed kept off-screen as the freeze overlay keeps its plate's.
        /// </summary>
        /// <param name="width">Element width, in design units.</param>
        /// <param name="height">Element height, in design units.</param>
        /// <param name="bleedPerSide">Edge bleed to push past each edge, in the same units.</param>
        /// <param name="visible">The logical region the viewport exposes.</param>
        /// <returns>The X and Y scale.</returns>
        public static Vector StretchToFull(float width, float height, float bleedPerSide, Rectangle visible)
        {
            return new Vector(
                visible.w / (width - (2f * bleedPerSide)),
                visible.h / (height - (2f * bleedPerSide)));
        }
    }
}
