// The small bubbles that drift up behind the splash, ported from the loader of the original
// HTML5 Cut the Rope. Each one rises from below the bottom edge to above the top at a constant
// speed while swaying sideways, and comes back at a fresh size and column every time it leaves.
// The big progress bubble is markup rather than part of this canvas, so its text stays real
// text; its bob lives in index.html's CSS.
//
// Purely decorative. Every failure here ends in the animation stopping, never in an exception:
// index.html treats any uncaught error during boot as a failed boot.

// Delay before the first rise, sway duration, rise duration and sway width, in ms and CSS
// pixels, as the original tuned them.
const BUBBLES = [
    { delay: 0, swayMs: 3000, riseMs: 5000, sway: 300 },
    { delay: 300, swayMs: 5000, riseMs: 6000, sway: 400 },
    { delay: 2000, swayMs: 2000, riseMs: 4000, sway: 200 },
    { delay: 3200, swayMs: 3000, riseMs: 5000, sway: 300 },
];

function easeInOutQuad(t, duration) {
    const p = Math.min(t / duration, 1) * 2;
    return p < 1 ? (p * p) / 2 : 1 - ((2 - p) * (2 - p)) / 2;
}

function start() {
    const splash = document.getElementById("splash");
    const canvas = document.getElementById("splash-bubbles");
    if (splash === null || canvas === null) {
        return;
    }
    const context = canvas.getContext("2d");
    if (context === null) {
        return;
    }
    const reducedMotion = globalThis.matchMedia?.(
        "(prefers-reduced-motion: reduce)",
    );

    const image = new Image();
    image.decoding = "async";
    image.src = "./splash/small-bubble.webp";

    let width = 0;
    let height = 0;
    let origin = null;
    let frame = 0;

    const bubbles = BUBBLES.map((tuning) => ({
        ...tuning,
        riseStart: 0,
        swayStart: 0,
        reversed: false,
        scale: 1,
        left: 0,
    }));

    // The original kept the sway width and picked the column against a page at least 800px
    // wide. On a phone that would push most bubbles off the left edge, so both are fitted to
    // whatever width there is.
    function respawn(bubble) {
        bubble.scale = 0.5 + 0.5 * Math.random();
        const size = image.naturalWidth * bubble.scale;
        bubble.span = Math.max(0, Math.min(bubble.sway, width - size));
        bubble.left = Math.random() * Math.max(0, width - size - bubble.span);
    }

    function resize() {
        const ratio = globalThis.devicePixelRatio || 1;
        width = canvas.clientWidth;
        height = canvas.clientHeight;
        canvas.width = Math.round(width * ratio);
        canvas.height = Math.round(height * ratio);
        context.setTransform(ratio, 0, 0, ratio, 0, 0);
    }

    function draw(now) {
        frame = 0;
        if (
            splash.classList.contains("hidden") ||
            reducedMotion?.matches === true
        ) {
            context.clearRect(0, 0, width, height);
            return;
        }
        if (
            canvas.clientWidth !== width ||
            canvas.clientHeight !== height
        ) {
            resize();
        }

        origin ??= now;
        const elapsed = now - origin;
        context.clearRect(0, 0, width, height);

        if (image.complete && image.naturalWidth > 0) {
            for (const bubble of bubbles) {
                if (elapsed < bubble.delay) {
                    bubble.riseStart = elapsed;
                    bubble.swayStart = elapsed;
                    continue;
                }
                if (bubble.span === undefined) {
                    respawn(bubble);
                }
                const size = image.naturalWidth * bubble.scale;
                const tall = image.naturalHeight * bubble.scale;

                if (elapsed - bubble.riseStart >= bubble.riseMs) {
                    bubble.riseStart = elapsed;
                    respawn(bubble);
                }
                if (elapsed - bubble.swayStart >= bubble.swayMs) {
                    bubble.swayStart = elapsed;
                    bubble.reversed = !bubble.reversed;
                }

                const rise = (elapsed - bubble.riseStart) / bubble.riseMs;
                const y = height + tall - rise * (height + 2 * tall);
                const sway =
                    bubble.span *
                    easeInOutQuad(elapsed - bubble.swayStart, bubble.swayMs);
                const x = Math.min(
                    bubble.left + (bubble.reversed ? bubble.span - sway : sway),
                    width - size,
                );
                context.drawImage(image, x, y, size, tall);
            }
        }
        schedule();
    }

    function schedule() {
        if (frame === 0) {
            frame = requestAnimationFrame((now) => {
                try {
                    draw(now);
                } catch (error) {
                    console.warn("ctrdx: splash bubbles stopped", error);
                }
            });
        }
    }

    // The splash fades out for play and comes back for a lost graphics context, so the loop
    // follows its visibility instead of being started and stopped by everyone who changes it.
    new MutationObserver(schedule).observe(splash, {
        attributes: true,
        attributeFilter: ["class"],
    });
    reducedMotion?.addEventListener?.("change", schedule);
    schedule();
}

try {
    start();
} catch (error) {
    console.warn("ctrdx: splash bubbles unavailable", error);
}
