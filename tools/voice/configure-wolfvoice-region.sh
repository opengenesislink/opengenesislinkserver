#!/usr/bin/env bash
# SPDX-License-Identifier: MPL-2.0
# Opt-in configuration for one OpenGenesisLINK simulator process.
# No network downloads, no service restarts, no access to secret contents.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
URL="${1:-}"
TARGET="${2:-$ROOT/bin/config/wolfvoice.ini}"
RPC_KEY="${WOLFVOICE_RPC_KEY_FILE:-/etc/opensim/voice_rpc_key}"
CRED_KEY="${WOLFVOICE_CRED_KEY_FILE:-/etc/opensim/voice_cred_key}"

if [[ ! "$URL" =~ ^https://[A-Za-z0-9][A-Za-z0-9.-]*(:[0-9]{1,5})?/?$ ]]; then
  echo "Usage: $0 https://voice.example.org:9443 [target/wolfvoice.ini]" >&2
  echo "Requires HTTPS with a matching, publicly verifiable certificate." >&2
  exit 2
fi
if [[ "$URL" == *example.* ]]; then
  echo "Replace the example host with the real wolfvoice TLS hostname." >&2
  exit 2
fi

for dll in WebRtcVoice.dll WebRtcVoiceRegionModule.dll WebRtcVoiceServiceModule.dll; do
  [[ -s "$ROOT/bin/$dll" ]] || {
    echo "Missing addon assembly $dll; run install-wolfvoice-region-addon.sh first." >&2
    exit 1
  }
done

for key in "$RPC_KEY" "$CRED_KEY"; do
  [[ -r "$key" && -s "$key" ]] || {
    echo "Voice key not readable/empty: $key (not printing its contents)." >&2
    exit 1
  }
done

# No silent overwrite of an existing configured voice backend.
if [[ -e "$TARGET" ]]; then
  echo "Refusing to overwrite existing voice config: $TARGET" >&2
  exit 1
fi
mkdir -p "$(dirname "$TARGET")"
umask 027
cat > "$TARGET" <<EOF
; OpenGenesisLINK Generation 1 - wolfvoice spatial backend (opt-in).
; Hardened os-webrtc-janus addon required; ensure voice allowed on estate/parcel.
; This affects every region hosted by this simulator process.

[OGLVoiceViewer]
    ; The built-in OGLVoice and wolfvoice addons MUST NOT both own Firestorm CAPS.
    Enabled = false

[WebRtcVoice]
    Enabled = true
    SpatialVoiceService = WebRtcVoice.dll:WebRtcVoiceServiceConnector
    NonSpatialVoiceService = WebRtcVoice.dll:WebRtcVoiceServiceConnector
    WebRtcVoiceServerURI = $URL
    RpcKeyFile = $RPC_KEY
    CredKeyFile = $CRED_KEY
    PositionFeed = true
    MessageDetails = false
    AcceptLegacyCallCredentials = false
    StunServers = stun:stun.l.google.com:19302,stun:stun1.l.google.com:19302

[VivoxVoice]
    enabled = false
EOF

echo "Wrote $TARGET without embedding key contents."
echo "Check HTTPS, TCP 9443 (region hosts only), UDP 40000-40999, and estate/parcel voice flags."
echo "Restart this simulator only when ready; no server was restarted by this script."
