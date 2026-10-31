// SPDX-License-Identifier: MPL-2.0
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Text;
using log4net;
using Mono.Addins;
using Nini.Config;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Framework.Console;
using Caps = OpenSim.Framework.Capabilities.Caps;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;

[assembly: Addin("OGLWeather", Namespace = "OpenGenesisLINK", Version = "0.1.0")]
[assembly: AddinDependency("::OpenSim.Region.Framework", OpenSim.VersionInfo.VersionNumber)]

namespace OpenGenesisLINK.Addons.OGLWeather
{
    /// <summary>
    /// One shared module instance serves EVERY region of this simulator.
    /// Provides one shared forecast for every region in this simulator and a
    /// read-only per-agent CAPS snapshot for future viewer-native precipitation.
    /// For unmodified Firestorm, temporarily creates nonpersistent weather emitters;
    /// never persists them or touches resident-owned scene objects.
    /// </summary>
    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "OGLWeather")]
    public sealed class OglWeatherRegionModule : ISharedRegionModule
    {
        private sealed class RegionRuntime
        {
            public Scene Scene;
            public EventManager.OnFrameDelegate Handler;
            public EventManager.RegisterCapsEvent CapsHandler;
            public long NextNow;
            public long NextDays;
            public long NextVisual;
            public string LastVisual = string.Empty;
            public string LastSkyVisual = string.Empty;
            public readonly Dictionary<UUID, ViewerEnvironment> WeatherSkies = new();
            public readonly object WeatherLock = new object();
            public readonly List<SceneObjectGroup> Emitters = new List<SceneObjectGroup>();
        }

        private static readonly ILog Log = LogManager.GetLogger(typeof(OglWeatherRegionModule));
        private readonly ConcurrentDictionary<UUID, RegionRuntime> m_Regions = new();
        private readonly OglWeatherTestOverride m_TestOverride = new();
        private int m_TestWeatherIntensity;
        private int m_TestWeatherDurationMinutes;
        private bool m_Enabled;
        private bool m_Closing;
        private int m_Seed;
        private int m_Channel;
        private int m_CurrentSeconds;
        private int m_DailySeconds;
        private TimeZoneInfo m_TimeZone;
        private int m_CloudBaseMeters;
        private bool m_LegacyParticleEmitters;
        private bool m_ViewerEnvironmentEffects;
        private int m_EmitterHeightMeters;
        private int m_EmitterSpacingMeters;
        private int m_MaxEmittersPerRegion;
        private int m_VisualRefreshSeconds;

        public string Name => "OGLWeather";
        public Type ReplaceableInterface => null;

        public void Initialise(IConfigSource source)
        {
            IConfig cfg = source?.Configs["OGLWeather"];
            m_Enabled = cfg?.GetBoolean("Enabled", false) ?? false;
            if (!m_Enabled) return;

            m_Seed = cfg.GetInt("WeatherSeed", 20261028);
            m_Channel = cfg.GetInt("LSLChannel", -835311);
            m_CurrentSeconds = Math.Clamp(cfg.GetInt("CurrentBroadcastSeconds", 60), 30, 300);
            m_DailySeconds = Math.Clamp(cfg.GetInt("ForecastBroadcastSeconds", 300), 60, 3600);
            m_CloudBaseMeters = Math.Clamp(cfg.GetInt("CloudBaseMeters", 200), 64, 2048);
            m_LegacyParticleEmitters = cfg.GetBoolean("LegacyParticleEmitters", true);
            m_ViewerEnvironmentEffects = cfg.GetBoolean("ViewerEnvironmentEffects", true);
            m_EmitterHeightMeters = Math.Clamp(cfg.GetInt("EmitterHeightMeters", 22), 12, 80);
            m_EmitterSpacingMeters = Math.Clamp(cfg.GetInt("EmitterSpacingMeters", 96), 32, 256);
            m_MaxEmittersPerRegion = Math.Clamp(cfg.GetInt("MaxEmittersPerRegion", 16), 1, 64);
            m_VisualRefreshSeconds = Math.Clamp(cfg.GetInt("VisualRefreshSeconds", 12), 5, 300);
            if (m_Channel >= 0)
            {
                Log.Error("[OGL-WEATHER]: LSLChannel muss negativ sein; Modul deaktiviert.");
                m_Enabled = false;
                return;
            }

            string zoneName = cfg.GetString("TimeZone", "Europe/Berlin").Trim();
            try
            {
                m_TimeZone = TimeZoneInfo.FindSystemTimeZoneById(zoneName);
            }
            catch (Exception ex)
            {
                // Wrong timezones would silently make the forecast date incorrect.
                m_Enabled = false;
                Log.Error("[OGL-WEATHER]: Unbekannte Zeitzone; Modul deaktiviert: " + zoneName, ex);
                return;
            }

            // Test weather is a bounded, simulator-local operator override.
            // Never enable indefinitely, expose a public write API, or
            // change the immutable World API / five-day climate forecast.
            m_TestWeatherIntensity = Math.Clamp(cfg.GetInt("TestWeatherIntensity", 70), 1, 100);
            m_TestWeatherDurationMinutes = Math.Clamp(
                cfg.GetInt("TestWeatherDurationMinutes", 15), 1, 60);
            string initialMode = cfg.GetString("TestWeatherMode", "auto")
                .Trim().ToLowerInvariant();
            if (initialMode == "rain" || initialMode == "snow")
                m_TestOverride.Activate(initialMode, m_TestWeatherIntensity,
                    m_TestWeatherDurationMinutes, DateTimeOffset.UtcNow);
            else if (initialMode != "auto")
                Log.Warn("[OGL-WEATHER]: Invalid TestWeatherMode, using auto.");

            // Available only from the trusted simulator operator console.
            // Not an inworld chat command, LSL endpoint or HTTP route.
            MainConsole.Instance?.Commands.AddCommand("Weather", true,
                "ogl weather",
                "ogl weather <status|auto|rain|snow> [intensity 1-100] [minutes 1-60]",
                "Temporarily force rain or snow in all regions of this simulator.",
                HandleWeatherConsole);

            Log.InfoFormat("[OGL-WEATHER]: Simulatorweite deterministische Wettervorhersage aktiv, Seed={0}, Zone={1}",
                m_Seed, m_TimeZone.Id);
        }

        private void HandleWeatherConsole(string module, string[] cmd)
        {
            if (!m_Enabled || m_Closing || cmd.Length < 3 ||
                cmd.Length > 5 || !string.Equals(cmd[0], "ogl",
                    StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(cmd[1], "weather",
                    StringComparison.OrdinalIgnoreCase))
            {
                MainConsole.Instance?.Output(
                    "Usage: ogl weather <status|auto|rain|snow> [intensity 1-100] [minutes 1-60]");
                return;
            }

            string mode = cmd[2].ToLowerInvariant();
            if (mode == "status" && cmd.Length == 3)
            {
                MainConsole.Instance?.Output("[OGL-WEATHER]: " +
                    m_TestOverride.Status(DateTimeOffset.UtcNow));
                return;
            }
            if (mode == "auto" && cmd.Length == 3)
            {
                m_TestOverride.Clear();
                RefreshAllWeather();
                MainConsole.Instance?.Output(
                    "[OGL-WEATHER]: Automatic simulation restored.");
                return;
            }
            if ((mode != "rain" && mode != "snow") || cmd.Length > 5)
            {
                MainConsole.Instance?.Output(
                    "Usage: ogl weather <status|auto|rain|snow> [intensity 1-100] [minutes 1-60]");
                return;
            }

            int intensity = m_TestWeatherIntensity;
            int minutes = m_TestWeatherDurationMinutes;
            if ((cmd.Length >= 4 && !int.TryParse(cmd[3], out intensity)) ||
                (cmd.Length >= 5 && !int.TryParse(cmd[4], out minutes)) ||
                !m_TestOverride.Activate(mode, intensity, minutes,
                    DateTimeOffset.UtcNow))
            {
                MainConsole.Instance?.Output(
                    "[OGL-WEATHER]: Intensity must be 1..100, duration 1..60 minutes.");
                return;
            }

            RefreshAllWeather();
            MainConsole.Instance?.Output("[OGL-WEATHER]: Test " + mode +
                " at " + intensity + "% for " + minutes + " minutes (all simulator regions).");
        }

        private void RefreshAllWeather()
        {
            // The next region frame immediately re-publishes NOW and
            // reconfigures only module-owned temporary emitters.
            foreach (RegionRuntime state in m_Regions.Values)
            {
                state.NextNow = 0;
                state.NextVisual = 0;
            }
        }

        public void PostInitialise() { }

        public void AddRegion(Scene scene)
        {
            if (!m_Enabled || m_Closing || scene == null) return;
            var region = new RegionRuntime { Scene = scene };
            region.Handler = () => OnFrame(region);
            region.CapsHandler = (agentID, caps) => OnRegisterCaps(region, agentID, caps);
            if (m_Regions.TryAdd(scene.RegionInfo.RegionID, region))
                scene.EventManager.OnRegisterCaps += region.CapsHandler;
        }

        public void RegionLoaded(Scene scene)
        {
            if (!m_Enabled || m_Closing || scene == null) return;
            if (m_Regions.TryGetValue(scene.RegionInfo.RegionID, out RegionRuntime runtime))
            {
                // Called exactly once for each loaded Scene.
                scene.EventManager.OnFrame -= runtime.Handler;
                scene.EventManager.OnFrame += runtime.Handler;
            }
        }

        private void OnRegisterCaps(RegionRuntime runtime, UUID agentID, Caps caps)
        {
            if (!m_Enabled || m_Closing || caps == null)
                return;

            caps.RegisterSimpleHandler("OGLWeatherState",
                new SimpleStreamHandler("/" + UUID.Random(),
                    (request, response) => GetViewerWeather(
                        runtime, agentID, request, response)));
        }

        private void GetViewerWeather(RegionRuntime runtime, UUID agentID,
            IOSHttpRequest request, IOSHttpResponse response)
        {
            response.ContentType = "application/json; charset=utf-8";
            response.AddHeader("Cache-Control", "no-store");

            if (!string.Equals(request?.HttpMethod, "GET",
                    StringComparison.OrdinalIgnoreCase))
            {
                response.StatusCode = (int)HttpStatusCode.MethodNotAllowed;
                return;
            }

            if (m_Closing || !m_Enabled ||
                !m_Regions.TryGetValue(runtime.Scene.RegionInfo.RegionID,
                    out RegionRuntime current) ||
                !object.ReferenceEquals(current, runtime))
            {
                response.StatusCode = (int)HttpStatusCode.ServiceUnavailable;
                return;
            }

            // Capability URL is issued per simulator/agent session; revoke
            // effective access on crossing and after the root agent leaves.
            ScenePresence avatar = runtime.Scene.GetScenePresence(agentID);
            if (avatar == null || avatar.IsChildAgent)
            {
                response.StatusCode = (int)HttpStatusCode.Forbidden;
                return;
            }

            try
            {
                // WeatherLock serializes this snapshot against Apply/Clear on
                // the region frame, crossing, or shutdown paths. Counting only
                // living, tracked scene objects avoids reporting "active" when
                // emitters are configured but none could be created.
                bool legacyActive;
                lock (runtime.WeatherLock)
                {
                    int liveEmitters = 0;
                    foreach (SceneObjectGroup emitter in runtime.Emitters)
                    {
                        if (emitter != null && !emitter.IsDeleted)
                            liveEmitters++;
                    }
                    legacyActive = OglWeatherViewerState.LegacyEmittersActive(
                        m_LegacyParticleEmitters, liveEmitters);
                }

                DateTimeOffset utc = DateTimeOffset.UtcNow;
                WeatherReading natural = OglWeatherModel.At(utc, m_Seed, m_TimeZone);
                bool testActive = m_TestOverride.TryGet(utc, natural,
                    out WeatherReading effective);
                string payload = OglWeatherViewerState.Serialize(
                    runtime.Scene.RegionInfo.RegionID.ToString(),
                    utc, m_Seed, m_TimeZone,
                    m_CloudBaseMeters, legacyActive,
                    testActive ? effective : null);
                response.RawBuffer = Encoding.UTF8.GetBytes(payload);
                response.StatusCode = (int)HttpStatusCode.OK;
            }
            catch (Exception ex)
            {
                Log.Warn("[OGL-WEATHER]: Viewer Wetter-CAPS konnte nicht geliefert werden.", ex);
                response.StatusCode = (int)HttpStatusCode.InternalServerError;
            }
        }

        private void OnFrame(RegionRuntime state)
        {
            if (!m_Enabled || m_Closing) return;
            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            if (now < state.NextNow && now < state.NextDays &&
                ((!m_LegacyParticleEmitters && !m_ViewerEnvironmentEffects) ||
                 now < state.NextVisual))
                return;

            // Each region has its own cadence. All of them use the same seed,
            // timezone and pure schedule, so no per-region random drift.
            try
            {
                IWorldComm comm = state.Scene.RequestModuleInterface<IWorldComm>();
                // Do not couple legacy precipitation to the availability of
                // the region chat service (forecast boards are optional).
                if (comm == null && !m_LegacyParticleEmitters &&
                    !m_ViewerEnvironmentEffects) return;
                Vector3 location = new Vector3(state.Scene.RegionInfo.RegionSizeX / 2f,
                    state.Scene.RegionInfo.RegionSizeY / 2f, 20f);
                DateTimeOffset utc = DateTimeOffset.UtcNow;
                WeatherReading natural = OglWeatherModel.At(utc, m_Seed, m_TimeZone);
                bool testActive = m_TestOverride.TryGet(utc, natural,
                    out WeatherReading effective);

                if (comm != null && now >= state.NextNow)
                {
                    state.NextNow = now + m_CurrentSeconds;
                    comm.DeliverMessage(ChatTypeEnum.Region, m_Channel,
                        "OGLWeather", UUID.Zero,
                        testActive
                            ? OglWeatherTestOverride.NowWire(utc, m_TimeZone, effective)
                            : OglWeatherModel.NowWire(utc, m_Seed, m_TimeZone),
                        location);
                }
                if (comm != null && now >= state.NextDays)
                {
                    state.NextDays = now + m_DailySeconds;
                    DateTime localDate = TimeZoneInfo.ConvertTime(utc, m_TimeZone).Date;
                    for (int i = 0; i < 5; ++i)
                    {
                        comm.DeliverMessage(ChatTypeEnum.Region, m_Channel,
                            "OGLWeather", UUID.Zero,
                            OglWeatherModel.DayWire(localDate.AddDays(i + 1),
                                m_Seed, i, m_TimeZone), location);
                    }
                }

                if (now >= state.NextVisual &&
                    (m_LegacyParticleEmitters || m_ViewerEnvironmentEffects))
                {
                    state.NextVisual = now + m_VisualRefreshSeconds;
                    WeatherReading weather = effective;
                    string signature = weather.Condition + "|" + weather.Intensity;
                    bool wet = weather.Condition == "snow" ||
                        weather.Condition == "rain";
                    bool overcast = wet || weather.Condition == "cloud";
                    lock (state.WeatherLock)
                    {
                        if (m_LegacyParticleEmitters)
                        {
                            // Existing sources are moved above the active
                            // viewers even when the weather has not changed.
                            // A static grid misses most avatars in Firestorm.
                            OglWeatherLegacyEmitters.Apply(
                                state.Scene, state.Emitters, weather,
                                m_EmitterHeightMeters, m_EmitterSpacingMeters,
                                m_MaxEmittersPerRegion);
                            state.LastVisual = signature;
                        }
                        if (m_ViewerEnvironmentEffects && overcast)
                        {
                            // Environment changes are transient and per agent.
                            // NEVER call StoreOnRegion: it writes estate EEP
                            // into the persistent database.
                            if (state.LastSkyVisual != signature)
                                OglWeatherViewerEnvironment.Clear(
                                    state.Scene, state.WeatherSkies);
                            OglWeatherViewerEnvironment.Apply(
                                state.Scene, state.WeatherSkies, weather);
                            state.LastSkyVisual = signature;
                        }
                        else if (state.WeatherSkies.Count > 0)
                        {
                            OglWeatherViewerEnvironment.Clear(
                                state.Scene, state.WeatherSkies);
                            state.LastSkyVisual = string.Empty;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // A weather failure must never interrupt a region frame.
                Log.Warn("[OGL-WEATHER]: Region-Wetterbroadcast fehlgeschlagen.", ex);
            }
        }

        public void RemoveRegion(Scene scene)
        {
            if (scene == null) return;
            if (m_Regions.TryRemove(scene.RegionInfo.RegionID, out RegionRuntime state))
            {
                scene.EventManager.OnFrame -= state.Handler;
                scene.EventManager.OnRegisterCaps -= state.CapsHandler;
                lock (state.WeatherLock)
                {
                    OglWeatherLegacyEmitters.Clear(scene, state.Emitters);
                    OglWeatherViewerEnvironment.Clear(scene, state.WeatherSkies);
                }
            }
        }

        public void Close()
        {
            m_Closing = true;
            foreach (RegionRuntime state in m_Regions.Values)
            {
                state.Scene.EventManager.OnFrame -= state.Handler;
                state.Scene.EventManager.OnRegisterCaps -= state.CapsHandler;
                lock (state.WeatherLock)
                {
                    OglWeatherLegacyEmitters.Clear(state.Scene, state.Emitters);
                    OglWeatherViewerEnvironment.Clear(state.Scene, state.WeatherSkies);
                }
            }
            m_Regions.Clear();
            m_TestOverride.Clear();
            m_Enabled = false;
        }
    }
}
