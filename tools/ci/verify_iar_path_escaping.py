#!/usr/bin/env python3
"""Regression contract for native writer names vs bounded OGL TAR preflight."""
from pathlib import Path

root = Path(__file__).resolve().parents[2]
util = (root / "OpenSim/Region/CoreModules/Avatar/Inventory/Archiver/InventoryArchiveUtils.cs").read_text()
writer = (root / "OpenSim/Region/CoreModules/Avatar/Inventory/Archiver/InventoryArchiveWriteRequest.cs").read_text()
inspector = (root / "NexVerse/RegionModules/Archives/OglIarManagement.cs").read_text()
test = (root / "tools/ci/OglIarPreflightRegression/Program.cs").read_text()
reader_test = (root / "OpenSim/Region/CoreModules/Avatar/Inventory/Archiver/Tests/InventoryArchiveLoadPathTests.cs").read_text()
encode = util.split("public static string EscapeArchivePath(string path)", 1)[1].split("public static string UnescapeArchivePath", 1)[0]
decode = util.split("public static string UnescapeArchivePath(string path)", 1)[1].split("\n        }", 1)[0]
for token in ('"&amp;"', '"&#47;"', '"&#92;"', '"&#58;"'):
    assert token in encode, f"missing archive path entity encoder {token}"
    assert token in decode, f"missing archive path entity decoder {token}"
assert encode.index('"&amp;"') < encode.index('"&#47;"') < encode.index('"&#92;"') < encode.index('"&#58;"'), "ampersand must be encoded first"
assert decode.index('"&#47;"') < decode.index('"&#92;"') < decode.index('"&#58;"') < decode.index('"&amp;"'), "ampersand must be decoded last"
assert "InventoryArchiveUtils.EscapeArchivePath(name)" in writer
assert "InventoryArchiveUtils.UnescapeArchivePath(newFolderName)" in (root / "OpenSim/Region/CoreModules/Avatar/Inventory/Archiver/InventoryArchiveReadRequest.cs").read_text()
assert "MaxIarPathChars = 4096" in inspector
assert "MaxGnuLongNameBytes = MaxIarPathChars" in inspector
for label in ("Doppelpunkt im TAR-Pfad", "Backslash im TAR-Pfad", "IAR-Pfad enthaelt Traversal-Segmente"):
    assert label in inspector
assert "TestArchivePathEscapingForWindowsUnsafeInventoryNames" in reader_test
assert "archive-encoded colon, backslash, slash and ampersand" in test
assert "long nested inventory paths" in test
assert "raw colon remains unsafe" in test
assert "raw backslash remains unsafe" in test
print("OGL IAR safe inventory-name writer and strict bounded TAR preflight: OK")
