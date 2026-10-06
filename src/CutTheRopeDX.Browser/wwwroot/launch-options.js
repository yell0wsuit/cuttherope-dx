// Launch options read from the page URL, which stands in for the desktop command line.

/** The menu style from ?menu=..., or "" when the URL names none. */
export function menuFromQuery() {
    return new URLSearchParams(globalThis.location.search).get("menu") ?? "";
}
