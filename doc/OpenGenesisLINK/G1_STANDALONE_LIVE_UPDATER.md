# OpenGenesisLINK Generation 1 – unabhängiger Node Live-Updater

**Status:** Opt-in und ausschließlich für Debian/Linux-Systemd. Die vorhandene `stage_update`-Funktion bleibt unverändert und verändert weiterhin keine Live-Dateien. Die neue Aktion `deploy_update` darf dagegen den aktiven Checkout zurücksetzen und `bin` neu kompilieren.

## Architektur

```
ROBUST Webinterface (Simulator-Nodes / Update)
    │ POST /api/v1/nodes/{nodeId}/control
    │ {"action":"deploy_update",
    │  "confirm":"live_git_reset_and_manual_restart"}
    │   Auth: simulators:manage
    ▼
ROBUST NexNodeApi
    │ separater NEXVERSE_UPDATE_CONTROL_KEY + 5-Minuten-HMAC
    │ NexBus: node.update.deploy.requested
    ▼
Simulator NodeAgent (validiert: Node-ID / Replay / Zeitfenster / HMAC)
    │ POST http://127.0.0.1:8097/v1/update
    │ (keine frei wählbaren Shell-Befehle, Pfade oder URLs)
    ▼
Separater Python-Systemd-Dienst (läuft unabhängig von OpenSim.dll)
    │ Git-Checkout aus OGL_UPDATER_BIN=.../bin ableiten
    ├─ config/Regions/Estates-Backup außerhalb Checkout
    ├─ bin/regions/NexVerse-Deutschland.ini (bzw. bin/Regions) löschen
    ├─ git fetch origin main und git reset --hard origin/main
    ├─ Configs zurückspielen, Deutschland-INI entfernt lassen
    ├─ ./runprebuild.sh + .NET10-Checks + ./compile.sh
    └─ DLL-Integritätscheck, persistenten Abschlussstatus speichern
    ▲
    └── NodeAgent pollt signiertes /v1/status,
        publiziert node.update.deploy.{accepted,running,completed,failed}
        → ROBUST zeigt Jobstatus und Neustarthinweis
```

## Einmalige Installation der eigenständigen Komponente

Auf **dem Simulatorhost**, auf dem `bin/OpenSim.dll` liegt, im aktualisierten Repository ausführen:

```bash
sudo bash tools/ops/install-ogl-node-live-updater.sh /opt/robust/bin NODE_ID deployer
```

Dabei `NODE_ID` durch den tatsächlichen Wert aus `[NexVerseNodeAgent] NodeId` und `deployer` durch einen **vorhandenen unprivilegierten Linux-Benutzer** ersetzen, der Schreibrechte am Git-Checkout besitzt. Das Installationsskript erstellt den Dienst root-eigenständig unter `/opt/opengenesislink-updater/`, seinen Datenordner unter `/var/lib/opengenesislink-updater/` und Backup-Basis unter `/opt/ogl-backups/`. Es startet und aktiviert ihn **nicht automatisch**.

**Nicht als root ausführen:** Der unabhängige Systemd-Dienst soll mit einem dedizierten Nicht-Root-Konto laufen. Der Benutzer benötigt Schreibrechte am Root-Checkout und `bin`, Git, Python3, tar sowie .NET-10-SDK und Runtime. Auf eigenen gesperrten Deploy-Checkout umstellen, wenn deine aktuelle Installation root gehört. Besonders beim Bau gegen eine **laufende** Instanz besteht ein Risiko für inkonsistente Live-Dateien; zuerst auf einer Test-Node prüfen und danach einen Wartungszeitraum wählen.

In der nur lokal lesbaren `/etc/opengenesislink/updater.env`:

```ini
OGL_UPDATER_ENABLE=1
OGL_UPDATER_NODE_ID=NODE_ID
OGL_UPDATER_BIN=/opt/robust/bin
OGL_UPDATER_BIND=127.0.0.1
OGL_UPDATER_PORT=8097
OGL_UPDATER_STATE=/var/lib/opengenesislink-updater
OGL_UPDATER_BACKUP_DIR=/opt/ogl-backups
NEXVERSE_UPDATE_CONTROL_KEY=NEUER_EIGENER_ZUFALLSSCHLUESSEL_MINDESTENS_32_BYTES
```

Der `NEXVERSE_UPDATE_CONTROL_KEY` muss auf **ROBUST**, im **NodeAgent-Prozess** und im **Updater-Dienst** gleich sein; es ist **nicht** `NEXVERSE_NEXBUS_SHARED_KEY`. Zum Erzeugen beispielsweise:

```bash
openssl rand -hex 32
```

Dieselben Key-Bytes in die geschützten Environment-Dateien übernehmen. Keine Secrets ins Repository einchecken. Danach:

```bash
sudo chmod 600 /etc/opengenesislink/updater.env
sudo systemctl enable --now opengenesislink-node-updater
sudo systemctl status opengenesislink-node-updater
curl -fsS http://127.0.0.1:8097/healthz
```

Der Dienst bindet **ausschließlich** an 127.0.0.1 (alternativ explizite IPv6-Loopback-IP), niemals öffentlich. Keine Portweiterleitung nach außen.

In der aktiven `bin/OpenSim.ini`:

```ini
[NexVerseNodeAgent]
    Enabled = true
    LiveUpdaterEnabled = true
    LiveUpdaterUrl = "http://127.0.0.1:8097"
```

`UpdateStagingEnabled` kann weiterhin false sein. Beide Funktionen sind getrennt. Falls ROBUST und Simulator auf **verschiedenen Hosts** laufen, erfolgt der Auftrag weiterhin über den vorhandenen signierten NexBus zum Simulator; **nicht** durch direkten HTTP-Zugriff von ROBUST auf Port 8097. Nach neuer NodeAgent-Konfiguration den Simulator einmal manuell neu starten.

## Bedienung und Fortschritt

1. Im **World-API-/ROBUST-Webinterface** mit Administrator-Token anmelden (berechtigt für `simulators:manage`).
2. **Simulator-Nodes** öffnen, Node auswählen; nur online Nodes mit `LiveUpdaterEnabled=true` bieten eine aktive **Update**-Schaltfläche.
3. Sicherheitsbestätigung lesen und bestätigen. ROBUST erzeugt genau einen frischen signierten Auftrag pro Klick.
4. Die API antwortet **202 Accepted** nur für den signierten Steuerauftrag. Der eigentliche Updater meldet **accepted / running / completed / failed** nach Prüfung und Ausführung.
5. Nach **completed**: OpenSim (und erforderlichenfalls ROBUST) **manuell** neu starten. Kein automatischer Restart der Prozesse. Die echte Aktualisierung ist erst nach dem Neustart geladen.

Aus Sicherheitsgründen existiert **keine** REST-Route zum freien Ausführen von Shell-Kommandos, Setzen anderer Git-Remotes, Ändern des Build-Skripts oder Auswählen weiterer Dateien. Der Root-Pfad stammt nur aus der geschützten Systemd-Konfiguration. `git reset --hard origin/main` verwirft alle tracked lokalen Git-Änderungen, die **nicht** zu gesicherten INIs/Regions/Estates gehören; vorher manuell aufräumen. Der Backup-Pfad wird im privaten Updater-Log ausgegeben. Ein Backup ist kein automatisches Rollback, und beim Fehler während des Builds können Dateien bereits aktualisiert sein.

## Diagnose

```bash
sudo journalctl -u opengenesislink-node-updater -n 100 --no-pager
sudo cat /var/lib/opengenesislink-updater/status.json
sudo ls -l /var/lib/opengenesislink-updater/*.log
```

Die Logdateien und der Zustand sind nur für den Dienstbenutzer und Administratoren lesbar. Der Status des unabhängigen Dienstes bleibt auch verfügbar, wenn OpenSim beendet oder später neu gestartet wird; die Anzeige im ROBUST-Webinterface ist abhängig vom laufenden NodeAgent und dessen Verbindung.

## Test und CI

```bash
python3 -m unittest -v tools/ci/test_ogl_live_updater.py
bash -n tools/ops/ogl-node-live-update.sh
```

Die Regressionen verwenden einen **Fake-Checkout** und gemockte Git-/dotnet-Befehle, niemals einen realen GitHub-Reset. Die C# `NexBusRegression` prüft zusätzlich, dass eine alte Stage-HMAC-Signatur keinen Live-Deploy auslösen kann.

## Wichtige Abgrenzung

Diese Funktion ist **kein unterbrechungsfreier Hot-Swap**: bei einer laufenden Simulatorinstanz kann das Überschreiben von Assemblies im `bin`-Ordner bis zum manuellen Neustart problematisch sein. Vor Produktivfreigabe echte Tests mit Regionen, Inventar, TrixieStorm, Offline-IM und eigenem Backup-Restore durchführen.
