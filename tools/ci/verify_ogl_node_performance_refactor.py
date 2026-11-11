#!/usr/bin/env python3
"""NodeAgent performance refactor must not change signed heartbeat surface."""
from pathlib import Path

root = Path(__file__).resolve().parents[2]
node = (root / "NexVerse/RegionModules/NodeAgent/NexVerseNodeAgentModule.cs").read_text()
split = (root / "NexVerse/RegionModules/NodeAgent/OglNodePerformanceHeartbeat.cs").read_text()
assert "OglNodePerformanceHeartbeat.Append(scenes, m_PerformanceSampler, data)" in node
assert 'Publish(new NexEvent(\n                    "node.heartbeat"' in node
assert "m_PerformanceSampler.Capture(frames)" not in node
for term in ("OglRegionTimingSample?", "TakeSnapshotAndReset()", "sampler.Capture(frames)",
             "IOglAssetCacheMetricsProvider", "CultureInfo.InvariantCulture",
             "RequestModuleInterface<IAssetCache>()"):
    assert term in split, term
print("OGL NodeAgent metrics refactor signature and collector wiring: OK")
