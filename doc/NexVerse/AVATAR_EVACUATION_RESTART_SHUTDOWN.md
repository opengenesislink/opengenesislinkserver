# OpenGenesisLINK Gen1 – Graceful Avatar Evacuation on Restart/Shutdown

## Ziel

Bei `region restart` und Inworld-Region-Restart werden vor dem eigentlichen
Regionsstopp alle Root-Viewer-Avatare zur **nächstgelegenen online registrierten**
Region desselben Grids teleportiert. Andere Regionen derselben Simulatorinstanz
sind zulässig, wenn **nur diese eine Region** neu startet.

Bei einem vollständigen `shutdown` / `quit` auf einem Simulator werden
**alle dort gehosteten Regionen** als Teleportziele ausgeschlossen. Nur
online registrierte Regionen auf **einem anderen Simulatorprozess** werden
verwendet. Die Zielwahl erfolgt über die vorhandene GridService-Datenbank,
also **keine neue Datenbank** (die bestehende MariaDB bleibt maßgeblich)
und keine fest einprogrammierte Liste.

Die vorhandene native `IEntityTransferModule.Teleport`-Schnittstelle prüft
weiterhin Einlassrechte, Land-/Estate-Regeln und die regulären
OpenSimulator-Teleportbedingungen. Ein Ziel ohne Zutrittsrechte kann nicht
durch einen administrativen Region-Restart zum erzwungenen Zutritt werden.

## Aktivierung

Bereits in der versionierten `bin/OpenSim.ini` aktiviert:

```ini
[NexVerseAvatarEvacuation]
    Enabled = true
    WaitSeconds = 25
```

Es sind keine weiteren per-Region-Einstellungen nötig.

## Abgedeckte Pfade

- `Scene.RestartNow()`: Root-Viewer vor `Close()` versetzen. Ohne
  verfügbares Ziel oder bei verbliebenen Avataren **Regionsneustart abbrechen**,
  nicht einfach alle Bürger herauswerfen.
- World-API/NodeAgent-gesteuerter **Managed Region Restart und Stop**:
  nach bestätigter Verwaltungsberechtigung dasselbe Verfahren; bei
  unvollständigem Transfer wird `region_evacuation_incomplete` gemeldet
  und die Region bleibt in Betrieb.
- Vollständiges `shutdown` einer OpenSim-Instanz:
  alle lokalen Regions-UUIDs als Ziele ausschließen, den Transfer vor
  `SceneManager.Close()` beginnen, Erfolg bis zum Timeout prüfen.
  Ein unvermeidbarer Prozess-Shutdown ist **best effort**; verbleibende
  Avatare werden protokolliert und durch die normale Abschaltung getrennt.

## Grenzen

Nur echte Viewer-Root-Presences lassen sich mit `IEntityTransferModule`
teleportieren. Child Agents sind keine eigenständigen Benutzer.
Server-NPCs besitzen keinen normalen Viewer-Circuit und werden **nicht** mit
dieser Viewer-Transfer-API verschoben; sie werden beim Schließen der
Ursprungsregion beendet und brauchen gegebenenfalls eine spätere eigene
persistente Spawn-/Rekonstruktionsfunktion.

Die online-Registrierung in der Grid-Datenbank ist eine notwendige, aber
keine vollständige Garantie für Erreichbarkeit oder Zutrittsrechte. Ein
abgeschalteter Zielserver, Zeitüberschreitung, fehlende Berechtigung oder
erzwungener Prozessabbruch (SIGKILL/OOM) kann eine Evakuierung verhindern.
Ein erfolgreicher Teleportaufruf allein gilt **nicht** als Erfolg:
OpenGenesisLINK prüft, ob der Root-Agent die Quellregion verlassen hat.

Ein gewaltsames `kill -9` oder ein VM-/Host-Ausfall kann **nicht**
abgefangen werden. Bei vollständigem Instanzshutdown wird nach dem
begrenzten Evakuierungsversuch normal heruntergefahren, selbst wenn kein
geeignetes Ziel erreichbar ist. Dieser Unterschied zum abbrechbaren
Regionsneustart ist beabsichtigt.

## Abnahme

1. Zwei laufende Regionen auf demselben Simulator, eine weitere auf
   separatem Simulatorhost innerhalb desselben Grids.
2. Mit einem Testavatar in Region A `region restart` auslösen:
   Wechsel zur nächsten Online-Region vor dem Schließen von A prüfen.
3. Mit Testavatare in A und B `shutdown` auslösen:
   beide dürfen nur zur Region auf dem anderen Simulator wechseln.
4. Für Testaccount mit fehlendem Zutrittsrecht verifizieren, dass keine
   Rechte umgangen werden. Ohne erreichbares Ziel bleibt bei
   `region restart` A geöffnet.
5. Teleport- und Abschaltlogs prüfen, ohne Tokens oder Bestandsdaten in
   Debuglogs auszugeben.

Quellcode-/CI-Validierung ersetzt keinen Firestorm-/OpenSim-Live-Test.
