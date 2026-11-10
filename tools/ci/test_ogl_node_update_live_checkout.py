#!/usr/bin/env python3
"""Run the REAL updater shell against an isolated local git checkout.

A tiny git wrapper turns only 'git fetch --no-tags origin main' into a local
no-op: origin/main is prepared from real local commits, so CI has no remote
GitHub update and never touches production paths.
"""
import os
from pathlib import Path
import shutil
import subprocess
import tarfile
import tempfile

PROJECT = Path(__file__).resolve().parents[2]
SCRIPT = PROJECT / "tools/ops/ogl-node-update-live.sh"
REAL_GIT = shutil.which("git")
assert REAL_GIT

def run(*args, cwd=None, env=None, expect=0):
    p = subprocess.run(args, cwd=cwd, env=env,
        capture_output=True, text=True, check=False, timeout=30)
    if p.returncode != expect:
        raise AssertionError(
            f"{args} exit {p.returncode} (wanted {expect})\n"
            f"stdout:\n{p.stdout[-3500:]}\nstderr:\n{p.stderr[-3500:]}")
    return p.stdout

with tempfile.TemporaryDirectory(prefix="ogl-live-checkout-regression-") as tmp:
    base = Path(tmp)
    root = base / "checkout"
    backup = base / "outside-backup"
    tools = base / "fake-bin"
    tools.mkdir()
    root.mkdir()
    for relative in (
        "bin/Regions", "bin/regions", "bin/config-include", "bin/Estates",
        "tools/gen11", "tools/ci",
    ):
        (root / relative).mkdir(parents=True, exist_ok=True)
    for relative, content in {
        "bin/OpenSim.ini": "custom=false\n",
        "bin/OpenSimDefaults.ini": "version=old\n",
        "bin/Regions/NexVerse-Deutschland.ini": "old-danger-region\n",
        "bin/regions/NexVerse-Deutschland.ini": "old-lowercase-region\n",
        "bin/Regions/my-local-region.ini": "region=old\n",
        "bin/config-include/local.ini": "setting=old\n",
        "bin/Estates/estate.ini": "estate=old\n",
        "bin/OpenSim.dll": "old-binary",
        "source.txt": "upstream-original\n",
        "runprebuild.sh": "#!/bin/sh\ntouch prebuild-complete\n",
        "compile.sh": (
            "#!/usr/bin/env bash\nset -e\n"
            "mkdir -p bin\n"
            "printf compiled > bin/OpenSim.dll\n"
            "printf compiled > bin/Robust.dll\n"
            "for addon in NexTraffic NexTransit NexSmartLight OGLWeather OGLEnergy; do\n"
            "  printf compiled > bin/OpenGenesisLINK.Addons.${addon}.dll\n"
            "done\n"
        ),
        "tools/gen11/verify_net10.py": "print('net10-ok')\n",
        "tools/ci/verify_gen1_main_net10_scope.py": "print('scope-ok')\n",
    }.items():
        (root / relative).write_text(content)
    run(REAL_GIT, "init", "-b", "main", cwd=root)
    run(REAL_GIT, "config", "user.name", "OGL Regression", cwd=root)
    run(REAL_GIT, "config", "user.email", "ci@invalid.example", cwd=root)
    run(REAL_GIT, "add", ".", cwd=root)
    run(REAL_GIT, "commit", "-m", "baseline", cwd=root)
    first = run(REAL_GIT, "rev-parse", "HEAD", cwd=root).strip()

    # New origin/main contains updated defaults; old local checkout will be
    # reset to it and active operator INIs restored from their backup.
    (root / "bin/OpenSimDefaults.ini").write_text("version=new-default\n")
    (root / "source.txt").write_text("upstream-new\n")
    run(REAL_GIT, "add", ".", cwd=root)
    run(REAL_GIT, "commit", "-m", "new main", cwd=root)
    newer = run(REAL_GIT, "rev-parse", "HEAD", cwd=root).strip()
    run(REAL_GIT, "update-ref", "refs/remotes/origin/main", newer, cwd=root)
    run(REAL_GIT, "reset", "--hard", first, cwd=root)
    run(REAL_GIT, "remote", "add", "origin",
        "https://github.com/opengenesislink/opengenesislinkserver.git", cwd=root)
    (root / "bin/OpenSim.ini").write_text("custom=true\n")
    (root / "bin/config-include/local.ini").write_text("setting=operator\n")
    (root / "bin/Regions/my-local-region.ini").write_text("region=operator\n")
    (root / "source.txt").write_text("local-dirty-source\n")

    (tools / "git").write_text(
        "#!/usr/bin/env bash\n"
        "if [[ \"$1\" == fetch && \"$2\" == --no-tags && "
        "\"$3\" == origin && \"$4\" == main ]]; then exit 0; fi\n"
        f"exec {REAL_GIT} \"$@\"\n"
    )
    (tools / "dotnet").write_text(
        "#!/usr/bin/env bash\n"
        "case \"$1\" in\n"
        "  --list-sdks) printf '10.0.100 [/sdk]\\n';;\n"
        "  --list-runtimes) printf 'Microsoft.NETCore.App 10.0.0 [/runtime]\\n';;\n"
        "  *) exit 65;;\n"
        "esac\n"
    )
    for name in ("git", "dotnet"):
        (tools / name).chmod(0o755)
    env = dict(os.environ)
    env["PATH"] = str(tools) + os.pathsep + env["PATH"]

    output = run("bash", str(SCRIPT), str(root), str(backup), cwd=base, env=env)
    assert "UPDATE ERFOLGREICH" in output
    assert "Neustart manuell" in output
    assert run(REAL_GIT, "rev-parse", "HEAD", cwd=root).strip() == newer
    assert (root / "source.txt").read_text() == "upstream-new\n"
    assert (root / "bin/OpenSimDefaults.ini").read_text() == "version=new-default\n"
    assert (root / "bin/OpenSim.ini").read_text() == "custom=true\n"
    assert (root / "bin/config-include/local.ini").read_text() == "setting=operator\n"
    assert (root / "bin/Regions/my-local-region.ini").read_text() == "region=operator\n"
    assert not (root / "bin/Regions/NexVerse-Deutschland.ini").exists()
    assert not (root / "bin/regions/NexVerse-Deutschland.ini").exists()
    for dll in ("OpenSim.dll", "Robust.dll"):
        assert (root / "bin" / dll).read_text() == "compiled"
    for addon in ("NexTraffic", "NexTransit", "NexSmartLight", "OGLWeather", "OGLEnergy"):
        assert (root / "bin" / ("OpenGenesisLINK.Addons." + addon + ".dll")).is_file()

    directories = [d for d in backup.iterdir() if d.is_dir()]
    assert len(directories) == 1
    saved = directories[0]
    assert (saved / "previous-commit.txt").read_text().strip() == first
    assert (saved / "updated-commit.txt").read_text().strip() == newer
    assert "local-dirty-source" in (saved / "local-changes.patch").read_text()
    with tarfile.open(saved / "configs.tar.gz") as archive:
        names = archive.getnames()
        assert "bin/Regions/NexVerse-Deutschland.ini" in names
        assert "bin/regions/NexVerse-Deutschland.ini" in names
    assert (root / "prebuild-complete").is_file(), "runprebuild did not execute"

    # Unsafe branch must be rejected BEFORE running git reset or any build.
    run(REAL_GIT, "checkout", "-b", "not-main", cwd=root)
    refused = subprocess.run(["bash", str(SCRIPT), str(root), str(backup)],
        cwd=base, env=env, capture_output=True, text=True, timeout=15)
    assert refused.returncode != 0 and "main" in refused.stderr
    assert run(REAL_GIT, "branch", "--show-current", cwd=root).strip() == "not-main"

print("PASS: real update shell backups, remote/main reset, per-node Germany INI deletion, .NET10 build gates, add-ons and non-main refusal")
