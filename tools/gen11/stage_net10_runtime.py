#!/usr/bin/env python3
"""Stage a framework-dependent .NET 10 G1.1 server package without live data.

Only compiled binaries, selected platform-native libraries, static defaults and
example configurations are included. Active INIs, DBs, IARs, FSAssets, logs,
region data, secrets and user-owned files are deliberately omitted.

This produces an UNRELEASED staging artifact. Running this tool never deploys it.
"""
from __future__ import annotations

import argparse
import hashlib
import json
import os
from pathlib import Path
import shutil

ROOT = Path(__file__).resolve().parents[2]
BIN = ROOT / "bin"

HOST_NAMES = ("Robust", "OpenSim")
RID_OPTIONS = ("linux-x64", "linux-arm64", "win-x64")
STATIC_INCLUDE = {
    "Grid.ini", "GridHypergrid.ini", "Standalone.ini",
    "StandaloneHypergrid.ini", "osslDefaultEnable.ini",
}
BUNDLED_DATA = ("openmetaverse_data", "assets", "inventory")


def validate_net10(source: Path) -> None:
    for host in HOST_NAMES:
        if not (source / (host + ".dll")).is_file():
            raise ValueError(f"Missing executable server assembly: {host}.dll")
        config = source / (host + ".runtimeconfig.json")
        if not config.is_file():
            raise ValueError(f"Missing runtimeconfig: {config.name}")
        options = json.loads(config.read_text(encoding="utf-8-sig"))["runtimeOptions"]
        if options.get("tfm") != "net10.0":
            raise ValueError(f"{config.name} does not target net10.0")
        framework = options.get("framework", {})
        if framework.get("name") != "Microsoft.NETCore.App" or not str(
            framework.get("version", "")
        ).startswith("10."):
            raise ValueError(f"{config.name} does not use Microsoft.NETCore.App 10")


def allow_native(name: str, rid: str) -> bool:
    lower = name.lower()
    if rid == "win-x64":
        return lower.endswith(".dll") and ("arm64" not in lower) and (
            "x86_64" in lower or "64" not in lower
        )
    if not lower.endswith(".so"):
        return False
    if rid == "linux-arm64":
        return "arm64" in lower or "aarch64" in lower
    return ("x86_64" in lower or lower in {"libbulletsim.so", "libsqlite3_64.so"})


def allow_root(name: str) -> bool:
    lower = name.lower()
    if lower.endswith(".dll") and not lower.endswith(".tests.dll"):
        return True
    if lower.endswith((".dll.config", ".exe.config", ".runtimeconfig.json", ".deps.json")):
        return True
    if lower.endswith((".so", ".ini.example", ".env.example")):
        return True
    if name in {
        "OpenSimDefaults.ini", "ScriptSyntax.xml", "avatar-texture.dat",
        "robuststartuplogo.txt", "startuplogo.txt", "opensim.sh",
        "http_404.html.example", "http_loginform.html.example",
    }:
        return True
    return False


def copy(source: Path, dest: Path) -> None:
    if source.is_symlink():
        raise ValueError(f"Symlinks are not allowed in staging: {source}")
    if not source.is_file():
        raise ValueError(f"Not a regular file: {source}")
    dest.parent.mkdir(parents=True, exist_ok=True)
    shutil.copy2(source, dest)


def build(rid: str, output: Path, source: Path, require_native_codec: bool = False) -> None:
    source = source.resolve()
    output = output.absolute()
    if source == output or source in output.parents or ROOT in output.parents:
        raise ValueError("Staging output must be outside the Git checkout and bin directory")
    if output.exists() or output.is_symlink():
        raise ValueError("Staging output already exists; refuse to overwrite")
    validate_net10(source)

    stage = output / "bin"
    stage.mkdir(parents=True)
    try:
        selected = []
        for item in sorted(source.iterdir()):
            if item.is_file() and allow_root(item.name):
                selected.append((item, stage / item.name))

        # OpenSim's shared drawing assembly must match the destination OS.
        platform_suffix = ".win" if rid.startswith("win") else ".linux"
        drawing = source / ("System.Drawing.Common.dll" + platform_suffix)
        if not drawing.is_file():
            raise ValueError(f"Missing platform drawing assembly: {drawing.name}")
        selected = [
            pair for pair in selected
            if pair[0].name != "System.Drawing.Common.dll"
        ]
        selected.append((drawing, stage / "System.Drawing.Common.dll"))
        selected.append((drawing, stage / drawing.name))

        native = source / "lib64"
        if not native.is_dir():
            raise ValueError("Missing lib64 native dependency directory")
        for item in sorted(native.iterdir()):
            if item.is_file() and allow_native(item.name, rid):
                selected.append((item, stage / "lib64" / item.name))

        cfg_dir = source / "config-include"
        if not cfg_dir.is_dir():
            raise ValueError("Missing config-include")
        for item in sorted(cfg_dir.iterdir()):
            if item.is_file() and (
                item.name.endswith(".ini.example") or item.name in STATIC_INCLUDE
            ):
                selected.append((item, stage / "config-include" / item.name))

        for name in BUNDLED_DATA:
            data_dir = source / name
            if not data_dir.is_dir():
                continue
            for item in sorted(data_dir.rglob("*")):
                if item.is_symlink():
                    raise ValueError(f"Bundled data contains a symlink: {item}")
                if item.is_file():
                    selected.append((item, stage / name / item.relative_to(data_dir)))

        for src, dest in selected:
            copy(src, dest)

        # Optional. Ship architecture-specific codec only when built/tested.
        codec_name = {
            "linux-x64": "libogl-openjpeg2-x86_64.so",
            "linux-arm64": "libogl-openjpeg2-arm64.so",
            "win-x64": "ogl-openjpeg2-x86_64.dll",
        }[rid]
        has_native_codec = (stage / "lib64" / codec_name).is_file()
        if require_native_codec and not has_native_codec:
            raise ValueError(f"Missing requested optional native OpenJPEG2 binary: {codec_name}")

        files = {}
        for item in sorted(stage.rglob("*")):
            if not item.is_file():
                continue
            name = item.relative_to(output).as_posix()
            if name.lower().endswith((".log", ".db", ".sqlite", ".iar", ".env")):
                raise ValueError(f"Private or live data unexpectedly staged: {name}")
            if name.lower().endswith(".ini") and (
                item.parent != stage / "config-include" or item.name not in STATIC_INCLUDE
            ) and item.name != "OpenSimDefaults.ini":
                raise ValueError(f"Active INI unexpectedly staged: {name}")
            files[name] = hashlib.sha256(item.read_bytes()).hexdigest()

        manifest = {
            "product": "OpenGenesisLINK Generation 1.1",
            "status": "staging-only-not-production-certified",
            "runtime": "Microsoft.NETCore.App 10.x (framework-dependent)",
            "rid": rid,
            "optional_openjpeg2_shim_included": has_native_codec,
            "commit": os.environ.get("GITHUB_SHA", "local"),
            "file_count": len(files),
            "files_sha256": files,
            "excluded": [
                "active .ini and .env, DB, secrets, logs, regions, fsassets, caches",
                "runtime installation itself (requires .NET 10 on target)",
            ],
        }
        (output / "manifest.json").write_text(
            json.dumps(manifest, indent=2, sort_keys=True) + "\n", encoding="utf-8"
        )
        (output / "STAGING-NOTICE.txt").write_text(
            "OpenGenesisLINK Generation 1.1 / .NET 10 staging artifact.\n"
            "No live configuration, database, region state or inventory backup is included.\n"
            "Install .NET 10 runtime separately. Configure only on an isolated staging node.\n"
            "This package is NOT approved for production deployment.\n",
            encoding="utf-8",
        )
        print(f"PASS: staged .NET10 {rid} server binaries with {len(files)} files; no active INI or DB data.")
    except BaseException:
        # The output path is newly created and outside the repository.
        shutil.rmtree(output)
        raise


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--rid", required=True, choices=RID_OPTIONS)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--source", type=Path, default=BIN)
    parser.add_argument("--require-native-codec", action="store_true",
                        help="require the optional G11-02 OpenJPEG2 shim; not needed for G11-01 core runtime")
    args = parser.parse_args()
    build(args.rid, args.output, args.source, args.require_native_codec)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
