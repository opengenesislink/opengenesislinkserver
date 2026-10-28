# OGLWeather – OpenGenesisLINK Generation 1

**Eigenständiges Wetter-Addon (0.1.0 Dev)** für alle Regionen eines Simulatorprozesses. Ersetzt das eingestellte OGLSeasons-Projekt. Kein externer DWD-Dienst und **keine echte meteorologische Wettervorhersage**: Dies ist eine vollständig deterministische Wetter-Simulation.

## Funktionen

- Gemeinsamer Wetterplan für **alle geladenen Regionen desselben Simulators** über `ISharedRegionModule`. Kein regionaler Zufall, keine Wetterabweichungen zwischen Regionen.
- Bei identischem Seed und identischer Zeitzone stimmt das Wetter auch **zwischen unterschiedlichen Simulatorprozessen** überein – ganz ohne Netzbus-Abhängigkeit.
- Echte Uhren: lokale Zeit mit `TimeZone = Europe/Berlin` inklusive Sommer-/Winterzeit.
- Jahreszeitlich schwankende Tages- und Nachttemperaturen; alle sechs Stunden ein fester Regen-/Schnee-/Sonne-/Wolken-Abschnitt. Bei Temperaturen bis einschließlich 1°C wird Niederschlag als Schnee ausgewiesen, sonst Regen.
- Aktueller Wetterstatus: Zustand, Temperatur, Stärke und Wind.
- Fünf kommende Kalendertage: Wetterart, tatsächliches Tagesminimum/-maximum und Stunden mit Niederschlag. **Vorhersage und spätere Wirklichkeit sind das gleiche deterministische Modell** und ändern sich bei Neustart nicht, solange Seed, Zeitzone und Modellversion gleich bleiben.
- **Keine Wetter-Prims, keine LSL-Emitter, keine temporären oder unsichtbaren Wetterobjekte.** Das Addon steuert einen read-only Viewer-Wettervertrag, nicht die Partikel von Firestorm. Für optische Niederschläge benötigt der Viewer einen nativen Renderer.
- Kein Schreiben in Terrain, Estate-, Parcel- oder EEP-Einstellungen. Keine Rezzes auf Grundstücken.
- Jede Region sendet ihre Wetterdaten über `IWorldComm` an die optionale **Prim-Wettertafel**. Für native Viewer gibt es zusätzlich die pro Agent/Region ausgegebene read-only-CAPS `OGLWeatherState`. Diese setzt kein Wetter-Prim voraus.

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

## Primfreier Niederschlag aus den Wolken

Das **Wetter-Emitter-LSL wurde entfernt**. Der Server rezzed weder unsichtbare Hilfsprims noch virtuelle Prim-Objekte. Der korrekte Weg ist ein **natives Viewer-Wettersystem**, das die Niederschlagspartikel beim Blick in die Region lokal vom Cloud-Layer beziehungsweise oberhalb der Kamera erzeugt und beim Regionswechsel wieder entfernt.

**Technische Grenze:** Standard-Firestorm hat dafür **keine** universelle Wetter-CAPS und bindet LSL-Partikel an Prims. EEP steuert Wolken/Himmel, nicht automatisch Regen und Schnee. Auch der separat geplante OGL Viewer ist noch in der Foundation-Stufe ohne lauffähiges eigenes Rendering. Darum **zeigt aktuell kein unveränderter Firestorm automatisch primfreien Regen**: Wettermodell, Prognose und OGLWeatherState-CAPS sind serverseitig vorbereitet, die Partikel-Darstellung ist noch nicht nutzbar, bis ein kompatibler Viewer-Renderer implementiert und ausgerollt ist. Keine gegenteilige Produktionszusage.

Der zusätzliche **read-only, pro Avatar/Region gebundene** CAPS `OGLWeatherState` liefert JSON (`ogl.weather`, v1) mit aktuellem Zustand, Temperatur, Wind, Stärke, `cloud_base_m`, `origin: clouds`, `rendering: viewer_native`, `requires_viewer_support: true` und fünf Prognosetagen. Kein öffentlich erratbarer globaler HTTP-Endpunkt und keine Möglichkeit für Viewer, Wetterzustände zu setzen. HTTPS-CAPS und Agent-/Region-Lebenszyklus folgen der normalen OpenSim-CAPS-Anmeldung. Der Viewer sollte höchstens alle 30–60 Sekunden abrufen, Wetter weich überblenden und Effekte bei Teleport, Logout und deaktiviertem Wetter bereinigen.

Die Konfigurationshöhe `CloudBaseMeters` (64–2048 m, Vorgabe 200 m) ist eine mögliche visuelle Partikelstart-Höhe im zukünftigen Viewer, keine versprochene automatische Zuordnung zum EEP-Wolkenlayer. 

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
- `tools/ci/verify_ogl_weather.py`: CAPS-/Plugin-Vertrag, sichere Defaults, garantiert kein Wetter-Prim-Code und keine Reste der aktiven OGLSeasons-Integration.
- `.github/workflows/nexjast-ci.yml`: eigenständiger Release-Build und Regressionen. Historische Release-Dokumente bleiben unberührt.
- Live-Abnahme: mindestens zwei Regionen im selben Prozess, eine Wettertafel, CAPS-GET / falsche Methode / Logout / Region-Crossing. Später einen **tatsächlich renderfähigen Viewer** auf Regen, Schnee, Wolkenhöhe, Wetterwechsel und Leistung testen. Derzeit ist Firestorm nur für Wetterstatus/EEP kompatibel, nicht für native OGL-Niederschläge. Zusätzlich anderen Simulatorprozess mit gleichem Seed auf dieselben Temperaturen prüfen.

**Hinweis:** Das geplante Wetter ist garantierte **Spielwelt-Simulation**, nicht reale Wetterdaten aus der Umgebung. Änderungen an Seed, Zeitzone oder Algorithmen verändern die zukünftige Wetterkurve. Keine EEP-/Terrainänderungen. Der native Viewer-Renderer ist ein separater noch offener Entwicklungsteil.
