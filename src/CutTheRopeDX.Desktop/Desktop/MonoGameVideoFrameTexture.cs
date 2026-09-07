using System;

using CutTheRopeDX.Framework.Platform;

using Microsoft.Xna.Framework.Graphics;

namespace CutTheRopeDX.Desktop
{
    /// <summary>A movie frame held in one MonoGame texture that each decoded frame overwrites.</summary>
    /// <param name="texture">The frame texture; ownership transfers to this handle.</param>
    internal sealed class MonoGameVideoFrameTexture(Texture2D texture) : IVideoFrameTexture
    {
        /// <summary>The underlying texture, for the host's movie draw.</summary>
        public Texture2D Texture { get; private set; } = texture;

        /// <inheritdoc />
        public int Width { get; } = texture.Width;

        /// <inheritdoc />
        public int Height { get; } = texture.Height;

        /// <inheritdoc />
        public void Update(ReadOnlySpan<byte> pixels)
        {
            ObjectDisposedException.ThrowIf(Texture == null, this);
            int expected = Width * Height * 4;
            if (pixels.Length != expected)
            {
                throw new ArgumentException(
                    $"A {Width}x{Height} frame needs exactly {expected} bytes, not {pixels.Length}.",
                    nameof(pixels));
            }

            Texture.SetData(pixels.ToArray());
        }

        /// <inheritdoc />
        public void Dispose()
        {
            Texture?.Dispose();
            Texture = null;
        }
    }
}
