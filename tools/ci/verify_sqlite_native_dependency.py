#!/usr/bin/env python3
"""Fail builds that reintroduce the high-severity e_sqlite3 NuGet dependency.

GHSA-2m69-gcr7-jv3q affects SQLitePCLRaw.lib.e_sqlite3 <= 2.1.11.
The native addon build copies exactly the version resolved by the project assets.
"""
from pathlib import Path
import json
import sys

root = Path(__file__).resolve().parents[2]
projects = [
    root / "addons/OGLAssetService/OGLAssetService.csproj",
    root / "addons/OGLAssetService.Migration/OGLAssetService.Migration.csproj",
    root / "addons/OGLAssetService.Tests/OGLAssetService.Tests.csproj",
]

def version_tuple(v):
    nums = v.split(".")[:3]
    if len(nums) != 3 or not all(p.isdecimal() for p in nums):
        raise AssertionError(f"Unexpected NuGet package version {v}")
    return tuple(map(int, nums))

versions = {}
for project in projects:
    assets_file = project.parent / "obj/project.assets.json"
    if not assets_file.exists():
        raise SystemExit(f"Run dotnet restore first: {assets_file}")
    assets = json.loads(assets_file.read_text(encoding="utf-8"))
    libraries = assets["libraries"]
    for package, minimum in (
        ("microsoft.data.sqlite", (10, 0, 14)),
        ("sqlitepclraw.lib.e_sqlite3", (2, 1, 12)),
    ):
        matches = [entry.partition("/")[2] for entry in libraries
                   if entry.lower().startswith(package + "/")]
        if len(matches) != 1 or version_tuple(matches[0]) < minimum:
            raise SystemExit(f"FAIL: {project.name}: require {package} >= {minimum}, resolved: {matches}")
        if package == "sqlitepclraw.lib.e_sqlite3":
            versions[project.name] = matches[0]

if len(set(versions.values())) != 1:
    raise SystemExit(f"FAIL: inconsistent native SQLite versions: {versions}")
print("PASS: all OGL AssetService projects use non-vulnerable SQLitePCLRaw native dependency " +
      next(iter(versions.values())))
