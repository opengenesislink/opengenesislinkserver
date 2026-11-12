#!/usr/bin/env bash
# Serve this file as the HTTPS response at https://install.opengenesislink.de/
set -Eeuo pipefail
export LC_ALL=C

if (( EUID != 0 )); then
  printf 'Bitte als root ausfuehren (zuerst sudo -i).\n' >&2
  exit 1
fi
if ! command -v wget >/dev/null 2>&1; then
  printf 'GNU wget wird fuer die Installation benoetigt.\n' >&2
  exit 1
fi
if ! command -v python3 >/dev/null 2>&1; then
  if ! command -v apt-get >/dev/null 2>&1; then
    printf 'Python 3 fehlt und apt-get ist nicht vorhanden.\n' >&2
    exit 1
  fi
  apt-get update
  apt-get install -y python3 ca-certificates
fi

tempdir="$(mktemp -d)"
trap 'rm -rf -- "$tempdir"' EXIT
# On production hosting, publish a reviewed/pinned installer copy from a commit,
# not an unreviewed development branch.
url="https://raw.githubusercontent.com/opengenesislink/opengenesislinkserver/main/installer/install.py"
wget --https-only --quiet --output-document="$tempdir/install.py" "$url"
test -s "$tempdir/install.py"
python3 "$tempdir/install.py" "$@"
