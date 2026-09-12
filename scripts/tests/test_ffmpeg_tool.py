import platform
import sys
from pathlib import Path

import pytest

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

from webcontent import audio, ffmpeg_tool, video


def _fake_binary(path: Path) -> Path:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text("#!/bin/sh\n")
    path.chmod(0o755)
    return path


def test_rid_names_this_platform():
    assert ffmpeg_tool.rid_for_platform() in {
        "osx-arm64",
        "osx-x64",
        "linux-x64",
        "linux-arm64",
        "windows-x64",
        "windows-arm64",
    }


def test_an_explicitly_pinned_binary_wins(tmp_path, monkeypatch):
    pinned = _fake_binary(tmp_path / "pinned" / "ffmpeg")
    monkeypatch.setattr(ffmpeg_tool.shutil, "which", lambda _: "/usr/bin/ffmpeg")

    found = ffmpeg_tool.find_ffmpeg({ffmpeg_tool.BINARY_ENV: str(pinned)})

    assert found == pinned


def test_a_pin_that_does_not_exist_is_reported_rather_than_ignored(tmp_path):
    missing = tmp_path / "absent" / "ffmpeg"

    with pytest.raises(ffmpeg_tool.FfmpegNotFoundError) as excinfo:
        ffmpeg_tool.find_ffmpeg({ffmpeg_tool.BINARY_ENV: str(missing)})

    assert ffmpeg_tool.BINARY_ENV in str(excinfo.value)


def test_a_pinned_binary_is_checked_against_its_recorded_hash(tmp_path):
    pinned = _fake_binary(tmp_path / "ffmpeg")
    expected = ffmpeg_tool.sha256_of(pinned)

    assert (
        ffmpeg_tool.find_ffmpeg(
            {
                ffmpeg_tool.BINARY_ENV: str(pinned),
                ffmpeg_tool.CHECKSUM_ENV: expected.upper(),
            }
        )
        == pinned
    )


def test_a_pinned_binary_that_is_not_the_pinned_build_is_refused(tmp_path):
    pinned = _fake_binary(tmp_path / "ffmpeg")

    with pytest.raises(ffmpeg_tool.ChecksumMismatchError) as excinfo:
        ffmpeg_tool.find_ffmpeg(
            {
                ffmpeg_tool.BINARY_ENV: str(pinned),
                ffmpeg_tool.CHECKSUM_ENV: "0" * 64,
            }
        )

    assert ffmpeg_tool.CHECKSUM_ENV in str(excinfo.value)


def test_the_path_binary_is_the_last_resort(monkeypatch):
    monkeypatch.setattr(ffmpeg_tool, "_homebrew_ffmpeg", lambda: None)
    monkeypatch.setattr(ffmpeg_tool.shutil, "which", lambda _: "/usr/bin/ffmpeg")

    assert ffmpeg_tool.find_ffmpeg({}) == Path("/usr/bin/ffmpeg")


def test_no_ffmpeg_anywhere_is_reported(monkeypatch):
    monkeypatch.setattr(ffmpeg_tool, "_homebrew_ffmpeg", lambda: None)
    monkeypatch.setattr(ffmpeg_tool.shutil, "which", lambda _: None)

    with pytest.raises(ffmpeg_tool.FfmpegNotFoundError):
        ffmpeg_tool.find_ffmpeg({})


@pytest.mark.skipif(platform.system() != "Darwin", reason="macOS keg layout")
def test_the_pinned_homebrew_keg_is_preferred_over_path(monkeypatch, tmp_path):
    keg = _fake_binary(tmp_path / f"ffmpeg@{ffmpeg_tool.FFMPEG_MAJOR}" / "bin" / "ffmpeg")
    monkeypatch.setattr(ffmpeg_tool, "_HOMEBREW_PREFIXES", (str(tmp_path),))
    monkeypatch.setattr(ffmpeg_tool.shutil, "which", lambda _: "/usr/bin/ffmpeg")

    assert ffmpeg_tool.find_ffmpeg({}) == keg


def test_require_encoders_raises_listing_the_missing_ones(monkeypatch, tmp_path):
    monkeypatch.setattr(ffmpeg_tool, "available_encoders", lambda _: {"aac"})

    with pytest.raises(ffmpeg_tool.MissingEncoderError) as excinfo:
        ffmpeg_tool.require_encoders(tmp_path / "ffmpeg", ["libvorbis"])

    assert "libvorbis" in str(excinfo.value)


def test_require_encoders_passes_when_present(monkeypatch, tmp_path):
    monkeypatch.setattr(ffmpeg_tool, "available_encoders", lambda _: {"libvorbis"})

    ffmpeg_tool.require_encoders(tmp_path / "ffmpeg", ["libvorbis"])


def test_the_resolved_ffmpeg_can_encode_the_video_formats():
    """Video is the half every ordinary ffmpeg build can do; audio is checked below."""
    try:
        binary = ffmpeg_tool.find_ffmpeg()
    except ffmpeg_tool.FfmpegNotFoundError:
        pytest.skip("no ffmpeg installed")
    ffmpeg_tool.require_encoders(binary, video.REQUIRED_ENCODERS)


def test_the_resolved_ffmpeg_can_encode_the_audio_formats():
    """The reason this module exists: most ffmpeg builds in the wild lack libvorbis.

    This is an assertion about the machine, not about the code. It skips rather than fails
    where no suitable build is installed, and names the way to supply one, because a
    developer who is not building web content should not be told their checkout is broken.
    """
    try:
        binary = ffmpeg_tool.find_ffmpeg()
    except ffmpeg_tool.FfmpegNotFoundError:
        pytest.skip("no ffmpeg installed")
    missing = set(audio.REQUIRED_ENCODERS) - ffmpeg_tool.available_encoders(binary)
    if missing:
        pytest.skip(
            f"{binary} cannot encode {sorted(missing)}; "
            f"point {ffmpeg_tool.BINARY_ENV} at a build that can to run the web audio pipeline"
        )
