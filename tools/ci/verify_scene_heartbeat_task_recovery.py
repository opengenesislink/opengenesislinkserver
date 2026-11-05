#!/usr/bin/env python3
"""Scene heartbeat background work must be retried after worker failures."""
from pathlib import Path

root = Path(__file__).resolve().parents[2]
scene = (root / "OpenSim/Region/Framework/Scenes/Scene.cs").read_text()
assert "private volatile bool m_cleaningTemps" in scene
assert "private volatile bool m_sendingCoarseLocations" in scene

def check_guard(guard, action, end):
    block = scene.split(guard + " = true;", 1)[1].split(end, 1)[0]
    assert "WorkManager.RunInThreadPool(" in block
    assert action in block
    assert "catch (Exception e)" in block
    assert "finally" in block
    assert guard + " = false;" in block.split("finally", 1)[1]
    assert block.count(guard + " = false;") == 2, (
        "Worker and enqueue-failure paths must each clear " + guard
    )

check_guard("m_sendingCoarseLocations", "SceneGraph.GetCoarseLocations(", "// Get the simulation frame time")
check_guard("m_cleaningTemps", "CleanTempObjects();", "nowMS = Util.GetTimeStampMS();")
print("PASS: scene heartbeat worker guards reset after exceptions and enqueue failures")
