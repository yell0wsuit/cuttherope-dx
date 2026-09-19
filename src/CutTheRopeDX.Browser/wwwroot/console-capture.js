// Records what the browser reports about the page - console warnings and errors, uncaught
// exceptions, rejected promises, and assets that failed to load - into the exported log.
//
// A desktop browser shows all of this in its developer console; a phone has none to open, so a
// player who hits a problem there has nothing to send back unless the page keeps it. Entries go
// to the page's own record in log.js and come out of the export as ctrdx-<run>-browser.log.
//
// A classic script, loaded first in <head>, so it is listening before coi.js, the bootstrap
// module, or the runtime can say anything. log.js is imported only when there is something to
// write, and entries that arrive before it has loaded wait in memory. Nothing here may throw into
// the page: a broken capture costs the player this log, never the game.
(() => {
    // A page stuck in a loop can report the same failure every frame. The first few hundred say
    // everything the rest would, and rewriting an ever-growing record costs the page it lives on.
    const MAX_ENTRIES = 500;
    const MAX_ENTRY_LENGTH = 8000;

    // log.js reports its own storage failures under this prefix. Recording them would try the
    // same storage again, and fail again, for as long as the page is open.
    const OWN_PREFIX = "ctrdx-log:";

    const queue = [];
    let recorded = 0;
    let sink = null;
    let loading = null;

    const pad = (value, width = 2) => String(value).padStart(width, "0");

    /** Local time with its offset, the same shape the game's entries carry. */
    function timestamp(date) {
        const offset = -date.getTimezoneOffset();
        const sign = offset >= 0 ? "+" : "-";
        const magnitude = Math.abs(offset);
        return (
            `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}` +
            `T${pad(date.getHours())}:${pad(date.getMinutes())}:${pad(date.getSeconds())}` +
            `.${pad(date.getMilliseconds(), 3)}` +
            `${sign}${pad(Math.floor(magnitude / 60))}:${pad(magnitude % 60)}`
        );
    }

    function isErrorLike(value) {
        return (
            value instanceof Error ||
            (typeof value === "object" &&
                value !== null &&
                typeof value.name === "string" &&
                typeof value.message === "string")
        );
    }

    /**
     * Turns one console argument or rejection reason into text.
     *
     * Errors keep their stack. Chromium's stack already starts with the name and message, while
     * Firefox's and Safari's hold only the frames, so the heading is added only where missing.
     */
    function describe(value, depth = 0) {
        if (typeof value === "string") {
            return value;
        }
        if (isErrorLike(value)) {
            const heading = `${value.name}: ${value.message}`;
            const stack = typeof value.stack === "string" ? value.stack : "";
            let text = stack.startsWith(heading)
                ? stack
                : stack
                  ? `${heading}\n${stack}`
                  : heading;
            if (value.cause !== undefined && depth < 3) {
                text += `\nCaused by: ${describe(value.cause, depth + 1)}`;
            }
            return text;
        }
        if (typeof Event === "function" && value instanceof Event) {
            return `${value.type} event on ${describeTarget(value.target)}`;
        }
        if (typeof value === "object" && value !== null) {
            try {
                return JSON.stringify(value);
            } catch {
                return String(value);
            }
        }
        return String(value);
    }

    function describeTarget(target) {
        if (target === null || target === undefined) {
            return "nothing";
        }
        if (target === globalThis) {
            return "window";
        }
        const tag = target.tagName?.toLowerCase();
        const source = target.currentSrc || target.src || target.href;
        if (tag) {
            return source ? `<${tag}> ${source}` : `<${tag}>`;
        }
        return target.constructor?.name ?? String(target);
    }

    function header() {
        const serviceWorker = globalThis.navigator?.serviceWorker;
        const standalone = globalThis.matchMedia?.(
            "(display-mode: standalone)",
        )?.matches;
        return [
            "Cut the Rope DX - browser log (page warnings and errors)",
            `Page: ${globalThis.location?.href}`,
            `Cross-origin isolated: ${globalThis.crossOriginIsolated === true}`,
            `Service worker in control: ${Boolean(serviceWorker?.controller)}`,
            `Viewport: ${globalThis.innerWidth}x${globalThis.innerHeight} @${globalThis.devicePixelRatio}x` +
                (standalone ? " (installed app)" : ""),
            `Language: ${globalThis.navigator?.language}`,
        ].join("\n");
    }

    function drain() {
        for (const line of queue.splice(0)) {
            sink.appendBrowser(line, true);
        }
    }

    function persist(line) {
        queue.push(line);
        if (sink !== null) {
            drain();
            return;
        }
        if (loading === null) {
            loading = import("./log.js").then(
                (module) => {
                    sink = module;
                    module.beginBrowser(header());
                    drain();
                },
                () => {
                    // No store, so nothing can be kept. The developer console, where there is
                    // one, still has everything.
                    queue.length = 0;
                },
            );
        }
    }

    function record(level, category, message) {
        if (recorded > MAX_ENTRIES) {
            return;
        }
        recorded++;

        let text =
            recorded > MAX_ENTRIES
                ? `further entries were dropped after the first ${MAX_ENTRIES}`
                : message;
        if (text.length > MAX_ENTRY_LENGTH) {
            text = `${text.slice(0, MAX_ENTRY_LENGTH)}… (${text.length - MAX_ENTRY_LENGTH} more characters)`;
        }
        persist(`${timestamp(new Date())} [${level}] ${category} ${text}`);
    }

    function safely(action) {
        try {
            action();
        } catch {
            // The capture gives up on this entry rather than disturb what it was watching.
        }
    }

    for (const [method, level] of [
        ["warn", "Warning"],
        ["error", "Error"],
    ]) {
        const original = console[method];
        if (typeof original !== "function") {
            continue;
        }
        console[method] = function (...args) {
            original.apply(this, args);
            safely(() => {
                if (
                    typeof args[0] === "string" &&
                    args[0].startsWith(OWN_PREFIX)
                ) {
                    return;
                }
                record(
                    level,
                    "Browser.Console",
                    args.map((arg) => describe(arg)).join(" "),
                );
            });
        };
    }

    // Capture phase, because an element's failed load does not bubble: this is the only place a
    // missing script, image or stylesheet shows up at all. Recording it is all this does - the
    // bootstrap in index.html decides which failures stop the boot, and deliberately not from
    // here.
    globalThis.addEventListener(
        "error",
        (event) =>
            safely(() => {
                if (
                    typeof ErrorEvent === "function" &&
                    event instanceof ErrorEvent
                ) {
                    const where = event.filename
                        ? ` (${event.filename}:${event.lineno}:${event.colno})`
                        : "";
                    const detail =
                        event.error !== undefined && event.error !== null
                            ? `\n${describe(event.error)}`
                            : "";
                    record(
                        "Error",
                        "Browser.Uncaught",
                        `${event.message}${where}${detail}`,
                    );
                    return;
                }
                if (event.target !== globalThis) {
                    record(
                        "Error",
                        "Browser.Resource",
                        `failed to load ${describeTarget(event.target)}`,
                    );
                }
            }),
        true,
    );

    globalThis.addEventListener("unhandledrejection", (event) =>
        safely(() =>
            record("Error", "Browser.Rejection", describe(event.reason)),
        ),
    );

    // The export reads the store, so it waits for anything still on its way there.
    globalThis.ctrdxCaptureSettled = () => loading ?? Promise.resolve();
})();
