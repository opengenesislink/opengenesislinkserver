// SPDX-License-Identifier: MPL-2.0

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using NexVerse.Core.Observability;
using OpenSim.Framework;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;

namespace NexVerse.RegionModules.NodeAgent
{
    /// <summary>
    /// Read-only NodeAgent performance heartbeat projection. It is deliberately
    /// independent of NexBus transports, credentials, update control and
    /// region mutation; the caller signs the resulting heartbeat as before.
    /// </summary>
    internal static class OglNodePerformanceHeartbeat
    {
        public static void Append(Scene[] scenes, OglPerformanceSampler sampler,
            IDictionary<string, string> destination)
        {
            if (scenes is null || sampler is null || destination is null)
                return;

            OglRegionFrame[] frames = scenes
                .OrderBy(scene => scene.RegionInfo.RegionID.ToString(), StringComparer.Ordinal)
                .Select(scene =>
                {
                    // Drain each per-region timing window at most once per
                    // signed heartbeat, never on the frame hot path.
                    OglRegionTimingSample? sample =
                        scene.OglPerformanceTiming?.TakeSnapshotAndReset();

                    return new OglRegionFrame(
                        scene.RegionInfo.RegionID.ToString(), scene.Frame, scene.FrameTime,
                        sample?.Frames ?? 0,
                        sample?.WorkAverageMs ?? 0,
                        sample?.FrameAverageMs ?? 0,
                        sample?.WorkP95Ms ?? 0,
                        sample?.FrameP95Ms ?? 0,
                        sample?.FrameP99Ms ?? 0,
                        sample?.WorkOverBudgetPercent ?? 0,
                        sample?.FrameOverBudgetPercent ?? 0);
                })
                .ToArray();

            foreach (KeyValuePair<string, string> metric in sampler.Capture(frames))
                destination[metric.Key] = metric.Value;

            // Discover the active shared cache only through the public
            // optional interface, not concrete Flotsam implementation.
            if (scenes.Length > 0 &&
                scenes[0].RequestModuleInterface<IAssetCache>()
                    is IOglAssetCacheMetricsProvider provider)
            {
                foreach (KeyValuePair<string, long> metric in provider.CaptureAssetCacheMetrics())
                    destination[metric.Key] =
                        metric.Value.ToString(CultureInfo.InvariantCulture);
            }
        }
    }
}
