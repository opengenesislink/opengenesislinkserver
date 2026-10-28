# OGLWeather – OpenGenesisLINK Generation 1

**Eigenständiges Wetter-Addon (0.1.0 Dev)** für alle Regionen eines Simulatorprozesses. Ersetzt das eingestellte OGLSeasons-Projekt. Kein externer DWD-Dienst und **keine echte meteorologische Wettervorhersage**: Dies ist eine vollständig deterministische Wetter-Simulation.

## Funktionen

- Gemeinsamer Wetterplan für **alle geladenen Regionen desselben Simulators** über `ISharedRegionModule`. Kein regionaler Zufall, keine Wetterabweichungen zwischen Regionen.
- Bei identischem Seed und identischer Zeitzone stimmt das Wetter auch **zwischen unterschiedlichen Simulatorprozessen** überein – ganz ohne Netzbus-Abhängigkeit.
- Echte Uhren: lokale Zeit mit `TimeZone = Europe/Berlin` inklusive Sommer-/Winterzeit.
- Jahreszeitlich schwankende Tages- und Nachttemperaturen; alle sechs Stunden ein fester Regen-/Schnee-/Sonne-/Wolken-Abschnitt. Bei Temperaturen bis einschließlich 1°C wird Niederschlag als Schnee ausgewiesen, sonst Regen.
- Aktueller Wetterstatus: Zustand, Temperatur, Stärke und Wind.
- Fünf kommende Kalendertage: Wetterart, tatsächliches Tagesminimum/-maximum und Stunden mit Niederschlag. **Vorhersage und spätere Wirklichkeit sind das gleiche deterministische Modell** und ändern sich bei Neustart nicht, solange Seed, Zeitzone und Modellversion gleich bleiben.
- **Kein manuelles Rezzen:** Die Legacy-Fallback-Emitter werden für Firestorm automatisch als kurzlebige, nicht persistente Objekte verwaltet. Zukünftige OGL Viewer können den nativen Wettervertrag verwenden.
- Kein Schreiben in Terrain, Estate-, Parcel- oder EEP-Einstellungen. Bei aktiviertem Firestorm-Fallback werden ausschließlich serververwaltete temporäre Scene-Objekte erstellt und wieder entfernt.
- Jede Region sendet ihre Wetterdaten über `IWorldComm` an die optionale **Prim-Wettertafel**. Für native Viewer gibt es zusätzlich die pro Agent/Region ausgegebene read-only-CAPS `OGLWeatherState`. Diese selbst setzt kein Wetter-Prim voraus.

## Installation

Im NexVerse-Quellbaum:

```bash
# Nur bauen, NICHT in ROBUST installieren
bash addon-modules/OGLWeather/build.sh Release

# Nur auf einem Simulator installieren
bash addon-modules/OGLWeather/build.sh Release --sim-bin /opt/developer/bin
```

INI-Beispiel `addons/OGLWeather/OGLWeather.ini.example` in die geladene `bin/config/` oder `bin/config-include/` des **Simulators** kopieren und gezielt aktivieren:

```ini
[OGLWeather]
Enabled = true
WeatherSeed = 20261028
TimeZone = "Europe/Berlin"
LSLChannel = -835311
CurrentBroadcastSeconds = 60
ForecastBroadcastSeconds = 300
CloudBaseMeters = 200
```

**Nicht** auf ROBUST installieren oder aktivieren. Mit `Enabled = false` ist das Modul inaktiv. Die genutzte DLL und Konfiguration erfordern einen Simulatorneustart.

## Hybrid-Niederschlag: Firestorm und native OGL Viewer

OGLWeather nutzt **ein gemeinsames Wettermodell** und bietet zwei Darstellungen:

- **Firestorm, ohne Viewer-Modifikation:** Der Server erzeugt automatisch, nur während Regen oder Schnee, eine begrenzte Anzahl **temporärer, unsichtbarer, nichtphysischer Emitter-Prims** in jeder besuchten Region. **Keine manuelle Platzierung, kein Rezz-Skript und keine LSL-Skripte in den Emitttern erforderlich.** Der Server setzt den SL/OpenSim-Standard-Partikelblock direkt auf die Prims. Regen/Snow und Stärke ändern sich aus der selben Vorhersage wie die Wettertafel.
- **Künftiger OGL Viewer mit native Renderer:** Die read-only, pro Region/Avatar gebundene CAPS `OGLWeatherState` liefert die Daten für echte GPU-Partikel mit Ursprung in der Wolkendecke. `legacy_prim_emitters_active: true` kennzeichnet den Firestorm-Fallback, damit ein kompatibler künftiger Viewer seinen nativen Effekt bei Bedarf unterdrücken kann. **Der Viewer-Renderer ist noch nicht implementiert.**

### Automatische Emitter-Konfiguration

```ini
[OGLWeather]
Enabled = true
LegacyParticleEmitters = true
EmitterHeightMeters = 65
EmitterSpacingMeters = 96
MaxEmittersPerRegion = 16
VisualRefreshSeconds = 60
CloudBaseMeters = 200
```

- `Enabled` bleibt in der bereitgestellten Beispiel-INI aus Sicherheitsgründen **false**. Erst beim bewussten Aktivieren greift der Fallback.
- `EmitterHeightMeters`: **20–150 m oberhalb des lokalen Terrains**, Standard 65 m. Auf hügeligem Terrain wird jede Position einzeln berechnet. Es ist eine **sichtbare Niederschlagszone aus Richtung Wolken**, kein physikalisch garantierter Start exakt auf der EEP-Wolkenoberfläche. Partikel besitzen begrenzte Lebensdauer; bei unrealistisch hohen Emitttern können sie über dem Boden verschwinden.
- `EmitterSpacingMeters`: 32–256 m, Standard 96 m. Typische 256×256 m Region: **3×3 = 9 Emitter**. Bei großen Varregions begrenzt `MaxEmittersPerRegion` die Anzahl und verteilt die Punkte über die ganze Fläche; dadurch kann bei sehr großen Regionen nur teilweise Niederschlag sichtbar sein.
- `MaxEmittersPerRegion`: 1–64, Standard 16. Bei Regen und Schnee sind gleichzeitig mehrere Tausend Partikel im Firestorm-Viewer möglich. Nicht alle Viewer zeigen alle Partikel; Grafik-/Partikelbudget kann die Menge begrenzen.
- `VisualRefreshSeconds`: 30–300 Sekunden, Standard 60. Dauerhaftes Wetter verwendet dieselben Objekte weiter, statt alle 60 Sekunden neue zu rezzen.
- Bei Sonne/Wolken oder wenn keine Viewer verbunden sind, werden **alle nur vom Modul erzeugten Emitter** aus dieser Region entfernt. Beim Entladen einer Region und beim Schließen des Moduls erfolgt die Bereinigung ebenfalls.
- Die Prims werden mit `Scene.AddNewSceneObject(group, false)` erzeugt: **nicht dauerhaft in der Regionsdatenbank gespeichert**, ohne Skriptinventar, physikfrei und phantom. Owner ist der Estate-Owner der Region. Es werden keine Objekte von Bewohnern gesucht oder gelöscht. Dennoch sind es echte temporäre Scene-Objekte und können bis zu ihrer Entfernung kurz in Objekt-/Primzählungen erscheinen.
- Keine neue Script- oder Inventar-Asset-Abhängigkeit. Ein Emitter-Primsystem ist nötig, weil Standard-LL-Partikel objektgebunden sind. Ein Wettermodus ohne Objekte ist in unverändertem Firestorm technisch nicht möglich. <https://create.secondlife.com/script/lsl-reference/functions/llparticlesystem/>

Der Native-CAPS `OGLWeatherState` liefert weiterhin JSON (`ogl.weather`, v1) mit aktueller Lage, Temperatur, Wind, Stärke, Wolkenhöhe und fünf Prognosetagen. Für den späteren nativen Renderer gilt weiterhin die Kompatibilitätsaufgabe [OGL Viewer Issue #28](https://github.com/opengenesislink/opengenesislinkviewer/issues/28).

**Betriebsgrenzen:** Sichtbarkeit hängt von Firestorm-Sichtweite, Viewer-Partikellimit, Höhe über Gelände, Quellposition und Wind ab. Regen wird nicht vollständig über jeder Stelle eines riesigen Simulators garantiert; vor einem Produktions-Rollout im Viewer prüfen. Kein meteorologischer DWD-Livefeed.

## Wettertafel

`WeatherForecastBoard.lsl` kommt in einen einzelnen Prim (standardmäßig Vorderseite `DISPLAY_FACE=3`): Status jetzt, Temperatur, Wind und fünf Tage mit Min/Max und Niederschlagsdauer. `osSetDynamicTextureDataFace` benötigt die entsprechende OSSL-Freigabe und VectorRender-Unterstützung.

Beim Start wartet die Tafel auf alle fünf Forecast-Einträge und rendert **einmal**. Danach werden neue dynamische Textur-UUIDs nur angefordert, wenn die Daten geändert wurden. Der vorhandene dynamische Textur-Cache-Handoff von Generation 1 kann die Sichtbarkeit verbessern, aber **vollständig flackerfreie Viewer-Updates sind nicht garantiert**.

## Berechtigungen und Protokoll

Die Region sendet ausschließlich in-world `IWorldComm.DeliverMessage(ChatTypeEnum.Region, -835311, "OGLWeather", UUID.Zero, ...)`:
- `OGLWX|1|NOW|yyyy-MM-dd HH:00|sun/cloud/rain/snow|tempC|intensity|windKmh`
- `OGLWX|1|DAY|0..4|yyyy-MM-dd|sun/cloud/rain/snow|minC|maxC|precipitationHours`

LSL akzeptiert ausschließlich die exakten Protokollkennungen, Channel, Quellnamen und `NULL_KEY` als serverseitige Quelle. Anonyme Avatar- oder Objekt-Chats gelten nicht als gültige Wetterdaten.

## Tests und Betrieb

- `tools/ci/OglWeatherRegression`: Prognose gegen tatsächliche zukünftige 24-Stunden-Zustände an 365 Tagen, mehrere Jahreszeiten, Temperatur-/Niederschlagsgrenzen, DST und identischer Seed.
- `tools/ci/verify_ogl_weather.py`: CAPS-/Plugin-Vertrag, sichere Defaults, gebundener automatischer Legacy-Emitter-Fallback und keine Reste der aktiven OGLSeasons-Integration.
- `.github/workflows/nexjast-ci.yml`: eigenständiger Release-Build und Regressionen. Historische Release-Dokumente bleiben unberührt.
- Live-Abnahme: mindestens zwei Regionen im selben Prozess, eine Wettertafel, Firestorm-Regen/Schnee mit automatisch erstellten Emitttern, Statuswechsel, Entfernung bei trockenem Wetter, CAPS-GET / falsche Methode / Logout / Region-Crossing. Später nativen OGL Viewer und Wolkenhöhe separat prüfen. Zusätzlich anderen Simulatorprozess mit gleichem Seed auf dieselben Temperaturen prüfen.

**Hinweis:** Das geplante Wetter ist garantierte **Spielwelt-Simulation**, nicht reale Wetterdaten aus der Umgebung. Änderungen an Seed, Zeitzone oder Algorithmen verändern die zukünftige Wetterkurve. Keine EEP-/Terrainänderungen. Der native Viewer-Renderer ist ein separater noch offener Entwicklungsteil.
