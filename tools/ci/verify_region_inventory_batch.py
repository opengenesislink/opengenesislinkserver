#!/usr/bin/env python3
"""Guard single-query task inventory loading for MariaDB and PostgreSQL."""
from pathlib import Path

root = Path(__file__).resolve().parents[2]
configs = [
    ("MySQL", "OpenSim/Data/MySQL/MySQLSimulationData.cs",
     'INNER JOIN prims AS p ON p.UUID = i.primID',
     'WHERE p.RegionUUID = ?RegionUUID'),
    ("PGSQL", "OpenSim/Data/PGSQL/PGSQLSimulationData.cs",
     'INNER JOIN prims AS p ON p.""UUID"" = inv.""primID""',
     'WHERE p.""RegionUUID"" = :RegionUUID'),
]

for provider, path, join, scope in configs:
    source = (root / path).read_text()
    loader = source.split("private int LoadItems(Dictionary<UUID, SceneObjectPart> prims", 1)[1].split(
        "return grouped.Count;", 1
    )[0]
    assert join in loader, f"{provider} missing indexed prim join"
    assert scope in loader, f"{provider} missing region filter"
    assert "RestoreInventoryItems(entry.Value)" in loader
    assert "new Dictionary<UUID, List<TaskInventoryItem>>()" in loader
    assert "SELECT DISTINCT" not in loader.upper()
    assert "foreach (SceneObjectPart objectPart" not in loader
    assert "LoadItems(prims, region" in source

tests = (root / "tools/ci/RegionInventoryBatchRegression/Program.cs").read_text()
assert "db.LoadObjects(regionA)" in tests and "db.LoadObjects(regionB)" in tests
assert "GetInventoryItems().Count != 2" in tests
print("PASS: both region SQL providers batch-load only requested prim inventory")
