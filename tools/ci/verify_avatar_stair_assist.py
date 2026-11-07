#!/usr/bin/env python3
"""OGL BulletSim avatar stair assist: geometry and safety invariants."""
from pathlib import Path

root = Path(__file__).resolve().parents[2]
move = (root / "OpenSim/Region/PhysicsModules/BulletS/BSActorAvatarMove.cs").read_text()
param = (root / "OpenSim/Region/PhysicsModules/BulletS/BSParam.cs").read_text()
ini = (root / "bin/OpenSimDefaults.ini").read_text()

assert "BSParam.OglAvatarStairAssistEnabled" in move
assert "? OglWalkUpStairs()" in move and ": WalkUpStairs();" in move
for needle in (
    "HorizontalStairDirection(m_velocityMotor.TargetValue)",
    "!m_controllingPrim.IsColliding", "m_controllingPrim.Flying",
    "m_controllingPrim.IsVolumeDetect", "stair.IsStatic",
    "!stair.IsSolid", "stair.IsVolumeDetect",
    "surface.Normal.Z", "surface.Point.Z",
    "CollisionType.Static", "maxStep, maxFrameRise, minNormalZ",
):
    assert needle in move, "Missing stair assist guard: " + needle

assist = move.split("private OMV.Vector3 OglWalkUpStairs()", 1)[1].split(
    "// Decide if the character is colliding with a low object", 1
)[0]
assert assist.count("RayTest2(") == 3, "Need high-obstacle, stair-top and headroom raycasts"
assert "if (highObstacle.hasHit())" in assist
assert "if (overhead.hasHit())" in assist
assert "Math.Clamp(BSParam.OglAvatarStairMaxHeight, 0f, 0.6f)" in assist
assert "Math.Clamp(BSParam.OglAvatarStairRisePerFrame, 0f, 0.15f)" in assist
assert "m_controllingPrim.ForcePosition = position + new OMV.Vector3(0f, 0f, rise);" in assist
assert "ForceVelocity" not in assist

for name in (
    "OglAvatarStairAssistEnabled", "OglAvatarStairMaxHeight",
    "OglAvatarStairProbeDistance", "OglAvatarStairRisePerFrame",
    "OglAvatarStairMinNormalZ",
):
    assert f'new ParameterDefn<' in param and f'("{name}",' in param
    assert name in ini, "Missing user-facing configuration: " + name

tests = (root / "tools/ci/AvatarStairAssistRegression/Program.cs").read_text()
for case in ("50 cm wall", "vertical wall face", "descending stairs",
             "strafing direction", "backwards movement", "non-finite input"):
    assert case in tests, "Missing regression: " + case
print("PASS: BulletSim avatar stair assist is bounded, direction-aware and configurable")
