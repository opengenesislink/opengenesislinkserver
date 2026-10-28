#!/usr/bin/env bash
# Build exactly one of three independently loadable RegionModule libraries.
# Deployment is explicit and refuses any ROBUST installation.
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
NAME="${1:-}"
CFG="${2:-Release}"
case "$NAME" in NexTraffic|NexTransit|NexSmartLight) ;; *) echo "Unknown addon" >&2; exit 2 ;; esac
case "$CFG" in Release|Debug) ;; *) echo "Unknown configuration" >&2; exit 2 ;; esac
cd "$ROOT"
if [[ "${3:-}" == "--sim-bin" ]]; then
  [[ $# -eq 4 ]] || { echo "Usage: $0 Addon [Release|Debug] [--sim-bin /path/to/simulator/bin]" >&2; exit 2; }
elif [[ $# -ne 2 ]]; then
  echo "Usage: $0 Addon [Release|Debug] [--sim-bin /path/to/simulator/bin]" >&2
  exit 2
fi
test -f "$ROOT/bin/prebuild.dll" || { echo "Missing bin/prebuild.dll" >&2; exit 1; }
bash runprebuild.sh
dotnet build "addons/$NAME/OpenGenesisLINK.Addons.$NAME.csproj" --configuration "$CFG"
DLL="$ROOT/addons/$NAME/bin/$CFG/net8.0/OpenGenesisLINK.Addons.$NAME.dll"
test -s "$DLL" || { echo "Compiled addon not found: $DLL" >&2; exit 1; }
if [[ "${3:-}" == "--sim-bin" ]]; then
  TARGET="$(realpath -m -- "$4")"
  if [[ "$TARGET" != */bin || "${TARGET,,}" == *robust* ||
      ! -f "$TARGET/OpenSim.Region.Framework.dll" ]]; then
    echo "Refusing non-simulator target $TARGET" >&2
    exit 2
  fi
  install -m 0644 "$DLL" "$TARGET/OpenGenesisLINK.Addons.$NAME.dll"
  echo "Installed only to chosen simulator: $TARGET"
else
  echo "$NAME compiled; no live simulator changes."
fi
