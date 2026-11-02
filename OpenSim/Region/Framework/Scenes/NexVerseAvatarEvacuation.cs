// SPDX-License-Identifier: MPL-2.0
// Graceful OpenGenesisLINK Gen1 restart/shutdown relocation. Grid-only,
// fail-closed region restarts; process shutdown is bounded best effort.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using log4net;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Services.Interfaces;
using GridRegion = OpenSim.Services.Interfaces.GridRegion;
using TPFlags = OpenSim.Framework.Constants.TeleportFlags;

namespace OpenSim.Region.Framework.Scenes
{
    public static class NexVerseAvatarEvacuation
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(NexVerseAvatarEvacuation));

        // Does not include child presences (neighbours), NPCs (no actual viewer
        // circuit), agents already transferring, or closed clients.
        private static List<ScenePresence> Residents(Scene scene)
        {
            return scene.GetScenePresences().Where(sp =>
                sp != null && !sp.IsChildAgent && !sp.IsNPC &&
                !sp.IsDeleted).ToList();
        }

        public static bool HasHumanRootAgents(Scene scene) =>
            scene != null && Residents(scene).Count != 0;

        /// <summary>
        /// Pick the closest ONLINE same-grid destination. Ignore every region
        /// on the shutting-down process and any explicitly excluded region.
        /// The grid service returns candidates in distance order and omits
        /// Hypergrid hyperlinks. A missing grid service is NOT a valid target.
        /// </summary>
        public static List<GridRegion> Destinations(Scene scene,
            ISet<UUID> excluded, int limit = 128)
        {
            if (scene == null) return new List<GridRegion>();
            List<GridRegion> online = scene.GridService.GetOnlineRegions(
                scene.RegionInfo.ScopeID,
                checked((int)scene.RegionInfo.WorldLocX),
                checked((int)scene.RegionInfo.WorldLocY),
                Math.Clamp(limit, 1, 1024));
            return (online ?? new List<GridRegion>())
                .Where(region => region != null && !region.RegionID.IsZero() &&
                    !excluded.Contains(region.RegionID) &&
                    region.RegionHandle != scene.RegionInfo.RegionHandle &&
                    region.RegionSizeX > 0 && region.RegionSizeY > 0 &&
                    region.RegionName?.Length > 0)
                .GroupBy(region => region.RegionID)
                .Select(group => group.First())
                .OrderBy(region =>
                {
                    double dx = (double)region.RegionLocX - scene.RegionInfo.WorldLocX;
                    double dy = (double)region.RegionLocY - scene.RegionInfo.WorldLocY;
                    return dx * dx + dy * dy;
                })
                .ThenBy(region => region.RegionID.ToString())
                .ToList();
        }

        /// <summary>
        /// Returns true only once all real root viewer presences are no longer
        /// rooted in the source region. A teleport request is asynchronous and
        /// may fail; issuing it is NOT counted as a successful evacuation.
        /// </summary>
        public static bool Evacuate(Scene scene, ISet<UUID> excluded, int waitSeconds,
            string reason = "Region restart")
        {
            if (scene == null) return true;
            List<ScenePresence> residents = Residents(scene);
            if (residents.Count == 0) return true;
            IEntityTransferModule transfer =
                scene.RequestModuleInterface<IEntityTransferModule>();
            if (transfer == null)
            {
                Log.WarnFormat("[NEX-EVAC]: {0} has no entity transfer service.", scene.Name);
                return false;
            }
            List<GridRegion> targets;
            try { targets = Destinations(scene, excluded); }
            catch (Exception e)
            {
                Log.Warn("[NEX-EVAC]: Grid online-region lookup failed; refusing to guess a target.", e);
                return false;
            }
            if (targets.Count == 0)
            {
                Log.WarnFormat("[NEX-EVAC]: No online grid destination for {0}.", scene.Name);
                foreach (ScenePresence sp in residents)
                    try { sp.ControllingClient?.SendAlertMessage(
                        "Region restart: no other online region is available."); }
                    catch (Exception) { }
                return false;
            }

            // Native entity transfer enforces access checks. Teleport() is
            // asynchronous: retry the next closest ONLINE destination only
            // after a request has had time to finish, and never while a
            // transfer is in progress. All residents share one bounded clock.
            var attempts = new Dictionary<UUID, int>();
            var lastAttempt = new Dictionary<UUID, long>();
            int options = Math.Min(targets.Count, 6);
            int timeoutMs = Math.Clamp(waitSeconds, 1, 90) * 1000;
            long until = Environment.TickCount64 + timeoutMs;
            while (Environment.TickCount64 < until)
            {
                List<ScenePresence> remaining = Residents(scene);
                if (remaining.Count == 0)
                {
                    Log.InfoFormat("[NEX-EVAC]: Moved {0} avatars away from {1}.",
                        residents.Count, scene.Name);
                    return true;
                }
                foreach (ScenePresence sp in remaining)
                {
                    if (sp.IsInTransit) continue;
                    int next = attempts.TryGetValue(sp.UUID, out int index) ? index : 0;
                    if (next >= options) continue;
                    long now = Environment.TickCount64;
                    if (lastAttempt.TryGetValue(sp.UUID, out long attempted) &&
                        now - attempted < 4000)
                        continue;

                    // Advance before dispatch so a synchronous rejection cannot
                    // cause the same destination to be tried indefinitely.
                    attempts[sp.UUID] = next + 1;
                    lastAttempt[sp.UUID] = now;
                    GridRegion destination = targets[next];
                    try
                    {
                        sp.ControllingClient?.SendAlertMessage(
                            reason + ": moving you to " + destination.RegionName + ".");
                        Vector3 landing = new Vector3(
                            Math.Clamp(destination.RegionSizeX / 2f, 1f,
                                destination.RegionSizeX - 1f),
                            Math.Clamp(destination.RegionSizeY / 2f, 1f,
                                destination.RegionSizeY - 1f),
                            35f);
                        transfer.Teleport(sp, destination.RegionHandle, landing,
                            new Vector3(1f, 0f, 0f), (uint)TPFlags.ViaLocation);
                    }
                    catch (Exception e)
                    {
                        Log.WarnFormat("[NEX-EVAC]: Teleport request for {0} to {1} failed: {2}",
                            sp.UUID, destination.RegionID, e.GetType().Name);
                    }
                }
                Thread.Sleep(250);
            }
            int left = Residents(scene).Count;
            Log.WarnFormat("[NEX-EVAC]: {0} root avatars still present in {1} after {2}s.",
                left, scene.Name, waitSeconds);
            return left == 0;
        }

        /// <summary>
        /// Excludes ALL regions hosted by this process: the destination must
        /// be on another simulator, even if a local region is technically up
        /// until the next Close() call.
        /// </summary>
        public static void EvacuateInstance(IReadOnlyCollection<Scene> scenes,
            int waitSeconds)
        {
            if (scenes == null || scenes.Count == 0) return;
            var excluded = new HashSet<UUID>(
                scenes.Where(s => s != null).Select(s => s.RegionInfo.RegionID));
            // One bounded budget for the *whole* process (not N times 25s
            // when there are N regions on this simulator).
            long deadline = Environment.TickCount64 +
                Math.Clamp(waitSeconds, 5, 90) * 1000L;
            foreach (Scene scene in scenes)
            {
                if (scene == null || !HasHumanRootAgents(scene)) continue;
                long remaining = deadline - Environment.TickCount64;
                if (remaining <= 0)
                {
                    Log.WarnFormat("[NEX-EVAC]: Shutdown deadline reached. " +
                        "Region {0} still has viewers; normal shutdown continues.", scene.Name);
                    continue;
                }
                int seconds = (int)Math.Max(1, (remaining + 999) / 1000);
                Evacuate(scene, excluded, seconds, "Simulator shutdown");
            }
        }
    }
}
