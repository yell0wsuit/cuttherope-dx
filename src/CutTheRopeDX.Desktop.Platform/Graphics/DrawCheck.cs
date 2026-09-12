using System;

using SkiaSharp;

namespace CutTheRopeDX.Desktop.Platform.Graphics
{
    /// <summary>Asks a device to prove that a shaded draw reaches its render target.</summary>
    /// <remarks>
    /// A driver that fails to link a program draws nothing while Skia reports success, so a clear
    /// on its own cannot tell a working device from a silent one; a gradient forces a program to
    /// exist. This says nothing about whether the frame was correct, and nothing about whether it
    /// reached the screen: a backend that renders offscreen and blits when it presents is read
    /// here before that blit has happened.
    /// </remarks>
    public static class DrawCheck
    {
        /// <summary>The color the target is cleared to before the shaded draw.</summary>
        public static SKColor Background => SKColors.Black;

        /// <summary>Clears the target and draws a gradient across all of it.</summary>
        /// <param name="canvas">The acquired frame's canvas.</param>
        /// <param name="width">Target width in pixels.</param>
        /// <param name="height">Target height in pixels.</param>
        public static void Draw(SKCanvas canvas, int width, int height)
        {
            ArgumentNullException.ThrowIfNull(canvas);
            canvas.Clear(Background);
            using SKShader shader = SKShader.CreateLinearGradient(
                new SKPoint(0, 0),
                new SKPoint(width, height),
                [SKColors.Red, SKColors.Blue],
                null,
                SKShaderTileMode.Clamp);
            using SKPaint paint = new() { Shader = shader };
            canvas.DrawRect(SKRect.Create(0, 0, width, height), paint);
        }

        /// <summary>The pixel to read back, which the gradient always covers.</summary>
        /// <param name="width">Target width in pixels.</param>
        /// <param name="height">Target height in pixels.</param>
        public static SKPointI Sample(int width, int height)
        {
            return new SKPointI(width / 2, height / 2);
        }

        /// <summary>Whether a sampled pixel shows the draw rather than the untouched background.</summary>
        /// <param name="sample">The pixel read back from the render target.</param>
        /// <remarks>
        /// The gradient runs red to blue, so no point along it can land on the background color.
        /// It is also opaque everywhere, drawn over an opaque clear, so a transparent sample is
        /// not something the check can produce: it is a readback that reported success without
        /// writing anything, which leaves the caller's zeroed buffer exactly as it allocated it.
        /// </remarks>
        public static bool Drew(SKColor sample)
        {
            return sample.Alpha != 0 && sample != Background;
        }
    }
}
