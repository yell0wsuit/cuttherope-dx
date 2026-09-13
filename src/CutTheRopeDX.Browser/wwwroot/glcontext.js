import * as hostEvents from "./host-events.js";

/** Returns the canvas box and its backing size, in the order managed code reads them. */
function measure(canvas) {
    const ratio = Math.min(globalThis.devicePixelRatio || 1, 2);
    const cssWidth = Math.max(1, Math.round(canvas.clientWidth));
    const cssHeight = Math.max(1, Math.round(canvas.clientHeight));
    return [
        cssWidth,
        cssHeight,
        Math.max(1, Math.round(cssWidth * ratio)),
        Math.max(1, Math.round(cssHeight * ratio)),
    ];
}

// The single-threaded build's answer to transferCanvasToThread: the game already runs on
// this thread, so it needs the shape of the canvas and nothing else. Nothing is given
// away, which is why this one is safe to call before the graphics are known to work.
export function measureCanvas(canvasId) {
    const canvas = document.getElementById(canvasId);
    return canvas === null ? [] : measure(canvas);
}

// Hands the canvas to the managed owner thread's worker. Ownership is permanent:
// the browser thread can never draw to this canvas or resize its backing store
// again, so nothing may fall back to browser-thread rendering after this returns.
export function transferCanvasToThread(canvasId, threadId) {
    const canvas = document.getElementById(canvasId);
    const worker = globalThis.ctrdxWasmModule?.PThread?.pthreads?.[threadId];
    if (canvas === null || !worker) {
        return [];
    }

    // Measured before the transfer, because an OffscreenCanvas has no CSS box to read
    // and the element stops reporting one the moment it gives its control away.
    const size = measure(canvas);

    let offscreen;
    try {
        offscreen = canvas.transferControlToOffscreen();
    } catch (error) {
        console.info(
            JSON.stringify({
                marker: "ctrdx-host",
                boundary: "canvas-transfer",
                threadId,
                message: String(error),
            }),
        );
        return [];
    }

    // Deliberately no `cmd` field. The runtime's own worker dispatcher ends in
    // `else if (e.data.cmd)`, so any message carrying one that it does not
    // recognize is reported twice to the console, on every delivery.
    worker.postMessage({ ctrdxTransferCanvas: offscreen }, [offscreen]);
    worker.addEventListener("message", (event) => {
        if (event.data?.ctrdxContextLost) {
            reportContextLost();
        }
    });
    return size;
}

// A lost context cannot be rebuilt in place yet: the GPU objects Core holds - every
// CTRTexture2D handle, the backend's render target - outlive the context that made them, and
// nothing re-resolves them. Reloading is what recovers, so the player is handed the reload
// rather than told to go and find it.
//
// This message is posted from the webglcontextlost listener itself, so it can arrive before
// the managed loop has run another frame - and a loss triggered by the page being suspended
// may mean no further frame arrives at all. It says nothing about a save having just
// happened. What makes the progress claim true is separate: Preferences.Update runs on every
// fixed step and writes eagerly when a save was requested.
function reportContextLost() {
    document.getElementById("splash")?.classList.remove("hidden");
    for (const element of ["splash-spinner", "splash-progress", "start"]) {
        document.getElementById(element)?.setAttribute("hidden", "");
    }
    globalThis.ctrdxStopHint?.();
    document.getElementById("context-lost-error")?.removeAttribute("hidden");

    const resume = document.getElementById("resume");
    if (resume === null) {
        return;
    }
    resume.hidden = false;
    resume.addEventListener("click", () => globalThis.location.reload(), {
        once: true,
    });
}

// The single-threaded build loses its context on this thread, so its handler reaches the
// page by calling this rather than by posting the message the threaded build has to send.
globalThis.ctrdxReportContextLost = reportContextLost;

let watchedCanvas = null;
let devicePixelRatioQuery = null;
let canvasObserver = null;

function report(canvas) {
    hostEvents.resize(
        Math.max(1, canvas.clientWidth),
        Math.max(1, canvas.clientHeight),
        Math.min(globalThis.devicePixelRatio || 1, 2),
    );
}

// A ResizeObserver reports the canvas box changing, but not the page moving to a display of a
// different pixel density. A media query pinned to the current ratio covers that: it stops
// matching the moment the ratio changes, and is re-armed against the new one.
function watchDevicePixelRatio() {
    devicePixelRatioQuery?.removeEventListener(
        "change",
        onDevicePixelRatioChange,
    );
    devicePixelRatioQuery = globalThis.matchMedia(
        `(resolution: ${globalThis.devicePixelRatio || 1}dppx)`,
    );
    devicePixelRatioQuery.addEventListener("change", onDevicePixelRatioChange);
}

function onDevicePixelRatioChange() {
    if (watchedCanvas !== null) {
        report(watchedCanvas);
        watchDevicePixelRatio();
    }
}

// The canvas fills the viewport through the stylesheet, so its CSS box needs no help from
// here. Measuring it rather than sizing it is what lets the game adopt whatever shape the
// window or the device is, instead of the page choosing a shape and the game obeying it.
export function watchCanvas(canvasId) {
    const canvas = document.getElementById(canvasId);
    if (canvas === null) {
        return;
    }
    // Replacing rather than adding: a second call would otherwise leave the first observer
    // running against a canvas nothing reads any more, reporting the same box twice.
    canvasObserver?.disconnect();
    watchedCanvas = canvas;
    canvasObserver = new ResizeObserver(() => report(canvas));
    canvasObserver.observe(canvas);
    watchDevicePixelRatio();
    report(canvas);
}

export function documentBaseUrl() {
    return document.baseURI;
}
