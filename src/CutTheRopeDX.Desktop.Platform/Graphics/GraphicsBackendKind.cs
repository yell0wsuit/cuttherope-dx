namespace CutTheRopeDX.Desktop.Platform.Graphics
{
    /// <summary>A native desktop renderer, independent of its window and device handles.</summary>
    public enum GraphicsBackendKind
    {
        /// <summary>Vulkan.</summary>
        Vulkan,

        /// <summary>Apple Metal.</summary>
        Metal,

        /// <summary>OpenGL fallback.</summary>
        OpenGL,

        /// <summary>OpenGL ES over ANGLE, for hosts without a usable native driver.</summary>
        Angle,
    }
}
