# OGL Shared Day/Night — regional EEP synchronized to server civil time

**Source implementation (2026-11-08):** the existing OpenSimulator
`EnvironmentModule` has an optional mode for OpenGenesisLINK's
region-wide, shared EEP day/night display.

## Activation on the simulator host

Put this in the **active OpenSim.ini** (or an included INI file), on
each simulator that should use a 24-hour day. The repository's
`bin/OpenSim.ini.example` already contains it for new installations;
updates do **not** silently overwrite your production OpenSim.ini.

```ini
[OGLSharedDayNight]
    Enabled = true
    TimeZone = "Europe/Berlin;W. Europe Standard Time"
```

The zone must be the **same** as the actual ROBUST
`[LoginService] DSTZone` driving TrixieStorm's public OGL discovery clock.
A standalone simulator can omit `TimeZone` when its loaded
`[LoginService] DSTZone` is present. Distributed region simulators
usually do not load ROBUST's LoginService section, so specify the
corresponding zone in their own configuration. The module rejects
unknown/missing zones rather than guessing Pacific or the viewer's local
time. Changes take effect when that simulator process reloads its
configuration (normally a region-server restart).

This flag is optional and **off if the section is absent**. Existing
regions that never opt in retain their previous EEP behavior.

## Implementation

Every 60 seconds the environment module obtains the current **UTC offset**
from the configured zone, using .NET `TimeZoneInfo` and the server's UTC
clock. It sets the shared EEP day cycle to `day_length = 86400` seconds
and `day_offset = current UTC offset modulo 86400` seconds.

The OpenSim and Viewer EEP clocks already use:

```text
day_fraction = ((utc_unix_seconds + day_offset) % 86400) / 86400
```

For `Europe/Berlin` this gives local midnight at day fraction 0.0,
06:00 at 0.25, noon at 0.5, and 18:00 at 0.75. At European DST
boundaries, the offset automatically switches between +3600 and +7200
seconds. Region EEP versions are updated and
`WindlightRefresh(0)` notifies participating avatars only when the
day offset/day length changes. Intermediate daylight transitions
are interpolated by the Viewer's existing shared EEP environment, so
the server does not transmit per-frame environment snapshots.

For a region without its own EEP settings, the module prefers the
bundled balanced **Default Daycycle** asset
`5646d39e-d3d7-6aff-ed71-30fc87d64a91`. Its ground-level sky track
contains actual sunrise, midday, sunset, and midnight frames.
An existing **custom, animated** EEP day cycle is preserved, including
its sky/water artwork. If a custom EEP environment is a fixed sky,
there is no valid animated day/night transition: the module warns
and leaves that region untouched rather than destroying the owner's
setting. Select a multi-keyframe day/night EEP asset in region settings
before enabling synchronization.

## Behavior of "Gemeinsame Umgebung" in TrixieStorm

The viewer's **Use Shared Environment / Gemeinsame Umgebung** setting
already consumes the region's EEP day cycle through
`ExtEnvironment` and updates on the normal server refresh.
**No per-user or client-side sky override is needed.**

- Avatars viewing the region shared environment see the same clock-driven
  sky and sun/moon/ambient lighting.
- Explicit local/custom sky overrides in a viewer remain user-controlled.
- Parcel EEP overrides and per-avatar environment overrides take
  precedence over the region shared EEP (existing OpenSim behavior).
  For a truly identical day/night experience over all land in a
  region, parcel owners must inherit the region's environment.
- This does **not** automatically turn on separate scripted lamps,
  glows, building lights, or fog. Those need their own smart-light/
  seasons/weather integrations.
- This is a *24-hour civil-day* EEP cycle, **not** an astronomical
  sunrise/sunset calculation for Germany by calendar date. The default
  balanced preset transitions near 06:00 and 18:00 year-round; select
  or implement seasonal EEP keyframe shifts separately if required.

## Source / operational verification

Run `python3 tools/ci/verify_ogl_shared_daynight.py`.
Expected when the active region config enables this feature:

- Around 00:00 Berlin: midnight sky/darkness.
- Around 06:00: morning light transition (balanced default).
- Around 12:00: daylight.
- Around 18:00: sunset transition (balanced default).
- Summer and winter Berlin offsets match the discovery clock sent
  to TrixieStorm in `clock.utc_offset_seconds`.
- Existing local overrides and parcels with custom EEP stay unchanged.
- Already active viewers receive `ExtEnvironment`/Windlight refresh
  at an offset change; a full build and multi-client staging test
  must still prove this end-to-end.

No login endpoint, authentication transport, or voice code changes
are part of this feature.
