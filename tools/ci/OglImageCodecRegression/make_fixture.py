#!/usr/bin/env python3
"""Generate a deterministic 64x64 PGM input for an OpenJPEG J2C/JP2 smoke test.

This file contains no private assets and is not a substitute for a real-world
texture corpus or viewer quality-layer parity checks.
"""
import sys
from pathlib import Path

if len(sys.argv) != 2:
    raise SystemExit("usage: make_fixture.py output.pgm")

path = Path(sys.argv[1])
path.parent.mkdir(parents=True, exist_ok=True)
data = bytes((4 * x + 3 * y) % 256 for y in range(64) for x in range(64))
path.write_bytes(b"P5\n64 64\n255\n" + data)
print(f"Generated deterministic PGM fixture: {path}")
