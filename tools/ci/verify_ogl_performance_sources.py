#!/usr/bin/env python3
"""Check the directly buildable G1 performance source graph before prebuild."""
from pathlib import Path

root = Path(__file__).resolve().parents[2]
framework = (root / "OpenSim/Framework/OpenSim.Framework.csproj").read_text(
    encoding="utf-8-sig"
)

required = (
    "OglBudgetedLruCache.cs",
    "OglInFlightByteLimiter.cs",
    "OglPerformanceWorkerPolicy.cs",
    "OglRegionTimingWindow.cs",
    "OglAdaptiveFileWriteGate.cs",
    "OglRegionPressureTracker.cs",
)

for name in required:
    assert (root / "OpenSim/Framework" / name).is_file(), name
    marker = f'<Compile Include="{name}" />'
    assert framework.count(marker) == 1, (
        f"Checked-in framework project must contain exactly one {marker}"
    )

scene = (root / "OpenSim/Region/Framework/Scenes/Scene.cs").read_text(
    encoding="utf-8-sig"
)
cache = (root / "OpenSim/Region/CoreModules/Asset/FlotsamAssetCache.cs").read_text(
    encoding="utf-8-sig"
)
node = (root / "NexVerse/RegionModules/NodeAgent/NexVerseNodeAgentModule.cs").read_text(
    encoding="utf-8-sig"
)

assert "OglPerformanceTiming?.Record(" in scene
assert "m_RegionPressureTracker.Sample(regionCounters)" in cache
assert "m_AdaptiveFileWriteGate.UpdatePressure(cpuPercent, regionOverrunPercent)" in cache
collector = (root / "NexVerse/RegionModules/NodeAgent/OglNodePerformanceHeartbeat.cs").read_text(
    encoding="utf-8-sig"
)
assert "scene.OglPerformanceTiming?.TakeSnapshotAndReset()" in collector
assert "OglNodePerformanceHeartbeat.Append(scenes, m_PerformanceSampler, data)" in node

print("OGL G1 performance direct-build graph, region pressure and telemetry: OK")
