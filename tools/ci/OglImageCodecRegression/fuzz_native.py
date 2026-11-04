#!/usr/bin/env python3
"""Deterministic native OpenJPEG2 codec crash/metadata smoke tests.

Generated and mutated synthetic JPEG2000 data ONLY. This is not a security
fuzzer replacement; it catches regressions, crashes, and invalid native ABI
outputs under GitHub Actions on Linux and Windows.
"""
from __future__ import annotations

import ctypes
from pathlib import Path
import platform
import random
import sys


class NativeInfo(ctypes.Structure):
    _fields_ = [
        ("width", ctypes.c_int32),
        ("height", ctypes.c_int32),
        ("components", ctypes.c_int32),
        ("layers", ctypes.c_int32),
        ("progression", ctypes.c_int32),
    ]


def load_native(root: Path):
    machine = platform.machine().lower()
    if sys.platform == "win32" and machine in ("amd64", "x86_64"):
        filename = "ogl-openjpeg2-x86_64.dll"
    elif sys.platform.startswith("linux") and machine in ("x86_64", "amd64"):
        filename = "libogl-openjpeg2-x86_64.so"
    elif sys.platform.startswith("linux") and machine in ("aarch64", "arm64"):
        filename = "libogl-openjpeg2-arm64.so"
    else:
        raise RuntimeError(f"Unsupported native codec platform: {sys.platform}/{machine}")
    dll_path = root / "bin" / "lib64" / filename
    if not dll_path.is_file():
        raise FileNotFoundError(dll_path)
    native = ctypes.CDLL(str(dll_path))
    func = native.ogl_openjpeg2_inspect
    func.argtypes = [ctypes.POINTER(ctypes.c_ubyte), ctypes.c_int32, ctypes.POINTER(NativeInfo)]
    func.restype = ctypes.c_int
    return func


def inspect(func, data: bytes) -> bool:
    buf = (ctypes.c_ubyte * max(1, len(data)))()
    if data:
        ctypes.memmove(buf, data, len(data))
    info = NativeInfo(123, 456, 789, 123, 456)
    result = func(buf, len(data), ctypes.byref(info))
    assert result in (0, 1), f"Unexpected native ABI return code {result}"
    if result == 0:
        assert tuple(getattr(info, key) for key, _ in info._fields_) == (0, 0, 0, 0, 0), (
            "Failed decodes must zero all output fields"
        )
        return False
    assert 0 < info.width <= 8192
    assert 0 < info.height <= 8192
    assert info.width * info.height <= 32 * 1024 * 1024
    assert 1 <= info.components <= 5
    assert 1 <= info.layers <= 1024
    assert info.progression == 0
    return True


def main() -> None:
    if len(sys.argv) < 2:
        raise SystemExit("usage: fuzz_native.py fixture.j2k [fixture.jp2 ...]")
    root = Path(__file__).resolve().parents[3]
    func = load_native(root)
    rng = random.Random(0x0C0DEC0)
    total = 0
    for filename in sys.argv[1:]:
        original = Path(filename).read_bytes()
        if not (16 <= len(original) <= 1024 * 1024):
            raise ValueError("This bounded smoke requires a small synthetic fixture")
        assert inspect(func, original), "Known valid JPEG2000 fixture rejected"
        total += 1
        rejected = 0

        mutations = [
            b"", b"\x00", b"not jpeg2000", bytes(len(original)),
            original[:12], original[:16], original[:24], original[:32],
            original[:len(original) // 2], original[:-1],
            original + b"\x00" * 5,
        ]
        for _ in range(48):
            damaged = bytearray(original)
            for __ in range(rng.randrange(1, 5)):
                position = rng.randrange(len(damaged))
                damaged[position] ^= 1 << rng.randrange(8)
            mutations.append(bytes(damaged))
        for _ in range(24):
            mutations.append(rng.randbytes(rng.randrange(1, min(4096, len(original) + 1))))

        for sample in mutations:
            if not inspect(func, sample):
                rejected += 1
            total += 1
        print(f"Native OpenJPEG2 smoke {Path(filename).suffix}: {len(mutations)} mutations, "
              f"{rejected} rejected, all outputs bounded")

    print(f"OGL OpenJPEG2 deterministic native smoke PASS ({total} cases)")


if __name__ == "__main__":
    main()
