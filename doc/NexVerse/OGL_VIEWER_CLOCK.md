# OGL Viewer clock v1 — server-authoritative civil time

This non-authenticated protocol extension was introduced to remove Firestorm's
hardcoded SLT/PST/PDT clock from the TrixieStorm Viewer.

## Source of truth

The server reads the **loaded** ROBUST `[LoginService] DSTZone` setting,
the same configuration used for legacy login daylight-savings flags. It picks
the first valid IANA/Windows zone in the configured semicolon-separated list,
or the server's local zone for `DSTZone=local`. `DSTZone=none` or an unresolved
zone falls back to UTC. It does not use the timezone of the user's PC.

On `GET /ogl/v1/grid-info`, `clock` is an optional additional field within
the existing schema `org.opengenesislink.viewer.grid-info.v1`:

```json
{
  "clock": {
    "schema": "org.opengenesislink.viewer.clock.v1",
    "unix_time": 1790000000,
    "timezone": "Europe/Berlin",
    "utc_offset_seconds": 7200,
    "abbreviation": "MESZ"
  }
}
```

The numeric values above are **illustrative**. Every response is recomputed
using the current UTC instant and the server's resolved timezone. No epoch or
offset value is hardcoded. The response uses `Cache-Control: no-store`.
The transport and legacy login remain unchanged.

## Viewer integration and DST

TrixieStorm reads `clock` only from the pinned G1 OGL discovery URL. It
validates schema, grid product and configured login target, timestamp bounds,
timezone name, display abbreviation and offset range. The clock uses server
UTC epoch plus a monotonic elapsed timer; it renders civil time using the
received offset, without changing the viewer's process or system timezone.
The viewer refreshes the metadata about once per minute and never trusts a
received clock field to change authentication endpoints.

Server response updates will naturally switch between Berlin winter
**MEZ (UTC+01:00)** and summer **MESZ (UTC+02:00)**. The refresh can produce
a transition delay of up to roughly a minute. When the server is unavailable
or is an older version without `clock`, the viewer displays **UTC**, not
mislabelled PST. The full IANA zone is retained in a tooltip.

This is a **wall-clock display** feature. It does not modify simulator day
cycles, sun positions, region environment/EEP, LSL time or grid authorization.

## Verification

- Winter UTC 2026-01-15: Berlin offset 3600, display MEZ.
- Summer UTC 2026-07-15: Berlin offset 7200, display MESZ.
- Europe/Berlin DST transition: refresh returns newly effective offset.
- Changing `DSTZone` to another valid zone changes the response after ROBUST
  restarts with the new configuration, no TrixieStorm rebuild needed.
- Legacy Firestorm and classic login response remain compatible.
- Full Windows/Linux ReleaseOS runtime tests are still required.

The endpoint is public and intentionally returns no secrets, credentials,
NexBus keys or privileged World API data.
