using System;

using SDL3;

using SkiaSharp;

namespace CutTheRopeDX.Desktop.Platform.Graphics
{
    /// <summary>SDL OpenGL context with a Skia surface over the actual default framebuffer.</summary>
    /// <remarks>Creates an uninitialized candidate so partial initialization has an owner.</remarks>
    public sealed class SdlGlDevice(Action<string> fault) : SdlGraphicsDevice
    {
        private nint gl;

        private uint windowFramebuffer;

        private readonly Action<string> fault = fault;

        /// <summary>Creates the window, context and first drawable.</summary>
        public unsafe void Initialize()
        {
            SDL.GLResetAttributes();
            Check(SDL.GLSetAttribute(SDL.GLAttr.ContextMajorVersion, 3));
            Check(SDL.GLSetAttribute(SDL.GLAttr.ContextMinorVersion, 2));
            Check(SDL.GLSetAttribute(SDL.GLAttr.ContextProfileMask, 1));
            Check(SDL.GLSetAttribute(SDL.GLAttr.DoubleBuffer, 1));
            Check(SDL.GLSetAttribute(SDL.GLAttr.RedSize, 8));
            Check(SDL.GLSetAttribute(SDL.GLAttr.GreenSize, 8));
            Check(SDL.GLSetAttribute(SDL.GLAttr.BlueSize, 8));
            Check(SDL.GLSetAttribute(SDL.GLAttr.AlphaSize, 8));
            Check(SDL.GLSetAttribute(SDL.GLAttr.StencilSize, 8));
            CreateWindow(SDL.WindowFlags.OpenGL);
            gl = SDL.GLCreateContext(Window);
            if (gl == 0)
            {
                throw new InvalidOperationException(SDL.GetError());
            }

            Own(() => Check(SDL.GLDestroyContext(gl)));
            Check(SDL.GLMakeCurrent(Window, gl));
            nint address = SDL.GLGetProcAddress("glGetIntegerv");
            if (address == 0)
            {
                throw new InvalidOperationException("glGetIntegerv is unavailable.");
            }

            int framebuffer = 0;
            ((delegate* unmanaged[Cdecl]<uint, int*, void>)address)(0x8CA6, &framebuffer);
            windowFramebuffer = (uint)framebuffer;
            _ = SDL.GLSetSwapInterval(1);
            fault("after-device");
            GRGlInterface binding = Own(GRGlInterface.Create(SDL.GLGetProcAddress)
                ?? throw new InvalidOperationException("Skia GL procedure resolution failed."));
            Context = Own(GRContext.CreateGl(binding)
                ?? throw new InvalidOperationException("Skia GL context creation failed."));
            Console.WriteLine("OpenGL adapter-type=unknown (no portable GL hardware classification)");
        }

        /// <inheritdoc />
        public override bool AcquireFrame()
        {
            if (!GetDrawableSize(out int width, out int height))
            {
                return false;
            }

            Check(SDL.GLMakeCurrent(Window, gl));
            if (width != Width || height != Height)
            {
                Resize();
            }

            return true;
        }

        /// <inheritdoc />
        public override void Resize()
        {
            if (!GetDrawableSize(out int width, out int height))
            {
                return;
            }

            Context.Flush(submit: true, synchronous: true);
            ClearSurface();
            fault("before-surface");
            Check(SDL.GLGetAttribute(SDL.GLAttr.StencilSize, out int stencil));
            Check(SDL.GLGetAttribute(SDL.GLAttr.MultisampleSamples, out int samples));
            Context.ResetContext();
            SetSurface(new GRBackendRenderTarget(width, height, samples, stencil,
                new GRGlFramebufferInfo(windowFramebuffer, 0x8058)), GRSurfaceOrigin.BottomLeft, SKColorType.Rgba8888);
            Width = width;
            Height = height;
            fault("after-surface");
        }

        /// <inheritdoc />
        /// <remarks>
        /// A swap that fails means the context is no longer usable, which on this backend is what
        /// a driver reset looks like. Skia notices the same loss when it abandons the context, but
        /// only on the frame after this one.
        /// </remarks>
        public override void Present()
        {
            CheckThread();
            if (!SDL.GLSwapWindow(Window))
            {
                throw new GraphicsDeviceLostException($"SDL could not swap the GL window: {SDL.GetError()}");
            }
        }
    }
}
