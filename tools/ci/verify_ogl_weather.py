#!/usr/bin/env python3
"""Fail-closed checks for isolated, simulator-wide OGLWeather add-on."""
from pathlib import Path
root = Path(__file__).resolve().parents[2]
def read(path):
    return (root / path).read_text(encoding="utf-8-sig")

model = read("NexVerse/Core/Weather/OglWeatherModel.cs")
assert not (root / "addons/OGLWeather/OglWeatherModel.cs").exists(), "No independent weather model allowed"
module = read("addons/OGLWeather/OglWeatherRegionModule.cs")
viewer = read("addons/OGLWeather/OglWeatherViewerState.cs")
legacy = read("addons/OGLWeather/OglWeatherLegacyEmitters.cs")
plan = read("addons/OGLWeather/OglWeatherEmitterPlan.cs")
board = read("addons/OGLWeather/WeatherForecastBoard.lsl")
config = read("addons/OGLWeather/OGLWeather.ini.example")
builder = read("addon-modules/OGLWeather/build.sh")
csproj = read("addons/OGLWeather/OpenGenesisLINK.Addons.OGLWeather.csproj")
workflow = read(".github/workflows/nexjast-ci.yml")
for term in ("public static WeatherReading At(", "public static WeatherDay ForecastDay(",
             "TimeZoneInfo.ConvertTimeFromUtc(hourUtc, zone)", "NowWire(", "DayWire(",
             '"OGLWX|1|NOW|"', '"OGLWX|1|DAY|"'):
    assert term in model, term
for term in ("ISharedRegionModule", 'Id = "OGLWeather"',
             "ConcurrentDictionary<UUID, RegionRuntime>",
             "IWorldComm", "EventManager.OnFrame",
             'Configs["OGLWeather"]', '"Enabled", false',
             'm_Regions.TryAdd(', "m_Channel",
             'comm.DeliverMessage(ChatTypeEnum.Region',
             '"OGLWeather", UUID.Zero', "localDate.AddDays(i + 1)",
             "state.Scene.EventManager.OnFrame -= state.Handler",
             "m_Regions.Clear();"):
    assert term in module, term
assert "INonSharedRegionModule" not in module
assert "OGLSeasons" not in module and "OGLSeasons" not in model
assert "Enabled = false" in config and 'TimeZone = "Europe/Berlin"' in config
assert "WeatherSeed = 20261028" in config
assert not (root / "addons/OGLWeather/WeatherEmitter.lsl").exists(), "No manually placed LSL emitters"
assert "llParticleSystem(" not in module and "AddNewPrim" not in module
for marker in (
    'LegacyParticleEmitters", true', 'EmitterHeightMeters", 65',
    'EmitterSpacingMeters", 96', 'MaxEmittersPerRegion", 16',
    'VisualRefreshSeconds", 60',
    'OglWeatherLegacyEmitters.Apply(', 'OglWeatherLegacyEmitters.Clear(',
    'lock (state.WeatherLock)',
):
    assert marker in module, marker
for marker in (
    'scene.AddNewSceneObject(group, false)',
    'PrimitiveBaseShape.CreateBox()',
    'texture.DefaultTexture.RGBA = new Color4(1f, 1f, 1f, 0f)',
    'scene.GetGroundHeight(x, y)',
    'group.RootPart.AddFlag(PrimFlags.Phantom)',
    'group.RootPart.AddNewParticleSystem(',
    'group.RootPart.RemoveParticleSystem()',
    'scene?.DeleteSceneObject(group, false)',
):
    assert marker in legacy, marker
assert 'OglWeatherEmitterPlan.Positions(' in legacy
assert 'Math.Clamp(maximum, 1, 64)' in plan
assert 'while (columns * (long)rows > maximum)' in plan
assert 'legacy_prim_emitters_active = legacyParticleEmitters' in viewer
assert 'LegacyEmittersActive(bool enabled, int liveEmitterCount)' in viewer
assert 'enabled && liveEmitterCount > 0' in viewer
assert 'lock (runtime.WeatherLock)' in module, "CAPS must snapshot under emitter lifecycle lock"
assert 'emitter != null && !emitter.IsDeleted' in module
assert 'OglWeatherViewerState.LegacyEmittersActive(' in module
assert 'm_CloudBaseMeters, legacyActive' in module
assert 'm_CloudBaseMeters, m_LegacyParticleEmitters' not in module, "Do not report config as actual emitters"

assert 'LegacyParticleEmitters = true' in config
assert 'EmitterHeightMeters = 65' in config
assert 'MaxEmittersPerRegion = 16' in config
assert 'caps.RegisterSimpleHandler("OGLWeatherState"' in module
assert "scene.EventManager.OnRegisterCaps += region.CapsHandler" in module
assert "state.Scene.EventManager.OnRegisterCaps -= state.CapsHandler" in module
for marker in (
    'm_CloudBaseMeters = Math.Clamp(cfg.GetInt("CloudBaseMeters", 200), 64, 2048)',
    'HttpStatusCode.MethodNotAllowed',
    'HttpStatusCode.ServiceUnavailable',
    'HttpStatusCode.Forbidden',
    'runtime.Scene.GetScenePresence(agentID)',
    'avatar.IsChildAgent',
    'response.AddHeader("Cache-Control", "no-store")',
    'OglWeatherViewerState.Serialize(',
):
    assert marker in module, marker
for marker in (
    'protocol = "ogl.weather"', 'version = 1',
    'origin = "clouds"', 'rendering = "viewer_native"',
    'requires_viewer_support = true', 'cloud_base_m = cloudBaseMeters',
    'OglWeatherModel.At(utc, seed, timeZone)',
    'OglWeatherModel.ForecastDay(',
):
    assert marker in viewer, marker
assert "CloudBaseMeters = 200" in config
assert "osSetDynamicTextureDataFace(" in board
assert "gRendered = signature" in board
assert "llSetTexture(" not in board
assert "OGLWeather" in csproj
assert "robust/bin" in builder and "--sim-bin" in builder
assert "Weather Forecast regression" in workflow
assert not (root / "addons/OGLSeasons").exists(), "retired addon sources still tracked"
assert not (root / "addon-modules/OGLSeasons").exists(), "retired build scripts still tracked"
for path in (
    "tools/ci/verify_ogl_seasons.py",
    "tools/ci/verify_ogl_seasons_eep.py",
    "tools/ci/OglSeasonsRegression",
    "tools/ci/OglSeasonsEepRegression",
):
    assert not (root / path).exists(), "old weather test survived: " + path
assert "OGLSeasons" not in workflow, "obsolete CI weather gate still active"
assert "OGLSeasons" not in read("runprebuild.sh"), "obsolete compile step still active"
assert "OGLWeather" in read("runprebuild.sh"), "new addon missing from compile script"
print("OGLWeather static contracts: simulator-wide, deterministic, opt-in, native viewer CAPS plus bounded ephemeral Firestorm particles PASS")
