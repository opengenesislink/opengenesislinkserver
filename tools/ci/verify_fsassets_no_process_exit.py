#!/usr/bin/env python3
"""Prevent single failed FSAssets reads from terminating ROBUST."""
from pathlib import Path

root = Path(__file__).resolve().parents[2]
source = (root / "OpenSim/Services/FSAssetService/FSAssetService.cs").read_text()
getter = source.split("private AssetBase Get(string id, out string sha)", 1)[1].split(
    "public virtual AssetMetadata GetMetadata(string id)", 1
)[0]
assert "Environment.Exit(" not in getter, "FSAssets must never terminate ROBUST on a read error"
assert "Thread.Sleep(5000)" not in getter, "Asset reads must not block for five seconds on failure"
failure = getter.split("catch (Exception exception)", 1)[1]
assert '"[FSASSETS]: Failed to retrieve asset "' in failure
assert "return null;" in failure, "Unexpected read errors must be surfaced as missing assets"
print("PASS: FSAssets read exceptions cannot deliberately terminate ROBUST")
