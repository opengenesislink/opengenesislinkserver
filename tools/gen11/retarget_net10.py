#!/usr/bin/env python3
"""Retarget Prebuild-generated OpenSim projects to the G1.1 .NET 10 SDK.

The checked-in legacy prebuild.dll accepts /targetframework net8_0 and
regenerates .csproj files accordingly. Until the bundled generator itself is
rebuilt with a net10_0 enum, run this step immediately after Prebuild.
Never touch source files, user configuration or binary dependencies.
"""
from __future__ import annotations

import argparse
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
OLD = "net8.0"
NEW = "net10.0"


def projects():
    for project in sorted(ROOT.rglob("*.csproj")):
        if {".git", "bin", "obj"} & set(project.relative_to(ROOT).parts):
            continue
        yield project


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--check", action="store_true",
                        help="fail when a project still targets net8.0")
    args = parser.parse_args()
    changed: list[str] = []
    stale: list[str] = []
    scanned = 0
    for project in projects():
        scanned += 1
        source = project.read_text(encoding="utf-8-sig")
        if OLD not in source:
            continue
        rel = str(project.relative_to(ROOT))
        stale.append(rel)
        if not args.check:
            project.write_text(source.replace(OLD, NEW), encoding="utf-8")
            changed.append(rel)
    if args.check:
        if stale:
            for path in stale:
                print(f"ERROR: net8.0 project still present: {path}")
            return 1
        print(f"G1.1 .NET 10: {scanned} project files checked, no net8.0 targets")
        return 0
    print(f"G1.1 .NET 10: retargeted {len(changed)} of {scanned} project files")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
