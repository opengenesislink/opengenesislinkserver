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

**Temporäre Wettertests auf einem Regionssimulator:** Die lokal über
`ogl weather rain|snow` ausgelösten Niederschlags-Overrides verändern
die deterministische Portal-Vorhersage **nicht**. Während solcher Tests
können aktuelle Partikel inworld bewusst von den regulären Portalwerten
abweichen. Diese World-API-Version besitzt keinen öffentlichen
Wetter-Schreibendpunkt; eine spätere Administratorsteuerung benötigt
Authentifizierung, Berechtigungsprüfung und Auditierung.

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

## 3. XML für OpenAI und ElevenLabs (Wetter-Audiobericht)

Zusätzlich zum unveränderten JSON-Endpunkt liefert die World API
denselben **simulierten** OGLWeather-Datenstand als XML mit einem
vorformulierten deutschen Wetterbericht.

```http
GET /weather/xml
Accept: application/xml
```

Alternativ (gleicher Inhalt und gleiches Zugriffsmodell):

```http
GET /api/v1/weather/xml
Accept: application/xml
```

Beide Wege sind ausdrücklich **read-only und öffentlich**, nutzen den
bereits vorhandenen World-API-Rate-Limiter und benötigen
`[OGLWeatherPortal] Enabled = true`. Nur **GET** wird akzeptiert;
andere Methoden liefern XML und HTTP 405, die nicht aktivierte
Konfiguration HTTP 503. Content-Type:
`application/xml; charset=utf-8`, Cache-Control:
`public, max-age=30`, Header `X-Content-Type-Options: nosniff`.
Fehlerantworten sind ebenfalls XML und setzen `Cache-Control: no-store`.

Auszug (Beispielwerte, keine aktuellen Daten):

```xml
<?xml version="1.0" encoding="utf-8"?>
<oglweather version="1" source="simulated_oglweather" simulation="true">
  <meta>
    <display_name>Stadt-NexVerse</display_name>
    <timezone>Europe/Berlin</timezone>
    <language>de-DE</language>
    <observed_at_utc>2026-10-29T12:00:00.0000000+00:00</observed_at_utc>
    <local_time>2026-10-29T13:00:00.0000000+01:00</local_time>
    <temperature_unit>C</temperature_unit>
    <wind_unit>km/h</wind_unit>
  </meta>
  <current condition="cloud" condition_de="Bewölkt"
           temperature_c="12" wind_kmh="15"
           precipitation_intensity_percent="0" />
  <today>
    <day date="2026-10-29" weekday_de="Donnerstag"
         condition="cloud" condition_de="Bewölkt"
         temperature_min_c="7" temperature_max_c="14"
         precipitation_hours="0" />
  </today>
  <forecast days="5">
    <day date="2026-10-30" weekday_de="Freitag"
         condition="rain" condition_de="Regen"
         temperature_min_c="6" temperature_max_c="13"
         precipitation_hours="6" />
    <!-- weitere vier Tage -->
  </forecast>
  <narration language="de-DE" format="plain-text">Hier ist der Wetterbericht für Stadt-NexVerse. ...</narration>
</oglweather>
```

Die echte Antwort enthält fünf vollständige `<day>`-Elemente
**ab morgen**, die aktuelle Wetterlage und einen direkt für die
Sprachausgabe verwendbaren `<narration>`-Text. Zeitstempel sind
mit UTC und lokaler Zeitzone versehen. XmlWriter maskiert
Sonderzeichen in Display-Namen und Texten korrekt.

**Beispielworkflow (separater Dienst, nicht Bestandteil von OGLWeather):**

1. XML von der öffentlich erreichbaren World-API abrufen.
2. `/oglweather/narration` auslesen und bei Bedarf mit OpenAI
   sprachlich redigieren, ohne Wetterwerte zu erfinden.
3. Den Sprechertext über die autorisierte ElevenLabs Text-to-Speech API
   mit dem eigenen API-Schlüssel in eine MP3 umwandeln.
4. Die MP3 auf dem eigenen Webserver speichern und den Audio-Guide
   in Stadt-NexVerse darauf verweisen lassen.

```bash
curl -fsS -H 'Accept: application/xml' \
  https://DEINE-WORLD-API.DOMAIN/weather/xml -o wetter.xml
python3 - <<'PY'
from xml.etree import ElementTree as ET
root = ET.parse("wetter.xml").getroot()
print(root.findtext("narration"))
PY
```

**Wichtig:** Der XML-Endpunkt erzeugt **keine MP3** und überträgt
keine Daten an OpenAI oder ElevenLabs. API-Keys bleiben ausschließlich
im späteren externen TTS-Dienst. Ein Proxy auf
`world.stadt-nexverse.de` muss `/weather/xml` gegebenenfalls
separat freigeben; `/api/v1/weather/xml` ist die Alternative für
Proxys, die ausschließlich das API-Präfix weiterleiten.

Das XML basiert genau wie JSON auf dem deterministischen Modell
mit demselben `WeatherSeed` und `TimeZone`. Ein per Simulator
erzwungener Regen-/Schneetest mit `ogl weather rain|snow` ist
**nicht Teil der ROBUST-XML-Prognose**. Der Sprecher darf die Angaben
nicht als reale Wetterdaten ausgeben.

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
