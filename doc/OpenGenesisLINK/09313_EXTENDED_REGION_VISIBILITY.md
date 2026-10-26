# OGL 0.9.3.13 — Extended Region Visibility (Opt-in)

Stand: 26.10.2026. Implementierungs-PR für eine **zusätzliche Pflichtabnahme**
vor 0.9.3.13 RC/Stable, nicht automatisch für Produktion aktiviert.

## Problem und technische Grenze

OpenSimulator-Standard: `Scene.MaxRegionViewDistance=255m`, trotz
`Scene.MaxDrawDistance=512m`. Die ScenePresence-RegionViewDistance
wird auf den Serverwert geklemmt; EntityTransfer sucht folglich
nur die Nachbarn im ~3×3-Bereich einer normalen 256×256-m-Region.
Dies ist keine fest codierte 9-Sim-Grenze in Firestorm.
Die Nachbarregionen werden über `GridService.GetRegionRange`
gefunden, dann nach **Avatarposition, individueller Viewer-Sichtweite
(+64m) und Regionsausdehnung** gefiltert, und für sichtbare Regionen
werden Child-Agents / Region-CAPS / UDP-Informationen eingerichtet.

Für `OGLExtendedRegionVisibility=true` gelten als **Preset-Defaults**:
- `MaxDrawDistance = 1024` Meter (zulässiger Viewerwert)
- `MaxRegionsViewDistance = 768` Meter (maximale Server-Regionssicht)
- `MinRegionsViewDistance = 96` Meter (bestehender Standard)

Die ursprünglichen Defaults 512/255/96 bleiben bei deaktiviertem Flag.
Explizit gesetzte `MaxDrawDistance` und `MaxRegionsViewDistance` behalten
**immer Vorrang**, und der vorhandene Schutz begrenzt Regionssicht auf die
maximale Darstellungsdistanz. Das Feature übersteuert die persönliche
Viewer-Sichtweite **nicht**. In Firestorm diese auf z. B. 512 m erhöhen,
sonst kann die Regionssicht weiter bei 3×3 bleiben.
Die Sicht hängt außerdem davon ab, dass Regionen zusammenhängend,
**online** und für den Viewer erreichbar sind; VR/VarRegion-Footprints
und Grid-Nachbarinformationen müssen stimmen. Getrennte Hypergrid-Grids
werden **nicht** zu einem optisch nahtlosen Multi-Grid zusammengeschaltet.

Zusätzlich behoben: `EntityTransferModule.GetNeighbors` verwendet
signierte 64-bit-Suchgrenzen mit Clamping zu gültigen GridService-Koordinaten,
statt bei größerer Reichweite nahe Grid-Weltkoordinate 0 durch `uint`-
Subtraktion zu unterlaufen. Die Nachbarsuche wird weiterhin alle 30 Sekunden
zwischengespeichert; bekannte Regionen und Seed-CAPS werden weiter durch
die bestehenden Mechanismen gepflegt.

## Testaktivierung (nur kontrolliertes Staging)

Aktive `[Startup]`-Sektion des **Simulatorservers** (nicht ROBUST):

```ini
[Startup]
OGLExtendedRegionVisibility = true
; Optional nur falls abweichender Wert gewünscht:
; MaxDrawDistance = 1024
; MaxRegionsViewDistance = 768
```

Nach Neustart muss das Simulatorlog
`[OGL-REGION-VIEW]: Erweiterte Regionssicht ...` mit effektiven
Werten ausgeben. Es gibt **keine Datenbankmigration**. Installation
über GitHub allein aktiviert den Schalter nicht. Bei Rollback
`OGLExtendedRegionVisibility=false` und den expliziten Overrides entfernen;
Simulator neu starten. `bin/OpenSim.ini` bleibt in diesem PR unverändert.

## 0.9.3.13 Pflichtabnahme

- **V01**: Auf demselben Firestorm 7.2.4-Konto bei 128/256/512/768 m
  Viewer-Draw-Distance testen; bei 512 m und einem zusammenhängenden
  5×5-Grid mehr als 9 gleichzeitige sichtbare Sims, Terrain UND Prims
  nachweisen. Nicht nur Kartenkacheln zählen.
- **V02**: Region-/Estate-Grenzen, Standard- und VarRegion 512/1024,
  Sim über mehrere Prozesse und Netzwerkhosts, Offline-/Online-Änderungen,
  Innen-/Außenbereiche und nahe Grid-Weltkoordinaten testen.
- **V03**: Avatarlogin/Logout, Re-Login, Grenzüberquerung,
  UDP/CAPS/Child-Agent-Aufbau, Hypergrid-Gast, Teleport und
  stabile Objektsichtbarkeit prüfen.
- **V04**: Vergleich Baseline vs Opt-in: Server-RAM, Prozess-CPU,
  Child-Agent-Anzahl, UDP-/CAPS-Datenverkehr, Avatar-FPS, Viewer-FPS,
  initiale Objektsicht und mindestens 30-minütige Lastprobe.
  Bei problematischer Last oder fehlerhaften Regionnachbarn nicht freigeben.

### Grenzen

Es wird kein garantierter SL-Paritätswert wie "64 sichtbare Regionen"
behauptet, da Firestorm, Verbindungszahl, Child-Agent-Limits, Objekt-Streaming,
Serverleistung und Draw-Distance unabhängig begrenzen können.
Bei starken Einbrüchen Reichweite reduzieren (z. B. 512m) oder Opt-in
abschalten. Die Änderungen liefern **keine** zusätzliche Grid-Render-Engine
und ersetzen nicht FastRez oder FastMap.

## OGLSeasons

Das alte OGLSeasons-Addon wird **nicht mehr für 0.9.3.13 weiterentwickelt**.
Es bleibt aus Sicherheits- und Kompatibilitätsgründen im Repository,
wird nicht automatisch entfernt oder umkonfiguriert und ist kein
0.9.3.13-Release-Gate. Erst **nach Stable 0.9.3.13** beginnt ein neues,
getrennt spezifiziertes OGLSeasons-Nachfolgeprojekt. Bestehende
Serverinstallationen dürfen nicht ungefragt verändert werden.
