using System;
using System.Threading;

using SDL3;

using SkiaSharp;

namespace CutTheRopeDX.Desktop.Platform.Graphics
{
    /// <summary>Shared main-thread window and native resource ownership for the probe.</summary>
    public abstract class SdlGraphicsDevice : IDesktopGraphicsDevice
    {
        private readonly int ownerThread = Environment.CurrentManagedThreadId;

        private bool disposed;

        private readonly CandidateLifetime resources = new();

        private SKSurface surface;

        private GRBackendRenderTarget target;

        /// <summary>The SDL window, owned by this device.</summary>
        public nint Window { get; private set; }

        /// <inheritdoc />
        public GRContext Context { get; protected set; }

        /// <inheritdoc />
        public SKCanvas Canvas => surface?.Canvas ?? throw new InvalidOperationException("No acquired frame.");

        /// <summary>
        /// Whether a frame is acquired and <see cref="Canvas"/> can be drawn into.
        /// </summary>
        /// <remarks>
        /// Core draws from inside updates as well as from the draw phase, so callers reached that
        /// way have to ask rather than assume there is a canvas.
        /// </remarks>
        public bool HasFrame => surface != null;

        /// <inheritdoc />
        public int Width { get; protected set; }

        /// <inheritdoc />
        public int Height { get; protected set; }

        /// <summary>Records a dependency for reverse-order cleanup.</summary>
        protected T Own<T>(T resource) where T : IDisposable
        {
            return resources.Own(resource);
        }

        /// <summary>Records a native release immediately after acquisition.</summary>
        protected void Own(Action release)
        {
            _ = resources.Own(new NativeRelease(release));
        }

        /// <summary>Creates a fresh window for this backend's required flags.</summary>
        protected void CreateWindow(SDL.WindowFlags flags)
        {
            CheckThread();
            Window = SDL.CreateWindow("Desktop Skia probe", 800, 600,
                flags | SDL.WindowFlags.Resizable | SDL.WindowFlags.HighPixelDensity);
            if (Window == 0)
            {
                throw new InvalidOperationException(SDL.GetError());
            }

            Own(() => SDL.DestroyWindow(Window));
        }

        /// <summary>Rejects operations on another thread or a disposed device.</summary>
        protected void CheckThread()
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (Environment.CurrentManagedThreadId != ownerThread || !SDL.IsMainThread())
            {
                throw new InvalidOperationException("SDL graphics must run on the main thread.");
            }
        }

        /// <summary>Queries drawable size, treating minimized windows as unavailable.</summary>
        protected bool GetDrawableSize(out int width, out int height)
        {
            CheckThread();
            Check(SDL.GetWindowSizeInPixels(Window, out width, out height));
            return width > 0 && height > 0 && (SDL.GetWindowFlags(Window) & SDL.WindowFlags.Minimized) == 0;
        }

        /// <summary>Wraps a backend target, retaining it even if Skia surface creation fails.</summary>
        protected void SetSurface(GRBackendRenderTarget renderTarget, GRSurfaceOrigin origin, SKColorType colorType)
        {
            ClearSurface();
            target = renderTarget;
            surface = SKSurface.Create(Context, target, origin, colorType)
                ?? throw new InvalidOperationException("Skia could not wrap the native render target.");
        }

        /// <summary>Releases presentation resources before their backing image or device.</summary>
        protected void ClearSurface()
        {
            surface?.Dispose();
            surface = null;
            target?.Dispose();
            target = null;
        }

        /// <summary>Converts an SDL failure into a useful initialization error.</summary>
        protected static void Check(bool success)
        {
            if (!success)
            {
                throw new InvalidOperationException(SDL.GetError());
            }
        }

        /// <inheritdoc />
        public abstract bool AcquireFrame();

        /// <inheritdoc />
        public abstract void Present();

        /// <inheritdoc />
        public abstract void Resize();

        /// <inheritdoc />
        public void Flush()
        {
            CheckThread();
            Canvas.Flush();
            Context.Flush(submit: true, synchronous: true);
            if (Context.IsAbandoned)
            {
                // Skia abandons a context when its driver reports the device gone, so this is the
                // one place a real loss is certain to surface: every draw after it would be
                // discarded silently, and the frame would present as if nothing had happened.
                throw new GraphicsDeviceLostException("Skia abandoned the graphics context.");
            }
        }

        /// <summary>Reads the current GPU frame for deterministic probe pixel checks.</summary>
        public SKBitmap ReadPixels()
        {
            CheckThread();
            SKBitmap bitmap = new(new SKImageInfo(Width, Height, SKColorType.Rgba8888, SKAlphaType.Premul));
            if (!surface.ReadPixels(bitmap.Info, bitmap.GetPixels(), bitmap.RowBytes, 0, 0))
            {
                bitmap.Dispose();
                throw new InvalidOperationException("GPU readback failed.");
            }
            return bitmap;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            CheckThread();
            disposed = true;
            try { ClearSurface(); }
            finally { resources.Dispose(); }
            GC.SuppressFinalize(this);
        }

        private sealed class NativeRelease(Action release) : IDisposable
        {
            private Action pending = release;
            public void Dispose()
            {
                Interlocked.Exchange(ref pending, null)?.Invoke();
            }
        }
    }
}
