using System.Runtime.InteropServices;

namespace CutTheRopeDX.Browser
{
    /// <summary>
    /// Native boundary that executes in the calling thread's own JavaScript scope,
    /// which managed interop cannot reach in the threaded build because it is proxied to
    /// the browser thread. The single-threaded build already runs there, and calls the
    /// same entry points: which canvas they find is the only difference, and the native
    /// side settles that.
    /// </summary>
    internal static unsafe partial class HostShim
    {
        private const string Library = "ctrdxhost";

        /// <summary>
        /// Returns the calling thread's pthread pointer, or zero in the single-threaded
        /// build, where nothing needs to address the thread the game runs on.
        /// </summary>
        [LibraryImport(Library, EntryPoint = "ctrdx_thread_id")]
        internal static partial int ThreadId();

        /// <summary>Returns whether the caller is the browser runtime thread.</summary>
        [LibraryImport(Library, EntryPoint = "ctrdx_is_main_runtime_thread")]
        internal static partial int IsMainRuntimeThread();

        /// <summary>Registers the function each animation frame invokes.</summary>
        [LibraryImport(Library, EntryPoint = "ctrdx_set_frame_callback")]
        internal static partial void SetFrameCallback(
            delegate* unmanaged<double, void> callback);

        /// <summary>Schedules one animation frame on this thread.</summary>
        [LibraryImport(Library, EntryPoint = "ctrdx_request_frame")]
        internal static partial void RequestFrame();

        /// <summary>
        /// Takes ownership of the canvas this thread draws to: the page's own element in
        /// the single-threaded build, or whatever the browser thread transfers here.
        /// </summary>
        [LibraryImport(Library, EntryPoint = "ctrdx_acquire_canvas")]
        internal static partial int AcquireCanvas();

        /// <summary>Returns whether the transferred canvas has arrived.</summary>
        [LibraryImport(Library, EntryPoint = "ctrdx_canvas_received")]
        internal static partial int CanvasReceived();

        /// <summary>Registers and makes current a WebGL2 context this thread owns.</summary>
        [LibraryImport(Library, EntryPoint = "ctrdx_create_context")]
        internal static partial int CreateContext(int width, int height);

        /// <summary>Resizes the transferred canvas backing store.</summary>
        [LibraryImport(Library, EntryPoint = "ctrdx_resize_canvas")]
        internal static partial int ResizeCanvas(int width, int height);

        /// <summary>Returns whether this thread's WebGL context has been lost.</summary>
        [LibraryImport(Library, EntryPoint = "ctrdx_context_lost")]
        internal static partial int ContextLost();

        /// <summary>
        /// Returns the shared event buffer, allocating it on first use. Its address is
        /// stable for the process, which is what lets the browser thread keep writing to
        /// it across a memory growth that replaces every typed-array view.
        /// </summary>
        [LibraryImport(Library, EntryPoint = "ctrdx_event_buffer")]
        internal static partial nint EventBuffer(int bytes);
    }
}
