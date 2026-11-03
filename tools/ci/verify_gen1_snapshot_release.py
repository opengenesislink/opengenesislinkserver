#!/usr/bin/env python3
"""Guarantee current-main ZIP publishing is CI-gated and Stable remains immutable."""
from pathlib import Path
flow = Path(".github/workflows/publish-gen1-main-snapshot.yml").read_text()
script = Path("tools/release/ogl_gen1_snapshot_zip.py").read_text()
for marker in (
    'workflows: ["NexVerse CI"]',
    "github.event.workflow_run.conclusion == 'success'",
    "github.event.workflow_run.event == 'push'",
    "github.event.workflow_run.head_repository.full_name == github.repository",
    "github.event.workflow_run.head_sha", 'tag="gen1-main"',
    "--prerelease", "group: ogl-gen1-rolling-release", "--clobber",
    "dotnet build --configuration Release OpenSim.sln",
    "sha256sum", "git/refs/tags/$tag", "is not a prerelease",
):
    assert marker in flow, marker
assert 'tag="v0.9.3.13"' not in flow
assert "gh release delete" not in flow
assert "gh release create" in flow and "gh release upload" in flow
for marker in ("--self-test", "BUILD-MANIFEST.json", "SKIP_SUFFIXES", "source_commit", "testzip()", "stable_release"):
    assert marker in script, marker
print("Generation 1 current-main ZIP publisher contracts PASS")
