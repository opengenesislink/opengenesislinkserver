#!/usr/bin/env python3
"""Keep Generation-1 repository sources separate from local runtime output.

Static contract only; does not delete files or inspect production services.
The retained bin/ binaries are important build/runtime dependencies.
"""
from __future__ import annotations

import json
import re
import subprocess
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
read = lambda relative: (ROOT / relative).read_text(encoding="utf-8")

version = json.loads(read("global.json"))["sdk"]["version"]
assert version.startswith("10."), f"Not a .NET 10 SDK: {version}"
readme = read("README.md")
assert "Generation 1 (0.9.4.0)" in readme
assert ".NET 10" in readme
assert "opengenesislink.de" in readme
assert ".NET 8" not in readme
assert "0.9.3." not in readme
assert "bash runprebuild.sh" in readme
assert "dotnet build OpenSim.sln" in readme

makefile = read("Makefile")
assert "bash runprebuild.sh" in makefile
assert "$(DOTNET) restore $(SOLUTION)" in makefile
assert "$(DOTNET) build $(SOLUTION) --configuration $(CONFIG) --no-restore" in makefile
assert "$(PYTHON) tools/gen11/verify_net10.py" in makefile
assert "nant" not in makefile.lower(), "Legacy NAnt build commands are unsupported"
assert "VERSIONING.md" and "0.9.4.0" in read("VERSIONING.md")

ignored = read(".gitignore")
for pattern in ("*.log", "*.log.*", "**/obj/", "bin/data/", "Makefile.local"):
    assert pattern in ignored, f"Missing .gitignore guard: {pattern}"

for present in (
    "doc/README.md",
    "doc/TESTING.md",
    "doc/NexVerse/WORLD_NPC_OWNER_API_R617.md",
    "bin/prebuild.dll",
    "bin/OpenMetaverse.dll",
    "runprebuild.sh",
    "tools/gen11/verify_net10.py",
):
    assert (ROOT / present).is_file(), f"Required source or dependency missing: {present}"

for obsolete in (
    "TESTING.txt",
    "WORLD_NPC_OWNER_API_R617.md",
    "bin/OpenSim.log",
    "bin/Robust.log",
    "bin/OpenSimStats.log",
    "bin/RobustStats.log",
):
    assert not (ROOT / obsolete).exists(), f"Obsolete tracked file: {obsolete}"

# A proper source checkout must not track machine-specific runtime logs,
# SDK intermediates or local IDE artifacts anywhere in the project.
tracked = subprocess.run(
    ["git", "ls-files"], cwd=ROOT, check=True,
    capture_output=True, text=True,
).stdout.splitlines()
bad = [
    path for path in tracked
    if re.search(r"(^|/)(?:obj|__pycache__)/", path)
    or re.search(r"\.log(?:\..+)?$", path, re.IGNORECASE)
    or path.endswith((".pyc", ".pyo"))
]
assert not bad, f"Runtime or build output is tracked: {bad[:20]}"

docs = read("doc/README.md")
for name in ("TESTING.md", "NexVerse/", "OpenGenesisLINK/",
             "GEN1_NET10_MAIN_MIGRATION.md"):
    assert name in docs, f"Documentation index missing: {name}"

# Ensure active CI protects the cleanup and historical release audit
# workflows are still available; this PR does not rewrite old releases.
for old_release in ("publish-0.9.3.8-stable.yml", "publish-0.9.3.10-stable.yml"):
    assert (ROOT / ".github" / "workflows" / old_release).is_file()

print("Generation 1 (.NET 10) repository hygiene, docs and build: PASS")
