// Two short lines, to fit the splash bubble: the stage, and how far into it the count is.
// The stages run one after another, so naming each is what tells a restarted count from a
// stalled one.
export function formatLoadingProgress(type, loaded, total) {
    return `Loading ${type}:\n${loaded} of ${total}`;
}

export function setLoadingProgress(type, loaded, total) {
    const progress = document.getElementById("splash-progress");
    if (progress !== null) {
        progress.textContent = formatLoadingProgress(type, loaded, total);
    }
}
