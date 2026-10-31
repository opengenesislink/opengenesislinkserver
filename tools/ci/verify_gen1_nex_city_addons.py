#!/usr/bin/env python3
"""Guard standalone city addons, non-destructive defaults and inworld API."""
from pathlib import Path
import re
root = Path(__file__).resolve().parents[2]
def content(path):
    return (root / path).read_text(encoding="utf-8-sig")
names = ("NexTraffic", "NexTransit", "NexSmartLight")
channels = (-835421, -835422, -835423)
for name, channel in zip(names, channels):
    base = f"addons/{name}"
    mod = content(f"{base}/{name}RegionModule.cs")
    model = content(f"{base}/{name}Model.cs")
    script = content({
        "NexTraffic": "addons/NexTraffic/NexTrafficSignal.lsl",
        "NexTransit": "addons/NexTransit/NexTransitDepartureBoard.lsl",
        "NexSmartLight": "addons/NexSmartLight/NexSmartLightLamp.lsl",
    }[name])
    cfg = content(f"{base}/{name}.ini.example")
    csproj = content(f"{base}/OpenGenesisLINK.Addons.{name}.csproj")
    wrapper = content(f"addon-modules/{name}/build.sh")
    assert f'Id = "{name}"' in mod
    assert f'Configs["{name}"]' in mod
    assert '"Enabled", false' in mod
    assert "ISharedRegionModule" in mod
    assert "ConcurrentDictionary<UUID, RegionRuntime>" in mod
    assert "IWorldComm" in mod and "ChatTypeEnum.Region" in mod
    assert f'"{name}", UUID.Zero' in mod
    assert "EventManager.OnFrame -= runtime.OnFrame" in mod
    assert "m_Regions.Clear();" in mod
    assert f"Enabled = false" in cfg
    assert f"LSLChannel = {channel}" in cfg
    assert f"integer CHANNEL = {channel};" in script
    assert f"OpenGenesisLINK.Addons.{name}" in csproj
    assert "net8.0" in csproj
    assert '<Target Name="CopyAddonToSimulatorBin" AfterTargets="Build"' in csproj
    assert 'DestinationFiles="$(MSBuildThisFileDirectory)../../bin/$(TargetFileName)"' in csproj
    assert "<ProjectGuid>" in csproj
    assert f"build-nex-city-addon.sh\" {name}" in wrapper, name
    assert not any(x in mod for x in (
        "AddNewPrim(", "AddNewSceneObject(", "DeleteSceneObject(",
        "DeleteAllSceneObjects(", "Teleport(", "GiveMoney(",
    )), name
    assert model and script
traffic = content("addons/NexTraffic/NexTrafficModel.cs")
assert 'AxisA = "RED", AxisB = "RED"' in traffic
assert '"NEXTRAFFIC|1|"' in traffic
transit = content("addons/NexTransit/NexTransitModel.cs")
assert 'regionName, r.StopRegion' in transit
assert '"NEXTRANSIT|1|"' in transit
assert "Math.Clamp(limit, 1, 10)" in transit
light = content("addons/NexSmartLight/NexSmartLightModel.cs")
assert "fromMinute > untilMinute" not in light
assert "now >= fromMinute || now < untilMinute" in light
assert '"NEXLIGHT|1|"' in light
assert "PRIM_POINT_LIGHT" in content("addons/NexSmartLight/NexSmartLightLamp.lsl")
assert "osSetDynamicTextureDataFace(" in content("addons/NexTransit/NexTransitDepartureBoard.lsl")
workflow = content(".github/workflows/nexjast-ci.yml")
assert "Regression-test Nex city modules and build independent addons" in workflow
builder = content("addon-modules/build-nex-city-addon.sh")
assert 'NexTraffic|NexTransit|NexSmartLight' in builder
assert 'test -f "$TARGET/OpenSim.Region.Framework.dll"' not in builder or "robust" in builder.lower()
assert 'dotnet build "addons/$NAME/OpenGenesisLINK.Addons.$NAME.csproj"' in builder
assert "runprebuild.sh" in builder
assert 'ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"' in builder
# A plain checkout build and a regenerated solution must both compile
# all four add-ons. CI also checks the actual bin output post-Release.
solution = content("OpenSim.sln").replace(chr(92), "/")
prebuild = content("runprebuild.sh")
compile_script = content("compile.sh")
for name in (*names, "OGLWeather"):
    target = f"OpenGenesisLINK.Addons.{name}.dll"
    project = f"addons/{name}/OpenGenesisLINK.Addons.{name}.csproj"
    assert solution.count(project) == 1, f"Missing from checked-in solution: {name}"
    assert project in prebuild, f"Missing from Prebuild regeneration: {name}"
    assert target not in solution, f"Solution should include project, not DLL: {name}"
    assert target not in compile_script or "bin/" in compile_script
    assert f'bin/OpenGenesisLINK.Addons.${{addon}}.dll' in compile_script
    assert f'<ProjectGuid>' in content(project)
weather_project = content("addons/OGLWeather/OpenGenesisLINK.Addons.OGLWeather.csproj")
assert 'CopyAddonToSimulatorBin' in weather_project
assert 'DestinationFiles="$(MSBuildThisFileDirectory)../../bin/$(TargetFileName)"' in weather_project
assert "bash addon-modules/OGLWeather/build.sh Release" not in prebuild, (
    "Weather is now in OpenSim.sln; do not double-build separately"
)
assert 'dotnet build -c Release OpenSim.sln' in compile_script
print("NexCity + OGLWeather solution bundling: all 4 DLLs checked")

assert "Verify Generation-1 Inworld LSL companion scripts" in workflow
print("NexCity addon contracts PASS: independent plugins, bounded wire, safe disabled defaults")


# Stadt-NexVerse active deployment config is intentionally enabled, while
# reusable addon examples stay disabled for independent installations.
# Catch duplicate INI sections, wrong host placement and protocol drift.
def active_ini_section(path, section):
    raw = content(path)
    header = re.compile(r"(?m)^[ \t]*\[" + re.escape(section) + r"\][ \t]*$")
    matches = list(header.finditer(raw))
    assert len(matches) == 1, f"{path}: expected exactly one [{section}]"
    remainder = raw[matches[0].end():]
    next_header = re.search(r"(?m)^[ \t]*\[[^]\r\n]+\][ \t]*$", remainder)
    block = remainder[:next_header.start()] if next_header else remainder
    values = {}
    for raw_line in block.splitlines():
        line = raw_line.strip()
        if not line or line.startswith((";", "#")):
            continue
        assert "=" in line, f"{path} [{section}]: invalid line {line}"
        key, value = (item.strip() for item in line.split("=", 1))
        assert key not in values, f"{path} [{section}]: duplicate {key}"
        values[key] = value.strip('"')
    return values

simulator_ini = "bin/OpenSim.ini"
robust_ini = "bin/Robust.HG.ini"
active = {
    "NexTraffic": {
        "Enabled": "true", "LSLChannel": "-835421", "RefreshSeconds": "5",
        "GreenSeconds": "30", "YellowSeconds": "5", "AllRedSeconds": "3",
        "Junctions": "Hauptstrasse:0;Marktplatz:11",
    },
    "NexTransit": {
        "Enabled": "true", "LSLChannel": "-835422", "RefreshSeconds": "30",
        "TimeZone": "Europe/Berlin", "MaxDepartures": "5",
        "Routes": "101,Magdeburg,10,0,*;150,Jenseits,20,15,*",
    },
    "NexSmartLight": {
        "Enabled": "true", "LSLChannel": "-835423", "RefreshSeconds": "60",
        "TimeZone": "Europe/Berlin", "NightStarts": "18:00",
        "NightEnds": "06:00",
    },
    "OGLWeather": {
        "Enabled": "true", "WeatherSeed": "20261028",
        "TimeZone": "Europe/Berlin", "LSLChannel": "-835311",
        "CurrentBroadcastSeconds": "60", "ForecastBroadcastSeconds": "300",
        "CloudBaseMeters": "200", "LegacyParticleEmitters": "true",
        "ViewerEnvironmentEffects": "true",
        "EmitterHeightMeters": "22", "EmitterSpacingMeters": "96",
        "MaxEmittersPerRegion": "16", "VisualRefreshSeconds": "12",
        "TestWeatherMode": "auto", "TestWeatherIntensity": "70",
        "TestWeatherDurationMinutes": "15",
    },
}
for section, expected in active.items():
    actual = active_ini_section(simulator_ini, section)
    assert actual == expected, f"{simulator_ini} [{section}] drift: {actual}"
    assert not re.search(r"(?m)^\[" + re.escape(section) + r"\]$", content(robust_ini)), (
        f"Simulator-only [{section}] must not appear in ROBUST"
    )

portal = active_ini_section(robust_ini, "OGLWeatherPortal")
assert portal == {
    "Enabled": "true", "WeatherSeed": active["OGLWeather"]["WeatherSeed"],
    "TimeZone": active["OGLWeather"]["TimeZone"],
    "DisplayName": "Stadt-NexVerse",
}, "Portal climate or activation mismatches simulator"
assert not re.search(r"(?m)^\[OGLWeatherPortal\]$", content(simulator_ini))
assert active_ini_section(robust_ini, "NexVerseWorldApi")["Enabled"].lower() == "true"
print("Gen1 active INIs PASS: 4 simulator modules + ROBUST portal enabled, unique and synchronized")
