#!/usr/bin/env bash
# Isolated S3+SQL HTTP integration against a temporary ROBUST instance only.
# Requires an ephemeral GitHub runner's MinIO and MariaDB test containers.
# NEVER use production credentials, S3 buckets or real AssetService INIs.
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$ROOT"

test -s bin/Robust.dll
test -s bin/OpenGenesisLINK.Addons.OGLAssetService.dll

: "${OGL_S3_CI_ENDPOINT:?S3 CI loopback endpoint required}"
: "${OGL_S3_CI_BUCKET:?S3 CI bucket required}"
: "${OGL_S3_CI_ACCESS_KEY:?Temporary test key required}"
: "${OGL_S3_CI_SECRET_KEY:?Temporary test secret required}"
: "${OGL_S3_CI_DB_PASSWORD:?Temporary CI database password required}"
test "$OGL_S3_CI_ENDPOINT" = "http://127.0.0.1:19119"
[[ "$OGL_S3_CI_BUCKET" =~ ^ogl-s3-ci-[0-9]+$ ]]

export OGL_S3_ACCESS_KEY="$OGL_S3_CI_ACCESS_KEY"
export OGL_S3_SECRET_KEY="$OGL_S3_CI_SECRET_KEY"

WORK="$(mktemp -d "${RUNNER_TEMP:-/tmp}/ogl-s3-robust.XXXXXXXX")"
STAGED_INI="$ROOT/bin/Robust.OGLS3Smoke.Tests.ini"
ROBUST_PID=""
if [ -e "$STAGED_INI" ]; then
  echo "::error::Refusing to overwrite existing test INI"
  exit 1
fi
cleanup() {
  if [ -n "$ROBUST_PID" ]; then
    kill "$ROBUST_PID" 2>/dev/null || true
    wait "$ROBUST_PID" 2>/dev/null || true
  fi
  rm -f -- "$STAGED_INI"
  rm -rf -- "$WORK"
}
trap cleanup EXIT

mkdir -p "$WORK/registry" "$WORK/include" "$WORK/fsassets/data" "$WORK/fsassets/tmp" "$WORK/ogl"
cat > "$STAGED_INI" <<EOF
[Startup]
  RegistryLocation = "$WORK/registry"
  ConfigDirectory = "$WORK/include"
[ServiceList]
  AssetServiceConnector = "19496/OpenSim.Server.Handlers.dll:AssetServiceConnector"
[Network]
  port = 19496
[DatabaseService]
  StorageProvider = "OpenSim.Data.MySQL.dll"
  ConnectionString = "Data Source=127.0.0.1;Port=19497;Database=ogl_s3_ci;User ID=root;Password=$OGL_S3_CI_DB_PASSWORD;Old Guids=true;SslMode=None;"
[AssetService]
  LocalServiceModule = "OpenGenesisLINK.Addons.OGLAssetService.dll:OGLAssetConnector"
  BaseDirectory = "$WORK/fsassets/data"
  SpoolDirectory = "$WORK/fsassets/tmp"
  ShowConsoleStats = false
  DefaultAssetLoader = ""
  FallbackService = ""
[OGLAssetService]
  Enabled = true
  Mode = compatibility
  StorageBackend = s3
  MemoryCacheMiB = 0
  SingleWriterCacheEnabled = false
  DiskCacheEnabled = false
  RedisEnabled = false
  ExperimentalAtomicUpdatesEnabled = false
  ParallelStoreEnabled = false
[OGLS3Storage]
  Enabled = true
  Endpoint = $OGL_S3_CI_ENDPOINT
  Bucket = $OGL_S3_CI_BUCKET
  Region = us-east-1
  ForcePathStyle = true
  UseSSL = false
  ConnectionTimeoutSeconds = 2
  RequestTimeoutSeconds = 8
  MaxRetryAttempts = 2
  HealthCheckEnabled = false
[OGLAssetServiceAdmin]
  Enabled = false
EOF

start_robust() {
  (cd bin && exec dotnet Robust.dll -inifile Robust.OGLS3Smoke.Tests.ini) > "$WORK/robust.log" 2>&1 &
  ROBUST_PID=$!
}
assert_ready() {
  local ready=0
  for _ in $(seq 1 120); do
    if grep -Fq '[OGL-ASSETS]: OGL AssetService geladen; authoritative=S3;' "$WORK/robust.log"; then
      if python3 - <<'PY'
import socket
with socket.create_connection(("127.0.0.1", 19496), timeout=1):
    pass
PY
      then ready=1; break; fi
    fi
    if ! kill -0 "$ROBUST_PID" 2>/dev/null; then
      echo "::error::Isolated S3 ROBUST failed startup"
      grep -Ei 'OGL-ASSETS|S3|Exception|ERROR|Plugin' "$WORK/robust.log" | head -n 40 || true
      exit 1
    fi
    sleep 0.5
  done
  if [ "$ready" -ne 1 ]; then
    echo "::error::Isolated S3 ROBUST did not become ready"
    tail -n 45 "$WORK/robust.log" || true
    exit 1
  fi
}

start_robust
assert_ready
python3 tools/ci/verify_ogl_s3_http_persistence.py write "$WORK/fixtures.json" "$WORK/fsassets/data"
if find "$WORK/fsassets/tmp" -name '*.asset' | grep -q .; then
  echo "::error::S3 mode unexpectedly wrote FSAssets spool payloads"
  exit 1
fi

# A NEW ROBUST process must read the same UUIDs+metadata from SQL and
# exactly the same immutable bytes from S3; no in-memory cache state.
kill "$ROBUST_PID"
wait "$ROBUST_PID" 2>/dev/null || true
ROBUST_PID=""
: > "$WORK/robust.log"
start_robust
assert_ready
python3 tools/ci/verify_ogl_s3_http_persistence.py read "$WORK/fixtures.json" "$WORK/fsassets/data"

echo 'OGL S3 isolated ROBUST HTTP + MariaDB metadata + MinIO readback/restart PASS'
