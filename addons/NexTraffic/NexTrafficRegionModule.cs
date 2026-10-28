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

[assembly: Addin("NexTraffic", Namespace = "OpenGenesisLINK", Version = "0.1.0")]
[assembly: AddinDependency("::OpenSim.Region.Framework", OpenSim.VersionInfo.VersionNumber)]

namespace OpenGenesisLINK.Addons.NexTraffic
{
    /// <summary>
    /// Opt-in simulator-wide controller; broadcasts read-only state to
    /// in-world listeners. Never rezzes, moves, or deletes resident objects.
    /// </summary>
    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "NexTraffic")]
    public sealed class NexTrafficRegionModule : ISharedRegionModule
    {
        private sealed class RegionRuntime
        {
            public Scene Scene;
            public EventManager.OnFrameDelegate OnFrame;
            public long NextPublish;
        }

        private static readonly ILog Log = LogManager.GetLogger(typeof(NexTrafficRegionModule));
        private readonly ConcurrentDictionary<UUID, RegionRuntime> m_Regions = new();
        private volatile bool m_Enabled;
        private volatile bool m_Closing;
        private int m_Channel;
        private int m_RefreshSeconds;
        private List<TrafficJunction> m_Junctions;
        private int m_GreenSeconds;
        private int m_YellowSeconds;
        private int m_AllRedSeconds;

        public string Name => "NexTraffic";
        public Type ReplaceableInterface => null;

        public void Initialise(IConfigSource source)
        {
            IConfig cfg = source?.Configs["NexTraffic"];
            m_Enabled = cfg?.GetBoolean("Enabled", false) ?? false;
            if (!m_Enabled) return;

            try
            {
                m_Channel = cfg.GetInt("LSLChannel", -835421);
                m_RefreshSeconds = Math.Clamp(
                    cfg.GetInt("RefreshSeconds", 5), 5, 300);
                if (m_Channel >= 0 || m_Channel == -835311 ||
                    m_Channel == -835421 && "NexTraffic" != "NexTraffic" ||
                    m_Channel == -835422 && "NexTraffic" != "NexTransit" ||
                    m_Channel == -835423 && "NexTraffic" != "NexSmartLight")
                    throw new FormatException("Use the dedicated negative LSL channel");
                            m_GreenSeconds = Math.Clamp(cfg.GetInt("GreenSeconds", 30), 10, 120);
            m_YellowSeconds = Math.Clamp(cfg.GetInt("YellowSeconds", 5), 2, 10);
            m_AllRedSeconds = Math.Clamp(cfg.GetInt("AllRedSeconds", 3), 1, 10);
            m_Junctions = NexTrafficModel.Parse(cfg.GetString(
                "Junctions", "Hauptstrasse:0;Marktplatz:11"));
                Log.InfoFormat("[NEXTRAFFIC]: simulator-wide service enabled, channel={0}", m_Channel);
            }
            catch (Exception e)
            {
                Log.Error("[NEXTRAFFIC]: invalid configuration; module disabled.", e);
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
                            foreach (TrafficJunction junction in m_Junctions)
            {
                TrafficPhase phase = NexTrafficModel.At(
                    now, junction.OffsetSeconds,
                    m_GreenSeconds, m_YellowSeconds, m_AllRedSeconds);
                comm.DeliverMessage(ChatTypeEnum.Region, m_Channel,
                    "NexTraffic", UUID.Zero,
                    NexTrafficModel.Wire(junction, phase, now), center);
            }
            }
            catch (Exception e)
            {
                Log.Warn("[NEXTRAFFIC]: region state publication failed.", e);
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
