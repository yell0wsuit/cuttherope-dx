using System;

namespace CutTheRopeDX.Framework.Platform
{
    /// <summary>
    /// A texture a video player rewrites once per decoded frame.
    /// </summary>
    /// <remarks>
    /// Ordinary game textures are uploaded once and sampled for the rest of the level, so they are
    /// created straight from encoded bytes. A movie replaces every pixel tens of times a second,
    /// which is why frames get their own handle: the renderer keeps one surface for the whole
    /// movie and the player only hands it new pixels.
    /// </remarks>
    internal interface IVideoFrameTexture : ITextureHandle
    {
        /// <summary>Replaces the whole texture with one decoded frame.</summary>
        /// <param name="pixels">
        /// Tightly packed opaque RGBA bytes, four per pixel, in top-to-bottom row order. Exactly
        /// <c>Width * Height * 4</c> of them; anything else is rejected rather than partly applied.
        /// </param>
        void Update(ReadOnlySpan<byte> pixels);
    }
}
