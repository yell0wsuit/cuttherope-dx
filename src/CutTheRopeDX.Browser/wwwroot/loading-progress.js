// The splash bubble has room for two short lines, so the counter leaves out which stage it is
// counting. The stages run one after another, so the count restarting is the only sign of a
// new one the player needs. The stage stays on the element for anyone inspecting a stall.
export function formatLoadingProgress(loaded, total) {
    return `Loading:\n${loaded} of ${total}`;
}

export function setLoadingProgress(type, loaded, total) {
    const progress = document.getElementById("splash-progress");
    if (progress !== null) {
        progress.textContent = formatLoadingProgress(loaded, total);
        progress.dataset.stage = type;
    }
}
