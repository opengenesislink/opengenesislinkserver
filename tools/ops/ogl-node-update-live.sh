#!/usr/bin/env bash
# SPDX-License-Identifier: MPL-2.0
# Trusted standalone updater payload. The HTTP request NEVER supplies paths,
# commands, remotes or branches. Installed outside the mutable Git checkout.
set -Eeuo pipefail
umask 077
export GIT_TERMINAL_PROMPT=0
export DOTNET_CLI_TELEMETRY_OPTOUT=1

trap 'echo "FEHLER: Update abgebrochen (Zeile $LINENO)." >&2' ERR

if [[ $# != 2 || -z "$1" || -z "$2" ]]; then
    echo "Aufruf: ogl-node-update-live.sh <repo-root> <backup-root>" >&2
    exit 64
fi
ROOT="$(realpath -e -- "$1")"
BACKUP_ROOT="$(realpath -m -- "$2")"
if [[ "$ROOT" == / || "$BACKUP_ROOT" == / ||
      "$BACKUP_ROOT" == "$ROOT" || "$BACKUP_ROOT" == "$ROOT/"* ||
      "$ROOT" == "$BACKUP_ROOT/"* || -L "$1" || -L "$2" ||
      ! -d "$ROOT/.git" || ! -s "$ROOT/bin/OpenSim.dll" ||
      ! -f "$ROOT/runprebuild.sh" || ! -f "$ROOT/compile.sh" ]]; then
    echo "FEHLER: Unsicherer Repository-/Backup-Pfad oder OpenSim.dll fehlt." >&2
    exit 65
fi
if [[ "$(basename -- "$ROOT/bin")" != bin ||
      -L "$ROOT/bin" ]]; then
    echo "FEHLER: bin muss ein reales Unterverzeichnis sein." >&2
    exit 65
fi
cd "$ROOT"
if [[ "$(git rev-parse --show-toplevel)" != "$ROOT" ||
      "$(git branch --show-current)" != main ]]; then
    echo "FEHLER: OpenGenesisLINK Checkout muss genau auf Branch main liegen." >&2
    exit 65
fi
remote="$(git remote get-url origin)"
case "$remote" in
    https://github.com/opengenesislink/opengenesislinkserver|https://github.com/opengenesislink/opengenesislinkserver.git|git@github.com:opengenesislink/opengenesislinkserver.git)
        ;;
    *)
        echo "FEHLER: Git origin ist nicht das erlaubte OpenGenesisLINK Repository." >&2
        exit 65
        ;;
esac

# Lock lives outside the git checkout and survives git reset --hard.
mkdir -p -- "$BACKUP_ROOT"
exec 9>"$BACKUP_ROOT/.ogl-live-update.lock"
if ! flock -n 9; then
    echo "FEHLER: Ein anderes Live-Update ist bereits aktiv." >&2
    exit 66
fi

echo "========================================"
echo " OpenGenesisLINK Generation 1"
echo " Direktes Update auf .NET 10"
echo "========================================"
echo
echo "[1/7] Voraussetzungen prüfen..."
for command in git dotnet python3 tar flock realpath; do
    command -v "$command" >/dev/null || {
        echo "FEHLER: Werkzeug fehlt: $command" >&2; exit 69;
    }
done
dotnet --list-sdks | grep -q '^10\.' ||
    { echo "FEHLER: .NET 10 SDK fehlt."; exit 69; }
dotnet --list-runtimes | grep -q '^Microsoft.NETCore.App 10\.' ||
    { echo "FEHLER: .NET 10 Runtime fehlt."; exit 69; }

echo "[2/7] Serverkonfiguration sichern..."
BACKUP_DIR="$(mktemp -d -- "$BACKUP_ROOT/$(date -u +%Y%m%d-%H%M%S)-XXXXXXXX")"
printf '%s\n' "$BACKUP_DIR" > "$BACKUP_ROOT/latest-backup-path"
git status --short > "$BACKUP_DIR/git-status.txt"
git diff HEAD --binary > "$BACKUP_DIR/local-changes.patch"
git rev-parse HEAD > "$BACKUP_DIR/previous-commit.txt"

BACKUP_FILES=()
shopt -s nullglob
for entry in bin/*.ini bin/config-include bin/Regions bin/regions bin/Estates; do
    [[ -e "$entry" ]] && BACKUP_FILES+=("$entry")
done
if (( ${#BACKUP_FILES[@]} > 0 )); then
    tar -czf "$BACKUP_DIR/configs.tar.gz" -- "${BACKUP_FILES[@]}"
fi
# Backup of the old Germany INI is retained for operator recovery,
# but it must NOT be restored into the updated installation.
for region_dir in bin/Regions bin/regions; do
    if [[ -f "$region_dir/NexVerse-Deutschland.ini" &&
          ! -L "$region_dir/NexVerse-Deutschland.ini" ]]; then
        rm -f -- "$region_dir/NexVerse-Deutschland.ini"
        echo "Entfernt: $region_dir/NexVerse-Deutschland.ini"
    fi
done
echo "Backup: $BACKUP_DIR"

echo "[3/7] Aktuellen GitHub-Stand holen..."
git fetch --no-tags origin main
# A local checkout can contain tracked customizations. The operator explicitly
# requested reset --hard; local diffs and active INIs were backed up above.
git reset --hard origin/main
echo "Commit: $(git log -1 --format='%h %s')"
# git reset can re-create tracked INIs; restore only old runtime configs, never
# git source, then remove the obsolete Germany file again.
if [[ -f "$BACKUP_DIR/configs.tar.gz" ]]; then
    # Preserve active operator INIs, not stale versioned templates/defaults.
    # New generation defaults must come from origin/main.
    tar -xzf "$BACKUP_DIR/configs.tar.gz" -C "$ROOT" \
        --exclude='bin/OpenSimDefaults.ini' \
        --exclude='bin/*.example.ini' \
        --exclude='bin/*.example'
fi
for region_dir in bin/Regions bin/regions; do
    if [[ -f "$region_dir/NexVerse-Deutschland.ini" &&
          ! -L "$region_dir/NexVerse-Deutschland.ini" ]]; then
        rm -f -- "$region_dir/NexVerse-Deutschland.ini"
    fi
done

echo "[4/7] Projektdateien für .NET 10 generieren..."
bash ./runprebuild.sh

echo "[5/7] .NET-10-Konfiguration überprüfen..."
python3 tools/gen11/verify_net10.py
python3 tools/ci/verify_gen1_main_net10_scope.py

echo "[6/7] ROBUST, OpenSim und Addons kompilieren..."
bash ./compile.sh

echo "[7/7] Kompilierte Dateien überprüfen..."
for name in Robust OpenSim; do
    test -s "bin/${name}.dll" ||
        { echo "FEHLER: bin/${name}.dll fehlt!"; exit 70; }
    echo "OK: ${name}.dll"
done
for addon in NexTraffic NexTransit NexSmartLight OGLWeather OGLEnergy; do
    file="bin/OpenGenesisLINK.Addons.${addon}.dll"
    test -s "$file" ||
        { echo "FEHLER: ${addon} fehlt!"; exit 70; }
    echo "OK: ${addon}"
done
printf '%s\n' "$(git rev-parse HEAD)" > "$BACKUP_DIR/updated-commit.txt"
echo "========================================"
echo " UPDATE ERFOLGREICH – Neustart manuell"
echo "========================================"
echo "Branch: main"
echo "Commit: $(git rev-parse --short HEAD)"
echo "Backup: $BACKUP_DIR"
echo "Simulator NICHT automatisch neu gestartet."
