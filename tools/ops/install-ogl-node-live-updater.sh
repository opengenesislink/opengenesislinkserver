#!/usr/bin/env bash
# Install the standalone service OUTSIDE the OpenGenesisLINK source checkout.
# Does NOT enable or start the update service; operator must configure secrets.
set -Eeuo pipefail
umask 077
if [[ $# -ne 3 || "$EUID" -ne 0 ]]; then
  echo "Usage: sudo bash install-ogl-node-live-updater.sh /opt/robust/bin NODE_ID NONROOT_USER" >&2
  exit 64
fi
bin="$(realpath -e "$1")"
node="$2"
user="$3"
[[ "$bin" == */bin && -f "$bin/OpenSim.dll" ]] || {
  echo "ERROR: OpenSim.dll not found in requested bin folder." >&2; exit 65; }
[[ "$node" =~ ^[A-Za-z0-9][A-Za-z0-9._-]{0,63}$ ]] || {
  echo "ERROR: Invalid Node ID." >&2; exit 65; }
[[ "$user" =~ ^[a-z_][a-z0-9_-]*$ && "$user" != root ]] || {
  echo "ERROR: A named NON-ROOT service account is required." >&2; exit 65; }
id "$user" >/dev/null
root="$(realpath -e "$bin/..")"
script_dir="$(cd -- "$(dirname -- "$0")" && pwd -P)"
install_dir="/opt/opengenesislink-updater"
config_dir="/etc/opengenesislink"
data_dir="/var/lib/opengenesislink-updater"
backups="/opt/ogl-backups"
if [[ "$root" == "$install_dir" || "$install_dir" == "$root/"* ]]; then
  echo "ERROR: Updater must not be inside the checkout." >&2; exit 65; fi

# The daemon and its script must be root-owned/immutable to its service user.
install -d -m 0755 -o root -g root "$install_dir"
install -m 0644 -o root -g root "$script_dir/ogl-node-live-updater.py" \
  "$install_dir/ogl-node-live-updater.py"
install -m 0644 -o root -g root "$script_dir/ogl-node-live-update.sh" \
  "$install_dir/ogl-node-live-update.sh"
install -d -m 0700 "$config_dir"
install -d -m 0700 -o "$user" -g "$(id -gn "$user")" "$data_dir"
install -d -m 0700 -o "$user" -g "$(id -gn "$user")" "$backups"
if [[ ! -e "$config_dir/updater.env" ]]; then
  cat > "$config_dir/updater.env" <<EOF
OGL_UPDATER_ENABLE=0
OGL_UPDATER_NODE_ID=$node
OGL_UPDATER_BIN=$bin
OGL_UPDATER_BIND=127.0.0.1
OGL_UPDATER_PORT=8097
OGL_UPDATER_STATE=$data_dir
OGL_UPDATER_BACKUP_DIR=$backups
NEXVERSE_UPDATE_CONTROL_KEY=SET_THE_SAME_NEW_64_CHARACTER_RANDOM_SECRET_AS_ROBUST_AND_NODEAGENT
EOF
  chmod 0600 "$config_dir/updater.env"
else
  echo "Existing updater.env preserved; please verify node ID and bin path."
fi

group="$(id -gn "$user")"
# No shell interpolation from remote requests reaches this unit.
cat > /etc/systemd/system/opengenesislink-node-updater.service <<EOF
[Unit]
Description=OpenGenesisLINK Independent .NET10 Node Updater ($node)
After=network-online.target
Wants=network-online.target

[Service]
Type=simple
User=$user
Group=$group
EnvironmentFile=$config_dir/updater.env
WorkingDirectory=$install_dir
ExecStart=/usr/bin/python3 $install_dir/ogl-node-live-updater.py
Restart=on-failure
RestartSec=5
UMask=0077
NoNewPrivileges=yes
PrivateTmp=yes
ProtectSystem=strict
ProtectHome=yes
ReadWritePaths=$root $backups $data_dir
RestrictAddressFamilies=AF_INET AF_INET6 AF_UNIX
CapabilityBoundingSet=
AmbientCapabilities=
PrivateDevices=yes
ProtectKernelTunables=yes
ProtectKernelModules=yes
ProtectControlGroups=yes

[Install]
WantedBy=multi-user.target
EOF
chmod 0644 /etc/systemd/system/opengenesislink-node-updater.service
systemctl daemon-reload
echo "Independent updater INSTALLED for node $node; not activated or started."
echo "Configure $config_dir/updater.env (shared update key, ENABLE=1, chmod 600)."
echo "Ensure account '$user' can write checkout '$root' and run dotnet 10."
echo "Then: systemctl enable --now opengenesislink-node-updater"
echo "In active OpenSim.ini: [NexVerseNodeAgent] LiveUpdaterEnabled=true"
