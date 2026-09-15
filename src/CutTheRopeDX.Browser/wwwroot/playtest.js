// Playtest transport. The editor and this game are served from one origin, so a BroadcastChannel
// reaches between them; it carries the level itself, because a browser tab has no argv to put it in.
// The editor also leaves the level in localStorage before it opens this tab, because opening it
// backgrounds the editor, and a mobile browser may suspend or discard a background tab before it
// can answer over the channel.

const CHANNEL = "ctrdx-playtest";
const STORAGE_PREFIX = "ctrdx-playtest:";

let channel = null;
let inbox = [];

/** The session nonce from ?playtest=..., or "" when this is a normal launch. */
export function nonceFromQuery() {
    return new URLSearchParams(globalThis.location.search).get("playtest") ?? "";
}

/** Opens the channel and starts queueing messages. Safe to call more than once. */
export function open() {
    if (channel) {
        return;
    }
    channel = new BroadcastChannel(CHANNEL);
    channel.onmessage = (event) => {
        // Everything on this channel is a JSON string; anything else is not ours.
        if (typeof event.data === "string") {
            inbox.push(event.data);
        }
    };
}

/** Posts one JSON message. A no-op before open(). */
export function post(json) {
    channel?.postMessage(json);
}

/**
 * The level message the editor stored for this session, or "" when there is none.
 *
 * Read rather than taken: a reload of this tab needs it again, and the editor owns its removal.
 */
export function storedLevel(nonce) {
    try {
        const stored = globalThis.localStorage.getItem(STORAGE_PREFIX + nonce);
        return stored === null ? "" : (JSON.parse(stored).message ?? "");
    } catch {
        // Blocked site data or a malformed entry: the channel is still there to fall back on.
        return "";
    }
}

/** Takes every message queued since the last call. */
export function drain() {
    if (inbox.length === 0) {
        return [];
    }
    const taken = inbox;
    inbox = [];
    return taken;
}

/**
 * Closes this window. Only legal because the editor opened it with window.open; a tab the user
 * navigated to themselves ignores this, which is why the caller falls back to doing nothing.
 */
export function closeWindow() {
    globalThis.close();
}
