#!/usr/bin/env python3
"""Isolated ROBUST HTTP tests against an explicit S3 + disposable SQL connector.

This imports the existing OGL FSAssets HTTP fixture instead of duplicating
its wire protocol. The caller must supply --phase write or read; the test
targets 127.0.0.1:19496 only. Never point it at production systems.
"""
import argparse
import base64
import hashlib
import json
import uuid
from pathlib import Path

import verify_ogl_assetservice_http_persistence as baseline

baseline.BASE_URL = "http://127.0.0.1:19496"


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("phase", choices=("write", "read"))
    parser.add_argument("state", type=Path)
    parser.add_argument("fs_dir", type=Path)
    args = parser.parse_args()

    if args.phase == "write":
        fixtures = [
            ("Texture", 0, b"\x00\x01\xfe\xff" * 8192),
            ("Mesh", 49, bytes(range(256)) * 192),
            ("Sound", 1, b"OGL-S3-WAVE" + bytes(range(128)) * 32),
            ("Animation", 20, bytes(range(64)) * 1024),
        ]
        entries = []
        for index, (name, asset_type, payload) in enumerate(fixtures):
            asset_id = str(uuid.uuid4())
            name = f"OGL S3 CI {name} {index}"
            reply = baseline.request(
                "POST", "/assets",
                baseline.make_asset(asset_id, name, asset_type, payload),
            )
            assert baseline.ET.fromstring(reply).text.strip() == asset_id, reply
            entry = {
                "id": asset_id,
                "name": name,
                "type": asset_type,
                "payload": base64.b64encode(payload).decode("ascii"),
            }
            baseline.assert_asset(entry)  # GET, GET /data, GET /metadata
            metadata = baseline.request("GET", f"/assets/{asset_id}/metadata")
            assert baseline.field(metadata, "Description") == "OGL isolated persistence smoke"
            entries.append(entry)
        args.state.write_text(json.dumps(entries), encoding="utf-8")
        baseline.assert_missing()
    else:
        entries = json.loads(args.state.read_text(encoding="utf-8"))
        assert len(entries) == 4, "Missing source fixtures after ROBUST restart"
        for entry in entries:
            baseline.assert_asset(entry)
        baseline.assert_missing()

    # Explicit S3 must not write blobs to the legacy FSAssets base.
    assert not list(args.fs_dir.rglob("*.gz")), "S3 mode wrote a legacy FSAssets blob"
    assert not list(args.fs_dir.rglob("*.asset")), "S3 mode wrote a legacy FSAssets spool blob"
    print(f"OGL S3 real ROBUST HTTP {args.phase}: UUID, metadata, binary payload, no FS fallback PASS")


if __name__ == "__main__":
    main()
