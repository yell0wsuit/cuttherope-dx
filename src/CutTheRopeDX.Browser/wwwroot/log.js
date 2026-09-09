// Per-run log storage for the web build, and the zip a player sends back.
//
// The game runs on a worker and the export button lives on the page, so the two halves of this
// module run in different threads with different module instances. IndexedDB is what they share:
// the worker appends, the page reads. Nothing here is on the frame path - entries are buffered
// and written in batches, because a transaction per line would cost more than the logging is
// worth.

const DB_NAME = "ctrdx-logs";
const STORE = "sessions";
const DB_VERSION = 1;

// Ten runs, matching what the desktop build keeps beside its save data.
const MAX_SESSIONS = 10;

// Long enough that a quiet run writes rarely, short enough that a browser closed without warning
// loses little. Warnings and worse bypass it entirely.
const FLUSH_DELAY_MS = 2000;

let sessionId = null;
let pending = [];
let flushTimer = null;
let dbPromise = null;

function openDatabase() {
    if (dbPromise === null) {
        dbPromise = new Promise((resolve, reject) => {
            const request = indexedDB.open(DB_NAME, DB_VERSION);
            request.onupgradeneeded = () => {
                if (!request.result.objectStoreNames.contains(STORE)) {
                    request.result.createObjectStore(STORE, { keyPath: "id" });
                }
            };
            request.onsuccess = () => resolve(request.result);
            request.onerror = () => reject(request.error);
        });
    }
    return dbPromise;
}

function transaction(db, mode) {
    return db.transaction(STORE, mode).objectStore(STORE);
}

/** Names a run the way the desktop build names its file, so both read alike. */
export function formatSessionId(date) {
    const pad = (value, width) => String(value).padStart(width, "0");
    return (
        `${date.getFullYear()}${pad(date.getMonth() + 1, 2)}${pad(date.getDate(), 2)}` +
        `-${pad(date.getHours(), 2)}${pad(date.getMinutes(), 2)}${pad(date.getSeconds(), 2)}`
    );
}

/** Starts this run's session. Safe to call more than once; only the first takes effect. */
export function begin(header) {
    if (sessionId !== null) {
        return sessionId;
    }

    sessionId = formatSessionId(new Date());
    pending.push(header);
    void flush();
    void prune();
    return sessionId;
}

/**
 * Adds one entry.
 *
 * @param {string} line The formatted entry.
 * @param {boolean} urgent Whether to write through rather than wait for the batch.
 */
export function append(line, urgent) {
    if (sessionId === null) {
        begin("");
    }

    pending.push(line);
    if (urgent) {
        void flush();
        return;
    }

    if (flushTimer === null) {
        flushTimer = setTimeout(() => {
            flushTimer = null;
            void flush();
        }, FLUSH_DELAY_MS);
    }
}

/** Writes whatever is buffered. A storage failure drops the batch rather than the run. */
export async function flush() {
    if (pending.length === 0 || sessionId === null) {
        return;
    }

    const batch = pending;
    pending = [];
    if (flushTimer !== null) {
        clearTimeout(flushTimer);
        flushTimer = null;
    }

    try {
        const db = await openDatabase();
        const store = transaction(db, "readwrite");
        const existing = await request(store.get(sessionId));
        const text = (existing?.text ?? "") + batch.join("\n") + "\n";
        await request(
            transaction(db, "readwrite").put({ id: sessionId, text }),
        );
    } catch (error) {
        console.warn("ctrdx-log: could not persist entries", error);
    }
}

function request(operation) {
    return new Promise((resolve, reject) => {
        operation.onsuccess = () => resolve(operation.result);
        operation.onerror = () => reject(operation.error);
    });
}

/** Drops the oldest runs so the database cannot grow without limit. */
async function prune() {
    try {
        const db = await openDatabase();
        const ids = await request(transaction(db, "readonly").getAllKeys());
        const doomed = ids
            .sort()
            .slice(0, Math.max(0, ids.length - MAX_SESSIONS));
        for (const id of doomed) {
            await request(transaction(db, "readwrite").delete(id));
        }
    } catch (error) {
        console.warn("ctrdx-log: could not prune old sessions", error);
    }
}

/** Reads every stored run, newest last. */
export async function readAll() {
    const db = await openDatabase();
    const sessions = await request(transaction(db, "readonly").getAll());
    return sessions.sort((a, b) => (a.id < b.id ? -1 : a.id > b.id ? 1 : 0));
}

const CRC_TABLE = (() => {
    const table = new Uint32Array(256);
    for (let i = 0; i < 256; i++) {
        let value = i;
        for (let bit = 0; bit < 8; bit++) {
            value = value & 1 ? 0xedb88320 ^ (value >>> 1) : value >>> 1;
        }
        table[i] = value >>> 0;
    }
    return table;
})();

export function crc32(bytes) {
    let crc = 0xffffffff;
    for (let i = 0; i < bytes.length; i++) {
        crc = CRC_TABLE[(crc ^ bytes[i]) & 0xff] ^ (crc >>> 8);
    }
    return (crc ^ 0xffffffff) >>> 0;
}

async function deflate(bytes) {
    // Every browser this build supports has compression streams; the check is for the one that
    // turns it off rather than for an old engine. Stored entries still make a valid archive.
    if (typeof CompressionStream !== "function") {
        return null;
    }

    try {
        const stream = new Blob([bytes])
            .stream()
            .pipeThrough(new CompressionStream("deflate-raw"));
        return new Uint8Array(await new Response(stream).arrayBuffer());
    } catch {
        return null;
    }
}

function dosTime(date) {
    const time =
        (date.getHours() << 11) |
        (date.getMinutes() << 5) |
        (date.getSeconds() >> 1);
    const day =
        ((date.getFullYear() - 1980) << 9) |
        ((date.getMonth() + 1) << 5) |
        date.getDate();
    return { time, day };
}

/**
 * Builds a zip holding one .log per stored run.
 *
 * @param {{name: string, bytes: Uint8Array}[]} files Entries to archive.
 * @returns {Promise<Blob>} The archive.
 */
export async function buildZip(files) {
    const encoder = new TextEncoder();
    const parts = [];
    const central = [];
    const stamp = dosTime(new Date());
    let offset = 0;

    for (const file of files) {
        const name = encoder.encode(file.name);
        const compressed = await deflate(file.bytes);
        const body = compressed ?? file.bytes;
        const method = compressed === null ? 0 : 8;
        const crc = crc32(file.bytes);

        const local = new DataView(new ArrayBuffer(30));
        local.setUint32(0, 0x04034b50, true);
        local.setUint16(4, 20, true);
        local.setUint16(6, 0, true);
        local.setUint16(8, method, true);
        local.setUint16(10, stamp.time, true);
        local.setUint16(12, stamp.day, true);
        local.setUint32(14, crc, true);
        local.setUint32(18, body.length, true);
        local.setUint32(22, file.bytes.length, true);
        local.setUint16(26, name.length, true);
        local.setUint16(28, 0, true);
        parts.push(new Uint8Array(local.buffer), name, body);

        const entry = new DataView(new ArrayBuffer(46));
        entry.setUint32(0, 0x02014b50, true);
        entry.setUint16(4, 20, true);
        entry.setUint16(6, 20, true);
        entry.setUint16(8, 0, true);
        entry.setUint16(10, method, true);
        entry.setUint16(12, stamp.time, true);
        entry.setUint16(14, stamp.day, true);
        entry.setUint32(16, crc, true);
        entry.setUint32(20, body.length, true);
        entry.setUint32(24, file.bytes.length, true);
        entry.setUint16(28, name.length, true);
        entry.setUint32(42, offset, true);
        central.push(new Uint8Array(entry.buffer), name);

        offset += 30 + name.length + body.length;
    }

    const centralSize = central.reduce((total, part) => total + part.length, 0);
    const end = new DataView(new ArrayBuffer(22));
    end.setUint32(0, 0x06054b50, true);
    end.setUint16(8, files.length, true);
    end.setUint16(10, files.length, true);
    end.setUint32(12, centralSize, true);
    end.setUint32(16, offset, true);

    return new Blob([...parts, ...central, new Uint8Array(end.buffer)], {
        type: "application/zip",
    });
}

/**
 * Writes every stored run into a zip and hands it to the browser to save.
 *
 * @returns {Promise<number>} How many runs the archive holds.
 */
export async function exportZip() {
    await flush();
    const sessions = await readAll();
    if (sessions.length === 0) {
        return 0;
    }

    const encoder = new TextEncoder();
    const archive = await buildZip(
        sessions.map((session) => ({
            name: `ctrdx-${session.id}.log`,
            bytes: encoder.encode(session.text),
        })),
    );

    const url = URL.createObjectURL(archive);
    const link = document.createElement("a");
    link.href = url;
    link.download = `ctrdx-logs-${formatSessionId(new Date())}.zip`;
    link.click();
    // Revoked on a later turn: revoking before the browser has taken the blob cancels the save.
    setTimeout(() => URL.revokeObjectURL(url), 60000);
    return sessions.length;
}
