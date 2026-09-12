using System;
using System.Runtime.InteropServices;

using CutTheRopeDX.Framework.Diagnostics;

using Microsoft.Extensions.Logging;

using SDL3;

using SkiaSharp;

namespace CutTheRopeDX.Desktop.Platform.Graphics
{
    /// <summary>SDL Metal layer with explicit drawable, queue and autorelease lifetimes.</summary>
    /// <remarks>Creates an uninitialized candidate.</remarks>
    /// <param name="fault">Fault-injection hook invoked at named initialization and frame points.</param>
    public sealed partial class MetalDevice(Action<string> fault) : SdlGraphicsDevice
    {
        private readonly Action<string> fault = fault;

        /// <summary>The <c>CAMetalLayer</c> owned by the SDL Metal view.</summary>
        private nint layer;

        /// <summary>The <c>MTLCommandQueue</c> shared with the Skia context.</summary>
        private nint queue;

        /// <summary><c>MTLCommandBufferStatusCompleted</c>.</summary>
        private const int MTLCommandBufferStatusCompleted = 4;

        /// <summary><c>MTLCommandBufferStatusError</c>.</summary>
        private const int MTLCommandBufferStatusError = 5;

        /// <summary>The <c>CAMetalDrawable</c> held between <see cref="AcquireFrame" /> and <see cref="Present" />.</summary>
        private nint drawable;

        /// <summary>The <c>NSAutoreleasePool</c> scoping the current frame's autoreleased objects.</summary>
        private nint pool;

        /// <summary>Creates the SDL Metal view and Skia queue context.</summary>
        public void Initialize()
        {
            if (!OperatingSystem.IsMacOS())
            {
                throw new PlatformNotSupportedException("Metal requires macOS.");
            }

            nint initPool = ObjC.Send(ObjC.Send(ObjC.Class("NSAutoreleasePool"), "alloc"), "init");
            Own(() => ObjC.Send(initPool, "drain"));
            CreateWindow(SDL.WindowFlags.Metal);
            nint view = SDL.MetalCreateView(Window);
            if (view == 0)
            {
                throw new InvalidOperationException(SDL.GetError());
            }

            Own(() => SDL.MetalDestroyView(view));
            layer = SDL.MetalGetLayer(view);
            if (layer == 0)
            {
                throw new InvalidOperationException("SDL did not create a CAMetalLayer.");
            }

            nint device = ObjC.MTLCreateSystemDefaultDevice();
            if (device == 0)
            {
                throw new InvalidOperationException("No Metal device.");
            }

            Own(() => ObjC.Send(device, "release"));
            string adapterName = Marshal.PtrToStringUTF8(ObjC.Send(ObjC.Send(device, "name"), "UTF8String"));
            ILogger logger = Log.For(LogCategories.SdlGraphics);
            // Metal has no driver version of its own: it ships with the system, so the OS
            // release is the version that identifies it.
            GraphicsDeviceLog.Adapter(logger, "hardware", adapterName, RuntimeInformation.OSDescription);
            _ = ObjC.Send(layer, "setDevice:", device);
            _ = ObjC.Send(layer, "setPixelFormat:", 80); // MTLPixelFormatBGRA8Unorm
            _ = ObjC.Send(layer, "setFramebufferOnly:", 0);
            queue = ObjC.Send(device, "newCommandQueue");
            if (queue == 0)
            {
                throw new InvalidOperationException("No Metal command queue.");
            }

            Own(() => ObjC.Send(queue, "release"));
            fault("after-device");
            Context = Own(GRContext.CreateMetal(new GRMtlBackendContext { DeviceHandle = device, QueueHandle = queue })
                ?? throw new InvalidOperationException("Skia Metal context creation failed."));
            Own(ReleaseDrawable);
        }

        /// <inheritdoc />
        public override bool AcquireFrame()
        {
            if (!GetDrawableSize(out int width, out int height))
            {
                return false;
            }

            ClearSurface();
            ReleaseDrawable();
            pool = ObjC.Send(ObjC.Send(ObjC.Class("NSAutoreleasePool"), "alloc"), "init");
            Width = width;
            Height = height;
            ObjC.SetSize(layer, ObjC.Selector("setDrawableSize:"), new ObjC.Size(width, height));
            drawable = ObjC.Send(layer, "nextDrawable");
            if (drawable == 0) { ReleaseDrawable(); return false; }
            fault("before-surface");
            nint texture = ObjC.Send(drawable, "texture");
            SetSurface(new GRBackendRenderTarget(width, height, new GRMtlTextureInfo(texture)),
                GRSurfaceOrigin.TopLeft, SKColorType.Bgra8888);
            fault("after-surface");
            return true;
        }

        /// <inheritdoc />
        public override void Present()
        {
            CheckThread();
            if (drawable == 0)
            {
                throw new InvalidOperationException("No Metal drawable to present.");
            }

            nint command = ObjC.Send(queue, "commandBuffer");
            if (command == 0)
            {
                throw new InvalidOperationException("Metal command buffer creation failed.");
            }

            _ = ObjC.Send(command, "presentDrawable:", drawable);
            _ = ObjC.Send(command, "commit");
            _ = ObjC.Send(command, "waitUntilCompleted"); // Prototype uses one frame in flight.

            // Waiting leaves the buffer either completed or in error, and the error state after a
            // present is how a GPU reset or a removed device reaches this process. Reported as an
            // ordinary failure it would take the game down instead of reaching recovery.
            nint status = ObjC.Send(command, "status");
            if (status == MTLCommandBufferStatusError)
            {
                throw new GraphicsDeviceLostException("The Metal presentation command failed.");
            }

            if (status != MTLCommandBufferStatusCompleted)
            {
                throw new InvalidOperationException(
                    $"Metal presentation ended in command buffer status {status}.");
            }

            ClearSurface();
            ReleaseDrawable();
        }

        /// <inheritdoc />
        public override void Resize()
        {
            CheckThread();
            Context.Flush(submit: true, synchronous: true);
            ClearSurface();
            ReleaseDrawable();
        }

        /// <summary>Drops the current drawable and drains the frame's autorelease pool.</summary>
        private void ReleaseDrawable()
        {
            drawable = 0;
            if (pool != 0) { _ = ObjC.Send(pool, "drain"); pool = 0; }
        }

        /// <summary>Minimal Objective-C runtime and Metal entry points used by this device.</summary>
        private static partial class ObjC
        {
            /// <summary>Creates the system default Metal device.</summary>
            /// <returns>A retained <c>MTLDevice</c>, or zero when Metal is unavailable.</returns>
            [LibraryImport("/System/Library/Frameworks/Metal.framework/Metal")]
            internal static partial nint MTLCreateSystemDefaultDevice();

            /// <summary>Looks up an Objective-C class by name.</summary>
            /// <param name="name">The registered class name.</param>
            /// <returns>The class object, or zero when no such class is registered.</returns>
            [LibraryImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_getClass", StringMarshalling = StringMarshalling.Utf8)]
            internal static partial nint Class(string name);

            /// <summary>Registers a method name and returns its selector.</summary>
            /// <param name="name">The method name, including any trailing colons.</param>
            /// <returns>The selector for <paramref name="name" />.</returns>
            [LibraryImport("/usr/lib/libobjc.A.dylib", EntryPoint = "sel_registerName", StringMarshalling = StringMarshalling.Utf8)]
            internal static partial nint Selector(string name);

            /// <summary>Sends a nullary message whose return value is pointer-sized.</summary>
            /// <param name="receiver">The object or class receiving the message.</param>
            /// <param name="selector">The selector to send.</param>
            /// <returns>The pointer-sized return value.</returns>
            [LibraryImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
            private static partial nint Send0(nint receiver, nint selector);

            /// <summary>Sends a message with one pointer-sized argument.</summary>
            /// <param name="receiver">The object or class receiving the message.</param>
            /// <param name="selector">The selector to send.</param>
            /// <param name="value">The pointer-sized argument.</param>
            /// <returns>The pointer-sized return value.</returns>
            [LibraryImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
            private static partial nint Send1(nint receiver, nint selector, nint value);

            /// <summary>Sends a message with one <see cref="Size" /> argument, which the pointer-sized overloads cannot pass.</summary>
            /// <param name="receiver">The object receiving the message.</param>
            /// <param name="selector">The selector to send.</param>
            /// <param name="size">The size argument.</param>
            [LibraryImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
            internal static partial void SetSize(nint receiver, nint selector, Size size);

            /// <summary>Sends a nullary message by selector name.</summary>
            /// <param name="receiver">The object or class receiving the message.</param>
            /// <param name="selector">The method name to send.</param>
            /// <returns>The pointer-sized return value.</returns>
            internal static nint Send(nint receiver, string selector)
            {
                return Send0(receiver, Selector(selector));
            }

            /// <summary>Sends a message with one pointer-sized argument by selector name.</summary>
            /// <param name="receiver">The object or class receiving the message.</param>
            /// <param name="selector">The method name to send.</param>
            /// <param name="value">The pointer-sized argument.</param>
            /// <returns>The pointer-sized return value.</returns>
            internal static nint Send(nint receiver, string selector, nint value)
            {
                return Send1(receiver, Selector(selector), value);
            }

            /// <summary>The <c>CGSize</c> layout expected by size-taking selectors.</summary>
            /// <param name="width">The width in pixels.</param>
            /// <param name="height">The height in pixels.</param>
            [StructLayout(LayoutKind.Sequential)]
            internal readonly struct Size(double width, double height)
            {
                private readonly double width = width;
                private readonly double height = height;
            }
        }
    }
}
