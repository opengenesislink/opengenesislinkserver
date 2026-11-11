#!/usr/bin/env python3
"""Fail-closed source contracts for the Generation 1 immutable stable and rolling updates."""
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
read = lambda p: (ROOT / p).read_text(encoding="utf-8")
version = read("OpenSim/Framework/VersionInfo.cs")
stable = read(".github/workflows/g1-stable-0940.yml")
updates = read(".github/workflows/g1-updates.yml")
legacy = read(".github/workflows/publish-gen1-main-snapshot.yml")
packager = read("tools/release/ogl_gen1_snapshot_zip.py")
acceptance = read("doc/release-acceptance/GEN1_0.9.4.0.md")
policy = read("doc/OpenGenesisLINK/GEN1_STABLE_UPDATE_POLICY.md")

assert 'VersionNumber = "0.9.4.0"' in version
assert 'VERSION_FLAVOUR = Flavour.Dev;' in version or 'VERSION_FLAVOUR = Flavour.Release;' in version
for marker in (
    "workflow_dispatch:", "OGL_GEN1_STABLE_RELEASE_APPROVED",
    "environment: generation1-stable", "source_sha:",
    "Gepruefter Code-Commit:", "git merge-base --is-ancestor",
    "git diff --name-only", "Status: APPROVED",
    "v0.9.4.0", "gh release create v0.9.4.0",
    "generation1-stable", "version", "checks.json",
):
    assert marker in stable, marker
for forbidden in ("gh release delete", "gh release edit", "git/refs/tags/v0.9.4.0"):
    assert forbidden not in stable, forbidden
for marker in (
    'workflows: ["NexVerse CI"]', "OGL_GEN1_UPDATES_APPROVED",
    "generation1-updates", 'tag="gen1-updates"',
    "gh release edit", "gh release upload", "git merge-base --is-ancestor",
    "NexVerse CI", "v0.9.4.0", ".NET 10 Windows x64 build",
):
    assert marker in updates, marker
assert "gh release delete" not in updates
assert 'tag="gen1-main"' in legacy
assert "Flavour.Dev" in legacy
assert "generation1-stable" in packager and "generation1-updates" in packager
assert 'Flavour.Release;' in packager and 'net10.0' in packager
assert "stable_release" in packager and "testzip()" in packager
assert "Status: PENDING" in acceptance or "Status: APPROVED" in acceptance
for marker in ("Login_Inventar:", "Teleport_Hypergrid:", "Mesh_Asset:",
               "LSL_Animesh:", "Feed_Datenloeschung:", "DB_Backup_Restore_Rollback:"):
    assert marker in acceptance, marker
assert "0.9.4.0" in policy and "gen1-updates" in policy
print("Generation 1 Stable / Updates release channel contracts PASS")
