#!/usr/bin/env bash
# OpenGenesisLINK Generation 1 standalone updater (outside bin).
set -Eeuo pipefail
umask 077
export GIT_TERMINAL_PROMPT=0 DOTNET_CLI_TELEMETRY_OPTOUT=1
trap 'echo "FEHLER: Update abgebrochen (Zeile $LINENO); Backup prüfen." >&2' ERR
if [[ $# -ne 2 ]]; then echo "Usage: updater <checkout-root> <backup-root>" >&2; exit 64; fi
root="$(realpath -e -- "$1")"
backup_base="$(realpath -m -- "$2")"
if [[ "$root" == "/" || ! -d "$root/.git" || ! -f "$root/bin/OpenSim.dll" ||
      ! -f "$root/runprebuild.sh" || ! -f "$root/compile.sh" ||
      "$backup_base" == "$root" || "$backup_base" == "$root/"* ||
      "$root" == "$backup_base/"* || -L "$root/bin" || -L "$backup_base" ]]; then
  echo "FEHLER: Ungültiger Checkout oder Backup-Pfad." >&2; exit 65
fi
cd "$root"
echo "=========================================="
echo " OpenGenesisLINK Generation 1 · .NET 10"
echo " Live-Update, manueller Simulator-Restart"
echo "=========================================="
echo "[1/7] Voraussetzungen prüfen"
[[ "$(git branch --show-current)" == "main" ]] || {
  echo "FEHLER: Repository muss auf main stehen." >&2; exit 65;
}
origin="$(git remote get-url origin)"
case "$origin" in
  "https://github.com/opengenesislink/opengenesislinkserver.git" | \
  "https://github.com/opengenesislink/opengenesislinkserver" | \
  "git@github.com:opengenesislink/opengenesislinkserver.git" | \
  "git@github.com:opengenesislink/opengenesislinkserver") ;;
  *) echo "FEHLER: Ungültiger Origin." >&2; exit 65 ;;
esac
for cmd in git tar python3 dotnet; do command -v "$cmd" >/dev/null || {
  echo "FEHLER: $cmd fehlt." >&2; exit 65; }; done
dotnet --list-sdks | grep -q '^10\.' || { echo ".NET 10 SDK fehlt" >&2; exit 65; }
dotnet --list-runtimes | grep -q '^Microsoft.NETCore.App 10\.' || {
  echo ".NET 10 Runtime fehlt" >&2; exit 65; }
echo ".NET 10 vorhanden."
echo "[2/7] Serverkonfiguration sichern"
mkdir -p -- "$backup_base"
backup="$(mktemp -d -- "$backup_base/$(date -u +%Y%m%d-%H%M%S)-XXXXXX")"
echo "OGL_UPDATE_BACKUP=$backup"
entries=()
shopt -s nullglob
for item in bin/*.ini bin/config-include bin/Regions bin/regions bin/Estates; do
  if [[ -e "$item" || -L "$item" ]]; then entries+=("$item"); fi
done
shopt -u nullglob
if ((${#entries[@]})); then
  tar -czf "$backup/configs.tar.gz" -- "${entries[@]}"
fi
git status --short > "$backup/git-status.txt"
git rev-parse HEAD > "$backup/previous-commit.txt"
# Delete only the precisely requested Germany region, in either case variant.
for region_dir in bin/regions bin/Regions; do
  target="$region_dir/NexVerse-Deutschland.ini"
  if [[ -L "$region_dir" || -L "$target" ]]; then
    echo "FEHLER: Region-Symlink abgewiesen." >&2; exit 65
  fi
  if [[ -f "$target" ]]; then rm -- "$target"; echo "Entfernt: $target"; fi
done
echo "[3/7] Aktuellen GitHub-Stand holen"
git -c protocol.file.allow=never fetch --no-tags origin main
git reset --hard origin/main
echo "Commit: $(git rev-parse --short HEAD)"
# Restore local configs after tracked sources reset; the deleted INI stays gone.
if [[ -f "$backup/configs.tar.gz" ]]; then
  tar -xzf "$backup/configs.tar.gz" -C "$root" --no-same-owner
fi
for region_dir in bin/regions bin/Regions; do
  target="$region_dir/NexVerse-Deutschland.ini"
  if [[ -L "$region_dir" || -L "$target" ]]; then
    echo "FEHLER: Region-Symlink abgewiesen." >&2; exit 65
  fi
  [[ ! -f "$target" ]] || rm -- "$target"
done
echo "Eigene INI-/Regions-/Estate-Konfiguration wiederhergestellt."
echo "NexVerse-Deutschland.ini bleibt entfernt."
echo "[4/7] .NET-10-Projektdateien generieren"
bash runprebuild.sh
echo "[5/7] .NET-10-Scope validieren"
python3 tools/gen11/verify_net10.py
python3 tools/ci/verify_gen1_main_net10_scope.py
echo "[6/7] ROBUST, OpenSim und Addons kompilieren"
bash compile.sh
echo "[7/7] Kompilierte DLLs prüfen"
for name in Robust OpenSim; do
  test -s "bin/$name.dll" || { echo "FEHLER: $name.dll fehlt" >&2; exit 1; }
  echo "OK: $name.dll"
done
for addon in NexTraffic NexTransit NexSmartLight OGLWeather OGLEnergy; do
  file="bin/OpenGenesisLINK.Addons.$addon.dll"
  test -s "$file" || { echo "FEHLER: $addon fehlt" >&2; exit 1; }
  echo "OK: $addon"
done
echo "=========================================="
echo " UPDATE ERFOLGREICH · .NET 10"
echo " Commit: $(git rev-parse --short HEAD)"
echo " Backup: $backup"
echo " Simulator/ROBUST müssen MANUELL neu gestartet werden."
echo "=========================================="
