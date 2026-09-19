// Run with node --test scripts/tests/test_browser_fallback.mjs.
import { test } from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import vm from "node:vm";
import {
    parseModeQuery,
    probeEnvironment,
    selectRuntime,
} from "../../src/CutTheRopeDX.Browser/wwwroot/runtime-mode.js";
import * as events from "../../src/CutTheRopeDX.Browser/wwwroot/host-events.js";

const root = new URL("../../src/CutTheRopeDX.Browser/", import.meta.url);
const source = (name) => readFileSync(new URL(name, root), "utf8");
const capable = {
    isolated: true,
    sharedMemory: true,
    offscreenCanvas: true,
    workerGraphics: true,
    localGraphics: true,
};

test("capable pages choose the threaded runtime", () => {
    assert.equal(selectRuntime(capable).runtime, "./_framework/dotnet.js");
});
for (const capability of [
    "isolated",
    "sharedMemory",
    "offscreenCanvas",
    "workerGraphics",
]) {
    test(`missing ${capability} selects the single-thread runtime`, () => {
        assert.equal(
            selectRuntime({ ...capable, [capability]: false }).runtime,
            "./_framework-single/dotnet.js",
        );
    });
}
test("missing graphics reports unsupported", () => {
    assert.equal(
        selectRuntime({
            ...capable,
            workerGraphics: false,
            localGraphics: false,
        }).mode,
        "unsupported",
    );
});
test("mode query accepts single and multi and reports anything else raw", () => {
    assert.deepEqual(parseModeQuery("?mode=single"), {
        mode: "single",
        raw: "single",
    });
    assert.deepEqual(parseModeQuery("?playtest=x&mode=MULTI"), {
        mode: "threaded",
        raw: "MULTI",
    });
    assert.deepEqual(parseModeQuery("?mode=thread"), {
        mode: null,
        raw: "thread",
    });
    assert.deepEqual(parseModeQuery(""), { mode: null, raw: null });
});
test("requesting single overrides a capable page", () => {
    assert.equal(selectRuntime(capable, "single").mode, "single");
});
test("requesting single cannot bypass missing local graphics", () => {
    assert.equal(
        selectRuntime({ ...capable, localGraphics: false }, "single").mode,
        "threaded",
    );
});
test("requesting multi where it cannot run still steps down", () => {
    assert.equal(
        selectRuntime({ ...capable, isolated: false }, "threaded").mode,
        "single",
    );
    assert.equal(selectRuntime(capable, "threaded").mode, "threaded");
});
test("blocked worker graphics can still use local graphics and release the probe", () => {
    let releases = 0;
    const result = probeEnvironment({
        crossOriginIsolated: true,
        SharedArrayBuffer,
        HTMLCanvasElement: { prototype: { transferControlToOffscreen() {} } },
        OffscreenCanvas: class {
            constructor() {
                throw Error("blocked");
            }
        },
        document: {
            createElement() {
                return {
                    getContext() {
                        return {
                            getExtension() {
                                return {
                                    loseContext() {
                                        releases++;
                                    },
                                };
                            },
                        };
                    },
                };
            },
        },
    });
    assert.equal(selectRuntime(result).mode, "single");
    assert.equal(releases, 1);
});

// Use real wasm memory, including the single-thread runtime's heap-only exports.
for (const shared of [false, true]) {
    test(`${shared ? "shared" : "heap-only"} memory handles input, growth, and lifecycle wakes`, () => {
        const memory = new WebAssembly.Memory({
            initial: 1,
            maximum: 2,
            shared,
        });
        let wakes = 0;
        const module = shared
            ? {
                  wasmMemory: memory,
                  PThread: {
                      pthreads: {
                          7: {
                              postMessage() {
                                  wakes++;
                              },
                          },
                      },
                  },
              }
            : {
                  HEAPU8: new Uint8Array(memory.buffer),
                  _ctrdx_wake() {
                      wakes++;
                  },
              };
        globalThis.ctrdxWasmModule = module;
        events.attach(64, shared ? 7 : 0);
        events.resize(800, 600, 1);
        assert.equal(new Int32Array(memory.buffer)[16], 1);
        memory.grow(1);
        module.HEAPU8 = new Uint8Array(memory.buffer);
        events.wheel(120);
        const words = new Int32Array(memory.buffer);
        assert.equal(words[16], 2);
        assert.equal(words[27], 120);
        events.active(false, true);
        events.start();
        assert.equal(wakes, 2);
        assert.equal(words[16], 4);
    });
}
test("pointer moves reserve room for a release", () => {
    const memory = new WebAssembly.Memory({ initial: 1 });
    globalThis.ctrdxWasmModule = { HEAPU8: new Uint8Array(memory.buffer) };
    events.attach(64, 0);
    for (let i = 0; i < 1100; i++) events.pointer(1, i, 0, 800, 600);
    const words = new Int32Array(memory.buffer);
    const count = words[16];
    assert.equal(count, 1024 - 32);
    events.pointer(2, 0, 0, 800, 600);
    assert.equal(words[16], count + 1);
});

test("native wakes use the animation frame clock in both runtimes", () => {
    // Execute the native wake's calls and inline JS with Emscripten's two clock
    // behaviors. Threaded get_now includes timeOrigin; animation frames do not.
    const body = source("Native/ctrdxhost.cpp")
        .match(/void ctrdx_wake\(void\)\s*\{([\s\S]*?)\n\}/)[1]
        .replace(/EM_ASM\(\{([\s\S]*?)\}\);/g, "$1");
    for (const threaded of [false, true]) {
        const frames = [];
        const context = vm.createContext({
            performance: { now: () => 2500 },
            emscripten_get_now: () => (threaded ? 1789000000000 : 0) + 2500,
            ctrdx_frame_entry: (value) => frames.push(value),
            _ctrdx_frame_entry: (value) => frames.push(value),
        });
        vm.runInContext(body, context);
        assert.deepEqual(frames, [2500]);
        assert.equal(context.ctrdxFrameToken, 1);
    }
});

test("COI timeout settles fallback and ignores a late controller", async () => {
    let timeout,
        controller,
        reloads = 0;
    const context = vm.createContext({
        console,
        crossOriginIsolated: false,
        sessionStorage: {
            getItem() {
                return null;
            },
            setItem() {},
        },
        location: {
            href: "https://example.test/",
            replace() {
                reloads++;
            },
        },
        setTimeout(fn) {
            timeout = fn;
            return 1;
        },
        clearTimeout() {},
        navigator: {
            serviceWorker: {
                register() {
                    return new Promise(() => {});
                },
                addEventListener(name, fn) {
                    controller = fn;
                },
            },
        },
    });
    vm.runInContext(source("wwwroot/coi.js"), context);
    timeout();
    assert.equal(await context.ctrdxIsolationReady, false);
    controller();
    assert.equal(reloads, 0);
});
test("missing service workers do not prevent fallback", async () => {
    const context = vm.createContext({
        console,
        navigator: {},
        crossOriginIsolated: false,
    });
    vm.runInContext(source("wwwroot/coi.js"), context);
    assert.equal(await context.ctrdxIsolationReady, false);
});
test("worker install caches common shell without downloading either runtime", async () => {
    const downloaded = [];
    const context = vm.createContext({
        URL,
        Request,
        Response,
        Headers,
        console,
        self: {
            assetsManifest: {
                version: "test",
                assets: [
                    { url: "index.html", hash: "sha256-test" },
                    { url: "_framework/dotnet.js", hash: "sha256-mt" },
                    { url: "_framework-single/dotnet.js", hash: "sha256-st" },
                ],
            },
            location: { href: "https://example.test/game/coi-sw.js" },
            importScripts() {},
            addEventListener() {},
        },
        caches: {
            async open() {
                return {
                    async add(request) {
                        downloaded.push(request.url);
                    },
                };
            },
        },
    });
    vm.runInContext(source("wwwroot/service-worker.published.js"), context);
    await vm.runInContext("onInstall()", context);
    assert.deepEqual(downloaded, ["https://example.test/game/index.html"]);
    assert.equal(vm.runInContext("shellHashes.size", context), 3);
});
function isolatedCoiContext(controller, registerCalls) {
    const registration = { id: "existing" };
    return vm.createContext({
        console,
        crossOriginIsolated: true,
        sessionStorage: { removeItem() {} },
        navigator: {
            serviceWorker: {
                controller,
                getRegistration() {
                    return Promise.resolve(registration);
                },
                register() {
                    registerCalls.push("register");
                    return Promise.resolve({ id: "registered" });
                },
            },
        },
    });
}
test("isolated controlled page hands over its worker before boot completes", async () => {
    const registerCalls = [];
    const context = isolatedCoiContext({}, registerCalls);
    vm.runInContext(source("wwwroot/coi.js"), context);
    assert.equal((await context.ctrdxServiceWorkerRegistration).id, "existing");
    context.ctrdxInstallWorker();
    await new Promise((resolve) => setImmediate(resolve));
    assert.deepEqual(registerCalls, []);
});
test("isolated uncontrolled page still waits for boot to register", async () => {
    const registerCalls = [];
    const context = isolatedCoiContext(null, registerCalls);
    vm.runInContext(source("wwwroot/coi.js"), context);
    let settled = false;
    context.ctrdxServiceWorkerRegistration.then(() => {
        settled = true;
    });
    await new Promise((resolve) => setImmediate(resolve));
    assert.equal(settled, false);
    context.ctrdxInstallWorker();
    assert.equal(
        (await context.ctrdxServiceWorkerRegistration).id,
        "registered",
    );
    assert.deepEqual(registerCalls, ["register"]);
});

async function captureContext() {
    const listeners = {};
    const printed = [];
    const sink = { header: null, lines: [] };
    const context = vm.createContext({
        console: {
            warn: (...args) => printed.push(["warn", ...args]),
            error: (...args) => printed.push(["error", ...args]),
        },
        addEventListener(name, fn) {
            listeners[name] = fn;
        },
        location: { href: "https://example.test/" },
        navigator: { language: "en" },
        Error,
    });
    context.loadLogStub = async () => ({
        beginBrowser(header) {
            sink.header = header;
        },
        appendBrowser(line) {
            sink.lines.push(line);
        },
    });
    // vm's own dynamic-import hook needs --experimental-vm-modules, which CI does not pass, so
    // the one import the capture makes is pointed at a stub instead.
    const body = source("wwwroot/console-capture.js");
    assert.ok(body.includes('import("./log.js")'));
    vm.runInContext(
        body.replace('import("./log.js")', "globalThis.loadLogStub()"),
        context,
    );
    return { context, listeners, printed, sink };
}
test("capture records console warnings and errors with their stacks", async () => {
    const { context, printed, sink } = await captureContext();
    const error = new Error("boom");
    context.console.warn("careful", { a: 1 });
    context.console.error("failed:", error);
    context.console.warn("ctrdx-log: could not persist entries");
    await context.ctrdxCaptureSettled();

    assert.equal(printed.length, 3);
    assert.match(sink.header, /browser log/);
    assert.equal(sink.lines.length, 2);
    assert.match(
        sink.lines[0],
        /\[Warning\] Browser\.Console careful \{"a":1\}$/,
    );
    assert.match(
        sink.lines[1],
        /\[Error\] Browser\.Console failed: Error: boom/,
    );
    assert.ok(sink.lines[1].includes(error.stack.split("\n")[1]));
});
test("capture records failed loads and unhandled rejections", async () => {
    const { context, listeners, sink } = await captureContext();
    listeners.error({
        target: { tagName: "SCRIPT", src: "https://example.test/main.js" },
    });
    listeners.unhandledrejection({ reason: new TypeError("nope") });
    await context.ctrdxCaptureSettled();

    assert.equal(sink.lines.length, 2);
    assert.match(
        sink.lines[0],
        /\[Error\] Browser\.Resource failed to load <script> https:\/\/example\.test\/main\.js$/,
    );
    assert.match(sink.lines[1], /\[Error\] Browser\.Rejection TypeError: nope/);
});
test("update watch checks at once and offers a worker already installing", async () => {
    let updates = 0,
        shown = 0,
        onStateChange;
    const installing = {
        state: "installing",
        addEventListener(name, fn) {
            onStateChange = fn;
        },
    };
    const registration = {
        waiting: null,
        installing,
        addEventListener() {},
        update() {
            updates++;
            return Promise.resolve();
        },
    };
    const dialog = {
        open: false,
        showModal() {
            shown++;
        },
    };
    const context = vm.createContext({
        console,
        navigator: { serviceWorker: { controller: {} } },
        document: {
            addEventListener() {},
            getElementById: (id) => (id === "update" ? dialog : {}),
        },
        addEventListener() {},
        removeEventListener() {},
        ctrdxServiceWorkerRegistration: Promise.resolve(registration),
    });
    vm.runInContext(source("wwwroot/pwa.js"), context);
    await new Promise((resolve) => setImmediate(resolve));

    assert.equal(updates, 1);
    assert.equal(shown, 0);
    installing.state = "installed";
    onStateChange();
    assert.equal(shown, 1);
});
