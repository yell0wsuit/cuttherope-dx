// Which of the two published runtimes this page can run.
//
// The threaded runtime draws from a worker through a transferred OffscreenCanvas and needs
// cross-origin isolation for the shared memory that carries events to it. Isolation is not
// always obtainable: a Discord activity and an Android WebView are both served without COOP
// and COEP, and no service worker the page installs can add them. The single-threaded
// runtime runs the whole game on this thread against the page's own canvas instead, so it
// asks nothing of the server.
//
// Everything here is pure or takes the scope it probes, because the choice has to be made
// and be testable before either runtime is fetched. The transfer the threaded runtime
// performs is permanent - a page that hands its canvas away and then discovers it has
// nowhere to draw from cannot take it back - so nothing may be left to discover later.
//
// Support data: https://github.com/mdn/browser-compat-data

/** The threaded runtime, published under the default framework path. */
export const THREADED_RUNTIME = "./_framework/dotnet.js";

/** The single-threaded runtime, published beside it. */
export const SINGLE_RUNTIME = "./_framework-single/dotnet.js";

const NO_GRAPHICS =
    "This browser cannot draw the 3D graphics the game needs. " +
    "iOS and iPadOS need version 15 or newer.";

/**
 * Reports what <paramref name="scope"/> can do, without regard for what that implies.
 *
 * Capability probes only, no user agent matching: what a runtime reports about itself is a
 * poorer answer to "can this run" than asking the runtime to do the thing. Safari is the
 * case that makes this pay - it grew OffscreenCanvas at 16.4 but only added a WebGL2
 * context on one at 17, so a 16.x iPhone answers yes to every question but the last.
 */
export function probeEnvironment(scope = globalThis) {
    return {
        isolated: scope.crossOriginIsolated === true,
        sharedMemory: typeof scope.SharedArrayBuffer === "function",
        offscreenCanvas:
            typeof scope.OffscreenCanvas === "function" &&
            typeof scope.HTMLCanvasElement?.prototype
                ?.transferControlToOffscreen === "function",
        workerGraphics: hasWebGL2(() => new scope.OffscreenCanvas(1, 1)),
        localGraphics: hasWebGL2(() => scope.document.createElement("canvas")),
    };
}

/**
 * Reads the runtime a visit asks for through its `mode` query parameter.
 *
 * `single` and `multi` are the accepted values, case-insensitively. The raw value is handed
 * back as well, so a caller can tell a misspelled request from no request at all.
 */
export function parseModeQuery(search = "") {
    const raw = new URLSearchParams(search).get("mode");
    const value = raw?.trim().toLowerCase();
    const mode =
        value === "single" ? "single" : value === "multi" ? "threaded" : null;
    return { mode, raw };
}

/**
 * Picks the runtime an environment can run, or reports that none of them can.
 *
 * Falling back is the default and failing is the exception: every capability the threaded
 * runtime needs beyond the single-threaded one is a reason to step down, not to stop.
 *
 * A <paramref name="requested"/> mode is honored only where the environment can run it. The
 * single-threaded runtime can be chosen on any page that could draw at all; asking for the
 * threaded one where it cannot run still steps down, because importing it there would fail
 * before the game could report anything.
 */
export function selectRuntime(environment, requested = null) {
    const threaded =
        environment.isolated &&
        environment.sharedMemory &&
        environment.offscreenCanvas &&
        environment.workerGraphics;
    if (requested === "single" && environment.localGraphics) {
        return { mode: "single", runtime: SINGLE_RUNTIME, reason: null };
    }
    if (threaded) {
        return { mode: "threaded", runtime: THREADED_RUNTIME, reason: null };
    }
    if (environment.localGraphics) {
        return { mode: "single", runtime: SINGLE_RUNTIME, reason: null };
    }
    return { mode: "unsupported", runtime: null, reason: NO_GRAPHICS };
}

/**
 * Returns whether a canvas from <paramref name="create"/> answers with a WebGL2 context.
 *
 * The context is handed straight back. A browser caps how many may exist at once and this
 * runs during boot, moments before the game asks for the one it keeps.
 */
function hasWebGL2(create) {
    try {
        const context = create()?.getContext("webgl2") ?? null;
        if (context === null) {
            return false;
        }
        context.getExtension("WEBGL_lose_context")?.loseContext();
        return true;
    } catch {
        return false;
    }
}
