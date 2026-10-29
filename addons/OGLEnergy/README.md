# OGLEnergy – Stadt-NexVerse Stromversorgung (Generation 1)

Independent OpenGenesisLINK simulator add-on for **simulated** electric power.
This is not real power, accounting or metering.

## Features / Funktionen

- One shared, deterministic power-grid model for all regions of one simulator.
  With identical config, clock and time zone, independently hosted simulator
  instances produce the same schedule (no live federation is implied).
- Generation: simulated solar, wind, hydroelectric and dispatchable base power.
  Demand: deterministic morning and evening peaks in local Europe/Berlin time.
- Status: NORMAL (supply >= demand), WARN (75–99% covered),
  OUTAGE (less than 75% supply/demand).
- Only **read-only** region broadcasts. No SQL schema, external APIs, HTTP
  requests, inventory edits, land edits, power-billing or automatic changes
  to existing objects. The capacity values are virtual gameplay values.
- Separate 1024x512 OSSL VectorRender dashboard and an **optional example**
  light that switches itself off on OUTAGE. Existing NexSmartLight remains
  independent and is never modified.
- Debug command on the simulator's operator console: `ogl energy status`.
- Default OFF for reusable deployments. No generator is rezzed by the server.

## Installation

This project is included in `OpenSim.sln` and `./runprebuild.sh`. Normal
`dotnet build -c Release OpenSim.sln` or `./compile.sh` builds
`bin/OpenGenesisLINK.Addons.OGLEnergy.dll` automatically.

Activate only in the **regions simulator** loaded `OpenSim.ini`:

```ini
[OGLEnergy]
Enabled = true
LSLChannel = -835431
RefreshSeconds = 60
TimeZone = Europe/Berlin
SolarCapacityKw = 900
WindCapacityKw = 1000
HydroCapacityKw = 500
BaseCapacityKw = 850
BaseDemandKw = 2200
```

Restart the simulator. Leave ROBUST untouched. Capacity of a source is
0–100000 virtual kW; BaseDemandKw must be 100–100000. All zero sources
disable the module safely. Refresh interval is clamped to 30–300 seconds.
A nonnegative channel or known conflicting weather/city channel disables it.

## Inworld board

1. Create a flat prim and insert `OGLEnergyGridDisplay.lsl`.
2. The script renders on `DISPLAY_FACE=3` by default; change for the
   correct face on other object types.
3. Required OSSL permissions:
   `osSetDynamicTextureDataFace`, `osSetFontName`,
   `osSetPenColor`, `osMovePen`, `osDrawText`,
   `osDrawFilledRectangle`.
4. If an experimental blackout lamp is wanted, put
   `OGLEnergyDemoLamp.lsl` into its **own light source prim**. It controls
   only that prim's point light and fails safely to OFF without data.

## Broadcast contract

```text
OGLENERGY|1|yyyy-MM-dd HH:mm|NORMAL/WARN/OUTAGE|supplyKw|demandKw|solarKw|windKw|hydroKw|baseKw|utilizationPct|reserveKw
```

The sender is `OGLEnergy`, `UUID.Zero`, channel `-835431` from
`IWorldComm.DeliverMessage(ChatTypeEnum.Region,...)`. Only trusted
server broadcasts are accepted by the example LSL scripts.

The model does **not** provide a physical distribution grid, per-house
meters, separately registered generators, parcel billing, actual weather
coupling, grid federation, authenticated central dispatch or emergency
power controls. Those are possible future development phases after
validation. In particular, a display showing OUTAGE does not shut down
existing resident objects.

## Regression

```bash
python3 tools/ci/verify_ogl_energy.py
dotnet run -c Release --project tools/ci/OglEnergyRegression/OglEnergyRegression.csproj
```

CI verifies module contracts and a full year of generation/demand,
including DST and threshold invariants.
