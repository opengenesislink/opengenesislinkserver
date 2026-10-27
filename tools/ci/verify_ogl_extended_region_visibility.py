#!/usr/bin/env python3
"""0.9.3.13: regression guard for opt-in multi-region Firestorm visibility.

The geometric checks model the existing EntityTransfer RegionsInSPView
bounding-box selection and prove that no 3x3/9-region restriction was added.
They are not a replacement for the required two-simulator Firestorm E2E test.
"""
import math
from pathlib import Path

root=Path(__file__).resolve().parents[2]
scene=(root/"OpenSim/Region/Framework/Scenes/Scene.cs").read_text()
transfer=(root/"OpenSim/Region/CoreModules/Framework/EntityTransfer/EntityTransferModule.cs").read_text()
defaults=(root/"bin/OpenSimDefaults.ini").read_text()
sample=(root/"bin/OpenSim.ini.example").read_text()
active=(root/"bin/OpenSim.ini").read_text()
acceptance=(root/"doc/NexVerse/09313_RELEASE_ACCEPTANCE.md").read_text()
roadmap=(root/"doc/NexVerse/ROADMAP.md").read_text()

required_scene=[
    'startupConfig.GetBoolean("OGLExtendedRegionVisibility", false)',
    'oglExtendedRegionVisibility ? 1024f : m_maxDrawDistance',
    'oglExtendedRegionVisibility ? 768f : m_maxRegionViewDistance',
    'startupConfig.GetFloat("MaxDrawDistance",',
    'startupConfig.GetFloat("MaxRegionsViewDistance",',
    'if (m_maxRegionViewDistance > m_maxDrawDistance)',
    'MaxRegionViewDistance',
]
for key in required_scene:
    assert key in scene, "Missing region-visibility opt-in: "+key

assert "protected float m_maxRegionViewDistance = 255f" in scene
assert "protected float m_maxDrawDistance = 512.0f" in scene
assert "OGLExtendedRegionVisibility = false" in sample
# Operator deployment profile explicitly enables opt-in; generic templates stay off.
assert "OGLExtendedRegionVisibility = true" in active
assert "OGLExtendedRegionVisibility = true" not in defaults

for key in [
    'long border = (long)Math.Ceiling(maxRange) - 1L;',
    'Math.Max(0L, (long)regionInfo.WorldLocX - border)',
    'Math.Max(0L, (long)regionInfo.WorldLocY - border)',
    'Math.Min(int.MaxValue, (long)regionInfo.WorldLocX + regionInfo.RegionSizeX + border)',
    'Math.Min(int.MaxValue, (long)regionInfo.WorldLocY + regionInfo.RegionSizeY + border)',
    'GridService.GetRegionRange(',
    'List<GridRegion> RegionsInSPView(ScenePresence sp)',
    'sp.RegionViewDistance',
    'RegionFlags.RegionOnline',
]:
    assert key in transfer, "Missing multi-region view pipeline guard: "+key

# Bounding-box visibility can discover many more than the nearest 8:
# avatar at center of a 256m region, 512m draw -> 576m view,
# and 768m neighbour query envelope.
def visible_regions(draw, server_region_max):
    view=min(draw+64,server_region_max)
    viewer_x=viewer_y=128
    count=0
    for x in range(-3,4):
        for y in range(-3,4):
            if x == 0 and y == 0:
                continue
            left, bottom=x*256,y*256
            if (viewer_x+view >= left and viewer_x-view <= left+256
                and viewer_y+view >= bottom and viewer_y-view <= bottom+256):
                count+=1
    return count

assert visible_regions(512,255)==8, "Legacy 3x3 cap unexpectedly changed"
assert visible_regions(512,768)>8, "Extended neighbour range must exceed 3x3"
assert visible_regions(512,768)<=48, "Fixture constructed incorrectly"
assert visible_regions(128,768)==8, "Viewer draw distance must still constrain visibility"

# The old unsigned subtraction wrapped when the region lay near world origin.
def new_bounds(base, size, view):
    border=math.ceil(view)-1
    return max(0,base-border), min(2_147_483_647,base+size+border)
assert new_bounds(256,256,768)==(0,1279)
assert new_bounds(0,256,768)==(0,1023)
assert new_bounds(1_000_000,256,768)==(999233,1001023)
assert "17.3 Extended Region Visibility" in roadmap
for key in ("V01", "V02", "V03", "V04"):
    assert key in acceptance, "Missing mandatory release gate: "+key
print("OGL extended view: operator profile opt-in, generic defaults unchanged, safe world bounds, release gates OK")
