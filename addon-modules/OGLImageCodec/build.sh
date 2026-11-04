#!/usr/bin/env bash
set -euo pipefail

# G1.1 opt-in native hybrid: build on the target Linux architecture.
# Does not replace or modify the existing OpenMetaverse/libopenjpeg-dotnet binary.
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
cd "$ROOT"

[[ "$(uname -s)" == "Linux" ]] || { echo "Linux native build only" >&2; exit 2; }
case "$(uname -m)" in
  x86_64) target="libogl-openjpeg2-x86_64.so" ;;
  aarch64) target="libogl-openjpeg2-arm64.so" ;;
  *) echo "Unsupported architecture $(uname -m)" >&2; exit 2 ;;
esac

command -v pkg-config >/dev/null || { echo "pkg-config required" >&2; exit 2; }
pkg-config --atleast-version=2.5 libopenjp2 || {
  echo "OpenJPEG 2.5+ headers and runtime required (libopenjp2-dev)" >&2; exit 2;
}
mkdir -p bin/lib64
"${CC:-cc}" -std=c11 -O2 -Wall -Wextra -Werror -fPIC -fvisibility=hidden \
  $(pkg-config --cflags libopenjp2) \
  -shared addon-modules/OGLImageCodec/native/ogl_openjpeg2.c \
  -o "bin/lib64/$target" \
  $(pkg-config --libs libopenjp2)
test -s "bin/lib64/$target"
echo "G1.1 OpenJPEG2 inspection shim: bin/lib64/$target (OpenJPEG $(pkg-config --modversion libopenjp2))"
