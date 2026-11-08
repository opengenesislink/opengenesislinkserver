#!/usr/bin/env python3
"""Guard linked object land impact wiring without changing LSL link part counts."""
from pathlib import Path

root = Path(__file__).resolve().parents[2]
def read(p): return (root / p).read_text(encoding="utf-8")

core = read("OpenSim/Region/CoreModules/World/Land/PrimCountModule.cs")
engine = read("OpenSim/Region/CoreModules/World/Land/OglMeshLandImpactCalculator.cs")
math = read("OpenSim/Region/CoreModules/World/Land/OglMeshLandImpactMath.cs")
interface = read("OpenSim/Region/Framework/Interfaces/IPrimCountModule.cs")
group = read("OpenSim/Region/Framework/Scenes/SceneObjectGroup.cs")
land = read("OpenSim/Region/CoreModules/World/Land/LandObject.cs")
limits = read("OpenSim/Region/OptionalModules/PrimLimitsModule/PrimLimitsModule.cs")
coreproj = read("OpenSim/Region/CoreModules/OpenSim.Region.CoreModules.csproj")

assert 'GetBoolean("Enabled", false)' in core
assert "int partCount = GetObjectLandImpact(obj);" in core
assert "GetObjectLandImpact(SceneObjectGroup group)" in core
assert "GetObjectLandImpactWeights" in core and "GetObjectLandImpactWeights" in interface
assert "MeshLandImpactEnabled" in core and "MeshLandImpactEnabled" in interface
assert "OglMeshLandImpactCalculator.cs" in coreproj
assert "OglMeshLandImpactMath.cs" in coreproj
assert "MaxCachedMeshAssets = 8192" in engine
assert "MaxMeshBytesToInspect = 64 * 1024 * 1024" in engine
assert "physics_convex" in engine and "BoundingVerts" in engine
assert "streaming += 1.0" in engine and "physics += 1.0" in engine
assert "OglMeshLandImpactMath.StreamingWeight" in engine
assert "OglMeshLandImpactMath.ServerWeight" in engine
assert "Math.Ceiling(highest)" in math
assert "GetObjectLandImpact(obj)" in land
assert "GetLandImpact(sog)" in limits
assert "m_scene?.RequestModuleInterface<IPrimCountModule>()" in group
assert "TriggerParcelPrimCountTainted" in group
assert "GetObjectLandImpact(group)" in read("OpenSim/Region/Framework/Scenes/Scene.Inventory.cs")
assert "GetObjectLandImpact(g)" in read("OpenSim/Region/CoreModules/Framework/InventoryAccess/InventoryAccessModule.cs")
assert "GetObjectLandImpact(grp)" in read("OpenSim/Region/ClientStack/Linden/Caps/ObjectCaps/UploadObjectAssetModule.cs")
assert "GetObjectLandImpact(group)" in read("OpenSim/Region/OptionalModules/Scripting/JsonStore/JsonStoreScriptModule.cs")

for file, enabled in [
    ("bin/OpenSimDefaults.ini", "false"),
    ("bin/OpenSim.ini.example", "false"),
    ("bin/OpenSim.ini", "true"),
]:
    cfg = read(file)
    section = cfg.split("[OGLMeshLandImpact]", 1)[1].split("[", 1)[0]
    assert f"Enabled = {enabled}" in section, f"{file}: unexpected mesh LI switch"

assert "get { return m_parts.Count; }" in group, "LSL/linkset part counting changed"
assert "GetPartCount()" in group, "Actual edit/physics linkset part count changed"
print("OpenGenesisLINK G1 Mesh Land Impact core configuration and integration: PASS")
