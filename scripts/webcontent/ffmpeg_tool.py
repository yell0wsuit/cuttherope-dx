"""Locates an ffmpeg to build web content with, and verifies it can encode what we need.

There is no ffmpeg in this repository's dependency graph, so one has to be found on the
machine running the build. Whichever one is found, it is never used until `require_encoders`
has confirmed it can produce the formats the pipeline asks for: an ffmpeg missing an encoder
the pipeline needs is common in the wild, and using one silently produces content that is
broken in a way nothing downstream notices.

Resolution order, most explicit first:

1. `CTRDX_FFMPEG`, an absolute path. This is how a build pins an exact binary -- CI points it
   at the FFmpeg 9 build the release scripts already download and checksum. Setting
   `CTRDX_FFMPEG_SHA256` alongside it makes the pin integrity-checked as well.
2. The Homebrew `ffmpeg@9` keg on macOS, which is the same one `bundle_ffmpeg_macos.sh` pins
   for the shipped runtime libraries.
3. Whatever is on PATH.
"""

from __future__ import annotations

import hashlib
import os
import platform
import re
import shutil
import subprocess
from collections.abc import Iterable
from pathlib import Path

#: The FFmpeg major this project pins everywhere: the runtime libraries the desktop build
#: bundles, the Homebrew formula CI installs, and the build tool resolved here.
FFMPEG_MAJOR = 9

#: Environment variable naming an exact ffmpeg binary to use.
BINARY_ENV = "CTRDX_FFMPEG"

#: Environment variable carrying the expected SHA-256 of that binary.
CHECKSUM_ENV = "CTRDX_FFMPEG_SHA256"

_HOMEBREW_PREFIXES = ("/opt/homebrew/opt", "/usr/local/opt")


class FfmpegNotFoundError(Exception):
    """No ffmpeg could be located."""


class MissingEncoderError(Exception):
    """The located ffmpeg lacks an encoder this pipeline requires."""


class ChecksumMismatchError(Exception):
    """The pinned ffmpeg is not the binary it was pinned to."""


def rid_for_platform() -> str:
    """Returns a short identifier for this platform, used in diagnostics."""
    system = platform.system()
    arch = "arm64" if platform.machine().lower() in {"arm64", "aarch64"} else "x64"
    if system == "Darwin":
        return f"osx-{arch}"
    if system == "Windows":
        return f"windows-{arch}"
    return f"linux-{arch}"


def sha256_of(path: Path) -> str:
    """Returns the lowercase hex SHA-256 of a file."""
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for block in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def _pinned_from_environment(environ: dict[str, str]) -> Path | None:
    """Returns the explicitly pinned binary, verifying it when a checksum is given."""
    configured = environ.get(BINARY_ENV)
    if not configured:
        return None

    binary = Path(configured)
    if not binary.is_file():
        raise FfmpegNotFoundError(
            f"{BINARY_ENV} points at {binary}, which is not a file."
        )

    expected = environ.get(CHECKSUM_ENV)
    if expected:
        actual = sha256_of(binary)
        if actual.lower() != expected.strip().lower():
            raise ChecksumMismatchError(
                f"{binary} has SHA-256 {actual}, but {CHECKSUM_ENV} pins {expected.strip()}."
            )
    return binary


def _homebrew_ffmpeg() -> Path | None:
    """Returns the pinned Homebrew keg's ffmpeg on macOS, if it is installed."""
    if platform.system() != "Darwin":
        return None
    for prefix in _HOMEBREW_PREFIXES:
        binary = Path(prefix) / f"ffmpeg@{FFMPEG_MAJOR}" / "bin" / "ffmpeg"
        if binary.is_file():
            return binary
    return None


def find_ffmpeg(environ: dict[str, str] | None = None) -> Path:
    """Returns the ffmpeg this build should use.

    The result is not yet known to be usable. Every caller must pass it through
    `require_encoders` before converting anything.
    """
    resolved = environ if environ is not None else dict(os.environ)
    pinned = _pinned_from_environment(resolved)
    if pinned is not None:
        return pinned

    brewed = _homebrew_ffmpeg()
    if brewed is not None:
        return brewed

    found = shutil.which("ffmpeg")
    if found is None:
        raise FfmpegNotFoundError(
            "No ffmpeg found. Install one that can encode the web pipeline's formats, "
            f"or point {BINARY_ENV} at a pinned build."
        )
    return Path(found)


def available_encoders(ffmpeg: Path) -> set[str]:
    """Returns the encoder names the given ffmpeg binary reports."""
    result = subprocess.run(
        [str(ffmpeg), "-hide_banner", "-encoders"],
        capture_output=True,
        text=True,
        check=False,
    )
    names = set()
    for line in result.stdout.splitlines():
        match = re.match(r"^\s*[A-Z.]{6}\s+(\S+)", line)
        if match:
            names.add(match.group(1))
    return names


def require_encoders(ffmpeg: Path, required: Iterable[str]) -> None:
    """Raises MissingEncoderError unless every required encoder is available.

    This is the check that makes the resolution order above safe. Without it, an ffmpeg
    that merely exists is assumed to be the right one, which is how a build silently
    produces audio nothing can play.
    """
    present = available_encoders(ffmpeg)
    missing = sorted(set(required) - present)
    if missing:
        raise MissingEncoderError(
            f"{ffmpeg} cannot encode: {', '.join(missing)}. "
            f"Install an ffmpeg build that can, or point {BINARY_ENV} at one."
        )
