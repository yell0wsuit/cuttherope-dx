// Custom cursor, as a browser can do it.
//
// The desktop host swaps a native cursor between two bitmaps rather than drawing one into the
// scene, so the CSS equivalent is the faithful port: same two images, same top-left hotspot,
// and the pointer keeps the responsiveness of a real cursor instead of trailing a frame behind.

// CSS pixels per art pixel, matching SdlCursorService.BaseScale. The size is fixed rather than
// following the canvas: a cursor shrunk with the art turns unreadable in a small window. The
// art itself is authored at twice this, so it doubles as the high-density image.
const BASE_SCALE = 0.5;

const canvas = document.getElementById("game");
// The cutscene overlay covers the canvas, so a cursor set only there would be invisible
// for the whole of a cutscene - including the paused state, where Core asks for it back.
const surfaces = [canvas, document.getElementById("movie")];
const sources = {
    idle: "./content/images/cursor.webp",
    pressed: "./content/images/cursor_active.webp",
};

const values = {};
let enabled = true;
let pressed = false;

/** Applies the cursor the current state calls for. */
function apply() {
    if (!enabled) {
        for (const surface of surfaces) {
            surface.style.cursor = "none";
        }
        return;
    }
    // Until the bitmaps load there is nothing to show, so the pointer stays the system one.
    const value = values[pressed ? "pressed" : "idle"] ?? "auto";
    for (const surface of surfaces) {
        surface.style.cursor = value;
    }
}

/**
 * Builds the cursor declaration for one loaded bitmap.
 *
 * @param {HTMLImageElement} image
 * @param {string} src
 * @returns {string}
 */
function build(image, src) {
    const target = document.createElement("canvas");
    target.width = Math.max(1, Math.round(image.naturalWidth * BASE_SCALE));
    target.height = Math.max(1, Math.round(image.naturalHeight * BASE_SCALE));
    const context = target.getContext("2d");
    // The default low-quality filter drops the thin dark outline when halving.
    context.imageSmoothingQuality = "high";
    context.drawImage(image, 0, 0, target.width, target.height);
    const base = target.toDataURL("image/png");

    // The first form the browser accepts wins; image-set in cursor is still uneven, and an
    // unsupported value would otherwise throw the whole declaration away.
    const set = `url("${base}") 1x, url("${src}") 2x`;
    const candidates = [
        `image-set(${set}) 0 0, auto`,
        `-webkit-image-set(${set}) 0 0, auto`,
        `url("${base}") 0 0, auto`,
    ];
    return (
        candidates.find((value) => CSS.supports("cursor", value)) ??
        candidates.at(-1)
    );
}

for (const [name, src] of Object.entries(sources)) {
    const image = new Image();
    image.decoding = "async";
    image.addEventListener("load", () => {
        values[name] = build(image, src);
        apply();
    });
    image.src = src;
}

/**
 * Shows or hides the cursor over the canvas. Core hides it while a cutscene plays.
 *
 * @param {boolean} value
 */
export function setEnabled(value) {
    enabled = value;
    apply();
}

/**
 * Switches between the idle and pressed bitmaps.
 *
 * @param {boolean} value
 */
export function setPressed(value) {
    pressed = value;
    apply();
}
