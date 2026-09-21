using System.Collections.Generic;

using CutTheRopeDX.Framework.Core;
using CutTheRopeDX.Framework.Visual;

namespace CutTheRopeDX.Framework.Platform
{
    /// <summary>
    /// Shared rendering canvas that manages viewport sizing, projection setup,
    /// and touch forwarding.
    /// </summary>
    internal sealed class GLCanvas : FrameworkTypes
    {
        /// <summary>
        /// Configures the renderer viewport and orthographic projection from the published
        /// viewport. The projection describes the logical region the game draws into and the
        /// viewport describes the surface pixels it lands on; both come from the same snapshot so
        /// they cannot disagree.
        /// </summary>
        public static void SetDefaultProjection()
        {
            Rectangle visible = ScreenPresentation.Instance.Snapshot.VisibleBounds;

            Renderer.SetViewport(XOffset, YOffset, BackingWidth, BackingHeight);
            Renderer.SetMatrixMode(15);
            Renderer.LoadIdentity();
            Renderer.SetOrthographic(0f, visible.w, visible.h, 0f, -1f, 1f);
            Renderer.SetMatrixMode(14);
            Renderer.LoadIdentity();
        }

        /// <summary>
        /// Forwards touch-begin events to the active touch delegate.
        /// </summary>
        /// <param name="touches">Touches that began this frame.</param>
        public void TouchesBeganwithEvent(IList<TouchLocation> touches)
        {
            _ = (touchDelegate?.TouchesBeganwithEvent(touches));
        }

        /// <summary>
        /// Forwards touch-move events to the active touch delegate.
        /// </summary>
        /// <param name="touches">Touches that moved this frame.</param>
        public void TouchesMovedwithEvent(IList<TouchLocation> touches)
        {
            _ = (touchDelegate?.TouchesMovedwithEvent(touches));
        }

        /// <summary>
        /// Forwards touch-end events to the active touch delegate.
        /// </summary>
        /// <param name="touches">Touches that ended this frame.</param>
        public void TouchesEndedwithEvent(IList<TouchLocation> touches)
        {
            _ = (touchDelegate?.TouchesEndedwithEvent(touches));
        }

        /// <summary>
        /// Forwards touch-cancel events to the active touch delegate.
        /// </summary>
        /// <param name="touches">Touches cancelled by the platform.</param>
        public void TouchesCancelledwithEvent(IList<TouchLocation> touches)
        {
            _ = (touchDelegate?.TouchesCancelledwithEvent(touches));
        }

        /// <summary>
        /// Returns whether the active touch delegate handled a back-button press.
        /// </summary>
        /// <returns><see langword="true" /> if the press was handled; otherwise <see langword="false" />.</returns>
        public bool BackButtonPressed()
        {
            return touchDelegate != null && touchDelegate.BackButtonPressed();
        }

        /// <summary>
        /// Returns whether the active touch delegate handled a menu-button press.
        /// </summary>
        /// <returns><see langword="true" /> if the press was handled; otherwise <see langword="false" />.</returns>
        public bool MenuButtonPressed()
        {
            return touchDelegate != null && touchDelegate.MenuButtonPressed();
        }

        /// <summary>
        /// Prepares renderer state for a frame before scene drawing begins.
        /// </summary>
        public static void BeforeRender()
        {
            SetDefaultProjection();
            Renderer.Disable(Renderer.GL_BLEND);
        }

        /// <summary>
        /// Logical width of the region the projection describes.
        /// </summary>
        internal static float ProjectionWidth =>
            ScreenPresentation.Instance.Snapshot.VisibleBounds.w;

        /// <summary>
        /// Logical height of the region the projection describes.
        /// </summary>
        internal static float ProjectionHeight =>
            ScreenPresentation.Instance.Snapshot.VisibleBounds.h;

        /// <summary>
        /// Active input delegate that receives touch and button events.
        /// </summary>
        public ITouchDelegate touchDelegate;

        /// <summary>
        /// Horizontal surface-pixel origin of the render viewport.
        /// </summary>
        public static int XOffset => (int)ScreenPresentation.Instance.Snapshot.RenderViewport.x;

        /// <summary>
        /// Vertical surface-pixel origin of the render viewport.
        /// </summary>
        public static int YOffset => (int)ScreenPresentation.Instance.Snapshot.RenderViewport.y;

        /// <summary>
        /// Width of the render viewport in surface pixels.
        /// </summary>
        public static int BackingWidth => (int)ScreenPresentation.Instance.Snapshot.RenderViewport.w;

        /// <summary>
        /// Height of the render viewport in surface pixels.
        /// </summary>
        public static int BackingHeight => (int)ScreenPresentation.Instance.Snapshot.RenderViewport.h;
    }
}
