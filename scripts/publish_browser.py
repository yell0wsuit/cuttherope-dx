#!/usr/bin/env python3
"""Publishes the browser game's two runtimes into one site.

The threaded runtime needs cross-origin isolation while the single-threaded one does not, so
which of them a visit can run is decided in the browser, by wwwroot/runtime-mode.js, after
the page has loaded.
Both therefore have to be on the server, under framework paths that do
not collide: `_framework/` for the threaded runtime and `_framework-single/` for the
fallback.
The WebAssembly SDK writes `_framework/` and offers no say in it, so the fallback
is published on its own and its tree is moved into place here.

Everything else comes from the threaded publish and is shared. The service worker's asset
manifest is merged so it covers both trees, and its version is recomputed so a change in
either one retires the old cache.

Usage:
    python3 scripts/publish_browser.py --output dist
    python3 scripts/publish_browser.py --output dist --no-aot
"""

from __future__ import annotations

import argparse
import base64
import hashlib
import json
import re
import shutil
import subprocess
import sys
import threading
from pathlib import Path

REPOSITORY_ROOT = Path(__file__).resolve().parent.parent
PROJECT = REPOSITORY_ROOT / "src/CutTheRopeDX.Browser/CutTheRopeDX.Browser.csproj"

#: Where the SDK writes a runtime, and where the fallback's copy has to end up.
FRAMEWORK = "_framework"
SINGLE_FRAMEWORK = "_framework-single"

#: The published service worker and the manifest it reads.
WORKER = "coi-sw.js"
ASSETS_MANIFEST = "service-worker-assets.js"

VERSION_COMMENT = re.compile(r"^/\* Manifest version: [^*]* \*/\r?\n")


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--output",
        type=Path,
        default=REPOSITORY_ROOT / "dist",
        help="where the finished site is written (default: dist)",
    )
    parser.add_argument(
        "--configuration", default="Release", help="build configuration"
    )
    parser.add_argument(
        "--no-aot",
        action="store_true",
        help="skip AOT compilation, for a check that publishing works at all",
    )
    parser.add_argument(
        "--work",
        type=Path,
        default=REPOSITORY_ROOT / "artifacts",
        help="where the two publishes are staged (default: artifacts)",
    )
    arguments = parser.parse_args(argv)

    threaded = arguments.work / "browser-threaded"
    single = arguments.work / "browser-single"

    # Concurrently, because most of each publish is the emscripten link, which runs on one
    # core for a minute or more. Side by side the pair costs about what the slower one does.
    publish_all(
        [
            (threaded, publish_command(threaded, arguments, single_threaded=False)),
            (single, publish_command(single, arguments, single_threaded=True)),
        ]
    )

    site = arguments.output
    if site.exists():
        shutil.rmtree(site)
    shutil.copytree(threaded / "publish", site)

    merge_fallback(site / "wwwroot", single / "publish/wwwroot")
    print(f"published both runtimes to {site}")
    return 0


def publish_command(
    destination: Path, arguments: argparse.Namespace, single_threaded: bool
) -> list[str]:
    """Returns the publish of one runtime into its own output and intermediate tree.

    The two builds disagree about the emscripten link, so they cannot share an obj
    directory: the second would reuse the first's native objects and quietly produce a
    runtime that is neither. Separate artifacts paths are what keeps them apart, and also
    what lets them run at the same time.
    """
    command = [
        "dotnet",
        "publish",
        str(PROJECT),
        "-c",
        arguments.configuration,
        "-o",
        str(destination / "publish"),
        f"-p:ArtifactsPath={destination}",
        f"-p:RunAOTCompilation={'false' if arguments.no_aot else 'true'}",
    ]
    if single_threaded:
        # Only the framework tree survives from this publish, so the content payload it
        # would otherwise copy is 30MB written to be deleted.
        command += ["-p:CtrdxSingleThreaded=true", "-p:CtrdxSkipContent=true"]
    return command


def publish_all(publishes: list[tuple[Path, list[str]]]) -> None:
    """Runs every publish at once, prefixing each output line with the tree it builds.

    All of them are waited for even after one fails, so no build is left writing into a
    tree the next run is about to delete.
    """
    for destination, _ in publishes:
        if destination.exists():
            shutil.rmtree(destination)

    lock = threading.Lock()
    width = max(len(destination.name) for destination, _ in publishes)

    def relay(name: str, stream) -> None:
        for line in stream:
            with lock:
                sys.stdout.write(f"[{name:<{width}}] {line}")
                sys.stdout.flush()

    running = []
    for destination, command in publishes:
        print(f"publishing {destination.name}", flush=True)
        process = subprocess.Popen(
            command,
            cwd=REPOSITORY_ROOT,
            stdout=subprocess.PIPE,
            stderr=subprocess.STDOUT,
            text=True,
            encoding="utf-8",
            errors="replace",
        )
        reader = threading.Thread(
            target=relay, args=(destination.name, process.stdout), daemon=True
        )
        reader.start()
        running.append((destination.name, process, reader))

    failed = []
    for name, process, reader in running:
        process.wait()
        reader.join()
        if process.returncode != 0:
            failed.append(f"{name} (exit {process.returncode})")
    if failed:
        raise SystemExit(f"publish failed: {', '.join(failed)}")


def merge_fallback(site: Path, fallback: Path) -> None:
    """Lays the fallback's runtime beside the threaded one and reconciles the manifest."""
    source = fallback / FRAMEWORK
    if not source.is_dir():
        raise SystemExit(f"the fallback publish has no {FRAMEWORK}: {source}")

    destination = site / SINGLE_FRAMEWORK
    if destination.exists():
        shutil.rmtree(destination)
    # Moved rather than copied: this is the only thing the fallback publish is kept for,
    # and it is the larger half of what that publish produced.
    shutil.move(str(source), str(destination))

    merged, version = merge_manifests(
        read_manifest(site / ASSETS_MANIFEST),
        read_manifest(fallback / ASSETS_MANIFEST),
    )
    write_manifest(site / ASSETS_MANIFEST, merged, version)
    stamp_worker(site / WORKER, version)
    print(f"merged {len(merged['assets'])} assets at version {version}")


def read_manifest(path: Path) -> dict:
    """Reads `self.assetsManifest = {...};` as the object it assigns."""
    text = path.read_text(encoding="utf-8")
    return json.loads(text[text.index("{") : text.rindex("}") + 1])


def merge_manifests(threaded: dict, single: dict) -> tuple[dict, str]:
    """Returns the two manifests as one, and the version that stands for the pair.

    The fallback's assets are readdressed on the way in, because they were published at
    `_framework/` and now live at `_framework-single/`. Anything else it lists - the page,
    the scripts - is the same file the threaded publish already contributed, so only its
    runtime crosses over.

    The version is derived from the two the SDK calculated rather than recalculated from
    the assets. Each of those already changes when anything in its own tree does, which is
    the whole property the cache name needs, and inheriting it avoids keeping a private
    copy of how the SDK arrives at one.
    """
    assets = list(threaded["assets"])
    known = {asset["url"] for asset in assets}
    for asset in single["assets"]:
        if not asset["url"].startswith(f"{FRAMEWORK}/"):
            continue
        moved = dict(asset)
        moved["url"] = f"{SINGLE_FRAMEWORK}/{asset['url'][len(FRAMEWORK) + 1 :]}"
        if moved["url"] in known:
            continue
        known.add(moved["url"])
        assets.append(moved)

    combined = f"{threaded['version']}{single['version']}".encode("utf-8")
    version = base64.b64encode(hashlib.sha256(combined).digest()).decode()[:8]
    return {"version": version, "assets": assets}, version


def write_manifest(path: Path, manifest: dict, version: str) -> None:
    manifest = {"version": version, "assets": manifest["assets"]}
    body = json.dumps(manifest, indent=2)
    path.write_text(f"self.assetsManifest = {body};\n", encoding="utf-8")
    drop_stale_copies(path)


def stamp_worker(path: Path, version: str) -> None:
    """Replaces the version the SDK stamped into the worker with the merged one.

    The comment is what makes the worker's own bytes change between publishes. A browser
    compares them to decide whether a new worker exists at all, so a stale one here means
    a deployment nobody is offered.
    """
    text = path.read_text(encoding="utf-8")
    text = VERSION_COMMENT.sub("", text, count=1)
    path.write_text(f"/* Manifest version: {version} */\n{text}", encoding="utf-8")
    drop_stale_copies(path)


def drop_stale_copies(path: Path) -> None:
    """Removes the .br and .gz beside a file this script rewrote.

    The SDK compressed the version it published, and those copies now decode to something
    the site no longer serves. A server that picks one by Accept-Encoding would hand out
    the pre-merge manifest to exactly the visitors whose browsers ask for it.
    """
    for suffix in (".br", ".gz"):
        compressed = path.with_name(path.name + suffix)
        if compressed.exists():
            compressed.unlink()


if __name__ == "__main__":
    sys.exit(main())
