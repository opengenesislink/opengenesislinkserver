#!/usr/bin/env python3
"""Retarget Prebuild-generated project TFMs for the Generation 1.1 .NET 10 build.

The checked-in prebuild.dll only recognizes framework IDs through net9_0.
Generation 1.1 therefore invokes the compatible net8_0 generator and retargets
the generated *project TFMs* before restore/build. No source, .ini, solution,
package dependency or binary file is changed here.
"""
from __future__ import annotations

import argparse
from pathlib import Path
import re
import sys

ROOT = Path(__file__).resolve().parents[2]
TFM_TAG = re.compile(r"(<TargetFrameworks?>)([^<]*)(</TargetFrameworks?>)")
OLD_TFM = re.compile(r"(?<![A-Za-z0-9])net8\.0(?=\s*(?:;|$)|-)")


def retarget(text: str) -> str:
    def change(match: re.Match[str]) -> str:
        return match.group(1) + OLD_TFM.sub("net10.0", match.group(2)) + match.group(3)

    return TFM_TAG.sub(change, text)


def candidate_projects():
    for file in sorted(ROOT.rglob("*.csproj")):
        parts = file.relative_to(ROOT).parts
        if set(parts) & {"obj", "bin", ".git", "Prebuild"}:
            continue
        yield file


def main() -> int:
    parser = argparse.ArgumentParser()
    mode = parser.add_mutually_exclusive_group(required=True)
    mode.add_argument("--fix", action="store_true", help="retarget net8.0 project TFMs to net10.0")
    mode.add_argument("--check", action="store_true", help="fail if any project still targets net8.0")
    mode.add_argument("--self-test", action="store_true", help="validate the exact TFM-only conversion")
    args = parser.parse_args()

    if args.self_test:
        before = "<TargetFramework>net8.0</TargetFramework>\n<!-- net8.0 retained in documentation -->\n<TargetFrameworks>net8.0;net6.0</TargetFrameworks>\n"
        after = retarget(before)
        assert "<TargetFramework>net10.0</TargetFramework>" in after
        assert "<TargetFrameworks>net10.0;net6.0</TargetFrameworks>" in after
        assert "<!-- net8.0 retained in documentation -->" in after
        assert retarget(after) == after
        print("Generation 1.1 retarget self-test passed")
        return 0

    scanned, changed, stale = 0, 0, []
    for file in candidate_projects():
        scanned += 1
        original = file.read_text(encoding="utf-8")
        converted = retarget(original)
        if converted != original:
            if args.fix:
                file.write_text(converted, encoding="utf-8", newline="")
                changed += 1
            else:
                stale.append(str(file.relative_to(ROOT)))

    if stale:
        for name in stale:
            print(f"ERROR: still targets net8.0: {name}", file=sys.stderr)
        return 1

    print(f"Generation 1.1 .NET 10 TFM gate: {scanned} projects inspected, {changed} converted")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
