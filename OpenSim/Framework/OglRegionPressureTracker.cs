// SPDX-License-Identifier: MPL-2.0
// OpenGenesisLINK G1: non-destructive, opt-in region-pressure sampling.
using System;
using System.Collections.Generic;

namespace OpenSim.Framework
{
    /// <summary>
    /// Monotonic per-region frame counters sampled independently of the
    /// NodeAgent histogram. Handles regions joining, leaving, and restarting
    /// without aggregating incompatible lifetime counters.
    /// </summary>
    public readonly struct OglRegionPressureCounter
    {
        public string RegionId { get; }
        public long Frames { get; }
        public long WorkOverruns { get; }

        public OglRegionPressureCounter(string regionId, long frames, long workOverruns)
        {
            RegionId = regionId ?? string.Empty;
            Frames = frames;
            WorkOverruns = workOverruns;
        }
    }

    public sealed class OglRegionPressureTracker
    {
        private readonly object m_Lock = new object();
        private readonly Dictionary<string, OglRegionPressureCounter> m_Last =
            new Dictionary<string, OglRegionPressureCounter>(StringComparer.Ordinal);

        /// <summary>
        /// Returns null for a baseline-only sample. A newly discovered or
        /// restarted region contributes only after a second observation.
        /// Removed regions do not dilute the remaining regions' pressure.
        /// </summary>
        public double? Sample(IReadOnlyList<OglRegionPressureCounter> regions)
        {
            lock (m_Lock)
            {
                long totalFrames = 0;
                long totalOverruns = 0;
                var observed = new HashSet<string>(StringComparer.Ordinal);

                if (regions != null)
                {
                    foreach (OglRegionPressureCounter region in regions)
                    {
                        if (string.IsNullOrWhiteSpace(region.RegionId) ||
                            region.Frames < 0 || region.WorkOverruns < 0 ||
                            region.WorkOverruns > region.Frames ||
                            !observed.Add(region.RegionId))
                            continue;

                        if (m_Last.TryGetValue(region.RegionId,
                                out OglRegionPressureCounter previous) &&
                            region.Frames >= previous.Frames &&
                            region.WorkOverruns >= previous.WorkOverruns)
                        {
                            long frameDelta = region.Frames - previous.Frames;
                            long overrunDelta = region.WorkOverruns - previous.WorkOverruns;
                            if (frameDelta > 0 && overrunDelta <= frameDelta)
                            {
                                totalFrames += frameDelta;
                                totalOverruns += overrunDelta;
                            }
                        }

                        m_Last[region.RegionId] = region;
                    }
                }

                // Do not leave stale regions in the baseline: their old
                // lifetime counts must never affect a restarted scene.
                var missing = new List<string>();
                foreach (string id in m_Last.Keys)
                    if (!observed.Contains(id))
                        missing.Add(id);
                foreach (string id in missing)
                    m_Last.Remove(id);

                return totalFrames > 0
                    ? Math.Clamp(100.0 * totalOverruns / totalFrames, 0.0, 100.0)
                    : null;
            }
        }

        public void Reset()
        {
            lock (m_Lock)
                m_Last.Clear();
        }
    }
}
