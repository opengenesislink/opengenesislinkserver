#!/usr/bin/env python3
"""Read-only Warp3D lifecycle and operator opt-in performance contract."""
from pathlib import Path

root = Path(__file__).resolve().parents[2]
source = (root / "OpenSim/Region/CoreModules/World/Warp3DMap/Warp3DImageModule.cs").read_text()
for item in (
    "lock (m_RenderLock)",
    "private readonly object m_RenderLock",
    "if (renderer.Scene != null)",
    "renderer.Scene.destroy();",
    "renderer.Reset();",
    "if (m_ForceFullGC)",
    "if (m_WriteMapDiagnosticPng)",
    "tile.Dispose();",
    'Util.GetConfigVarFromSections<bool>(source, "Warp3DForceFullGC"',
    'Util.GetConfigVarFromSections<bool>(source, "Warp3DWriteMapPng"',
):
    assert item in source, item
assert source.count("lock (m_RenderLock)") == 2
for path in ("bin/OpenSimDefaults.ini", "bin/OpenSim.ini.example"):
    config = (root / path).read_text()
    assert "Warp3DWriteMapPng = true" in config, path
    assert "Warp3DForceFullGC = true" in config, path
print("Warp3D legacy defaults and exception-safe resource lifecycle: OK")
