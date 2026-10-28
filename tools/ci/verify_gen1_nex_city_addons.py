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
print("NexCity addon contracts PASS: independent plugins, bounded wire, safe disabled defaults")
