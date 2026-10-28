# OGLWeather in der OpenGenesisLINK World API (Stadtportal)

**Generation 1 – öffentliches, read-only Wetter-Widget.** Diese Schnittstelle
liefert die **simulierte** OGLWeather-Wetterlage für das Stadtportal. Kein
DWD/realer Messwert, keine fremde Wetter-API, keine offenen Schreibbefehle,
keine Wetter-Prims in ROBUST. Vorhersagen werden aus derselben, nun in
`NexVerse.Core` gemeinsam verwendeten Modellimplementierung berechnet wie
die tatsächlichen Simulator-Niederschläge und die Inworld-Wettertafel.

## 1. ROBUST-Konfiguration

In der **tatsächlich aktiven** `bin/Robust.HG.ini` beziehungsweise
`Robust.ini` (oder einer geladenen Konfigurationsdatei) eintragen:

```ini
[OGLWeatherPortal]
Enabled = true
WeatherSeed = 20261028
TimeZone = "Europe/Berlin"
DisplayName = "Stadt-NexVerse"
```

Die Simulator-DLL heißt `OpenGenesisLINK.Addons.OGLWeather.dll`; sie
wird weiterhin automatisch mit dem Standard-Server-Build erzeugt. Die
World-API-Route ist dagegen Bestandteil der bestehenden `OGL.API.dll`.

Die World API muss bereits durch `[NexVerseWorldApi]` und
`[ServiceList]` aktiv sein. Ohne diese explizite
`OGLWeatherPortal.Enabled=true`-Freigabe antwortet die Wetter-Route mit
**HTTP 503 / `weather_portal_disabled`** statt zufällige Temperaturen zu
präsentieren.

**Zwingend:** `WeatherSeed` und `TimeZone` müssen mit der
`[OGLWeather]`-Konfiguration auf dem **Regionssimulator** übereinstimmen:

```ini
[OGLWeather]
Enabled = true
WeatherSeed = 20261028
TimeZone = "Europe/Berlin"
LegacyParticleEmitters = true
```

Bei mehreren Simulatorprozessen mit unterschiedlichen Seeds/Zeitzonen ist
dieser Endpunkt nur die **ausgewählte gemeinsame Portal-Klimakonfiguration**;
es gibt keine automatisch ermittelte regionenspezifische Konfiguration
und keine Behauptung, dass alle Regionen denselben Seed nutzen. Für die
erste Version ist eine gemeinsame Klimakonfiguration empfohlen.
Nach der ROBUST-Konfigurationsänderung ROBUST neu starten.
Das Modul selbst läuft auf den Simulatoren und steuert dort Wetterpartikel
für Firestorm; ROBUST erzeugt nur JSON für die Webseite.

## 2. HTTP-Endpunkt

```http
GET /api/v1/weather
Accept: application/json
```

Optional kann der vorhandene Reverse-Proxy des Stadtportals den Endpunkt
über dieselbe Portal-Domain unter `/api/v1/weather` bereitstellen. Da die
Wetterdaten absichtlich öffentlich sind, ist **kein OAuth-Bearer-Token**
nötig. Der vorhandene **World-API-Request-Gate** begrenzt Anfragen.
Nur GET wird angenommen; POST/PUT/PATCH/DELETE ergeben HTTP 405, mit
`Allow: GET`. Nicht aktiviert oder falsch konfiguriert: HTTP 503.
Fehlende Konfiguration lässt den restlichen ROBUST/World-API-Betrieb
unangetastet.

Die Antwort hat `Content-Type: application/json; charset=utf-8`,
`X-Content-Type-Options: nosniff`,
`Cache-Control: public, max-age=30`.
Nicht verfügbare Antworten setzen `Cache-Control: no-store`.

Beispiel (Illustration des Datenformats, **keine** gemessene Temperatur):

```json
{
  "protocol": "ogl.weather.portal",
  "version": 1,
  "source": "simulated_oglweather",
  "simulation": true,
  "display_name": "Stadt-NexVerse",
  "timezone": "Europe/Berlin",
  "observed_at_utc": "2026-10-28T19:00:00.0000000+00:00",
  "local_time": "2026-10-28T20:00:00.0000000+01:00",
  "units": {"temperature":"°C","wind":"km/h"},
  "current": {
    "condition": "rain",
    "condition_label_de": "Regen",
    "icon": "cloud-rain",
    "temperature_c": 8,
    "precipitation_intensity_percent": 54,
    "wind_kmh": 16
  },
  "today": {
    "date":"2026-10-28","weekday_de":"Mi","condition":"rain",
    "condition_label_de":"Regen","icon":"cloud-rain",
    "temperature_min_c":5,"temperature_max_c":13,"precipitation_hours":6
  },
  "hourly": [
    {
      "time_utc":"2026-10-28T19:00:00.0000000+00:00",
      "time_local":"2026-10-28T20:00:00.0000000+01:00",
      "hour":"20:00", "condition":"rain",
      "condition_label_de":"Regen", "icon":"cloud-rain",
      "temperature_c":8,"precipitation_intensity_percent":54,"wind_kmh":16
    }
  ],
  "forecast": [
    {
      "date":"2026-10-29","weekday_de":"Do","condition":"cloud",
      "condition_label_de":"Bewölkt","icon":"cloud",
      "temperature_min_c":3,"temperature_max_c":12,"precipitation_hours":0
    }
  ],
  "forecast_days": 5
}
```

In echten Antworten enthält `hourly` **24** aufeinanderfolgende
reale Stunden und `forecast` **fünf volle Tage ab morgen**.
`today` enthält das aktuelle Tagesminimum/-maximum aus dem Wettermodell.
Die Beispielwerte sind ausschließlich zur Darstellung des Schemas.
`precipitation_intensity_percent` ist die **Stärke des simulierten
Niederschlags**, **nicht** dessen Eintrittswahrscheinlichkeit.

**Wettercodes:** `sun`, `cloud`, `rain`, `snow` mit deutschen Labels
und portalgeeigneten Icon-Namen `sun`, `cloud`, `cloud-rain`,
`cloud-snow`. Bei der Zeitumstellung sind `time_utc` und `time_local`
inklusive Offset enthalten, damit mehrfach vorkommende Ortsstunden
unterscheidbar bleiben.

## 3. Fertige Wetterkarte für das Stadtportal

[Responsive HTML/CSS/JavaScript-Demo](examples/oglweather-stadtportal-widget.html)

Darstellung: aktuelles Wetter mit großer Temperatur und Wetter-Symbol,
Wind und Niederschlagsintensität, heutige Minimal-/Maximaltemperatur,
horizontaler 24-Stunden-Verlauf und fünf Vorhersagetage. Automatische
Aktualisierung alle 60 Sekunden; **keine** eingebauten Access-Tokens.

```bash
# Aus dem World-API/ROBUST-Netz nach dem Opt-in:
curl -fsS https://DEINE-WORLD-API.DOMAIN/api/v1/weather
```

Für einen Cross-Origin-Browseraufruf muss das Stadtportal-Backend
sicherheitstechnisch korrekt als **Same-Origin-Reverse-Proxy** vermitteln,
anstatt API-Secrets im Frontend zu speichern oder global CORS aufzuweichen.
Der HTML-Code verwendet deshalb `const WEATHER_URL = "/api/v1/weather";`
und muss entweder über diesen Proxy laufen oder entsprechend in das
existierende Portal-Frontend übertragen werden. Er wurde **nicht** auf
`stadt-nexverse.de` live ausgerollt.

## 4. Regression, Sicherheits- und Betriebsgrenzen

Die C#-Wetterregression prüft die vollständige Übereinstimmung der
API-Werte mit `OglWeatherModel.At` und `ForecastDay` über normale
Tage sowie Sommer-/Winterzeit-Wechsel, 24 aufeinanderfolgende UTC-Stunden,
fünf Tage und temperaturabhängiges Regen/Schnee-Verhalten. Ein separater
CI-Quellvertragscheck prüft die Route, das Rate-Gate, die ROBUST-Beispiel-
Konfigurationen, den gemeinsam verwendeten Core-Modelltyp und das
sichere Frontend-Handling.

Die API bietet keine Änderung der Wetterlage und keine persönliche
Datenabfrage. Der tatsächliche Wetterzustand im Viewer hängt weiterhin
davon ab, ob das OGLWeather-Addon auf dem Simulator aktiviert und korrekt
konfiguriert ist. Ein **Inworld-/ROBUST-End-to-End-Test** auf dem
produktiven Server ist nach dem Deployment weiterhin erforderlich.
