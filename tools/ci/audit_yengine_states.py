#!/usr/bin/env python3
"""Read-only inventory of YEngine .state snapshots; does NOT deserialize them.

This tool deliberately does not guess whether a BinaryFormatter graph is
present by scanning bytes for arbitrary tag values. Each opaque snapshot
requires a staged restore or a format-aware offline migration review.
"""
import argparse
import base64
import binascii
from collections import Counter
import hashlib
import json
import os
from pathlib import Path
import re
import sys
import xml.etree.ElementTree as ET

MAX_XML = 8 * 1024 * 1024
MAX_FILES = 100000
PROHIBITED_XML = re.compile(rb"<!\s*(?:DOCTYPE|ENTITY)\b", re.I)


def inspect(path: Path, root: Path) -> dict:
    row = {"path": str(path.relative_to(root)), "status": "inspect-required"}
    if path.is_symlink():
        row["status"] = "symlink-skipped"
        return row
    try:
        size = path.stat().st_size
        row["file_bytes"] = size
        if size > MAX_XML:
            row["status"] = "oversize"
            return row
        raw = path.read_bytes()
        row["file_sha256"] = hashlib.sha256(raw).hexdigest()
        if PROHIBITED_XML.search(raw):
            row["status"] = "blocked-dtd"
            return row
        document = ET.fromstring(raw)
        if document.tag != "ScriptState":
            row["status"] = "not-yengine-state"
            return row
        snapshot = document.find("Snapshot")
        if snapshot is None or snapshot.text is None:
            row["status"] = "missing-snapshot"
            return row
        encoded = "".join(snapshot.itertext())
        encoded = re.sub(r"\s+", "", encoded)
        if not encoded:
            row["status"] = "missing-snapshot"
            return row
        binary = base64.b64decode(encoded, validate=True)
        row["snapshot_bytes"] = len(binary)
        row["snapshot_sha256"] = hashlib.sha256(binary).hexdigest()
        row["format"] = "opaque-yengine-migration-stream"
        row["requires_staging_restore"] = True
        return row
    except (ET.ParseError, UnicodeError):
        row["status"] = "invalid-xml"
    except (OSError, binascii.Error, ValueError):
        row["status"] = "invalid-state-data"
    return row


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--directory", required=True, help="root of a copy of YEngine state files")
    parser.add_argument("--report", required=True, help="new JSON report path, must not exist")
    args = parser.parse_args()
    directory = Path(args.directory).resolve()
    report = Path(args.report).absolute()
    if not directory.is_dir() or report.exists() or report.suffix != ".json":
        parser.error("input must be a directory and report must be a new .json file")
    if report.is_symlink():
        parser.error("report path must not be a symbolic link")

    results = []
    for parent, dirs, names in os.walk(directory, followlinks=False):
        dirs[:] = [d for d in dirs if not (Path(parent) / d).is_symlink()]
        for name in sorted(names):
            if not name.endswith(".state"):
                continue
            if len(results) >= MAX_FILES:
                raise SystemExit("too many .state files; aborting")
            results.append(inspect(Path(parent) / name, directory))

    results.sort(key=lambda x: x["path"])
    stats = Counter(row["status"] for row in results)
    summary = {
        "total": len(results),
        "counts": dict(sorted(stats.items())),
        "warning": "Snapshot classification is NOT a legacy BinaryFormatter detector. "
                   "Never delete or rewrite originals. Inspect on an isolated staging restore.",
        "files": results,
    }
    # FileMode.CreateNew equivalent. Never overwrite a report or any .state.
    with report.open("x", encoding="utf-8") as output:
        json.dump(summary, output, ensure_ascii=False, indent=2)
        output.write("\n")
    print("Read-only YEngine .state audit:", len(results), "files;", dict(stats))
    print("Report:", report)
    return 0


if __name__ == "__main__":
    sys.exit(main())
