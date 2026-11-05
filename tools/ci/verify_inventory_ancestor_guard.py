#!/usr/bin/env python3
"""Static guard for nested Lost And Found ancestor classification."""
from pathlib import Path

root = Path(__file__).resolve().parents[2]
path = root / "OpenSim/Services/InventoryService/XInventoryService.cs"
code = path.read_text()
predicate = code.split("private bool ParentIsTrashOrLost(UUID folderID)", 1)[1]
assert "parent[0].type == (int)FolderType.Trash || parent[0].type == (int)FolderType.LostAndFound" in predicate, (
    "Descendants must be checked against their current ancestor's folder type"
)
assert "parent[0].type == (int)FolderType.Trash || folder[0].type == (int)FolderType.LostAndFound" not in predicate

tests = (root / "OpenSim/Services/InventoryService/Tests/XInventoryServiceTests.cs").read_text()
assert "TestPurgeNestedLostAndFoundDescendant" in tests
assert "Assert.That(inventory.PurgeFolder(grandchild), Is.True);" in tests
assert "Assert.That(inventory.PurgeFolder(regular), Is.False);" in tests
print("PASS: nested Lost And Found ancestor predicate is correct")
