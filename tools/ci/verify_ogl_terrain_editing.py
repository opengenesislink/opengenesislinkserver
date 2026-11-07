#!/usr/bin/env python3
"""Source regression guards for G1 viewer terrain paint responsiveness."""
from pathlib import Path

terrain = Path("OpenSim/Region/CoreModules/World/Terrain/TerrainModule.cs").read_text(encoding="utf-8")
defaults = Path("bin/OpenSimDefaults.ini").read_text(encoding="utf-8")

for required in (
    "lock (m_terrainEditLock)",
    "m_nextTerrainEditByResident.TryGetValue(user",
    "m_nextTerrainEditByResident.Remove(client)",
    "m_nextTerrainEditByResident[user] = now + 0.02",
    "OglTerrainBrushStrengthMultiplier",
    "float.IsFinite(brushSize)",
    "float.IsFinite(height)",
    "Math.Min(brushSize, 64.0f)",
    "m_scene.Permissions.CanTerraformLand(user",
    "EnforceEstateLimits(startX, endX, startY, endY)",
    "m_channel, allowMask, west, south, height, brushSize, effectiveStrength,",
):
    assert required in terrain, f"Missing terrain safety/responsiveness guard: {required}"

assert "NextModifyTerrainTime" not in terrain, "Region-global paint throttle must not return"
assert "UpdateTerrainEveryNFrames = 15" in defaults
assert "OglTerrainBrushStrengthMultiplier = 2.0" in defaults
print("OGL G1 viewer terrain editor source guards: OK")
