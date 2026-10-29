#!/usr/bin/env python3
"""OGLEnergy Generation 1: fail-closed addon, runtime, build and LSL contracts."""
from pathlib import Path
import re

root = Path(__file__).resolve().parents[2]
def read(p):
    return (root / p).read_text(encoding="utf-8-sig")

model = read("addons/OGLEnergy/OglEnergyModel.cs")
module = read("addons/OGLEnergy/OglEnergyRegionModule.cs")
project = read("addons/OGLEnergy/OpenGenesisLINK.Addons.OGLEnergy.csproj")
config = read("addons/OGLEnergy/OGLEnergy.ini.example")
board = read("addons/OGLEnergy/OGLEnergyGridDisplay.lsl")
lamp = read("addons/OGLEnergy/OGLEnergyDemoLamp.lsl")
suite = read("tools/ci/OglEnergyRegression/Program.cs")
workflow = read(".github/workflows/nexjast-ci.yml")

for marker in (
    'Id = "OGLEnergy"', "ISharedRegionModule",
    'Configs["OGLEnergy"]', '"Enabled", false',
    'ConcurrentDictionary<UUID, RegionRuntime>',
    "EventManager.OnFrame",
    "m_Regions.TryAdd(", "m_Regions.TryRemove(",
    "state.Scene.EventManager.OnFrame -= state.OnFrame",
    "m_Regions.Clear();",
    "IWorldComm", "ChatTypeEnum.Region",
    '"OGLEnergy", UUID.Zero',
    '"LSLChannel", -835431', '"RefreshSeconds", 60',
    'Math.Clamp(', '"TimeZone", "Europe/Berlin"',
    "OglEnergyModel.At(", "OglEnergyModel.Wire(",
    'MainConsole.Instance?.Commands.AddCommand(',
    '"ogl energy"', "HandleStatus"
):
    assert marker in module, marker

assert "HttpClient" not in module
assert not any(keyword in module for keyword in (
    "AddNewPrim(", "AddNewSceneObject(", "DeleteSceneObject(",
    "GiveMoney(", "SetEstate", "Teleport(", "ChangeParcel("
)), "The add-on must not mutate citizen objects"

for marker in (
    "public sealed class OglEnergySettings",
    "public static OglEnergyReading At(",
    "public static string Wire(",
    "TimeZoneInfo.ConvertTime(utc, zone)",
    'return "OGLENERGY|1|"',
    'supply >= demand ? "NORMAL"',
    'supply * 4 >= demand * 3 ? "WARN" : "OUTAGE"',
    "SupplyKw - DemandKw",
    "SolarCapacityKw * daylight",
    "BaseDemandKw * demandFactor",
):
    assert marker in model, marker

assert re.search(r"(?m)^Enabled\s*=\s*false\s*$", config)
assert "LSLChannel = -835431" in config
assert 'integer CHANNEL = -835431;' in board
assert 'integer CHANNEL = -835431;' in lamp
assert "llListen(CHANNEL, \"OGLEnergy\", NULL_KEY, \"\")" in board
assert "llListen(CHANNEL, \"OGLEnergy\", NULL_KEY, \"\")" in lamp
assert "llGetListLength(f) != 12" in board
assert "llGetListLength(f) != 12" in lamp
assert 'llList2String(f, 0) != "OGLENERGY"' in board
assert 'id != NULL_KEY' in board and 'id != NULL_KEY' in lamp
assert "gUpdated = llGetUnixTime();" in board
assert "osSetDynamicTextureDataFace(" in board
assert "if (signature == gRendered) return;" in board
assert "PRIM_POINT_LIGHT" in lamp
assert "setPowered(FALSE);" in lamp
assert 'status != "OUTAGE"' in lamp

assert "net8.0" in project
assert "<ProjectGuid>" in project
assert 'AfterTargets="Build"' in project
assert 'DestinationFiles="$(MSBuildThisFileDirectory)../../bin/$(TargetFileName)"' in project
assert "OglEnergyModel.cs" in read("tools/ci/OglEnergyRegression/OglEnergyRegression.csproj")
assert "DST" in suite and "full-year" in suite
assert "Verify OGLEnergy simulator addon contracts and deterministic grid regression" in workflow
assert "bin/OpenGenesisLINK.Addons.OGLEnergy.dll" in workflow

solution = read("OpenSim.sln").replace(chr(92), "/")
prebuild = read("runprebuild.sh")
compile_script = read("compile.sh")
project_path = "addons/OGLEnergy/OpenGenesisLINK.Addons.OGLEnergy.csproj"
assert solution.count(project_path) == 1, "Checked-in solution missing energy"
assert project_path in prebuild, "Prebuild loses energy"
assert "OGLEnergy" in compile_script, "Release script missing energy"
ini = read("bin/OpenSim.ini")
assert len(re.findall(r"(?m)^\[OGLEnergy\]\s*$", ini)) == 1
start = ini.index("[OGLEnergy]")
end = ini.find("\n[", start + 1)
block = ini[start:end if end != -1 else None]
assert re.search(r"(?m)^\s*Enabled\s*=\s*false\s*$", block)
assert "LSLChannel = -835431" in block
assert "[OGLEnergy]" not in read("bin/Robust.HG.ini"), "Energy is a simulator module"
print("OGLEnergy static contract: isolated, safe default, no automatic object changes, DLL bundled PASS")
