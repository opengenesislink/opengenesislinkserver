#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$ROOT"
CONFIG="${1:-Release}"
case "$CONFIG" in
  Release|Debug) ;;
  *) echo "Expected Release or Debug" >&2; exit 2 ;;
esac

# The addon uses OutputPath=../../bin in its csproj because ROBUST loads
# plugins directly from the shared OpenGenesisLINK bin/ directory.
# Do not expect the old addons/OGLAssetService/bin/<config>/net10.0 path
# or copy assemblies over those already built into shared bin/.
dotnet build addons/OGLAssetService/OGLAssetService.csproj -c "$CONFIG"

OUT="$ROOT/bin"

# ROBUST loads the addon dynamically from shared bin/ and does not always
# include its NuGet runtime native assets in Robust.deps.json. SQLitePCL
# needs the correct native e_sqlite3 beside Robust.dll at process startup.
# Install only the package-restored binary for this host RID.
case "$(uname -s)" in
  Linux) sqlite_os="linux"; sqlite_lib="libe_sqlite3.so" ;;
  Darwin) sqlite_os="osx"; sqlite_lib="libe_sqlite3.dylib" ;;
  MINGW*|MSYS*|CYGWIN*) sqlite_os="win"; sqlite_lib="e_sqlite3.dll" ;;
  *) echo "Unsupported SQLite runtime host: $(uname -s)" >&2; exit 1 ;;
esac
case "$(uname -m)" in
  x86_64|amd64) sqlite_arch="x64" ;;
  aarch64|arm64) sqlite_arch="arm64" ;;
  *) echo "Unsupported SQLite runtime architecture: $(uname -m)" >&2; exit 1 ;;
esac
sqlite_rid="${sqlite_os}-${sqlite_arch}"
nuget_root="$(dotnet nuget locals global-packages --list | sed -n 's/^global-packages: //p' | head -n 1)"
# Resolve the exact package selected by NuGet for this addon build, never an
# arbitrary cached DLL. An older vulnerable e_sqlite3 may still be installed
# in the global NuGet cache after an upgrade.
sqlite_version="$(python3 - "$ROOT/addons/OGLAssetService/obj/project.assets.json" <<'PY'
import json
import sys
from pathlib import Path

assets = json.loads(Path(sys.argv[1]).read_text(encoding="utf-8"))
versions = [key.partition("/")[2]
            for key in assets["libraries"]
            if key.lower().startswith("sqlitepclraw.lib.e_sqlite3/")]
if len(versions) != 1:
    raise SystemExit("Expected one NuGet-resolved SQLitePCLRaw.lib.e_sqlite3 native package")
version = versions[0]
parts = version.split(".")
if len(parts) < 3 or not all(x.isdecimal() for x in parts[:3]) or tuple(map(int, parts[:3])) < (2, 1, 12):
    raise SystemExit(f"Refusing vulnerable SQLitePCLRaw native package {version}")
print(version)
PY
)"
sqlite_pkg="${nuget_root%/}/sqlitepclraw.lib.e_sqlite3/$sqlite_version"
sqlite_native="$sqlite_pkg/runtimes/$sqlite_rid/native/$sqlite_lib"
if [ ! -s "$sqlite_native" ]; then
  echo "Missing NuGet-resolved SQLite native runtime $sqlite_native" >&2
  exit 1
fi
cp -f -- "$sqlite_native" "$OUT/$sqlite_lib"
chmod 644 "$OUT/$sqlite_lib"
cmp -- "$sqlite_native" "$OUT/$sqlite_lib"
echo "Installed SQLite native runtime $sqlite_version for $sqlite_rid into shared ROBUST bin/."

for dll in \
  OpenGenesisLINK.Addons.OGLAssetService.dll \
  OpenSim.Services.FSAssetService.dll \
  StackExchange.Redis.dll \
  Pipelines.Sockets.Unofficial.dll \
  Microsoft.Data.Sqlite.dll \
  Microsoft.Extensions.Logging.Abstractions.dll; do
  if [ ! -s "$OUT/$dll" ]; then
    echo "Missing required OGL AssetService runtime assembly: $OUT/$dll" >&2
    exit 1
  fi
done

echo "OGL AssetService assemblies verified in bin/ (ROBUST remains on its configured asset provider)."
