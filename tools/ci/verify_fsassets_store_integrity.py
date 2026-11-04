#!/usr/bin/env python3
"""Static regression gate for the legacy FSAssets Store paths.

This protects source-level invariants; the full build and integration gates
exercise compilation and other asset-service behavior separately.
"""
from pathlib import Path

root = Path(__file__).resolve().parents[2]
service = (root / "OpenSim/Services/FSAssetService/FSAssetService.cs").read_text()
sql = (root / "OpenSim/Data/MySQL/MySQLFSAssetData.cs").read_text()

store = service.split("private string Store(AssetBase asset, bool force)", 1)[1].split(
    "protected bool TryUpdateContentAtomic", 1
)[0]
assert store.count("ExternalRepresentationUtils.SanitizeXml(") == 1, (
    "Object XML must be normalized exactly once in the legacy Store path"
)
assert store.index("ExternalRepresentationUtils.SanitizeXml(") < store.index(
    "string hash = GetSHA256Hash(asset.Data);"
), "FSAssets must hash the final persisted object bytes"
assert store.index("string hash = GetSHA256Hash(asset.Data);") < store.index(
    "if (!AssetExists(hash))"
), "Hash must be determined before selecting the spool path"
assert "m_DataConnector.Store(asset.Metadata, hash)" in store

metadata_store = sql.split("public bool Store(AssetMetadata meta, string hash)", 1)[1].split(
    "public bool[] AssetsExist", 1
)[0]
assert "return ExecuteNonQuery(cmd);" in metadata_store, (
    "Metadata INSERT failures must be propagated"
)
assert "ExecuteNonQuery(cmd);\n\n                        return true;" not in metadata_store
print("PASS: FSAssets metadata-INSERT failures propagate; object bytes are normalized before hashing")
