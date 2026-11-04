#!/usr/bin/env python3
"""Static regression gate: deletions must not silently report success."""
from pathlib import Path

root = Path(__file__).resolve().parents[2]
source = (root / "OpenSim/Services/InventoryService/XInventoryService.cs").read_text()

delete_folders = source.split(
    "public virtual bool DeleteFolders(UUID principalID, List<UUID> folderIDs, bool onlyIfTrash)", 1
)[1].split("public virtual bool PurgeFolder(InventoryFolderBase folder)", 1)[0]
assert "if (!PurgeFolder(folder, onlyIfTrash))" in delete_folders
assert "if (!DeleteFolderOrAlreadyAbsent(id))" in delete_folders
assert "return allDeleted;" in delete_folders

purge = source.split(
    "public virtual bool PurgeFolder(InventoryFolderBase folder, bool onlyIfTrash)", 1
)[1].split("public virtual bool AddItem(InventoryItemBase item)", 1)[0]
assert "if (!PurgeFolder(ConvertToOpenSim(child), onlyIfTrash) ||" in purge
assert "!DeleteFolderOrAlreadyAbsent(child.folderID)" in purge
assert 'm_Database.GetItems(' in purge, "Zero affected rows need a persisted-state check"
assert "remaining == null || remaining.Length != 0" in purge
assert "return remaining != null && remaining.Length == 0;" in purge

items = source.split(
    "public virtual bool DeleteItems(UUID principalID, List<UUID> itemIDs)", 1
)[1].split("public virtual InventoryItemBase GetItem(", 1)[0]
assert "bool allDeleted = true;" in items
assert "AssetType.Link" in items and "AssetType.LinkFolder" in items
assert 'm_Database.GetItems(' in items
assert "remaining == null || remaining.Length != 0" in items
assert "return allDeleted;" in items

tests = (root / "OpenSim/Services/InventoryService/Tests/XInventoryDeleteResultTests.cs").read_text()
assert "TestDeleteItemReportsFailureAndAllowsIdempotentDelete" in tests
assert "TestFailedFolderPurgeDoesNotDeleteFolder" in tests
assert "RefusingDeletes" in tests
print("PASS: inventory delete paths distinguish missing rows from failures")
