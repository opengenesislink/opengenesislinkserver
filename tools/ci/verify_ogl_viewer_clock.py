#!/usr/bin/env python3
"""Source-level guard for the optional OGL discovery civil-time extension."""
from pathlib import Path
from datetime import datetime, timezone
from zoneinfo import ZoneInfo

root = Path(__file__).resolve().parents[2]
source = (root / "OpenSim/Server/Handlers/Grid/GridInfoHandlers.cs").read_text()
markers = (
    'clock = BuildViewerClock()',
    'private object BuildViewerClock()',
    'm_Config?.Configs["LoginService"]',
    '"DSTZone"',
    'TimeZoneInfo.FindSystemTimeZoneById',
    'now.ToUnixTimeSeconds()',
    'zone.GetUtcOffset(now)',
    'TimeZoneInfo.ConvertTime(now, zone)',
    'org.opengenesislink.viewer.clock.v1',
    'utc_offset_seconds = offsetSeconds',
    'httpResponse.AddHeader("Cache-Control", "no-store")',
)
for marker in markers:
    assert marker in source, marker
zone = ZoneInfo("Europe/Berlin")
for instant, seconds, label in (
    (datetime(2026, 1, 15, 12, tzinfo=timezone.utc), 3600, "MEZ"),
    (datetime(2026, 7, 15, 12, tzinfo=timezone.utc), 7200, "MESZ"),
    (datetime(2026, 3, 29, 0, 59, tzinfo=timezone.utc), 3600, "MEZ"),
    (datetime(2026, 3, 29, 1, 1, tzinfo=timezone.utc), 7200, "MESZ"),
    (datetime(2026, 10, 25, 0, 59, tzinfo=timezone.utc), 7200, "MESZ"),
    (datetime(2026, 10, 25, 1, 1, tzinfo=timezone.utc), 3600, "MEZ"),
):
    berlin = instant.astimezone(zone)
    assert int(berlin.utcoffset().total_seconds()) == seconds
    assert (("MESZ" if berlin.dst().total_seconds() else "MEZ")) == label
print("OGL discovery clock v1 source guard and Berlin DST fixtures passed.")
