#!/usr/bin/env python3
"""OGLWeather Public World API/Portal contract, no runtime/HTTP illusion."""
from pathlib import Path

root = Path(__file__).resolve().parents[2]
def read(path):
    return (root / path).read_text(encoding="utf-8-sig")

core = read("NexVerse/Core/Weather/OglWeatherModel.cs")
projection = read("NexVerse/Core/Weather/OglWeatherPortalProjection.cs")
xml_projection = read("NexVerse/Core/Weather/OglWeatherXmlProjection.cs")
api = read("NexVerse/Server/Api/NexWorldWeatherApi.cs")
connector = read("NexVerse/Server/Api/NexVerseWorldApiConnector.cs")
prebuild = read("prebuild.xml")
addon = read("addons/OGLWeather/OpenGenesisLINK.Addons.OGLWeather.csproj")
regression = read("tools/ci/OglWeatherRegression/OglWeatherRegression.csproj")
docs = read("doc/NexVerse/WORLD_API_OGLWEATHER_PORTAL.md")
widget = read("doc/NexVerse/examples/oglweather-stadtportal-widget.html")
workflow = read(".github/workflows/nexjast-ci.yml")

assert not (root / "addons/OGLWeather/OglWeatherModel.cs").exists()
assert "public static WeatherReading At(" in core
assert "public static WeatherDay ForecastDay(" in core
assert "OpenGenesisLINK.Addons.OGLWeather" in core
assert "NexVerse/Core/Weather/OglWeatherModel.cs" in regression
assert "NexVerse/Core/Weather/OglWeatherPortalProjection.cs" in regression
assert 'ProjectReference Include="../../NexVerse/Core/NexVerse.Core.csproj"' in addon
assert '<Project name="NexVerse.Core"' in prebuild
assert '<Project name="OGL.API"' in prebuild
assert "NexVerse.Core" in prebuild.split('<Project name="OGL.API"', 1)[1].split("</Project>", 1)[0]
assert "OglWeatherModel.At(instant, seed, zone)" in projection
assert "OglWeatherModel.ForecastDay(" in projection
assert "public static string Serialize(" in projection
for marker in (
    "public static string Serialize(", "XmlWriter.Create(", "new UTF8Encoding(false)",
    'xml.WriteStartElement("oglweather")',
    'xml.WriteStartElement("current")',
    'xml.WriteStartElement("forecast")',
    'xml.WriteStartElement("narration")',
    'xml.WriteAttributeString("language", "de-DE")',
    'xml.WriteAttributeString("format", "plain-text")',
    "OglWeatherModel.At(", "OglWeatherModel.ForecastDay(",
    "OglWeatherPortalProjection.ConditionLabelDe(",
    "private static string Narration(",
):
    assert marker in xml_projection, marker
assert "HttpClient" not in xml_projection
assert "ElevenLabs" not in xml_projection, "No provider coupling in pure XML renderer"
assert "OglWeatherXmlProjection.cs" in regression

for value in (
    'protocol = "ogl.weather.portal"', "version = 1",
    'source = "simulated_oglweather"', "simulation = true",
    "current = new", "hourly,", "forecast = days",
    "forecast_days = days.Length",
    "precipitation_intensity_percent", "wind_kmh",
    "temperature_min_c", "temperature_max_c", "precipitation_hours",
    "TimeZoneInfo.ConvertTime(", "hourStart.AddHours(i)",
):
    assert value in projection, value
for value in (
    'Configs["OGLWeatherPortal"]', '"Enabled", false',
    '"WeatherSeed", 20261028', '"TimeZone", "Europe/Berlin"',
    '"DisplayName", "Stadt-NexVerse"',
    'OglWeatherPortalProjection.Serialize(',
    'string.Equals(request.HttpMethod, "GET"',
    "HttpStatusCode.MethodNotAllowed", "HttpStatusCode.ServiceUnavailable",
    'response.AddHeader("Allow", "GET")',
    'response.AddHeader("Cache-Control", "public, max-age=30")',
    'response.AddHeader("X-Content-Type-Options", "nosniff")',
):
    assert value in api, value
assert '"/api/v1/weather"' in connector
assert '"/weather/xml"' in connector
assert '"/api/v1/weather/xml"' in connector
assert connector.count("apiGate.Wrap(portalWeather.HandleXml)") == 2
assert 'OglWeatherXmlProjection.Serialize(' in api
assert 'public void HandleXml(' in api
assert 'response.ContentType = "application/xml; charset=utf-8"' in api
assert 'XmlError(response, HttpStatusCode.MethodNotAllowed,' in api
assert 'XmlError(response, HttpStatusCode.ServiceUnavailable,' in api
assert 'response.AddHeader("X-Content-Type-Options", "nosniff")' in api
assert connector.index('"/weather/xml"') < connector.index(
    "FriendsService friendsService = new FriendsService(config);")
assert connector.index('"/api/v1/weather/xml"') < connector.index(
    "FriendsService friendsService = new FriendsService(config);")

assert "apiGate.Wrap(portalWeather.Handle)" in connector
assert connector.index('"/api/v1/weather"') < connector.index(
    "FriendsService friendsService = new FriendsService(config);"), (
        "Weather route must remain available when privileged API startup fails")
assert "m_Auth.TryAuthenticate" not in api
assert "Request.Query" not in api and "InputStream" not in api
for p in ("bin/Robust.ini.example", "bin/Robust.HG.ini.example",
          "bin/Robust.HG.OGLAsset.ini.example"):
    cfg = read(p)
    assert cfg.count("[OGLWeatherPortal]") == 1, p
    sec = cfg.split("[OGLWeatherPortal]", 1)[1]
    for term in ("Enabled = false", "WeatherSeed = 20261028",
                 'TimeZone = "Europe/Berlin"', 'DisplayName = "Stadt-NexVerse"'):
        assert term in sec, (p, term)
assert 'const WEATHER_URL = "/api/v1/weather";' in widget
assert "credentials:\"omit\"" in widget
assert "textContent" in widget and "innerHTML" not in widget
assert "setInterval(refresh,60000)" in widget
assert "Simuliertes Wetter" in widget
assert "5-Tage-Vorhersage" in widget
assert "GET /api/v1/weather" in docs
assert "WeatherSeed" in docs and "TimeZone" in docs
assert "OpenGenesisLINK.Addons.OGLWeather" in docs
assert "Verify World API OGLWeather portal contracts" in workflow
assert "GET /weather/xml" in docs
assert "/api/v1/weather/xml" in docs
assert "ElevenLabs" in docs
print("World API OGLWeather public widget contracts PASS")
