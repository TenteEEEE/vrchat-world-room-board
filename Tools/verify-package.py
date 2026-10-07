#!/usr/bin/env python3
"""Fast, dependency-free VPM package structure check for Room Board."""
from __future__ import annotations

import json
import re
import sys
from pathlib import Path


PACKAGE_NAME = "com.tentee.vrc-room-board"
REPOSITORY_ROOT = Path(__file__).resolve().parents[1]
PACKAGE_ROOT = REPOSITORY_ROOT / "Packages" / PACKAGE_NAME
ERRORS: list[str] = []


def fail(message: str) -> None:
    ERRORS.append(message)


def text(path: Path) -> str:
    return path.read_text(encoding="utf-8")


def package_relative(path: Path) -> Path:
    return path.relative_to(PACKAGE_ROOT)


def guid(path: Path) -> str | None:
    match = re.search(r"^guid:\s*([0-9a-f]{32})\s*$", text(path), re.MULTILINE)
    return match.group(1) if match else None


def check_manifest() -> None:
    manifest_path = PACKAGE_ROOT / "package.json"
    try:
        manifest = json.loads(text(manifest_path))
    except (OSError, json.JSONDecodeError) as exc:
        fail(f"invalid package.json: {exc}")
        return

    if manifest.get("name") != PACKAGE_NAME:
        fail(f"package name must be {PACKAGE_NAME}")
    version = manifest.get("version")
    if not isinstance(version, str) or not re.fullmatch(r"\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?", version):
        fail("package version must be SemVer")
    if manifest.get("unity") != "2022.3":
        fail("package must target Unity 2022.3")
    worlds = manifest.get("vpmDependencies", {}).get("com.vrchat.worlds")
    if not isinstance(worlds, str) or not worlds:
        fail("package must declare com.vrchat.worlds in vpmDependencies")


def check_meta_pairs() -> dict[str, Path]:
    seen: dict[str, Path] = {}

    def ignored(path: Path) -> bool:
        return any(part.endswith("~") for part in package_relative(path).parts)

    for path in PACKAGE_ROOT.rglob("*"):
        if ignored(path) or path.is_dir() or path.suffix == ".meta" or path.name.startswith("."):
            continue
        meta = Path(str(path) + ".meta")
        if not meta.is_file():
            fail(f"missing .meta: {package_relative(path)}")

    for meta in PACKAGE_ROOT.rglob("*.meta"):
        if ignored(meta):
            continue
        target = Path(str(meta)[:-5])
        if not target.exists():
            fail(f"orphan .meta: {package_relative(meta)}")
        value = guid(meta)
        if value is None:
            fail(f"missing GUID: {package_relative(meta)}")
        elif value in seen:
            fail(f"duplicate GUID {value}: {package_relative(seen[value])} and {package_relative(meta)}")
        else:
            seen[value] = meta
    return seen


def check_asmdefs(meta_guids: dict[str, Path]) -> None:
    asmdefs: dict[str, Path] = {}
    for path in PACKAGE_ROOT.rglob("*.asmdef"):
        try:
            data = json.loads(text(path))
        except json.JSONDecodeError as exc:
            fail(f"invalid asmdef JSON {package_relative(path)}: {exc.msg}")
            continue
        name = data.get("name")
        if not isinstance(name, str) or not name:
            fail(f"asmdef missing name: {package_relative(path)}")
        else:
            asmdefs[name] = path

    for name in sorted({"RoomBoard.Runtime", "RoomBoard.Editor"} - asmdefs.keys()):
        fail(f"required assembly missing: {name}")

    runtime = asmdefs.get("RoomBoard.Runtime")
    if runtime:
        refs = json.loads(text(runtime)).get("references", [])
        if any("Editor" in ref for ref in refs):
            fail("runtime asmdef references an editor assembly")

    assembly_assets = list(PACKAGE_ROOT.rglob("*.UdonSharpAssembly.asset"))
    if not assembly_assets:
        fail("UdonSharp assembly definition asset is missing")
    for asset in assembly_assets:
        match = re.search(r"sourceAssembly:.*guid:\s*([0-9a-f]{32})", text(asset))
        if not match:
            fail(f"UdonSharp assembly asset missing sourceAssembly: {package_relative(asset)}")
        elif match.group(1) not in meta_guids:
            fail(f"UdonSharp assembly asset points to unknown GUID: {package_relative(asset)}")


def check_shipped_assets() -> None:
    required = (
        "Runtime/RoomBoardManager.asset",
        "Runtime/RoomBoardArea.asset",
        "Runtime/RoomBoardDisplay.asset",
        "Prefabs/RoomBoard Manager.prefab",
        "Prefabs/RoomBoard Area.prefab",
        "Prefabs/RoomBoard Overview.prefab",
        "Prefabs/RoomBoard Door Sign.prefab",
        "Fonts/RoomBoard JP Dynamic.asset",
        "Fonts/NotoSansJP-Regular.otf",
        "Fonts/OFL-1.1.txt",
        "LICENSE",
        "README.md",
        "README.ja.md",
        "CHANGELOG.md",
    )
    for relative in required:
        if not (PACKAGE_ROOT / relative).is_file():
            fail(f"shipped file missing: {relative}")

    font = PACKAGE_ROOT / "Fonts" / "RoomBoard JP Dynamic.asset"
    if font.is_file():
        source = text(font)
        if not re.search(r"m_AtlasPopulationMode:\s*1", source):
            fail("font asset must be dynamic")
        if not re.search(r"m_ClearDynamicDataOnBuild:\s*1", source):
            fail("font asset must clear dynamic data on build")
        if re.search(r"m_SourceFontFile:\s*\{fileID:\s*0\}", source):
            fail("font asset has no source font reference")


def check_release_files() -> None:
    required = (
        REPOSITORY_ROOT / ".github" / "workflows" / "release.yml",
        REPOSITORY_ROOT / "Packages" / ".gitignore",
        REPOSITORY_ROOT / "ProjectSettings" / "ProjectVersion.txt",
    )
    for path in required:
        if not path.is_file():
            fail(f"VPM project file missing: {path.relative_to(REPOSITORY_ROOT)}")


def main() -> int:
    try:
        if not PACKAGE_ROOT.is_dir():
            fail(f"package directory is missing: {PACKAGE_ROOT.relative_to(REPOSITORY_ROOT)}")
        else:
            check_manifest()
            metadata = check_meta_pairs()
            check_asmdefs(metadata)
            check_shipped_assets()
        check_release_files()
    except OSError as exc:
        fail(f"file access error: {exc}")

    if ERRORS:
        print("PACKAGE CHECK FAILED")
        for error in ERRORS:
            print("- " + error)
        return 1
    print("PACKAGE CHECK PASSED")
    return 0


if __name__ == "__main__":
    sys.exit(main())
