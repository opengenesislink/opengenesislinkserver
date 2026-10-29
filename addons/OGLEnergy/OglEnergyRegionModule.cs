// SPDX-License-Identifier: MPL-2.0
using System;
using System.Collections.Concurrent;
using log4net;
using Mono.Addins;
using Nini.Config;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Framework.Console;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;

[assembly: Addin("OGLEnergy", Namespace = "OpenGenesisLINK", Version = "0.1.0")]
[assembly: AddinDependency("::OpenSim.Region.Framework", OpenSim.VersionInfo.VersionNumber)]

namespace OpenGenesisLINK.Addons.OGLEnergy
{
    /// <summary>
    /// Simulator-wide, opt-in simulated power grid. Read-only LSL broadcasts;
    /// never modifies, scans or deletes resident objects or land.
    /// </summary>
    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "OGLEnergy")]
    public sealed class OglEnergyRegionModule : ISharedRegionModule
    {
        private sealed class RegionRuntime
        {
            public Scene Scene;
            public EventManager.OnFrameDelegate OnFrame;
            public long NextPublish;
        }

        private static readonly ILog Log = LogManager.GetLogger(typeof(OglEnergyRegionModule));
        private readonly ConcurrentDictionary<UUID, RegionRuntime> m_Regions = new();
        private bool m_Enabled;
        private bool m_Closing;
        private int m_Channel;
        private int m_RefreshSeconds;
        private TimeZoneInfo m_Zone;
        private OglEnergySettings m_Settings;

        public string Name => "OGLEnergy";
        public Type ReplaceableInterface => null;

        public void Initialise(IConfigSource source)
        {
            IConfig cfg = source?.Configs["OGLEnergy"];
            m_Enabled = cfg?.GetBoolean("Enabled", false) ?? false;
            if (!m_Enabled) return;
            try
            {
                m_Channel = cfg.GetInt("LSLChannel", -835431);
                // Dedicated channel must not collide with known city add-ons.
                if (m_Channel >= 0 || m_Channel == -835311 ||
                    m_Channel == -835421 || m_Channel == -835422 ||
                    m_Channel == -835423)
                    throw new FormatException("OGLEnergy requires its own negative LSLChannel");
                m_RefreshSeconds = Math.Clamp(
                    cfg.GetInt("RefreshSeconds", 60), 30, 300);
                m_Zone = TimeZoneInfo.FindSystemTimeZoneById(
                    cfg.GetString("TimeZone", "Europe/Berlin").Trim());
                m_Settings = new OglEnergySettings(
                    cfg.GetInt("SolarCapacityKw", 900),
                    cfg.GetInt("WindCapacityKw", 1000),
                    cfg.GetInt("HydroCapacityKw", 500),
                    cfg.GetInt("BaseCapacityKw", 850),
                    cfg.GetInt("BaseDemandKw", 2200));

                // Local operator console only. This is not a public API.
                MainConsole.Instance?.Commands.AddCommand(
                    "Energy", true, "ogl energy",
                    "ogl energy status",
                    "Show simulated power generation and demand for this simulator.",
                    HandleStatus);

                Log.InfoFormat(
                    "[OGL-ENERGY]: grid enabled, channel={0}, interval={1}s, region count increases on load",
                    m_Channel, m_RefreshSeconds);
            }
            catch (Exception ex)
            {
                m_Enabled = false;
                Log.Error("[OGL-ENERGY]: invalid configuration; module disabled.", ex);
            }
        }

        private void HandleStatus(string module, string[] cmd)
        {
            if (cmd.Length != 3 || !string.Equals(cmd[0], "ogl",
                    StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(cmd[1], "energy",
                    StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(cmd[2], "status",
                    StringComparison.OrdinalIgnoreCase))
            {
                MainConsole.Instance?.Output("Usage: ogl energy status");
                return;
            }
            if (!m_Enabled || m_Closing) return;
            OglEnergyReading state = OglEnergyModel.At(
                DateTimeOffset.UtcNow, m_Zone, m_Settings);
            MainConsole.Instance?.Output(
                "[OGL-ENERGY]: " + state.Condition +
                "; production=" + state.SupplyKw + " kW" +
                "; demand=" + state.DemandKw + " kW" +
                "; reserve=" + state.ReserveKw + " kW" +
                "; active regions=" + m_Regions.Count);
        }

        public void PostInitialise() { }

        public void AddRegion(Scene scene)
        {
            if (!m_Enabled || m_Closing || scene == null) return;
            RegionRuntime state = new RegionRuntime { Scene = scene };
            state.OnFrame = () => Publish(state);
            m_Regions.TryAdd(scene.RegionInfo.RegionID, state);
        }

        public void RegionLoaded(Scene scene)
        {
            if (!m_Enabled || m_Closing || scene == null) return;
            if (m_Regions.TryGetValue(scene.RegionInfo.RegionID, out RegionRuntime state))
            {
                scene.EventManager.OnFrame -= state.OnFrame;
                scene.EventManager.OnFrame += state.OnFrame;
            }
        }

        private void Publish(RegionRuntime state)
        {
            if (!m_Enabled || m_Closing) return;
            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            if (now < state.NextPublish) return;
            // Avoid retrying on every frame if chat is absent.
            state.NextPublish = now + m_RefreshSeconds;
            try
            {
                IWorldComm comm = state.Scene.RequestModuleInterface<IWorldComm>();
                if (comm == null) return;

                DateTimeOffset utc = DateTimeOffset.UtcNow;
                OglEnergyReading reading = OglEnergyModel.At(utc, m_Zone, m_Settings);
                Vector3 center = new Vector3(
                    state.Scene.RegionInfo.RegionSizeX / 2f,
                    state.Scene.RegionInfo.RegionSizeY / 2f, 20f);
                comm.DeliverMessage(ChatTypeEnum.Region, m_Channel,
                    "OGLEnergy", UUID.Zero,
                    OglEnergyModel.Wire(utc, m_Zone, reading), center);
            }
            catch (Exception ex)
            {
                Log.Warn("[OGL-ENERGY]: region update failed.", ex);
            }
        }

        public void RemoveRegion(Scene scene)
        {
            if (scene == null) return;
            if (m_Regions.TryRemove(scene.RegionInfo.RegionID, out RegionRuntime state))
                scene.EventManager.OnFrame -= state.OnFrame;
        }

        public void Close()
        {
            m_Closing = true;
            foreach (RegionRuntime state in m_Regions.Values)
                state.Scene.EventManager.OnFrame -= state.OnFrame;
            m_Regions.Clear();
            m_Enabled = false;
        }
    }
}
