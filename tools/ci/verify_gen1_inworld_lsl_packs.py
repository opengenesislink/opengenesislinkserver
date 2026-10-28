#!/usr/bin/env python3
"""Source-contract verification for Generation-1 Inworld LSL companion packs.

This does NOT imply an inworld compiler or runtime test. Intentionally guard
against protocol drift, accidentally unbounded chat or prim-emitter scripts.
"""
from pathlib import Path
import re

ROOT = Path(__file__).resolve().parents[2]
PACKAGES = {
    "NexTraffic": {
        "channel": -835421, "source": "NexTraffic", "wire": "NEXTRAFFIC",
        "files": (
            "NexTrafficSignal.lsl",
            "NexTrafficCountdown.lsl",
            "NexTrafficJunctionMonitor.lsl",
        ),
    },
    "NexTransit": {
        "channel": -835422, "source": "NexTransit", "wire": "NEXTRANSIT",
        "files": (
            "NexTransitDepartureBoard.lsl",
            "NexTransitStopInfo.lsl",
            "NexTransitRouteHeadSign.lsl",
            "NexTransitDepartureAnnouncement.lsl",
        ),
    },
    "NexSmartLight": {
        "channel": -835423, "source": "NexSmartLight", "wire": "NEXLIGHT",
        "files": (
            "NexSmartLightLamp.lsl",
            "NexSmartLightLinkset.lsl",
            "NexSmartLightStatus.lsl",
        ),
    },
    "OGLWeather": {
        "channel": -835311, "source": "OGLWeather", "wire": "OGLWX",
        "files": (
            "WeatherForecastBoard.lsl",
            "WeatherCurrentSign.lsl",
            "WeatherForecastText.lsl",
        ),
    },
}

def source_without_literals(source):
    # Verify balanced delimiters in code, excluding strings and comments.
    # This is not a substitute for an actual LSL compiler.
    expression = re.compile(r'"(?:\\.|[^"\\])*"|//[^\n]*|/\*[\s\S]*?\*/')
    return expression.sub(lambda m: " " * len(m.group()), source)

def check_balanced(source, file):
    body = source_without_literals(source)
    stack = []
    pairs = {')': '(', ']': '[', '}': '{'}
    for idx, ch in enumerate(body):
        if ch in "([{":
            stack.append((ch, idx))
        elif ch in ")]}":
            assert stack and stack[-1][0] == pairs[ch], (
                f"{file}: unbalanced {ch} at char {idx}")
            stack.pop()
    assert not stack, f"{file}: unclosed delimiter {stack[-1] if stack else ''}"
    assert "default" in body and "state_entry()" in body, file
    assert "llListen(" in body and "listen(" in body, file
    # LSL does not have a portable C-style ?: operator.
    assert " ? " not in body and " : " not in body, file

docs = (ROOT / "doc/NexVerse/GEN1_INWORLD_LSL_PACKS.md").read_text()
seen = set()
total = 0
for package, info in PACKAGES.items():
    module = (ROOT / f"addons/{package}/{package}RegionModule.cs").read_text()
    assert f'"{package}", UUID.Zero' in module, package
    assert f'"LSLChannel", {info["channel"]}' in module, package
    for filename in info["files"]:
        file = ROOT / "addons" / package / filename
        assert file.is_file(), file
        assert filename not in seen, filename
        seen.add(filename)
        data = file.read_text(encoding="utf-8-sig")
        check_balanced(data, file)
        assert f"integer CHANNEL = {info['channel']};" in data, file
        assert f'"{info["wire"]}"' in data, file
        assert f'"{info["source"]}"' in data or (
            package == "OGLWeather" and
            'string SOURCE = "OGLWeather"' in data
        ), file
        assert "llParseStringKeepNulls(" in data, file
        assert "id != NULL_KEY" in data, file
        assert "on_rez(integer " in data, file
        assert filename in docs, f"{filename} missing from installation guide"
        assert "llGiveMoney(" not in data and "llTeleportAgent(" not in data
        assert "llRezObject(" not in data and "llRezAtRoot(" not in data
        assert "llHTTPRequest(" not in data
        if package == "OGLWeather":
            assert "llParticleSystem(" not in data, (
                "Weather visuals must remain server-managed, not resident-owned")
        total += 1
    assert all(x in docs for x in info["files"]), package

# All LSL calls used by the legacy/portable scripts must exist in the
# shipped LSL API; OSSL dynamic texture calls are allowed explicitly.
stub = (ROOT / "OpenSim/Region/ScriptEngine/Shared/Api/Runtime/LSL_Stub.cs").read_text()
for package, info in PACKAGES.items():
    for filename in info["files"]:
        file = ROOT / "addons" / package / filename
        source = source_without_literals(file.read_text(encoding="utf-8-sig"))
        # A few calls may be declared in the LSL ScriptBaseClass API rather
        # than forwarded as source methods; checks below test regular ll* calls.
        for name in set(re.findall(r'\b(ll[A-Z][A-Za-z0-9_]*)\s*\(', source)):
            assert name in stub, f"Unknown/missing LSL binding {name} in {file}"

assert total == 13, f"Expected 13 companion scripts, got {total}"
traffic = (ROOT / "addons/NexTraffic/NexTrafficSignal.lsl").read_text()
assert "PHASE_END = serverTime + remaining" in traffic
assert "llSetTimerEvent(1.0)" in traffic
assert 'CURRENT = "RED"' in traffic
transit = (ROOT / "addons/NexTransit/NexTransitDepartureBoard.lsl").read_text()
assert 'if (payload != "")' in transit and "if (llGetListLength(rows) > 10)" in transit
assert 'osSetDynamicTextureDataFace("", "vector", d,' in transit
assert "llSetTexture(" not in transit
announce = (ROOT / "addons/NexTransit/NexTransitDepartureAnnouncement.lsl").read_text()
assert "integer ANNOUNCE_IN_CHAT = FALSE;" in announce
assert "COOLDOWN_SECONDS = 600" in announce
assert "llSay(0," in announce
linkset = (ROOT / "addons/NexSmartLight/NexSmartLightLinkset.lsl").read_text()
assert 'string PREFIX = "LIGHT_";' in linkset
assert "PRIM_POINT_LIGHT" in linkset and "CHANGED_LINK" in linkset
weather = (ROOT / "addons/OGLWeather/WeatherForecastText.lsl").read_text()
assert "llListReplaceList(gDays, [message], slot, slot)" in weather
assert "osSetDynamicTextureDataFace" not in weather
assert "server-managed" in docs or "Server" in docs
assert "Wetter" in docs

print(f"Generation-1 LSL pack: {total} scripts, 4 server wire protocols, "
      "13 structural and compatibility contracts PASS (source only)")
