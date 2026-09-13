// Bodies are EM_ASM because it runs in the JavaScript scope of the calling
// thread. In the threaded build the runtime proxies [JSImport] to the browser
// thread, so managed interop cannot reach the owner thread's own scope, and the
// owner thread is where the WebGL context and the animation frame have to live.
//
// __EMSCRIPTEN_PTHREADS__ is what separates the two builds here. Threaded, this
// runs on a worker and the canvas arrives as a transferred OffscreenCanvas;
// single-threaded, it runs on the browser thread and the canvas is simply the
// element already in the page. Everything downstream of acquiring it - the GL
// registration, the frame, the resize, the loss check - is the same code.
//
// This is C++ rather than C only because of the emcc command line. SkiaSharp's
// WebAssembly native assets link Dawn's emdawnwebgpu port, which adds its own
// webgpu.cpp and -std=c++20 to the same emcc invocation that compiles this
// file, and that dialect flag applies to every input. Hence extern "C" below,
// the exported names have to stay unmangled for DirectPInvoke to bind them.

#include <emscripten.h>
#include <emscripten/threading.h>
#include <stdint.h>
#include <stdlib.h>

#ifdef __EMSCRIPTEN_PTHREADS__
#include <pthread.h>
#endif

extern "C"
{

static void (*frame_callback)(double) = NULL;
static void *event_buffer = NULL;

// Zero in the single-threaded build: glcontext.js reads this to find the owner
// thread's worker, and there is none to find.
EMSCRIPTEN_KEEPALIVE
int ctrdx_thread_id(void)
{
#ifdef __EMSCRIPTEN_PTHREADS__
    return (int)(intptr_t)pthread_self();
#else
    return 0;
#endif
}

EMSCRIPTEN_KEEPALIVE
int ctrdx_is_main_runtime_thread(void)
{
    return emscripten_is_main_runtime_thread();
}

EMSCRIPTEN_KEEPALIVE
void ctrdx_set_frame_callback(void (*callback)(double))
{
    frame_callback = callback;
}

EMSCRIPTEN_KEEPALIVE
void ctrdx_frame_entry(double timestamp)
{
    if (frame_callback != NULL)
    {
        frame_callback(timestamp);
    }
}

// Runs a frame now and abandons the one the browser still owes this thread. A
// hidden page stops being given animation frames, so the loop cannot notice its
// own pause; this is how a lifecycle change reaches it. Exported for the
// single-threaded build, where host-events.js calls it in place of the message
// it would post to a worker.
EMSCRIPTEN_KEEPALIVE
void ctrdx_wake(void)
{
    EM_ASM({
        globalThis.ctrdxFrameToken = (globalThis.ctrdxFrameToken | 0) + 1;
        // Match requestAnimationFrame's relative clock. In threaded builds,
        // emscripten_get_now adds timeOrigin and would make the next frame's
        // elapsed time negative, stalling the game's fixed-step accumulator.
        _ctrdx_frame_entry(performance.now());
    });
}

EMSCRIPTEN_KEEPALIVE
void ctrdx_request_frame(void)
{
    EM_ASM({
        var token = (globalThis.ctrdxFrameToken | 0) + 1;
        globalThis.ctrdxFrameToken = token;
        requestAnimationFrame(function (timestamp) {
            if (globalThis.ctrdxFrameToken !== token) {
                return;
            }
            _ctrdx_frame_entry(timestamp);
        });
    });
}

#ifdef __EMSCRIPTEN_PTHREADS__

// Coexists with the runtime worker's own onmessage. Messages here carry no `cmd`
// field on purpose: that handler ends in `else if (e.data.cmd)` and reports any
// command it does not recognize twice, so a `cmd` would make every wake noisy.
EMSCRIPTEN_KEEPALIVE
int ctrdx_acquire_canvas(void)
{
    return EM_ASM_INT({
        if (globalThis.ctrdxCanvasListener) {
            return 1;
        }
        globalThis.ctrdxCanvasListener = true;
        globalThis.ctrdxCanvas = null;
        // The page is a postMessage away; glcontext.js is listening there.
        globalThis.ctrdxReportContextLost = function () {
            postMessage({ ctrdxContextLost: 1 });
        };
        addEventListener('message', function (event) {
            var data = event.data;
            if (data && data.ctrdxTransferCanvas) {
                globalThis.ctrdxCanvas = data.ctrdxTransferCanvas;
            } else if (data && data.ctrdxWake) {
                _ctrdx_wake();
            }
        });
        return 1;
    });
}

#else

// The canvas is already here and stays here. Nothing is transferred, so nothing
// is waited for and the page keeps the element it drew the splash over.
// ctrdxReportContextLost needs no installing either: this is the page's own
// scope, and glcontext.js put it there.
EMSCRIPTEN_KEEPALIVE
int ctrdx_acquire_canvas(void)
{
    return EM_ASM_INT({
        globalThis.ctrdxCanvas = document.getElementById('game');
        return globalThis.ctrdxCanvas ? 1 : 0;
    });
}

#endif

EMSCRIPTEN_KEEPALIVE
int ctrdx_canvas_received(void)
{
    return EM_ASM_INT({
        return globalThis.ctrdxCanvas ? 1 : 0;
    });
}

EMSCRIPTEN_KEEPALIVE
int ctrdx_create_context(int width, int height)
{
    return EM_ASM_INT({
        var surface = globalThis.ctrdxCanvas;
        if (!surface) {
            return 0;
        }
        surface.width = $0;
        surface.height = $1;
        var context = surface.getContext('webgl2', {
            alpha: true,
            depth: true,
            stencil: true,
            antialias: false,
            premultipliedAlpha: true,
            preserveDrawingBuffer: false
        });
        if (!context) {
            return 0;
        }
        var handle = GL.registerContext(context, {
            majorVersion: 2,
            minorVersion: 0,
            enableExtensionsByDefault: 1,
            alpha: 1,
            depth: 1,
            stencil: 8,
            antialias: 0,
            premultipliedAlpha: 1,
            preserveDrawingBuffer: 0
        });
        if (!handle) {
            return 0;
        }
        GL.makeContextCurrent(handle);
        globalThis.ctrdxContextLost = 0;
        surface.addEventListener('webglcontextlost', function (event) {
            event.preventDefault();
            globalThis.ctrdxContextLost = 1;
            // Installed per build, because the notice has to appear on the page
            // and only one of the two builds is already on it. A preprocessor
            // branch cannot go here: this block is a macro argument.
            globalThis.ctrdxReportContextLost();
        });
        return handle;
    }, width, height);
}

EMSCRIPTEN_KEEPALIVE
int ctrdx_resize_canvas(int width, int height)
{
    return EM_ASM_INT({
        var surface = globalThis.ctrdxCanvas;
        if (!surface) {
            return 0;
        }
        surface.width = $0;
        surface.height = $1;
        return 1;
    }, width, height);
}

EMSCRIPTEN_KEEPALIVE
int ctrdx_context_lost(void)
{
    return EM_ASM_INT({
        return globalThis.ctrdxContextLost | 0;
    });
}

EMSCRIPTEN_KEEPALIVE
void *ctrdx_event_buffer(int bytes)
{
    if (event_buffer == NULL)
    {
        event_buffer = calloc(1, (size_t)bytes);
    }
    return event_buffer;
}

} // extern "C"
