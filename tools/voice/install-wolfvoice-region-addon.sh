#!/usr/bin/env bash
# SPDX-License-Identifier: MPL-2.0
# Optional, operator-triggered OpenGenesisLINK / wolfvoice integration.
# Pins the hardened os-webrtc-janus region addon; never installs the Rust
# voice server or modifies a running region without explicit operator action.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
ADDON_DIR="$ROOT/addon-modules/os-webrtc-janus"
SOURCE_REPO="https://github.com/intelligentwolf/os-webrtc-janus.git"
SOURCE_COMMIT="7f08ce453fc5938beea12c452348d46b3ed1088e"
BUILD_MODE="${1:-build}"

if [[ "$BUILD_MODE" != "build" && "$BUILD_MODE" != "fetch" && "$BUILD_MODE" != "check" ]]; then
  echo "Usage: bash tools/voice/install-wolfvoice-region-addon.sh [fetch|build|check]" >&2
  exit 2
fi

cd "$ROOT"
for bin in git dotnet; do
  command -v "$bin" >/dev/null || { echo "Missing prerequisite: $bin" >&2; exit 1; }
done

# This repository includes add-ons via addon-modules/*/prebuild*.xml.
# A second in-tree copy would produce conflicting projects/cap handlers.
if [[ -d OpenSim/Addons/os-webrtc-janus || -d Addons/os-webrtc-janus ]]; then
  echo "Conflicting in-tree os-webrtc-janus found; refusing duplicate addon." >&2
  exit 1
fi

if [[ -e "$ADDON_DIR" ]]; then
  if [[ ! -d "$ADDON_DIR/.git" ]]; then
    echo "Existing addon directory is not a Git checkout: $ADDON_DIR" >&2
    exit 1
  fi
  current="$(git -C "$ADDON_DIR" rev-parse HEAD)"
  if [[ "$current" != "$SOURCE_COMMIT" ]]; then
    echo "Existing addon has unexpected revision $current; do not overwrite it." >&2
    exit 1
  fi
  if [[ -n "$(git -C "$ADDON_DIR" status --porcelain)" ]]; then
    echo "Addon checkout has local changes; refusing to continue." >&2
    exit 1
  fi
elif [[ "$BUILD_MODE" == "check" ]]; then
  echo "Addon not fetched: $ADDON_DIR" >&2
  exit 1
else
  echo "Fetching pinned hardened addon $SOURCE_COMMIT ..."
  git clone --no-checkout "$SOURCE_REPO" "$ADDON_DIR"
  git -C "$ADDON_DIR" checkout --detach "$SOURCE_COMMIT"
fi

for file in prebuild-webrtc.xml prebuild-regionmodule.xml prebuild-servicemodule.xml; do
  test -f "$ADDON_DIR/$file" || { echo "Missing $file" >&2; exit 1; }
done

if [[ "$BUILD_MODE" == "fetch" ]]; then
  echo "Pinned wolfvoice region addon fetched, not built."
  exit 0
fi

if [[ "$BUILD_MODE" == "build" ]]; then
  # Regen solution locally; this can touch generated .csproj/.sln files.
  # Operator must review 'git status' before committing anything.
  ./runprebuild.sh
  dotnet build --configuration Release OpenSim.sln
fi

for dll in WebRtcVoice.dll WebRtcVoiceRegionModule.dll WebRtcVoiceServiceModule.dll; do
  test -s "$ROOT/bin/$dll" || {
    echo "Missing $ROOT/bin/$dll; addon was not built successfully." >&2
    exit 1
  }
done

echo "wolfvoice region addon ready at pinned revision $SOURCE_COMMIT."
echo "Only activate after checking TLS, voice keys, and OGLVoiceViewer conflicts."
echo "Next: bash tools/voice/configure-wolfvoice-region.sh https://VOICE-HOST:9443"
