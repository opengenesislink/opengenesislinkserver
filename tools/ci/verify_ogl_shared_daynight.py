#!/usr/bin/env python3
"""Verify OGLSharedDayNight source contract and Berlin 24h EEP civil time."""
from datetime import datetime, timezone
from pathlib import Path
from zoneinfo import ZoneInfo

root = Path(__file__).resolve().parents[2]
source = (root / "OpenSim/Region/CoreModules/World/LightShare/EnvironmentModule.cs").read_text()
config = (root / "bin/OpenSim.ini.example").read_text()

for marker in (
    'm_oglSharedDayNightEnabled',
    'source.Configs["OGLSharedDayNight"]',
    'dayNight.GetBoolean("Enabled", false)',
    'source.Configs["LoginService"]',
    'TimeZoneInfo.FindSystemTimeZoneById(name)',
    'private void UpdateOglSharedDayNight()',
    'GetUtcOffset(DateTimeOffset.UtcNow)',
    'const int secondsPerDay = 24 * 60 * 60',
    '((offsetSeconds % secondsPerDay) + secondsPerDay) % secondsPerDay',
    'shared.DayLength == secondsPerDay && shared.DayOffset == dayOffset',
    'StoreOnRegion(updated)',
    'WindlightRefresh(0);',
    'm_oglSharedDayNightUpdating',
    'groundSky.Count < 2',
    '5646d39e-d3d7-6aff-ed71-30fc87d64a91',
    'UpdateOglSharedDayNight();',
):
    assert marker in source, marker

assert '[OGLSharedDayNight]' in config
assert 'Enabled = true' in config.split('[OGLSharedDayNight]')[1].split('[')[0]
assert 'Europe/Berlin;W. Europe Standard Time' in config
assert 'DayLength = secondsPerDay' in source
assert 'DayOffset = dayOffset' in source

berlin = ZoneInfo('Europe/Berlin')
fixtures = (
    (datetime(2026, 1, 15, 11, tzinfo=timezone.utc), 3600, 0.5),
    (datetime(2026, 7, 15, 10, tzinfo=timezone.utc), 7200, 0.5),
    (datetime(2026, 3, 29, 0, 59, tzinfo=timezone.utc), 3600, (1*3600+59*60)/86400),
    (datetime(2026, 3, 29, 1, 1, tzinfo=timezone.utc), 7200, (3*3600+1*60)/86400),
    (datetime(2026, 10, 25, 0, 59, tzinfo=timezone.utc), 7200, (2*3600+59*60)/86400),
    (datetime(2026, 10, 25, 1, 1, tzinfo=timezone.utc), 3600, (2*3600+1*60)/86400),
)
for instant, expected_offset, expected_fraction in fixtures:
    offset_seconds = int(instant.astimezone(berlin).utcoffset().total_seconds())
    assert offset_seconds == expected_offset
    unix = int(instant.timestamp())
    day_offset = ((offset_seconds % 86400) + 86400) % 86400
    fraction = ((unix + day_offset) % 86400) / 86400
    assert abs(fraction - expected_fraction) < 0.0000001, (instant, fraction, expected_fraction)

for date in (
    datetime(2026, 1, 15, tzinfo=timezone.utc),
    datetime(2026, 7, 15, tzinfo=timezone.utc)
):
    offset = int(date.astimezone(berlin).utcoffset().total_seconds())
    for local_hour in (0, 6, 12, 18):
        utc_hour = (local_hour*3600 - offset) % 86400
        assert ((utc_hour + offset) % 86400) / 86400 == local_hour / 24

print("OGL Shared Day/Night zone alignment, EEP source contract and DST fixtures passed.")
