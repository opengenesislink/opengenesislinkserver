#!/usr/bin/env python3
"""Static integration guard: Generation 1 Firestorm/OGL-only admission."""
from pathlib import Path

root = Path(__file__).resolve().parents[2]


def read(name):
    return (root / name).read_text(encoding="utf-8-sig")


def section(data, name):
    assert data.count("[" + name + "]") == 1, name
    return data.split("[" + name + "]", 1)[1].split("\n[", 1)[0]


policy = read("OpenSim/Framework/OglApprovedViewerPolicy.cs")
for marker in (
    '"Firestorm", "OpenGenesisLINK", "OGL Viewer"',
    '"Darkstorm", "Hydrastorm", "Copybot"',
    "StartsWithApprovedName",
    "ContainsKnownCopybotName",
    "IsNumericVersion",
):
    assert marker in policy, marker

project = read("OpenSim/Framework/OpenSim.Framework.csproj")
assert 'Compile Include="OglApprovedViewerPolicy.cs"' in project

for path, needle in (
    ("OpenSim/Services/LLLoginService/LLLoginService.cs",
     "OglApprovedViewerPolicy.IsAllowed(clientVersion, channel)"),
    ("OpenSim/Services/HypergridService/GatekeeperService.cs",
     "OglApprovedViewerPolicy.IsAllowed(aCircuit.Viewer, aCircuit.Channel)"),
    ("OpenSim/Region/Framework/Scenes/Scene.cs",
     "OglApprovedViewerPolicy.IsAllowed(acd.Viewer, acd.Channel)"),
):
    src = read(path)
    assert needle in src, path
    assert '"OGLApprovedViewersOnly"' in src, path

for path in (
    "bin/Robust.HG.ini",
    "bin/Robust.HG.ini.example",
    "bin/Robust.HG.OGLAsset.ini.example",
    "bin/Robust.ini.example",
    "bin/OpenSim.ini",
    "bin/OpenSim.ini.example",
):
    body = section(read(path), "AccessControl")
    assert "OGLApprovedViewersOnly = true" in body, path

hg = read("bin/config-include/GridCommon.ini")
robust = read("bin/Robust.HG.ini")
assert "OutboundPermission = true" in hg
assert "DisallowAllExport = false" in robust
assert "MaxHGAssetExistIds = 16384" in robust

print("Generation 1 approved-viewer access: login + gatekeeper + region + configs OK")
