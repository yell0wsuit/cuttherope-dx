import hashlib
import importlib.util
import sys
import zipfile
from pathlib import Path

import pytest

ROOT = Path(__file__).resolve().parents[2]


def load_release_windows():
    """Import the release script without running it; the file guards on __main__."""
    spec = importlib.util.spec_from_file_location(
        "release_windows", ROOT / "distribution" / "release_windows.py"
    )
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


rw = load_release_windows()

COMPLETE = ("libEGL.dll", "libGLESv2.dll", "LICENSES.chromium.html")


@pytest.fixture
def electron_release(tmp_path, monkeypatch):
    """Serves a locally built archive in place of an Electron download."""

    def serve(arch, members=COMPLETE, corrupt=False):
        archive_name = f"electron-{rw.ANGLE_ELECTRON_VERSION}-win32-{arch}.zip"
        source = tmp_path / archive_name
        with zipfile.ZipFile(source, "w") as archive:
            for member in members:
                archive.writestr(member, f"contents of {member}")

        digest = "0" * 64 if corrupt else hashlib.sha256(source.read_bytes()).hexdigest()
        calls = []

        def urlretrieve(url, target):
            calls.append(url)
            if url.endswith("SHASUMS256.txt"):
                Path(target).write_text(f"{digest} *{archive_name}\n", encoding="utf-8")
            else:
                Path(target).write_bytes(source.read_bytes())

        monkeypatch.setattr(rw.urllib.request, "urlretrieve", urlretrieve)
        monkeypatch.setattr(rw, "ANGLE_DOWNLOAD_ATTEMPTS", 1)
        return calls

    return serve


@pytest.mark.parametrize("arch", ["x64", "arm64"])
def test_both_architectures_install_the_libraries_and_the_notice(tmp_path, electron_release, arch):
    _ = electron_release(arch)
    output = tmp_path / "publish"

    rw.download_angle(output, arch)

    installed = output / rw.ANGLE_DIRECTORY
    assert (installed / "libEGL.dll").is_file()
    assert (installed / "libGLESv2.dll").is_file()
    assert (installed / rw.ANGLE_NOTICE_NAME).is_file()


def test_the_notice_keeps_its_markup_extension(tmp_path, electron_release):
    _ = electron_release("x64")
    output = tmp_path / "publish"

    rw.download_angle(output, "x64")

    assert rw.ANGLE_NOTICE_NAME.endswith(".html")
    assert (output / rw.ANGLE_DIRECTORY / rw.ANGLE_NOTICE_NAME).is_file()


def test_a_checksum_mismatch_installs_nothing(tmp_path, electron_release):
    _ = electron_release("x64", corrupt=True)
    output = tmp_path / "publish"

    with pytest.raises(SystemExit) as exit_info:
        rw.download_angle(output, "x64")

    assert exit_info.value.code == 1
    assert not (output / rw.ANGLE_DIRECTORY / "libEGL.dll").exists()


@pytest.mark.parametrize("missing", ["libEGL.dll", "libGLESv2.dll", "LICENSES.chromium.html"])
def test_an_incomplete_archive_is_refused(tmp_path, electron_release, missing):
    _ = electron_release("x64", members=[m for m in COMPLETE if m != missing])
    output = tmp_path / "publish"

    with pytest.raises(SystemExit) as exit_info:
        rw.download_angle(output, "x64")

    assert exit_info.value.code == 1


def test_a_complete_installation_is_not_downloaded_again(tmp_path, electron_release):
    calls = electron_release("x64")
    output = tmp_path / "publish"
    installed = output / rw.ANGLE_DIRECTORY
    installed.mkdir(parents=True)
    for name in (*rw.ANGLE_DLL_NAMES, rw.ANGLE_NOTICE_NAME):
        (installed / name).write_text("already here")

    rw.download_angle(output, "x64")

    assert calls == []
    assert (installed / "libEGL.dll").read_text() == "already here"


def test_a_partial_installation_is_completed_rather_than_skipped(tmp_path, electron_release):
    calls = electron_release("x64")
    output = tmp_path / "publish"
    installed = output / rw.ANGLE_DIRECTORY
    installed.mkdir(parents=True)
    (installed / "libEGL.dll").write_text("half an install")

    rw.download_angle(output, "x64")

    assert calls != []
    assert (installed / "libGLESv2.dll").is_file()
    assert (installed / rw.ANGLE_NOTICE_NAME).is_file()
    assert (installed / "libEGL.dll").read_text() != "half an install"


def test_the_module_imports_without_the_packaging_extras(monkeypatch):
    """py7zr and tqdm are needed to build a release, not to import the script."""
    monkeypatch.setitem(sys.modules, "py7zr", None)
    monkeypatch.setitem(sys.modules, "tqdm", None)

    load_release_windows()


def test_packaging_asks_for_the_extras_it_needs(monkeypatch, tmp_path):
    """The build still stops with the install line when the archiver is absent."""
    monkeypatch.setitem(sys.modules, "py7zr", None)
    monkeypatch.setitem(sys.modules, "tqdm", None)

    with pytest.raises(SystemExit):
        rw.package(tmp_path, "1.0.0.0", "x64")
