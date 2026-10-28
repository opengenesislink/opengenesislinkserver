// SPDX-License-Identifier: MPL-2.0
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using log4net;
using Mono.Addins;
using Nini.Config;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;

[assembly: Addin("NexSmartLight", Namespace = "OpenGenesisLINK", Version = "0.1.0")]
[assembly: AddinDependency("::OpenSim.Region.Framework", OpenSim.VersionInfo.VersionNumber)]

namespace OpenGenesisLINK.Addons.NexSmartLight
{
    /// <summary>
    /// Opt-in simulator-wide controller; broadcasts read-only state to
    /// in-world listeners. Never rezzes, moves, or deletes resident objects.
    /// </summary>
    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "NexSmartLight")]
    public sealed class NexSmartLightRegionModule : ISharedRegionModule
    {
        private sealed class RegionRuntime
        {
            public Scene Scene;
            public EventManager.OnFrameDelegate OnFrame;
            public long NextPublish;
        }

        private static readonly ILog Log = LogManager.GetLogger(typeof(NexSmartLightRegionModule));
        private readonly ConcurrentDictionary<UUID, RegionRuntime> m_Regions = new();
        private volatile bool m_Enabled;
        private volatile bool m_Closing;
        private int m_Channel;
        private int m_RefreshSeconds;
        private TimeZoneInfo m_TimeZone;
        private int m_NightStart;
        private int m_NightEnd;

        public string Name => "NexSmartLight";
        public Type ReplaceableInterface => null;

        public void Initialise(IConfigSource source)
        {
            IConfig cfg = source?.Configs["NexSmartLight"];
            m_Enabled = cfg?.GetBoolean("Enabled", false) ?? false;
            if (!m_Enabled) return;

            try
            {
                m_Channel = cfg.GetInt("LSLChannel", -835423);
                m_RefreshSeconds = Math.Clamp(
                    cfg.GetInt("RefreshSeconds", 60), 60, 300);
                if (m_Channel >= 0 || m_Channel == -835311 ||
                    m_Channel == -835421 && "NexSmartLight" != "NexTraffic" ||
                    m_Channel == -835422 && "NexSmartLight" != "NexTransit" ||
                    m_Channel == -835423 && "NexSmartLight" != "NexSmartLight")
                    throw new FormatException("Use the dedicated negative LSL channel");
                            m_TimeZone = TimeZoneInfo.FindSystemTimeZoneById(
                cfg.GetString("TimeZone", "Europe/Berlin").Trim());
            m_NightStart = NexSmartLightModel.ParseTime(
                cfg.GetString("NightStarts", "18:00"));
            m_NightEnd = NexSmartLightModel.ParseTime(
                cfg.GetString("NightEnds", "06:00"));
                Log.InfoFormat("[NEXSMARTLIGHT]: simulator-wide service enabled, channel={0}", m_Channel);
            }
            catch (Exception e)
            {
                Log.Error("[NEXSMARTLIGHT]: invalid configuration; module disabled.", e);
                m_Enabled = false;
            }
        }

        public void PostInitialise() { }

        public void AddRegion(Scene scene)
        {
            if (!m_Enabled || m_Closing || scene == null) return;
            var runtime = new RegionRuntime { Scene = scene };
            runtime.OnFrame = () => Publish(runtime);
            m_Regions.TryAdd(scene.RegionInfo.RegionID, runtime);
        }

        public void RegionLoaded(Scene scene)
        {
            if (!m_Enabled || m_Closing || scene == null) return;
            if (m_Regions.TryGetValue(scene.RegionInfo.RegionID, out RegionRuntime runtime))
            {
                scene.EventManager.OnFrame -= runtime.OnFrame;
                scene.EventManager.OnFrame += runtime.OnFrame;
            }
        }

        private void Publish(RegionRuntime state)
        {
            if (!m_Enabled || m_Closing) return;
            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            if (now < state.NextPublish) return;
            // Advance before any work so missing chat modules do not cause a
            // per-frame spin or log flood. One publication per interval.
            state.NextPublish = now + m_RefreshSeconds;

            try
            {
                IWorldComm comm = state.Scene.RequestModuleInterface<IWorldComm>();
                if (comm == null) return;
                Vector3 center = new Vector3(
                    state.Scene.RegionInfo.RegionSizeX / 2f,
                    state.Scene.RegionInfo.RegionSizeY / 2f, 20f);
                            DateTime local = TimeZoneInfo.ConvertTime(
                DateTimeOffset.UtcNow, m_TimeZone).DateTime;
            bool on = NexSmartLightModel.IsOn(local, m_NightStart, m_NightEnd);
            comm.DeliverMessage(ChatTypeEnum.Region, m_Channel,
                "NexSmartLight", UUID.Zero,
                NexSmartLightModel.Wire(local, on), center);
            }
            catch (Exception e)
            {
                Log.Warn("[NEXSMARTLIGHT]: region state publication failed.", e);
            }
        }

        public void RemoveRegion(Scene scene)
        {
            if (scene != null &&
                m_Regions.TryRemove(scene.RegionInfo.RegionID, out RegionRuntime runtime))
                scene.EventManager.OnFrame -= runtime.OnFrame;
        }

        public void Close()
        {
            m_Closing = true;
            foreach (RegionRuntime runtime in m_Regions.Values)
                runtime.Scene.EventManager.OnFrame -= runtime.OnFrame;
            m_Regions.Clear();
            m_Enabled = false;
        }
    }
}
