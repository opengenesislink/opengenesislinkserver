#!/usr/bin/env python3
"""All shape-detail logs must avoid allocating argument arrays when disabled."""
from pathlib import Path

root = Path(__file__).resolve().parents[2]
code = (root / "OpenSim/Region/PhysicsModules/BulletS/BSShapeCollection.cs").read_text()

assert "m_physicsScene.PE.ResetBroadphasePool(m_physicsScene.World);" in code, (
    "Native Bullet broadphase behavior must remain unchanged in this PR"
)
assert code.count("if (DDetail && m_physicsScene.PhysicsLogging.Enabled) DetailLog(") == 11
assert "if (DDetail) DetailLog(" not in code
assert 'if (m_physicsScene.PhysicsLogging.Enabled)\n                DetailLog(' in code
assert "DDetail = true;" in code, "Do not disable previously available diagnostics"
assert "if (m_physicsScene.PhysicsLogging.Enabled)\n                m_physicsScene.DetailLog(msg, args);" in code
print("PASS: BulletSim shape diagnostics skip argument allocation when disabled")
