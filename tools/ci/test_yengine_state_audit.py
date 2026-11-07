#!/usr/bin/env python3
"""CI exercises a bounded, no-write YEngine state audit on synthetic files."""
import base64
import hashlib
import json
from pathlib import Path
import subprocess
import sys
import tempfile

TOOL = Path(__file__).with_name("audit_yengine_states.py")


def run(root, report, expected=0):
    p = subprocess.run([sys.executable, str(TOOL), "--directory", str(root),
        "--report", str(report)], text=True, capture_output=True, timeout=30)
    assert p.returncode == expected, (p.stdout, p.stderr)


with tempfile.TemporaryDirectory(prefix="ogl-yengine-audit-") as temp:
    root = Path(temp) / "script-data"
    root.mkdir()
    data = b"opaque-script-stream\x00\x15\x16"
    good = root / "good.state"
    good.write_text("<ScriptState><Snapshot>" +
        base64.b64encode(data).decode("ascii") +
        "</Snapshot></ScriptState>")
    malformed = root / "malformed.state"
    malformed.write_text("<ScriptState>unterminated")
    dtd = root / "entity.state"
    dtd.write_text("<!DOCTYPE a [<!ENTITY ext SYSTEM 'file:///etc/shadow'>]>" +
        "<ScriptState><Snapshot>&ext;</Snapshot></ScriptState>")
    missing = root / "missing.state"
    missing.write_text("<ScriptState><Other/></ScriptState>")
    large = root / "oversize.state"
    large.write_bytes(b"X" * (8 * 1024 * 1024 + 1))
    (root / "outside.state").symlink_to(good)
    before = {p.name: hashlib.sha256(p.read_bytes()).hexdigest()
              for p in [good, malformed, dtd, missing, large]}
    report = Path(temp) / "audit.json"
    run(root, report)
    rows = json.loads(report.read_text())
    assert rows["total"] == 6
    got = {x["path"]: x for x in rows["files"]}
    assert got["good.state"]["status"] == "inspect-required"
    assert got["good.state"]["snapshot_sha256"] == hashlib.sha256(data).hexdigest()
    assert got["malformed.state"]["status"] == "invalid-xml"
    assert got["entity.state"]["status"] == "blocked-dtd"
    assert got["missing.state"]["status"] == "missing-snapshot"
    assert got["oversize.state"]["status"] == "oversize"
    assert got["outside.state"]["status"] == "symlink-skipped"
    run(root, report, expected=2)  # report collision is rejected
    for p in [good, malformed, dtd, missing, large]:
        assert before[p.name] == hashlib.sha256(p.read_bytes()).hexdigest()
    assert report.exists()
print("PASS: YEngine audit validates bounds, DTD rejection, hashes, no overwrites "
      "or changes to original snapshots")
