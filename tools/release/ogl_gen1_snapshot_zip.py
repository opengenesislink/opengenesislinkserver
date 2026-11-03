#!/usr/bin/env python3
"""Create safe, SHA-identifiable Generation 1 development build ZIPs."""
import argparse
import json
import tempfile
import zipfile
from pathlib import Path

PREFIX = "OpenGenesisLINK-Generation1"
NEEDED = ("OpenSim.dll", "Robust.dll", "OpenGenesisLINK.Addons.OGLWeather.dll")
SKIP_SUFFIXES = (".log", ".bak", ".db", ".sqlite", ".sqlite3",
                 ".pass", ".pem", ".key", ".p12", ".pfx", ".pdb", ".tmp", ".dmp")
SKIP_DIRS = {"logs", "log", "backups", "cache", "tmp", "temp", "obj", "data"}

def allowed(path):
    parts = path.parts
    name = parts[-1].lower()
    return (not any(p.startswith(".") for p in parts)
            and not any(p.lower() in SKIP_DIRS for p in parts[:-1])
            and not name.endswith(SKIP_SUFFIXES)
            and name not in ("secrets.json", "appsettings.production.json")
            and not (name.endswith(".ini") and not name.endswith(".ini.example")))

def create_zip(bin_dir, out, sha, repo):
    if len(sha) != 40 or any(c not in "0123456789abcdef" for c in sha):
        raise ValueError("Expected verified full Git commit SHA")
    if not bin_dir.is_dir() or any(not (bin_dir / x).is_file() for x in NEEDED):
        raise ValueError("Missing required compiled OpenSim/ROBUST binaries")
    if out.resolve().is_relative_to(bin_dir.resolve()):
        raise ValueError("ZIP must not be inside bin/")
    out.parent.mkdir(parents=True, exist_ok=True)
    count = 0
    with zipfile.ZipFile(out, "w", zipfile.ZIP_DEFLATED, compresslevel=6, allowZip64=True) as z:
        for file in sorted(bin_dir.rglob("*")):
            if file.is_symlink():
                raise ValueError("Symlink in bin/: " + str(file))
            if not file.is_file():
                continue
            relative = file.relative_to(bin_dir)
            if allowed(relative):
                z.write(file, f"{PREFIX}/bin/{relative.as_posix()}")
                count += 1
        for name in ("README.md", "LICENSE_POLICY.md", "NOTICE.md"):
            path = repo / name
            if path.is_file():
                z.write(path, f"{PREFIX}/{name}")
        for dirname in ("LICENSES", "ThirdPartyLicenses"):
            folder = repo / dirname
            if folder.is_dir():
                for file in sorted(folder.rglob("*")):
                    if file.is_symlink():
                        raise ValueError("Symlink in license tree")
                    if file.is_file() and allowed(file.relative_to(folder)):
                        z.write(file, f"{PREFIX}/{dirname}/{file.relative_to(folder).as_posix()}")
        z.writestr(f"{PREFIX}/BUILD-MANIFEST.json", json.dumps({
            "product": "OpenGenesisLINK Generation 1",
            "channel": "development-main-snapshot", "stable_release": False,
            "source_commit": sha, "runtime": ".NET 8",
            "platform": "linux-x64", "built_files": count
        }, indent=2) + "\n")
        z.writestr(f"{PREFIX}/INSTALL.txt",
            "Development build; NOT an approved Stable release.\n"
            "Do not overwrite production INI files or databases.\n"
            "Back up local configuration and persistent assets first.\n"
            "Verify BUILD-MANIFEST.json and the published SHA256 checksum.\n")
    with zipfile.ZipFile(out) as z:
        if z.testzip():
            raise ValueError("ZIP failed integrity check")
        for item in NEEDED:
            if f"{PREFIX}/bin/{item}" not in z.namelist():
                raise ValueError("Missing required DLL in output")
    print(f"ZIP verified: {out} ({count} bin files)")

def selftest():
    with tempfile.TemporaryDirectory() as folder:
        root = Path(folder)
        bin_dir = root / "bin"
        bin_dir.mkdir()
        for name in NEEDED:
            (bin_dir / name).write_bytes(b"example DLL bytes")
        for name in ("OpenSim.ini", "system.pass", "access.log"):
            (bin_dir / name).write_text("PRIVATE")
        (bin_dir / "OpenSim.ini.example").write_text("example")
        out = root / "test.zip"
        create_zip(bin_dir, out, "a" * 40, root)
        with zipfile.ZipFile(out) as z:
            names = set(z.namelist())
            assert f"{PREFIX}/bin/OpenSim.ini.example" in names
            assert f"{PREFIX}/bin/OpenSim.ini" not in names
            assert f"{PREFIX}/bin/system.pass" not in names
            assert f"{PREFIX}/bin/access.log" not in names
            assert json.loads(z.read(f"{PREFIX}/BUILD-MANIFEST.json"))["source_commit"] == "a" * 40
    print("Generation 1 ZIP packaging self-test PASS")

if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--self-test", action="store_true")
    parser.add_argument("--bin-dir", type=Path)
    parser.add_argument("--output", type=Path)
    parser.add_argument("--sha")
    args = parser.parse_args()
    if args.self_test:
        selftest()
    else:
        if args.bin_dir is None or args.output is None or args.sha is None:
            parser.error("Provide --bin-dir, --output and --sha")
        create_zip(args.bin_dir, args.output, args.sha, Path(__file__).resolve().parents[2])
