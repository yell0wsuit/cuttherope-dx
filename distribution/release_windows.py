#!/usr/bin/env python3
"""Build a Windows release package for Cut the Rope DX.

Windows ships one build. The graphics backend is chosen when the game starts:
the SDL host tries Vulkan, then ANGLE, then OpenGL, and keeps
the first that presents a frame.

    CutTheRope-DX.exe   the game        + SDL3.dll, SDL3_mixer.dll, libSkiaSharp.dll, ...
    ffmpeg/  angle/  content/    beside it

The publish is single-file, so the managed assemblies live inside the executable.
What stays loose is native: SDL, its mixer codecs, and Skia. macOS and Linux ship
the same shape and are built by their own scripts.
"""

import hashlib
import shutil
import subprocess
import sys
import tempfile
import urllib.request
import zipfile
from http.client import HTTPException
from pathlib import Path

SCRIPT_DIR = Path(__file__).parent
PROJECT_ROOT = SCRIPT_DIR.parent.resolve()
CSPROJ = PROJECT_ROOT / "src" / "CutTheRopeDX.Desktop" / "CutTheRopeDX.Desktop.csproj"
RELEASE_DIR = PROJECT_ROOT / "src" / "CutTheRopeDX.Desktop" / "bin" / "release_github"

ARCHITECTURES = {
    "x64": {"rid": "win-x64", "btbn": "win64", "electron": "x64", "label": "x64"},
    "arm64": {"rid": "win-arm64", "btbn": "winarm64", "electron": "arm64", "label": "ARM64"},
}

# The name the game publishes under, from the project's AssemblyName.
GAME_ASSEMBLY = "CutTheRope-DX"

CONTENT_DIRECTORY = "content"
UNSHIPPED_SUFFIXES = ".pdb"
FFMPEG_DIRECTORY = "ffmpeg"
FFMPEG_DOWNLOAD_ATTEMPTS = 5
# BtbN also publishes a "latest" tag, whose assets are deleted and re-uploaded under the same
# names on every build, with the checksum file regenerated alongside them. Verifying against that
# proves the download arrived intact and nothing more: two runs of this script for the same game
# version would ship different FFmpeg binaries. A dated tag is written once and keeps its
# assets, so the checksum becomes a statement about a particular build rather than about
# whichever one is current.
#
# The archive name carries the exact build, so moving this pin forward means moving both lines.
FFMPEG_BUILD_TAG = "autobuild-2026-09-11-13-20"
FFMPEG_BUILD_VERSION = "n9.0.1-29-gad500d59cb"
ANGLE_DIRECTORY = "angle"
ANGLE_DOWNLOAD_ATTEMPTS = 5
# ANGLE ships no standalone desktop build, so this takes the libraries from an Electron
# release: both architectures are published, versions stay archived, and every artifact is
# covered by a checksum file that can be verified in the same step.
#
# Pinned to the 43 series because 44.0.0 stopped shipping these as separate files and links
# ANGLE into electron.exe instead, where nothing else can load it. Verified against the
# published archives: 43.7.0 carries both DLLs for x64 and arm64, 44.0.0 carries neither.
# Moving this pin forward means checking the archive still contains them, not just that the
# tag exists.
ANGLE_ELECTRON_VERSION = "v43.7.0"
ANGLE_DLL_NAMES = ("libEGL.dll", "libGLESv2.dll")
# Kept as .html because that is what it is: renaming markup to .txt gives players a file their
# text editor renders as tag soup.
ANGLE_NOTICE_SOURCE = "LICENSES.chromium.html"
ANGLE_NOTICE_NAME = "ANGLE-LICENSE.html"
FFMPEG_DLL_GLOBS = (
    "avcodec-*.dll",
    "avdevice-*.dll",
    "avfilter-*.dll",
    "avformat-*.dll",
    "avutil-*.dll",
    "postproc-*.dll",
    "swresample-*.dll",
    "swscale-*.dll",
)


def sha256_of(path: Path) -> str:
    """Returns the lowercase hex SHA-256 of a file, read in chunks."""
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for block in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def publish(out_dir: Path, version: str, use_aot: bool, runtime_id: str) -> None:
    """Publish the game into out_dir, failing the script if the build fails."""
    cmd = [
        "dotnet",
        "publish",
        str(CSPROJ),
        "-c",
        "Release",
        "-f",
        "net10.0",
        "-r",
        runtime_id,
        f"-p:VersionPrefix={version}",
        "-p:VersionSuffix=",
        f"-p:PublishAot={str(use_aot).lower()}",
        "-o",
        str(out_dir),
    ]
    print(f"\n> {' '.join(cmd)}\n")
    result = subprocess.run(cmd, check=False)
    if result.returncode != 0:
        sys.exit(result.returncode)


def download_ffmpeg(output_dir: Path, btbn_arch: str) -> None:
    """Download BtbN FFmpeg LGPL shared libraries for one Windows architecture.

    The major version has to match the one FFmpeg.AutoGen binds against, because it
    resolves each function from a library named for its own soname; a mismatch loads
    nothing and leaves the game with no video decoder.
    """
    release_url = (
        f"https://github.com/BtbN/FFmpeg-Builds/releases/download/{FFMPEG_BUILD_TAG}"
    )
    archive_name = f"ffmpeg-{FFMPEG_BUILD_VERSION}-{btbn_arch}-lgpl-shared-9.0.zip"
    ffmpeg_url = f"{release_url}/{archive_name}"
    checksums_url = f"{release_url}/checksums.sha256"
    destination = output_dir / FFMPEG_DIRECTORY
    if destination.is_dir() and any(destination.glob("avcodec-*.dll")):
        print(f"FFmpeg shared libraries already present in {destination}")
        return

    print("\n=== FFmpeg ===")
    print(f"Downloading {ffmpeg_url}")

    with tempfile.TemporaryDirectory() as temp_dir_name:
        temp_dir = Path(temp_dir_name)
        archive_path = temp_dir / archive_name
        checksums_path = temp_dir / "checksums.sha256"

        for attempt in range(1, FFMPEG_DOWNLOAD_ATTEMPTS + 1):
            try:
                urllib.request.urlretrieve(ffmpeg_url, archive_path)
                urllib.request.urlretrieve(checksums_url, checksums_path)

                expected_checksum = None
                for line in checksums_path.read_text(encoding="utf-8").splitlines():
                    parts = line.split(maxsplit=1)
                    if len(parts) == 2 and parts[1].lstrip("*") == archive_name:
                        expected_checksum = parts[0].lower()
                        break

                actual_checksum = sha256_of(archive_path)

                if expected_checksum is None:
                    raise ValueError(f"No checksum found for {archive_name}")
                if actual_checksum != expected_checksum:
                    raise ValueError(f"Checksum mismatch for {archive_name}")
            except (OSError, HTTPException, ValueError) as error:
                if attempt == FFMPEG_DOWNLOAD_ATTEMPTS:
                    print(
                        f"FFmpeg download or verification failed after "
                        f"{FFMPEG_DOWNLOAD_ATTEMPTS} attempts: {error}",
                        file=sys.stderr,
                    )
                    sys.exit(1)
                print(
                    f"FFmpeg download or verification failed: {error}; "
                    f"retrying ({attempt + 1}/{FFMPEG_DOWNLOAD_ATTEMPTS})..."
                )
            else:
                break

        with zipfile.ZipFile(archive_path) as archive:
            archive.extractall(temp_dir / "extracted")

        roots = [path for path in (temp_dir / "extracted").iterdir() if path.is_dir()]
        if len(roots) != 1:
            print("Could not find the extracted FFmpeg directory", file=sys.stderr)
            sys.exit(1)

        extracted = roots[0]
        bin_dir = extracted / "bin"
        if not bin_dir.is_dir():
            print(f"FFmpeg bin directory not found at {bin_dir}", file=sys.stderr)
            sys.exit(1)

        dlls = sorted(
            {dll for pattern in FFMPEG_DLL_GLOBS for dll in bin_dir.glob(pattern)}
        )
        if not dlls:
            print(
                "No FFmpeg shared DLLs found in the downloaded archive",
                file=sys.stderr,
            )
            sys.exit(1)

        destination.mkdir(parents=True, exist_ok=True)
        for dll in dlls:
            shutil.copy2(dll, destination / dll.name)

        license_file = extracted / "LICENSE.txt"
        if license_file.is_file():
            shutil.copy2(license_file, destination / "FFmpeg-LICENSE.txt")

    print(f"FFmpeg shared libraries copied to {destination}")


def download_angle(output_dir: Path, electron_arch: str) -> None:
    """Download the ANGLE libraries for one Windows architecture.

    ANGLE maps OpenGL ES onto Direct3D 11, which is the GL implementation the game
    prefers on Windows over whatever the machine's own driver provides. Google publishes
    no standalone desktop build, so these come out of an Electron release.
    """
    release_url = f"https://github.com/electron/electron/releases/download/{ANGLE_ELECTRON_VERSION}"
    archive_name = f"electron-{ANGLE_ELECTRON_VERSION}-win32-{electron_arch}.zip"
    angle_url = f"{release_url}/{archive_name}"
    checksums_url = f"{release_url}/SHASUMS256.txt"
    destination = output_dir / ANGLE_DIRECTORY
    installed = (*ANGLE_DLL_NAMES, ANGLE_NOTICE_NAME)
    if destination.is_dir() and all((destination / name).is_file() for name in installed):
        print(f"ANGLE libraries already present in {destination}")
        return

    print("\n=== ANGLE ===")
    print(f"Downloading {angle_url}")

    with tempfile.TemporaryDirectory() as temp_dir_name:
        temp_dir = Path(temp_dir_name)
        archive_path = temp_dir / archive_name
        checksums_path = temp_dir / "SHASUMS256.txt"

        for attempt in range(1, ANGLE_DOWNLOAD_ATTEMPTS + 1):
            try:
                urllib.request.urlretrieve(angle_url, archive_path)
                urllib.request.urlretrieve(checksums_url, checksums_path)

                expected_checksum = None
                for line in checksums_path.read_text(encoding="utf-8").splitlines():
                    parts = line.split(maxsplit=1)
                    if len(parts) == 2 and parts[1].lstrip("*") == archive_name:
                        expected_checksum = parts[0].lower()
                        break

                if expected_checksum is None:
                    raise ValueError(f"No checksum found for {archive_name}")
                if sha256_of(archive_path) != expected_checksum:
                    raise ValueError(f"Checksum mismatch for {archive_name}")
            except (OSError, HTTPException, ValueError) as error:
                if attempt == ANGLE_DOWNLOAD_ATTEMPTS:
                    print(
                        f"ANGLE download or verification failed after "
                        f"{ANGLE_DOWNLOAD_ATTEMPTS} attempts: {error}",
                        file=sys.stderr,
                    )
                    sys.exit(1)
                print(
                    f"ANGLE download or verification failed: {error}; "
                    f"retrying ({attempt + 1}/{ANGLE_DOWNLOAD_ATTEMPTS})..."
                )
            else:
                break

        destination.mkdir(parents=True, exist_ok=True)
        with zipfile.ZipFile(archive_path) as archive:
            members = {Path(name).name: name for name in archive.namelist()}
            for dll in ANGLE_DLL_NAMES:
                if dll not in members:
                    print(f"{dll} not found in {archive_name}", file=sys.stderr)
                    sys.exit(1)
                with archive.open(members[dll]) as source:
                    (destination / dll).write_bytes(source.read())

            # ANGLE is BSD-3-Clause, so the notice is a condition of shipping the libraries at
            # all, not a nicety. An archive without it is not one we can redistribute from.
            if ANGLE_NOTICE_SOURCE not in members:
                print(f"{ANGLE_NOTICE_SOURCE} not found in {archive_name}", file=sys.stderr)
                sys.exit(1)
            with archive.open(members[ANGLE_NOTICE_SOURCE]) as source:
                (destination / ANGLE_NOTICE_NAME).write_bytes(source.read())

    print(f"ANGLE libraries copied to {destination}")


def is_shipped(output_dir: Path, path: Path) -> bool:
    """Whether a published file belongs in the archive players download."""
    return not any(
        part.endswith(UNSHIPPED_SUFFIXES) for part in path.relative_to(output_dir).parts
    )


def packaging_tools():
    """The archiver and the progress bar, which only the packaging step needs.

    They are imported here rather than at the top of the file so that importing this
    module - to build another platform, or to test it - does not need them installed.
    """
    try:
        import py7zr
        from tqdm import tqdm
    except ImportError:
        print("Required: pip install py7zr tqdm", file=sys.stderr)
        sys.exit(1)
    return py7zr, tqdm


def package(output_dir: Path, version: str, arch_label: str):
    """Compress the build output into a .7z archive."""
    py7zr, tqdm = packaging_tools()
    RELEASE_DIR.mkdir(parents=True, exist_ok=True)
    archive_name = f"CutTheRopeDX-v{version}-Windows-{arch_label}.7z"
    archive_path = RELEASE_DIR / archive_name

    published = sorted(f for f in output_dir.rglob("*") if f.is_file())
    files = [f for f in published if is_shipped(output_dir, f)]
    if len(published) != len(files):
        print(
            f"Excluding {len(published) - len(files)} "
            "debug/documentation file(s) from the archive"
        )
    sizes = [f.stat().st_size for f in files]

    print(f"\nPackaging {archive_name}...")
    with py7zr.SevenZipFile(
        archive_path, "w", filters=[{"id": py7zr.FILTER_LZMA, "preset": 9}]
    ) as archive:
        with tqdm(total=sum(sizes), unit="B", unit_scale=True) as pbar:
            for file, size in zip(files, sizes, strict=True):
                archive.write(file, str(file.relative_to(output_dir)))
                pbar.update(size)

    size_mb = archive_path.stat().st_size / (1024 * 1024)
    print(f"Created {archive_path} ({size_mb:.1f} MB)")


def resolve_options() -> tuple[str, bool, str]:
    """Take version, AOT choice, and target architecture from argv or prompts."""
    args = sys.argv[1:]
    use_aot = "--no-aot" not in args
    args = [a for a in args if a != "--no-aot"]

    arch = "x64"
    if "--arch" in args:
        index = args.index("--arch")
        try:
            arch = args[index + 1].lower()
        except IndexError:
            print("--arch requires x64 or arm64", file=sys.stderr)
            sys.exit(2)
        del args[index : index + 2]

    # Accept "arm" as a convenient alias, but Windows/.NET and BtbN call it ARM64.
    if arch == "arm":
        arch = "arm64"
    if arch not in ARCHITECTURES:
        print("Architecture must be x64 or arm64", file=sys.stderr)
        sys.exit(2)

    if args:
        return args[0], use_aot, arch

    if not sys.stdin.isatty():
        print(
            "Usage: release_windows.py <version> [--arch x64|arm64] [--no-aot]",
            file=sys.stderr,
        )
        sys.exit(1)

    version = input("Version (e.g. 2.12.0.1): ").strip()
    if not version:
        print("Version is required.", file=sys.stderr)
        sys.exit(1)

    use_aot = input("Use NativeAOT? [Y/n]: ").strip().lower() != "n"
    return version, use_aot, arch


def main():
    """Build and package the selected Windows architecture."""
    version, use_aot, arch = resolve_options()
    # Ask for the archiver before the build rather than after it, so a missing
    # dependency costs a second instead of a full publish and two downloads.
    packaging_tools()
    config = ARCHITECTURES[arch]
    runtime_id = config["rid"]
    btbn_arch = config["btbn"]
    arch_label = config["label"]
    output_dir = (
        PROJECT_ROOT / "src" / "CutTheRopeDX.Desktop" / "bin" / "Publish" / runtime_id
    )

    print(f"\nBuilding v{version} for {runtime_id} " f"(NativeAOT: {use_aot})...")

    if output_dir.exists():
        shutil.rmtree(output_dir)

    publish(output_dir, version, use_aot, runtime_id)

    produced = output_dir / f"{GAME_ASSEMBLY}.exe"
    if not produced.is_file():
        print(f"No game executable at {produced}", file=sys.stderr)
        sys.exit(1)

    download_ffmpeg(output_dir, btbn_arch)
    download_angle(output_dir, config["electron"])
    package(output_dir, version, arch_label)


if __name__ == "__main__":
    main()
