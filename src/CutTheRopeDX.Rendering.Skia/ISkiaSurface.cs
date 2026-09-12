using SkiaSharp;

namespace CutTheRopeDX.Rendering.Skia
{
    /// <summary>Drawing access to an acquired GPU surface, with host-owned presentation.</summary>
    public interface ISkiaSurface
    {
        /// <summary>The current frame's canvas, valid until submission or resize.</summary>
        SKCanvas Canvas { get; }

        /// <summary>The context that owns uploaded resources.</summary>
        GRContext Context { get; }

        /// <summary>Current width in drawable pixels.</summary>
        int Width { get; }

        /// <summary>Current height in drawable pixels.</summary>
        int Height { get; }

        /// <summary>Submits completed drawing before host presentation.</summary>
        void Flush();
    }
}
