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

# Runtime-region INIs must remain operator-owned, not delivered as
# source-controlled production identities and externally reachable ports.
assert "/bin/Regions/*.ini" in ignored
assert (ROOT / "bin/Regions/.keep").exists()
assert not (ROOT / "bin/Regions/NexVerse-Deutschland.ini").exists()
assert "CONTRIBUTING.md" in read("doc/README.md")
commit_rules = read("CONTRIBUTING.md")
for prefix in ("Neu:", "Update:", "Verbesserung:"):
    assert prefix in commit_rules, prefix

# Completed old publishers must not be reintroduced on new main commits.
for publisher in (
    "publish-0.9.3.8-stable.yml", "publish-0.9.3.10-stable.yml",
    "publish-0.9.3.12-stable.yml", "publish-0.9.3.13-dev.yml",
    "publish-0.9.3.13-stable.yml",
):
    assert not (ROOT / ".github/workflows" / publisher).exists(), publisher

# Branch deletion only via checks for closed/merged PRs with unchanged SHA
# or full reachability from main. Protected and open work must survive.
branch_cleaner = read("tools/ops/bereinige_gemergte_branches.py")
assert "darf_entfernt_werden" in branch_cleaner
assert "hat_offenen_pr" in branch_cleaner
assert "unveraendert(" in branch_cleaner
assert "LONG_LIVED" in branch_cleaner
assert (ROOT / ".github/workflows/g1-branches-bereinigung.yml").is_file()
subprocess.run(
    ["python3", "tools/ops/bereinige_gemergte_branches.py", "--selbsttest"],
    cwd=ROOT, check=True,
)

for legacy in (
    "doc/NexVerse/RELEASE_0.9.3.4_RC1.md",
    "doc/NexVerse/RELEASE_0.9.3.10_RC1_DRAFT.md",
    "doc/NexVerse/RELEASE_0.9.3.13_DEV.md",
    "doc/NexVerse/09312_RC1_CANDIDATE_PLAN.md",
    "tools/ci/verify_stable_release_gate.py",
):
    assert not (ROOT / legacy).exists(), legacy

# Removed documents must not remain as broken relative Markdown links
# anywhere in the still-active documentation.
removed_documents = (
    "CHECKPOINT_0.9.3.2.md", "CHECKPOINT_0.9.3.3.md",
    "RELEASE_0.9.3.1.md", "RELEASE_0.9.3.4.md",
    "RELEASE_0.9.3.4_RC1.md", "RELEASE_0.9.3.8_RC1.md",
    "RELEASE_0.9.3.8.md", "RELEASE_0.9.3.10_RC1_DRAFT.md",
    "RELEASE_0.9.3.10.md", "RELEASE_TRAIN_0.9.3.10.md",
    "RELEASE_0.9.3.12.md", "RELEASE_0.9.3.13_DEV.md",
    "RELEASE_0.9.3.13_RC1.md", "09310_RC1_RELEASE_READINESS.md",
    "09310_ACCEPTANCE_BLOCKERS.md", "RC1_OPERATOR_ACCEPTANCE_09310.md",
    "09312_RC1_CANDIDATE_PLAN.md", "09312_RC1_STABLE_ACCEPTANCE.md",
    "FUTURE_VIEWER_NAVIGATION.md", "R287_WORLD_API_DELIVERY.md",
    "FIRESTORM_VOICE_PATHFINDING_LOG_20261010.md",
)
for file in (ROOT / "doc").rglob("*.md"):
    body = file.read_text(encoding="utf-8")
    for obsolete in removed_documents:
        assert "](" + obsolete + ")" not in body, (
            f"Defekter Markdown-Link auf geloeschte Datei: {file}: {obsolete}"
        )

print("Generation 1 (.NET 10) repository hygiene, docs, branches and build: PASS")
