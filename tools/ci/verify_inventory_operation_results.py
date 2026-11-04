#!/usr/bin/env python3
"""Prevent false-positive inventory create/move success results."""
from pathlib import Path

root = Path(__file__).resolve().parents[2]
service = (root / "OpenSim/Services/InventoryService/XInventoryService.cs").read_text()
create = service.split("protected XInventoryFolder CreateFolder(", 1)[1].split(
    "protected virtual XInventoryFolder[] GetSystemFolders", 1
)[0]
assert "if (!m_Database.StoreFolder(newFolder))" in create
assert "throw new InvalidOperationException" in create

move = service.split("public virtual bool MoveItems(", 1)[1].split(
    "public virtual bool DeleteItems(", 1
)[0]
assert "bool allMoved = true" in move
assert "if (!m_Database.MoveItem(" in move
assert "allMoved = false" in move
assert "return allMoved;" in move
assert "return true;" not in move

tests = (root / "OpenSim/Services/InventoryService/Tests/XInventoryServiceTests.cs").read_text()
assert "TestMoveItemsPropagatesBackendFailure" in tests
assert "MoveItems(owner, new List<InventoryItemBase> { item }), Is.False" in tests
assert "MoveItems(owner, new List<InventoryItemBase> { item }), Is.True" in tests
print("PASS: inventory create/move error results are propagated")
