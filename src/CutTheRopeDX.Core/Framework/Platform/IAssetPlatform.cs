using CutTheRopeDX.Framework.Visual;

namespace CutTheRopeDX.Framework.Platform
{
    /// <summary>
    /// The asset-loading operations that require a graphics device. Swapping this is what
    /// lets the game run headless; everything else in the engine is device-independent.
    /// </summary>
    internal interface IAssetPlatform
    {
        /// <summary>Pixel dimensions of an image, or <see langword="null"/> when it is missing.</summary>
        /// <param name="contentPath">Content-relative path, e.g. <c>images/obj_candy</c>.</param>
        /// <returns>The image's pixel size, or <see langword="null"/> when the asset is missing.</returns>
        (int W, int H)? ImageDimensions(string contentPath);

        /// <summary>The backing texture, or <see langword="null"/> when running without a device.</summary>
        /// <param name="contentPath">Content-relative path, e.g. <c>images/obj_candy</c>.</param>
        /// <returns>The loaded texture handle, or <see langword="null"/>.</returns>
        ITextureHandle ImageTexture(string contentPath);

        /// <summary>
        /// Builds a texture from a region of <paramref name="source"/> whose pixels keep their
        /// alpha and wear <paramref name="tint"/>, or <see langword="null"/> without a device.
        /// </summary>
        /// <param name="source">Texture to copy the region out of.</param>
        /// <param name="x">Left edge of the region in source pixels.</param>
        /// <param name="y">Top edge of the region in source pixels.</param>
        /// <param name="width">Region width in pixels.</param>
        /// <param name="height">Region height in pixels.</param>
        /// <param name="tint">Color the region's pixels take on.</param>
        /// <returns>The recolored texture, or <see langword="null"/>.</returns>
        ITextureHandle TintedRegion(
            ITextureHandle source,
            int x,
            int y,
            int width,
            int height,
            RGBAColor tint);

        /// <summary>
        /// Starts decoding an image in the background so that loading it later only has to upload
        /// it. Loading works the same whether or not this was called first.
        /// </summary>
        /// <param name="contentPath">Content-relative path, e.g. <c>images/obj_candy</c>.</param>
        void PrepareImage(string contentPath);

        /// <summary>
        /// Whether loading an image now would not wait on a background decode: its decode has
        /// finished, it is already loaded, or nothing is decoding it.
        /// </summary>
        /// <param name="contentPath">Content-relative path, e.g. <c>images/obj_candy</c>.</param>
        /// <returns><see langword="true"/> when loading the image would not block on a decode.</returns>
        bool IsImageReady(string contentPath);

        /// <summary>
        /// Drops a prepared image that is no longer wanted. Never releases a loaded texture.
        /// </summary>
        /// <param name="contentPath">Content-relative path, e.g. <c>images/obj_candy</c>.</param>
        void DiscardPreparedImage(string contentPath);

        /// <summary>
        /// Reports the most decoded pixel memory held for prepared images not yet loaded since the
        /// last call, and starts the next reading from what is held now.
        /// </summary>
        /// <remarks>
        /// Counts decoded pixels only: not decoder scratch memory, encoded file bytes, or GPU
        /// textures.
        /// </remarks>
        /// <returns>Peak decoded pixel bytes awaiting upload.</returns>
        long TakePeakPreparedPixelBytes();

        /// <summary>Releases the cached content manager backing an image, if any.</summary>
        /// <param name="contentPath">Content-relative path, e.g. <c>images/obj_candy</c>.</param>
        void FreeImage(string contentPath);

        /// <summary>Loads a font by logical resource name.</summary>
        /// <param name="resourceName">Logical font resource name.</param>
        /// <returns>The loaded font.</returns>
        FontGeneric Font(string resourceName);

        /// <summary>Clears any cached font resources held by the platform's font loader.</summary>
        void ClearFontCache();
    }
}
