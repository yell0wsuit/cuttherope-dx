import sys
import subprocess
import wave
from pathlib import Path

import pytest

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

from webcontent import audio, ffmpeg_tool


def test_sfx_detected_by_directory():
    assert audio.is_sfx(Path("sounds/sfx/tap.wav"))
    assert not audio.is_sfx(Path("sounds/menu_music.wav"))


def test_music_command_is_stereo_192k_libopus():
    command = audio.ogg_command(
        Path("/ff"), Path("in.wav"), Path("out.ogg"), 192, mono=False
    )
    assert command[0] == str(Path("/ff"))
    assert "-c:a" in command and command[command.index("-c:a") + 1] == "libopus"
    assert "-b:a" in command and command[command.index("-b:a") + 1] == "192k"
    assert "-ac" not in command
    assert "-q:a" not in command


def test_sfx_command_is_mono_96k():
    command = audio.ogg_command(
        Path("/ff"), Path("in.wav"), Path("out.ogg"), 96, mono=True
    )
    assert command[command.index("-ac") + 1] == "1"
    assert command[command.index("-ar") + 1] == "48000"
    assert command[command.index("-b:a") + 1] == "96k"


def test_settings_differ_between_music_and_sfx():
    assert audio.settings_for(Path("sounds/menu_music.wav")) != audio.settings_for(
        Path("sounds/sfx/tap.wav")
    )


def test_flac_music_and_wav_effects_both_become_ogg_jobs(tmp_path):
    sounds = tmp_path / "content" / "sounds"
    (sounds / "sfx").mkdir(parents=True)
    (sounds / "menu_music.flac").write_bytes(b"fLaC")
    (sounds / "sfx" / "tap.wav").write_bytes(b"RIFF")
    (sounds / "notes.txt").write_text("not audio")

    jobs = {job.out_rel: job for job in audio._jobs(tmp_path / "content", tmp_path / "out")}

    assert set(jobs) == {"sounds/menu_music.ogg", "sounds/sfx/tap.ogg"}
    assert jobs["sounds/menu_music.ogg"].settings == audio.settings_for(Path("sounds/menu_music.flac"))
    assert jobs["sounds/sfx/tap.ogg"].settings == audio.settings_for(Path("sounds/sfx/tap.wav"))


def test_sfx_command_encodes_22050_hz_source(tmp_path):
    try:
        ffmpeg = ffmpeg_tool.find_ffmpeg()
        ffmpeg_tool.require_encoders(ffmpeg, audio.REQUIRED_ENCODERS)
    except (ffmpeg_tool.FfmpegNotFoundError, ffmpeg_tool.MissingEncoderError) as error:
        pytest.skip(str(error))

    source = tmp_path / "low-rate.wav"
    with wave.open(str(source), "wb") as wav:
        wav.setnchannels(2)
        wav.setsampwidth(2)
        wav.setframerate(22050)
        wav.writeframes(b"\0\0" * 2 * 22050)

    destination = tmp_path / "low-rate.ogg"
    subprocess.run(
        audio.ogg_command(ffmpeg, source, destination, 96, mono=True),
        check=True,
    )
    assert destination.stat().st_size > 0
