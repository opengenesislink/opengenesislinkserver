#!/usr/bin/env python3
"""Guard against accidental reversion of the NexVerse login DST policy.

DSTZone changes the DST *flag* in the LLLoginResponse. It must not
silently alter the viewer's PST/PDT clock or unrelated simulator time.
"""
from pathlib import Path
from datetime import datetime, timezone
from zoneinfo import ZoneInfo

root = Path(__file__).resolve().parents[2]
expected = 'DSTZone = "Europe/Berlin;W. Europe Standard Time"'
for relative in (
    "bin/Robust.HG.ini",
    "bin/Robust.HG.ini.example",
    "bin/Robust.HG.OGLAsset.ini.example",
    "bin/Robust.ini.example",
):
    content = (root / relative).read_text(encoding="utf-8-sig")
    section = content.split("[LoginService]", 1)[1].split("\n[", 1)[0]
    assignments = [line.strip() for line in section.splitlines()
                   if line.strip().startswith("DSTZone =")]
    assert assignments == [expected], (relative, assignments)

login = (root / "OpenSim/Services/LLLoginService/LLLoginService.cs").read_text(
    encoding="utf-8-sig")
response = (root / "OpenSim/Services/LLLoginService/LLLoginResponse.cs").read_text(
    encoding="utf-8-sig")
assert 'm_LoginServerConfig.GetString("DSTZone"' in login
assert "DSTZone.Split(';')" in response
assert "TimeZoneInfo.FindSystemTimeZoneById(tzName)" in response
assert 'loginFlagsHash["daylight_savings"] = DST;' in response

berlin = ZoneInfo("Europe/Berlin")
winter = datetime(2026, 1, 15, 12, tzinfo=timezone.utc).astimezone(berlin)
summer = datetime(2026, 7, 15, 12, tzinfo=timezone.utc).astimezone(berlin)
assert winter.utcoffset().total_seconds() == 3600 and not winter.dst()
assert summer.utcoffset().total_seconds() == 7200 and summer.dst()
print("NexVerse Berlin Login DST: config and CET/CEST transitions PASS")
