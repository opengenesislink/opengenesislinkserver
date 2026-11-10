# OpenGenesisLINK G1 – unabhängiger Live-Update-Dienst

**Ziel:** Im World-API-Control-Center unter *Simulator-Nodes* die Schaltfläche **Update** klicken. ROBUST sendet daraufhin eine signierte NodeAgent-Anforderung. Der NodeAgent delegiert an einen **eigenständigen systemd-Dienst**, dessen ausführbarer Code außerhalb des Git-Checkouts, außerhalb von `bin` und außerhalb des laufenden OpenSim-Prozesses liegt. Dieser Dienst führt den gewünschten .NET-10-Updateablauf aus und überlebt ein manuelles Beenden der Simulatorinstanz.

## Was passiert bei „Update“?

1. Der ROBUST-Endpunkt `POST /api/v1/nodes/{nodeId}/control` mit `{"action":"update"}` verlangt `simulators:manage`; nur online beobachtete Nodes sind auswählbar.
2. ROBUST sendet über den authentifizierten NexBus ein gesondert signiertes, auf 5 Minuten begrenztes `node.update.live.requested` mit eigener HMAC-Signaturdomäne (nicht austauschbar mit Staging).
3. Der NodeAgent validiert NodeID, Operations-ID, Zeitfenster und getrennten Control-Key. Erst bei `LiveUpdateEnabled=true` kontaktiert er den lokalen Updater auf `127.0.0.1` mit einem **zweiten eigenen, mindestens 32 Bytes langen Service-Token**. Kein Benutzer kann einen Shell-Befehl, Repo-Pfad oder Git-Remote über HTTP angeben.
4. Der eigenständige Update-Dienst überprüft `<repo>/bin/OpenSim.dll`, `.git`, die lokale NodeID, sichert die bisherigen Konfigurationsdateien und beginnt den Build. Ein paralleles Update wird abgewiesen.
5. Der Live-Update-Ablauf nutzt nur das offizielle `opengenesislink/opengenesislinkserver`-Repository und dessen `main`-Branch: `git fetch origin main`, `git reset --hard origin/main`, `runprebuild.sh`, die zwei .NET-10-Verifikationsskripte und `compile.sh`.
6. Die bisherige lokale Konfiguration aus `bin/*.ini`, `bin/config-include`, `bin/Regions`/`bin/regions` und `bin/Estates` wird gesichert und nach dem Reset wiederhergestellt (ausgenommen versionierte `OpenSimDefaults.ini`- und Beispielvorlagen, die aus dem neuen `main` stammen müssen), damit der Reset keine aktiven Regionseinstellungen löscht. Backups liegen standardmäßig unter `/opt/ogl-backups` und damit **außerhalb** des Git-Checkouts.
7. `NexVerse-Deutschland.ini` wird sowohl vor dem Git-Reset als auch **nach der Wiederherstellung** aus `bin/Regions` bzw. `bin/regions` entfernt; ein Backup enthält die ursprüngliche Fassung für den Notfall. Nach dem Build werden `bin/Robust.dll`, `bin/OpenSim.dll` und die fünf angeforderten Addon-DLLs überprüft.

**Auslösegrenze:** Der erste ROBUST-Befehl benötigt einen **online** laufenden NodeAgent, weil er den signierten Auftrag an den unabhängigen Dienst weiterreicht. **Ab dessen Annahme** kann der eigenständige Dienst weiterarbeiten, auch wenn der Simulator beendet wird. Ein direkter Neuauftrag an eine schon offline befindliche Node über die World API ist in dieser ersten Integration noch nicht möglich.

**Kein automatischer Prozessneustart.** Die laufende OpenSim-Instanz kann noch alte Assemblies im Speicher haben. Nach erfolgreichem Update muss der Betreiber die OpenSim-Instanz manuell beenden und neu starten. Für ROBUST auf demselben Installationsbaum empfiehlt sich ein geplanter Wartungszeitraum mit entsprechendem Neustart.

## Einmalige Installation auf jeder Simulator-Node

Mit aktueller Repository-Version und Root-Rechten:

```bash
cd /opt/robust
sudo bash tools/ops/install-ogl-node-updater.sh
```

Die Installation kopiert die Skripte nach `/usr/local/libexec/ogl-node-updater/`, installiert die Unit `ogl-node-updater.service`, erzeugt bei Bedarf das Systemkonto `oglupdate` und die privaten Daten-/Backupverzeichnisse. Sie **startet den Dienst noch nicht** und überschreibt niemals eine vorhandene Konfiguration.

Den separaten Service konfigurieren:

```bash
sudo editor /etc/opengenesislink/ogl-node-updater.env
```

Beispiel für einen NodeID-Namen `Mainland_Server_001`:

```ini
OGL_UPDATER_NODE_ID=Mainland_Server_001
OGL_UPDATER_PORT=19090
OGL_UPDATER_REPO_ROOT=/opt/robust
OGL_UPDATER_BACKUP_ROOT=/opt/ogl-backups
OGL_UPDATER_STATE_DIR=/var/lib/ogl-node-updater
OGL_UPDATER_TOKEN=
```

Einen starken Service-Schlüssel erzeugen und **erst danach** hinter `OGL_UPDATER_TOKEN=` eintragen (die leere Beispielkonfiguration kann nicht starten):

```bash
openssl rand -hex 32
```

**Wichtig:** Das Dienstkonto `oglupdate` benötigt Schreibrechte auf das **vollständige Git-Checkout samt `bin`** und auf sein Backup-/Statusverzeichnis, sowie Leserechte für `dotnet`. Die Installation ändert aus Sicherheitsgründen **nicht stillschweigend** die Besitzrechte deines produktiven `/opt/robust`-Verzeichnisses. Sollte dein Server als ein anderer Benutzer betrieben werden, das Dienstkonto/Dateirechte bewusst anpassen; der Dienst darf nicht allein deshalb unter Root laufen, weil es bequemer ist.

In die **Simulator-Prozessumgebung** (nicht in Git und nicht in die öffentliche World API) denselben Service-Schlüssel unter dem *anderen Variablennamen* setzen:

```bash
OGL_NODE_UPDATER_TOKEN=DER_GLEICHE_WERT_WIE_OGL_UPDATER_TOKEN
```

Zusätzlich benötigen ROBUST und Simulator den schon vorhandenen, unabhängig davon zufälligen **`NEXVERSE_UPDATE_CONTROL_KEY`** (mindestens 32 UTF-8-Bytes), der die ROBUST-Update-Anforderungen signiert. NexBus muss korrekt konfiguriert sein.

Simulator-`bin/OpenSim.ini`:

```ini
[NexVerseNodeAgent]
    Enabled = true
    LiveUpdateEnabled = true
    LiveUpdateServiceUrl = "http://127.0.0.1:19090"
```

Nicht versehentlich die separate bestehende `UpdateStagingEnabled`-Funktion aktivieren oder deaktivieren – sie ist von Live-Updates unabhängig.

Sobald die **Dateirechte und Schlüssel** geprüft sind:

```bash
sudo systemctl enable --now ogl-node-updater.service
sudo systemctl status ogl-node-updater.service
sudo journalctl -u ogl-node-updater.service -n 60 --no-pager
```

Dienst nach geänderter EnvironmentFile-Konfiguration neu starten. Einmalige Aktivierung der NodeAgent-Konfiguration erfordert einen normalen Neustart der Simulatorinstanz; **nach künftigen Updates** erfolgt kein automatischer Neustart.

## Control-Center-Bedienung

In der ROBUST-World-API-Weboberfläche *Simulator-Nodes* öffnen, mittels eines Tokens mit `simulators:read` und `simulators:manage` authentifizieren, den gewünschten Online-Node auswählen und dort **Update** drücken. Eine Bestätigung weist auf `git reset --hard`, Backup, Entfernen der alten Germany-INI und manuellen Neustart hin. In der Detailansicht wird der Auftrag angezeigt. `GET /api/v1/nodes/{nodeId}` meldet unter `node.update.state` etwa `live.accepted`, `live.running`, `live.completed` oder `live.failed`. Ein `202 Accepted` bedeutet **nur**, dass ROBUST den Auftrag per NexBus eingestellt hat.

Auch direkt per REST:

```http
POST /api/v1/nodes/Mainland_Server_001/control
Authorization: Bearer <SIMULATORS_MANAGE_TOKEN>
Content-Type: application/json

{"action":"update"}
```

Der alte Endpunkt mit `{"action":"stage_update"}` bleibt weiterhin **nicht-destruktives Staging**, bei dem die aktive Installation unangetastet bleibt. `POST /api/v1/nodes/updates` startet weiterhin nur Staging auf allen Online-Nodes; für ein direktes Live-Update muss die Node individuell gewählt werden.

## Fehler, Status, Rollback

Der eigenständige Dienst hält seinen Zustand unter `/var/lib/ogl-node-updater/current.json` fest und legt private Logs `update-<operation-id>.log` im selben Verzeichnis ab. Der NodeAgent erfragt den Status alle 10 Sekunden. Nach einem manuellen Stop des Simulators läuft der separate Dienst weiter, aber es werden keine NexBus-Statusmeldungen mehr weitergegeben, bis der Simulator wieder erreichbar ist. Den lokalen Dienststatus kann man ohne Neustart via Journal und JSON-Datei einsehen.

Fehler beim Build führen **nicht** zu einem automatischen Neustart. Das Backup enthält `configs.tar.gz`, `local-changes.patch`, `git-status.txt`, `previous-commit.txt` und bei Erfolg `updated-commit.txt`. Für einen manuellen Rollback **zuerst** alle betroffenen Prozesse beenden und die vorherige Commit-SHA prüfen. `git reset --hard <previous-commit>` und das Backup dürfen nur nach bewusster Bestandsaufnahme wieder eingespielt werden.

Beachte: In-place-Updates können während des Builds binäre Dateien verändern. Für stark genutzte produktive Simulatoren sollte vor dem Update ein Wartungsfenster vereinbart werden. Datenbankmigrationen und ein robuster atomarer Binärdeploy sind hier ausdrücklich **nicht** enthalten.

## Tests

```bash
python3 tools/ci/verify_ogl_node_live_updater.py
dotnet run --configuration Release --project tools/ci/NexBusRegression/NexBusRegression.csproj
```

Zusätzlich auf einer Test-Node den gesamten Pfad über World API → signierten NexBus → NodeAgent → eigenen Update-Dienst mit vorhandener .NET-10-Installation prüfen. **Keinen produktiven Live-Update-Klick als ersten Integrationstest verwenden.**

## Node-Statusanzeige im ROBUST-Webinterface

Die Node-Tabelle zeigt den gemeldeten `online`-, `stale`- oder `offline`-
Zustand und darunter im Update-Feld je Node `live.accepted`,
`live.running`, `live.completed` oder `live.failed`. Laufende Updates
sperren den Button fuer diese Node gegen erneutes versehentliches Ausloesen.
Bei Erfolg erinnert die Tabelle an den **manuellen Simulator-Neustart**.

Die Tabelle wird bei Aufruf sowie bei geoeffneter Node-Seite alle 15 Sekunden
ueber den vorhandenen, geschuetzten `GET /api/v1/nodes`-Endpunkt erneuert.
Ein fehlender Heartbeat wird als `stale` angezeigt; dies ist keine sichere
Aussage ueber den Zustand eines bereits lokal angenommenen separaten
systemd-Update-Workers. Dessen private Status-/Logdateien bleiben im Fehlerfall
die Quelle fuer den letzten lokalen Fortschritt.
