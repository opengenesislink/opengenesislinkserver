# OGLWeather – OpenGenesisLINK Generation 1

**Eigenständiges Wetter-Addon (0.1.0 Dev)** für alle Regionen eines Simulatorprozesses. Ersetzt das eingestellte OGLSeasons-Projekt. Kein externer DWD-Dienst und **keine echte meteorologische Wettervorhersage**: Dies ist eine vollständig deterministische Wetter-Simulation.

## Funktionen

- Gemeinsamer Wetterplan für **alle geladenen Regionen desselben Simulators** über `ISharedRegionModule`. Kein regionaler Zufall, keine Wetterabweichungen zwischen Regionen.
- Bei identischem Seed und identischer Zeitzone stimmt das Wetter auch **zwischen unterschiedlichen Simulatorprozessen** überein – ganz ohne Netzbus-Abhängigkeit.
- Echte Uhren: lokale Zeit mit `TimeZone = Europe/Berlin` inklusive Sommer-/Winterzeit.
- Jahreszeitlich schwankende Tages- und Nachttemperaturen; alle sechs Stunden ein fester Regen-/Schnee-/Sonne-/Wolken-Abschnitt. Bei Temperaturen bis einschließlich 1°C wird Niederschlag als Schnee ausgewiesen, sonst Regen.
- Aktueller Wetterstatus: Zustand, Temperatur, Stärke und Wind.
- Fünf kommende Kalendertage: Wetterart, tatsächliches Tagesminimum/-maximum und Stunden mit Niederschlag. **Vorhersage und spätere Wirklichkeit sind das gleiche deterministische Modell** und ändern sich bei Neustart nicht, solange Seed, Zeitzone und Modellversion gleich bleiben.
- Kein Schreiben in Terrain, Estate-, Parcel- oder EEP-Einstellungen. Keine automatischen Rezzes auf fremden Grundstücken. Partikel werden ausschließlich durch freiwillig platzierte LSL-Emitter erzeugt.
- Jede Region empfängt die Statusdaten über ihren eigenen `IWorldComm`-Channel; die Region-Clients und Emitter müssen **nicht** gegenseitig per Grid-Chat kommunizieren.

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
```

**Nicht** auf ROBUST installieren oder aktivieren. Mit `Enabled = false` ist das Modul inaktiv. Die genutzte DLL und Konfiguration erfordern einen Simulatorneustart.

## Wetterpartikel

`WeatherEmitter.lsl` kommt in ein unsichtbares Emitter-Prim der jeweiligen Region, idealerweise oberhalb des geplanten Niederschlagsbereichs. Die Effektfläche ist durch Viewer-Partikel begrenzt; für eine ganze 256×256-Region braucht es mehrere sinnvoll verteilte Emitter, **es regnet nicht allein durch Installation der DLL überall sichtbar**. Pro Emitter gibt es eine bewusst begrenzte Partikelrate; zu viele Emitter können Viewer-FPS beeinträchtigen.

Die Emitter schalten bei Sonne/Wolken automatisch ab, bei Schnee/Regen die passende Partikelart ein. Bei abgeschaltetem Modul stoppen sie nach spätestens 240 Sekunden ohne Meldung. Ein optionales Partikel-Asset kann später ergänzt werden.

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
- `tools/ci/verify_ogl_weather.py`: Build-/LSL-/Plugin-Vertrag, sichere Defaults und keine Reste der aktiven OGLSeasons-Integration.
- `.github/workflows/nexjast-ci.yml`: eigenständiger Release-Build und Regressionen. Historische Release-Dokumente bleiben unberührt.
- Live-Abnahme: mindestens zwei Regionen im selben Prozess, einige Wetter-Emitter, eine Wettertafel, Firestorm und OpenGenesisLINK Viewer. Zusätzlich anderen Simulatorprozess mit gleichem Seed auf dieselben Temperaturen prüfen.

**Hinweis:** Das geplante Wetter ist garantierte **Spielwelt-Simulation**, nicht reale Wetterdaten aus der Umgebung. Änderungen an Seed, Zeitzone oder Algorithmen verändern die zukünftige Wetterkurve. Keine Wolken-/EEP-/Terraininteraktion in dieser ersten Entwicklungsstufe.
