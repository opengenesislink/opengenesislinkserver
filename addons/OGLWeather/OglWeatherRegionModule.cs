// SPDX-License-Identifier: MPL-2.0
using System;
using System.Collections.Concurrent;
using log4net;
using Mono.Addins;
using Nini.Config;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;

[assembly: Addin("OGLWeather", Namespace = "OpenGenesisLINK", Version = "0.1.0")]
[assembly: AddinDependency("::OpenSim.Region.Framework", OpenSim.VersionInfo.VersionNumber)]

namespace OpenGenesisLINK.Addons.OGLWeather
{
    /// <summary>
    /// One shared module instance serves EVERY region of this simulator.
    /// Only broadcasts authenticated-by-origin region chat; an owner-installed
    /// LSL emitter renders particles locally to viewers (no server rezzing).
    /// </summary>
    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "OGLWeather")]
    public sealed class OglWeatherRegionModule : ISharedRegionModule
    {
        private sealed class RegionRuntime
        {
            public Scene Scene;
            public EventManager.OnFrameDelegate Handler;
            public long NextNow;
            public long NextDays;
        }

        private static readonly ILog Log = LogManager.GetLogger(typeof(OglWeatherRegionModule));
        private readonly ConcurrentDictionary<UUID, RegionRuntime> m_Regions = new();
        private bool m_Enabled;
        private bool m_Closing;
        private int m_Seed;
        private int m_Channel;
        private int m_CurrentSeconds;
        private int m_DailySeconds;
        private TimeZoneInfo m_TimeZone;

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

            Log.InfoFormat("[OGL-WEATHER]: Simulatorweite deterministische Wettervorhersage aktiv, Seed={0}, Zone={1}",
                m_Seed, m_TimeZone.Id);
        }

        public void PostInitialise() { }

        public void AddRegion(Scene scene)
        {
            if (!m_Enabled || m_Closing || scene == null) return;
            var region = new RegionRuntime { Scene = scene };
            region.Handler = () => OnFrame(region);
            m_Regions.TryAdd(scene.RegionInfo.RegionID, region);
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

        private void OnFrame(RegionRuntime state)
        {
            if (!m_Enabled || m_Closing) return;
            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            if (now < state.NextNow && now < state.NextDays) return;

            // Each region has its own cadence. All of them use the same seed,
            // timezone and pure schedule, so no per-region random drift.
            try
            {
                IWorldComm comm = state.Scene.RequestModuleInterface<IWorldComm>();
                if (comm == null) return;
                Vector3 location = new Vector3(state.Scene.RegionInfo.RegionSizeX / 2f,
                    state.Scene.RegionInfo.RegionSizeY / 2f, 20f);
                DateTimeOffset utc = DateTimeOffset.UtcNow;

                if (now >= state.NextNow)
                {
                    state.NextNow = now + m_CurrentSeconds;
                    comm.DeliverMessage(ChatTypeEnum.Region, m_Channel,
                        "OGLWeather", UUID.Zero,
                        OglWeatherModel.NowWire(utc, m_Seed, m_TimeZone), location);
                }
                if (now >= state.NextDays)
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
                scene.EventManager.OnFrame -= state.Handler;
        }

        public void Close()
        {
            m_Closing = true;
            foreach (RegionRuntime state in m_Regions.Values)
                state.Scene.EventManager.OnFrame -= state.Handler;
            m_Regions.Clear();
            m_Enabled = false;
        }
    }
}
