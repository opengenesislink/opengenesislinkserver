#!/usr/bin/env python3
"""Static safeguards for Generation 1's public Hypergrid asset egress lockdown.

These assertions complement (do not replace) live negative HTTP, viewer, and
HG tests. No endpoint access is attempted from CI.
"""
from pathlib import Path

root = Path(__file__).resolve().parents[2]


def read(path):
    return (root / path).read_text(encoding="utf-8-sig")


def section(path, name):
    src = read(path)
    start = "[{}]".format(name)
    assert src.count(start) == 1, (path, name)
    body = src.split(start, 1)[1]
    return body.split("\n[", 1)[0]


perms = read("OpenSim/Framework/AssetPermissions.cs")
assert perms.count("private readonly bool m_BlockAllExports;") == 1
assert perms.count("public bool BlockAllExports => m_BlockAllExports;") == 1
assert perms.count('config.GetBoolean("DisallowAllExport", false)') == 1
assert "public bool AllowedExport(sbyte type)\n        {\n            if (m_BlockAllExports)\n                return false;" in perms
assert "public bool AllowedImport(sbyte type)" in perms

connector = read("OpenSim/Server/Handlers/Asset/AssetServerConnector.cs")
assert 'String.Equals(m_ConfigName, "HGAssetService"' in connector
assert 'serverConfig.GetBoolean("DisallowAllExport", false)' in connector
assert "if (!hgExportLocked)" in connector
assert "new AssetServerGetHandler(m_AssetService, auth, redirectURL)" in connector.split("if (!hgExportLocked)", 1)[1].split("}", 1)[0]
assert "new AssetsExistHandler(m_AssetService)" in connector.split("if (!hgExportLocked)", 1)[1].split("}", 1)[0]
assert "server.AddStreamHandler(new AssetServerPostHandler(m_AssetService, auth));" in connector

for path, base_get, base_meta, exists in [
    ("OpenSim/Services/HypergridService/HGFSAssetService.cs",
     "public override AssetBase Get(string id)", "public override AssetMetadata GetMetadata(string id)",
     "public override bool[] AssetsExist(string[] ids)"),
    ("OpenSim/Services/HypergridService/HGAssetService.cs",
     "public  AssetBase Get(string id)", "public AssetMetadata GetMetadata(string id)",
     "public bool[] AssetsExist(string[] ids)"),
    ("OpenSim/Services/HypergridService/HGRemoteAssetService.cs",
     "public AssetBase Get(string id)", "public AssetMetadata GetMetadata(string id)",
     "public bool[] AssetsExist(string[] ids)")
]:
    src = read(path)
    assert base_get in src and base_meta in src and exists in src, path
    for member in (base_get, base_meta, exists):
        first = src.split(member, 1)[1].split("{", 1)[1][:190]
        assert "if (m_AssetPerms.BlockAllExports)" in first, (path, member)
    assert "public " in src and "Store(AssetBase asset)" in src, path

for name in (
    "bin/Robust.HG.ini", "bin/Robust.HG.ini.example",
    "bin/Robust.HG.OGLAsset.ini.example",
    "bin/config-include/GridCommon.ini",
    "bin/config-include/GridCommon.ini.example",
    "bin/config-include/StandaloneCommon.ini.example",
):
    hg = section(name, "HGAssetService")
    assert "DisallowAllExport = true" in hg, name
    assert not any(line.strip().startswith("DisallowImport =")
                   for line in hg.splitlines()), name

for name in (
    "bin/config-include/GridCommon.ini",
    "bin/config-include/GridCommon.ini.example",
    "bin/config-include/StandaloneCommon.ini.example",
):
    section_data = section(name, "HGInventoryAccessModule")
    assert "OutboundPermission = false" in section_data, name

notes = read("doc/NexVerse/GEN1_ASSET_EGRESS_LOCKDOWN.md")
assert "not" in notes.lower() and "100% Copybot prevention" in notes
print("Generation 1 HG egress safeguards: policy, HTTP routes, services, configuration, threat model OK")
