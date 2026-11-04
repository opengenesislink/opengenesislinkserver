#!/usr/bin/env python3
"""Generation 1.1 / .NET 10 foundation safety gate.

This deliberately fails if the main simulator, ROBUST or a dynamically
loaded addon accidentally falls back to net8.0. It does not mistake
the legacy .NET Framework 4.8 *source* for a migrated prebuilt generator.
"""
from __future__ import annotations

import json
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
issues: list[str] = []
checked = 0

sdk = json.loads((ROOT / "global.json").read_text())
if not sdk["sdk"]["version"].startswith("10.0."):
    issues.append("global.json must select the .NET 10 SDK")

for project in ROOT.rglob("*.csproj"):
    rel = project.relative_to(ROOT)
    if {".git", "obj", "bin"} & set(rel.parts):
        continue
    content = project.read_text(encoding="utf-8-sig")
    # The repository carries a legacy .NET Framework 4.8 Prebuild source
    # tree: its *bundled* generator is temporarily adapted through a
    # deterministic generated-project retarget step until rebootstrap.
    if rel.as_posix() == "Prebuild/src/Prebuild.csproj":
        continue
    checked += 1
    if "net8.0" in content:
        issues.append(f"{rel}: still references net8.0")
    targets = re.findall(r"<TargetFrameworks?>([^<]+)</TargetFrameworks?>", content)
    if not targets:
        issues.append(f"{rel}: TargetFramework missing")
    elif any("net10.0" not in item for item in targets):
        issues.append(f"{rel}: unexpected target {targets}")

for runtime in sorted((ROOT / "bin").glob("*.runtimeconfig.json")) + [
    ROOT / "Prebuild/bootstrap/prebuild.runtimeconfig.json"
]:
    content = json.loads(runtime.read_text())
    options = content.get("runtimeOptions", {})
    if options.get("tfm") != "net10.0" or options.get("framework", {}).get("version", "").split(".")[0] != "10":
        issues.append(f"{runtime.relative_to(ROOT)}: not net10.0 / runtime 10")

for path, token in {
    "runprebuild.sh": "python3 tools/gen11/retarget_net10.py",
    "runprebuild.bat": r"tools\gen11\retarget_net10.py",
    "addon-modules/OGLWeather/build.sh": "net10.0/",
    "addon-modules/build-nex-city-addon.sh": "net10.0/",
    "Prebuild/src/Core/Nodes/ProjectNode.cs": "net10_0",
    "Prebuild/src/Core/Targets/VS2022Target.cs": "FrameworkVersion.net10_0",
    ".github/workflows/gen11-net10.yml": "dotnet-version: '10.0.x'",
}.items():
    if token not in (ROOT / path).read_text(encoding="utf-8-sig"):
        issues.append(f"{path}: missing {token!r}")

if issues:
    for issue in issues:
        print("::error::" + issue)
    raise SystemExit(1)
print(f"G1.1 .NET 10 foundation checks: OK ({checked} SDK projects + runtimes)")
