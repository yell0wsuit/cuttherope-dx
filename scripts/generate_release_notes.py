#!/usr/bin/env python3
"""Generate GitHub release notes markdown for Cut the Rope DX."""

import sys

REPO = "yell0wsuit/cuttherope-dx"
BASE_URL = f"https://github.com/{REPO}/releases/download"


def generate(version: str) -> str:
    """Builds the download section for a release, linking every published artifact.

    The names below are reproduced, not discovered: the release workflow uploads
    whatever the packaging scripts emit, by glob. A rename on that side has to be
    mirrored here or the notes will link to assets that do not exist.
    """
    dl = f"{BASE_URL}/v{version}"
    # File names swap a prerelease version's "+" for "_"; the tag keeps it.
    file_tag = f"v{version.replace('+', '_')}"

    files = {
        "win_x64": f"CutTheRopeDX-{file_tag}-Windows-x64.7z",
        "mac_ffmpeg": f"CutTheRopeDX-{file_tag}-macOS-arm64-ffmpeg.dmg",
        "mac_avf": f"CutTheRopeDX-{file_tag}-macOS-arm64-avfoundation.dmg",
        "appimage": f"CutTheRope-DX-{file_tag}-x86_64.AppImage",
        "deb": f"cuttherope-dx_{file_tag}_amd64.deb",
    }

    md = f"""## Downloads

### 🪟 Windows

- **Windows x64**
  - [{files['win_x64']}]({dl}/{files['win_x64']})

---

### 🍎 macOS (Apple silicon)

- **macOS build with FFmpeg backend**
  Recommended for macOS < 26
  - [{files['mac_ffmpeg']}]({dl}/{files['mac_ffmpeg']})

- **macOS Tahoe (26+) – AVFoundation backend**
  - [{files['mac_avf']}]({dl}/{files['mac_avf']})

> [!Note]
> App downloaded from Internet is usually marked as "can't be opened" / "damaged" due to macOS security.
> Remove it with `xattr -dr com.apple.quarantine <appname.app>`

---

### 🐧 Linux

- **AppImage (x86_64)** – *Recommended*
  - [{files['appimage']}]({dl}/{files['appimage']})

- **Debian / Ubuntu**
  - [{files['deb']}]({dl}/{files['deb']})"""

    return md


def main():
    """Prompts for a version and prints the generated markdown."""
    version = input("Version (without 'v' prefix, e.g. 2.30.0): ").strip()
    if not version:
        print("Version is required.", file=sys.stderr)
        sys.exit(1)

    result = generate(version)
    print("\n--- Generated Markdown ---\n")
    print(result)


if __name__ == "__main__":
    main()
