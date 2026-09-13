"""Dual publish preserves runtime compression and refreshes merged cache metadata."""
import importlib.util
from pathlib import Path

SPEC = importlib.util.spec_from_file_location(
    "publish_browser", Path(__file__).resolve().parents[1] / "publish_browser.py"
)
publisher = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(publisher)


def test_merge_keeps_shared_shell_and_both_runtime_hashes():
    threaded = {"version": "mt1", "assets": [
        {"url": "index.html", "hash": "shell"},
        {"url": "_framework/dotnet.js", "hash": "mt"},
    ]}
    single = {"version": "st1", "assets": [
        {"url": "index.html", "hash": "other-shell"},
        {"url": "_framework/dotnet.js", "hash": "st"},
    ]}
    merged, version = publisher.merge_manifests(threaded, single)
    assert {a["url"]: a["hash"] for a in merged["assets"]} == {
        "index.html": "shell", "_framework/dotnet.js": "mt",
        "_framework-single/dotnet.js": "st",
    }
    assert publisher.merge_manifests(threaded, single)[1] == version
    assert publisher.merge_manifests(threaded, {**single, "version": "st2"})[1] != version
    assert publisher.merge_manifests({**threaded, "version": "mt2"}, single)[1] != version


def test_merge_preserves_runtime_compression_and_removes_stale_metadata_copies(tmp_path):
    site, fallback = tmp_path / "site", tmp_path / "fallback"
    for tree, version in ((site, "mt"), (fallback, "st")):
        (tree / "_framework").mkdir(parents=True)
        for suffix in ("", ".br", ".gz"):
            (tree / ("_framework/dotnet.js" + suffix)).write_bytes(version.encode())
        publisher.write_manifest(tree / publisher.ASSETS_MANIFEST, {
            "assets": [{"url": "_framework/dotnet.js", "hash": version}],
        }, version)
    (site / publisher.WORKER).write_text("/* Manifest version: old */\nworker();\n")
    for name in (publisher.ASSETS_MANIFEST, publisher.WORKER):
        for suffix in (".br", ".gz"):
            (site / (name + suffix)).write_bytes(b"stale")
    publisher.merge_fallback(site, fallback)
    for directory, expected in (("_framework", b"mt"), ("_framework-single", b"st")):
        for suffix in ("", ".br", ".gz"):
            assert (site / directory / ("dotnet.js" + suffix)).read_bytes() == expected
    manifest = publisher.read_manifest(site / publisher.ASSETS_MANIFEST)
    assert manifest["version"] in (site / publisher.WORKER).read_text()
    assert "old" not in (site / publisher.WORKER).read_text()
    for name in (publisher.ASSETS_MANIFEST, publisher.WORKER):
        for suffix in (".br", ".gz"):
            assert not (site / (name + suffix)).exists()
