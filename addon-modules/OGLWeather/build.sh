#!/usr/bin/env bash
set -euo pipefail

# This add-on ONLY runs in a simulator, never ROBUST.
# Build without deploying; optional explicit --sim-bin path installs the DLL.
if [[ "${1:-}" == "--help" ]]; then
  echo "Usage: bash addon-modules/OGLWeather/build.sh [Release|Debug] [--sim-bin /absolute/simulator/bin]"
  exit 0
fi

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$ROOT"
CONFIG="${1:-Release}"
case "$CONFIG" in Release|Debug) ;; *) echo "Invalid build configuration" >&2; exit 2 ;; esac
if [[ $# -ne 0 && $# -ne 1 && $# -ne 3 ]]; then
  echo "Expected [Release|Debug] [--sim-bin /absolute/simulator/bin]" >&2
  exit 2
fi
if [[ $# -eq 3 && "$2" != "--sim-bin" ]]; then
  echo "Expected --sim-bin" >&2
  exit 2
fi

# OpenGenesisLINK uses the classic Prebuild tool. Checked-in project
# files may be stale or malformed until the normal project-generation step.
# Regenerate first so this independent addon builds from a clean checkout.
if [[ ! -f "$ROOT/bin/prebuild.dll" ]]; then
  echo "Missing bin/prebuild.dll; build from a complete OpenGenesisLINK source tree." >&2
  exit 1
fi
bash runprebuild.sh

dotnet build addons/OGLWeather/OpenGenesisLINK.Addons.OGLWeather.csproj -c "$CONFIG"
DLL="addons/OGLWeather/bin/$CONFIG/net8.0/OpenGenesisLINK.Addons.OGLWeather.dll"
test -s "$DLL" || { echo "OGLWeather DLL missing" >&2; exit 1; }

if [[ $# -eq 3 ]]; then
  SIM_BIN="$(realpath -m -- "$3")"
  if [[ "${SIM_BIN,,}" == */robust/bin ||
        "$SIM_BIN" != */bin ||
        ! -f "$SIM_BIN/OpenSim.Region.Framework.dll" ]]; then
    echo "Refusing installation: expected simulator bin with Region.Framework.dll, never ROBUST." >&2
    exit 2
  fi
  install -m 0644 "$DLL" "$SIM_BIN/OpenGenesisLINK.Addons.OGLWeather.dll"
  echo "OGLWeather installed to simulator: $SIM_BIN"
else
  echo "OGLWeather built only. Manual install:"
  echo "bash addon-modules/OGLWeather/build.sh $CONFIG --sim-bin /opt/developer/bin"
fi
