using System;

using CutTheRopeDX.Rendering.Skia;

namespace CutTheRopeDX.Desktop.Platform.Graphics
{
    /// <summary>A main-thread desktop device and its current presentation surface.</summary>
    public interface IDesktopGraphicsDevice : ISkiaSurface, IDisposable
    {
        /// <summary>Acquires a drawable. False means minimized or temporarily unavailable.</summary>
        bool AcquireFrame();

        /// <summary>Presents the submitted frame.</summary>
        void Present();

        /// <summary>Rebuilds presentation resources using the window's drawable size.</summary>
        void Resize();
    }
}
