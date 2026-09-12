using System;
using System.Runtime.InteropServices;

using CutTheRopeDX.Framework.Diagnostics;

using Microsoft.Extensions.Logging;

using SDL3;

using SkiaSharp;

namespace CutTheRopeDX.Desktop.Platform.Graphics
{
    /// <summary>SDL OpenGL context with a Skia surface over the actual default framebuffer.</summary>
    /// <remarks>Creates an uninitialized candidate so partial initialization has an owner.</remarks>
    public sealed class SdlGlDevice(Action<string> fault, GlContextProfile profile) : SdlGraphicsDevice
    {
        private nint gl;

        private uint windowFramebuffer;

        private readonly Action<string> fault = fault;

        private readonly GlContextProfile profile = profile;

        /// <summary>Points SDL at the profile's own GL libraries for the rest of the scope.</summary>
        /// <param name="profile">An ANGLE profile, naming the libraries to load from.</param>
        /// <param name="hints">The scope the hints belong to.</param>
        /// <remarks>
        /// Forcing EGL is what does the work. Windows has two loaders behind the same request,
        /// and the one it picks by default opens the named library as though it were the system
        /// <c>opengl32</c> and looks for <c>wgl</c> entry points that an ES library does not
        /// export, so the attempt dies before any of the rest of this matters. The driver hint
        /// is the older half of the same decision, kept because it is what SDL consults when
        /// choosing between the two. The libraries are named separately because each loader
        /// reads its own: one opens EGL, the other the client GL it dispatches through.
        /// </remarks>
        internal static void ApplyAngleHints(GlContextProfile profile, GlHintScope hints)
        {
            hints.Set(SDL.Hints.VideoForceEGL, "1");
            hints.Set(SDL.Hints.OpenGLESDriver, "1");
            hints.Set(SDL.Hints.EGLLibrary, profile.EglLibrary);
            hints.Set(SDL.Hints.OpenGLLibrary, profile.GlesLibrary);
        }

        /// <summary>Rebuilds the video subsystem so hints read while it comes up can take effect.</summary>
        /// <param name="apply">What to change while there is no video device to ignore it.</param>
        /// <remarks>
        /// The hints that choose between the two Windows GL loaders are read once, when the video
        /// device is built. Setting them against a subsystem that is already up changes nothing at
        /// all — the loader has been chosen and its entry points bound — so the subsystem is taken
        /// down and brought back around the change, which is what gives them somewhere to land.
        /// <para>
        /// Nothing may hold a window across this. Candidates are built one at a time and the one
        /// before has already been released by the time the next starts, so nothing does.
        /// </para>
        /// </remarks>
        private static void RecycleVideo(Action apply)
        {
            SDL.QuitSubSystem(SDL.InitFlags.Video);
            try
            {
                apply?.Invoke();
            }
            catch
            {
                // A refused hint rejects this candidate, which is the useful half to report, but
                // the subsystem still has to come back for whatever is tried next.
                _ = SDL.InitSubSystem(SDL.InitFlags.Video);
                throw;
            }

            if (!SDL.InitSubSystem(SDL.InitFlags.Video))
            {
                throw new InvalidOperationException(
                    $"SDL could not restart its video subsystem: {SDL.GetError()}");
            }
        }

        /// <summary>Creates the window, context and first drawable.</summary>
        /// <remarks>
        /// Every candidate rebuilds the subsystem, not only the ANGLE one, because a candidate
        /// cannot trust the loader the device it inherits was built around. An ANGLE attempt that
        /// comes up and is then rejected by its validation frame leaves the device bound to EGL,
        /// and the native driver tried next would open ANGLE's libraries as its own; asking for
        /// the device it wants is the only thing that does not depend on how the one before ended.
        /// </remarks>
        public void Initialize()
        {
            using GlHintScope hints = new();
            RecycleVideo(profile.UsesAngle ? () => ApplyAngleHints(profile, hints) : null);
            try
            {
                CreateContext();
            }
            catch
            {
                // Whatever is tried next has to find the video device it would have had, so the
                // hints go back before the subsystem is rebuilt around them.
                hints.Dispose();
                try
                {
                    RecycleVideo(null);
                }
                catch (InvalidOperationException)
                {
                    // Why this candidate was rejected is the useful half; a subsystem that will
                    // not come back will say so again, loudly, on the next one.
                }

                throw;
            }
        }

        private unsafe void CreateContext()
        {
            SDL.GLResetAttributes();
            Check(SDL.GLSetAttribute(SDL.GLAttr.ContextMajorVersion, profile.Major));
            Check(SDL.GLSetAttribute(SDL.GLAttr.ContextMinorVersion, profile.Minor));
            Check(SDL.GLSetAttribute(SDL.GLAttr.ContextProfileMask, profile.ProfileMask));
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
            ILogger logger = Log.For(LogCategories.SdlGraphics);

            // GL will not classify its hardware, but it does name the renderer and the driver, and
            // GL_VERSION carries the driver build on most desktop drivers. That is the part worth
            // having in a report.
            string renderer = ReadGlString(GlRenderer);
            string version = ReadGlString(GlVersion);
            GraphicsDeviceLog.Adapter(logger, "unknown", renderer, version);
        }

        /// <summary>Name of the renderer, as <c>GL_RENDERER</c>.</summary>
        private const uint GlRenderer = 0x1F01;

        /// <summary>Version string, as <c>GL_VERSION</c>.</summary>
        private const uint GlVersion = 0x1F02;

        /// <summary>
        /// Reads one of the driver's own description strings.
        /// </summary>
        /// <param name="name">The <c>glGetString</c> name to read.</param>
        /// <returns>The value, or a placeholder when the driver will not answer.</returns>
        /// <remarks>
        /// Called with a current context, which is the only state in which these are defined.
        /// A driver that refuses is described rather than allowed to fail the device: the game
        /// runs perfectly well without knowing what it is running on.
        /// </remarks>
        private static unsafe string ReadGlString(uint name)
        {
            nint address = SDL.GLGetProcAddress("glGetString");
            if (address == 0)
            {
                return "unknown";
            }

            byte* value = ((delegate* unmanaged[Cdecl]<uint, byte*>)address)(name);
            return value == null ? "unknown" : Marshal.PtrToStringUTF8((nint)value) ?? "unknown";
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
