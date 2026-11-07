#!/usr/bin/env python3
"""CI: verify read-only conversion of old NRBF blobs in batch TSV format."""
import hashlib
from pathlib import Path
import subprocess
import sys
import tempfile
import uuid

ROOT = Path(__file__).resolve().parents[2]
PROJECT = ROOT / "tools/migration/KeyframeLegacyConverter/KeyframeLegacyConverter.csproj"
original = Path(sys.argv[1]).read_bytes()
obj1, obj2 = uuid.uuid4(), uuid.uuid4()


def run_batch(source, target, success):
    command = ["dotnet", "run", "--no-build", "--project", str(PROJECT),
               "-c", "Release", "--", "--batch-hex", str(source), str(target)]
    result = subprocess.run(command, capture_output=True, text=True, timeout=90)
    assert (result.returncode == 0) == success, (
        f"unexpected batch exit {result.returncode}: {result.stdout}\n{result.stderr}")
    return result


with tempfile.TemporaryDirectory(prefix="ogl-keyframe-batch-") as folder:
    root = Path(folder)
    source, target = root / "legacy.tsv", root / "converted.tsv"
    source.write_text(f"{obj1}\t{original.hex()}\n{obj2}\t{original.hex()}\n")
    run_batch(source, target, True)
    rows = [r.split("\t") for r in target.read_text().splitlines()]
    assert len(rows) == 2 and all(len(r) == 3 for r in rows)
    assert {r[0] for r in rows} == {str(obj1), str(obj2)}
    for obj, hash_value, hex_record in rows:
        assert hash_value == hashlib.sha256(original).hexdigest().upper()
        assert bytes.fromhex(hex_record).startswith(b"OGKF\x01")

    original_target = target.read_bytes()
    run_batch(source, target, False)
    assert target.read_bytes() == original_target, "existing output was modified"

    target.unlink()
    source.write_text(f"{obj1}\t{original.hex()}\n{obj2}\tDEADBEEF\n")
    run_batch(source, target, False)
    assert not target.exists(), "failed batch left an output file"
    assert not list(root.glob("*.tmp")), "failed batch left temporary files"

    source.write_text(f"{obj1}\t{original.hex()}\n{obj1}\t{original.hex()}\n")
    run_batch(source, target, False)
    assert not target.exists(), "duplicate UUID batch left an output"

    source.write_text("")
    run_batch(source, target, False)
    assert not target.exists(), "empty batch left an output"
print("PASS: two NRBF records convert with SHA-256, existing output unchanged,"
      " invalid/duplicate/empty batches have no output")
