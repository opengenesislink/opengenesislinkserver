#!/usr/bin/env python3
"""Guard rollback-safe MySQL writes for object and terrain persistence."""
from pathlib import Path

root = Path(__file__).resolve().parents[2]
code = (root / "OpenSim/Data/MySQL/MySQLSimulationData.cs").read_text()

def body(start, end):
    return code.split(start, 1)[1].split(end, 1)[0]

obj = body("public virtual void StoreObject(SceneObjectGroup obj, UUID regionUUID)",
           "public virtual void RemoveObject(")
for token in ("dbcon.BeginTransaction()", "cmd.Transaction = transaction;",
              "transaction.Commit();", "transaction.Rollback();", "throw;"):
    assert token in obj, "StoreObject: missing " + token
assert obj.index("cmd.Transaction = transaction;") < obj.index("ExecuteNonQuery(cmd);")
assert obj.index("transaction.Commit();") > obj.rindex("ExecuteNonQuery(cmd);")

for name, end in (("StoreTerrain", "StoreBakedTerrain"),
                  ("StoreBakedTerrain", "// Legacy region loading")):
    region = body("public void " + name + "(TerrainData terrData, UUID regionID)",
                  ("public void " + end + "(") if end == "StoreBakedTerrain"
                  else end)
    for token in ("dbcon.BeginTransaction()", "cmd.Transaction = transaction;",
                  "cmd2.Transaction = transaction;", "transaction.Commit();",
                  "transaction.Rollback();"):
        assert token in region, name + ": missing " + token
    assert region.index("transaction.Commit();") > region.index("ExecuteNonQuery(cmd2);")
    assert region.index("ExecuteNonQuery(cmd);") < region.index("ExecuteNonQuery(cmd2);")
inventory = body("public virtual void StorePrimInventory(UUID primID, ICollection<TaskInventoryItem> items)",
                 "public UUID[] GetObjectIDs(UUID regionID)")
for token in ("dbcon.BeginTransaction()", "cmd.Transaction = transaction;",
              "transaction.Commit();", "transaction.Rollback();", "throw;"):
    assert token in inventory, "StorePrimInventory: missing " + token
assert inventory.index("cmd.Transaction = transaction;") < inventory.index(
    'cmd.CommandText = "delete from primitems')
assert "if (items.Count != 0)" in inventory, "Empty inventory must still commit deletion"
assert inventory.index("transaction.Commit();") > inventory.index("foreach (TaskInventoryItem item in items)")
print("PASS: StoreObject, StorePrimInventory and both terrain paths are transactional")
