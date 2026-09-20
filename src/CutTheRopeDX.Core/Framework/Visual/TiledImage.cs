using CutTheRopeDX.Framework.Core;

namespace CutTheRopeDX.Framework.Visual
{
    /// <summary>
    /// An <see cref="Image"/> that tiles a single quad to fill its width and height.
    /// </summary>
    internal sealed class TiledImage : Image
    {
        /// <summary>
        /// Sets the quad index to tile.
        /// </summary>
        /// <param name="t">Quad index, or -1 for full image.</param>
        public void SetTile(int t)
        {
            q = t;
        }

        /// <inheritdoc />
        public override void Draw()
        {
            PreDraw();
            DrawHelper.DrawImageTiled(texture, q, drawX, drawY, width, height);
            PostDraw();
        }

        /// <summary>
        /// Quad index to tile, or -1 for full image.
        /// </summary>
        private int q;
    }
}
