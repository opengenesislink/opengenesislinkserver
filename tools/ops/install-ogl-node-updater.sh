#!/usr/bin/env bash
# SPDX-License-Identifier: MPL-2.0
set -Eeuo pipefail
umask 077
# Install only trusted, local repository files. Never automatically enable
# a privileged update service before the operator configured the token/user.
if [[ "${EUID}" -ne 0 ]]; then
    echo "Must run installation as root." >&2
    exit 1
fi
cd "$(dirname "${BASH_SOURCE[0]}")/../.."
install -d -m 0755 /usr/local/libexec/ogl-node-updater
install -m 0755 tools/ops/ogl-node-updater.py /usr/local/libexec/ogl-node-updater/ogl-node-updater.py
install -m 0755 tools/ops/ogl-node-update-live.sh /usr/local/libexec/ogl-node-updater/ogl-node-update-live.sh
if ! id oglupdate >/dev/null 2>&1; then
    useradd --system --home-dir /var/lib/ogl-node-updater --shell /usr/sbin/nologin oglupdate
fi
install -d -o oglupdate -g oglupdate -m 0700 /var/lib/ogl-node-updater
install -d -o oglupdate -g oglupdate -m 0700 /opt/ogl-backups
install -d -m 0755 /etc/opengenesislink
if [[ ! -e /etc/opengenesislink/ogl-node-updater.env ]]; then
    install -m 0600 tools/ops/ogl-node-updater.env.example /etc/opengenesislink/ogl-node-updater.env
    echo "Vorlage erstellt: /etc/opengenesislink/ogl-node-updater.env"
else
    echo "Vorhandene Updater-Konfiguration bleibt unverändert."
fi
install -m 0644 tools/ops/ogl-node-updater.service /etc/systemd/system/ogl-node-updater.service
systemctl daemon-reload
echo "Updater-Skripte sind außerhalb von bin und außerhalb des Git-Checkout installiert."
echo "Noch NICHT gestartet. Dienstkonto oglupdate und Repo-Berechtigungen,"
echo "Token, Node-ID und Konfigurationspfade prüfen, dann manuell systemctl enable --now ogl-node-updater."
