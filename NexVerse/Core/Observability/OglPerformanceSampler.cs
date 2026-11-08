// SPDX-License-Identifier: MPL-2.0
// OpenGenesisLINK G1: opt-in process and per-region performance diagnostics.
// Sampling is read-only: it never changes simulation timing, thread affinity or GC policy.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading;

namespace NexVerse.Core.Observability
{
    public readonly struct OglRegionFrame
    {
        public string Id { get; }
        public uint Frame { get; }
        public float TargetFrameSeconds { get; }
        public long SampledFrames { get; }
        public double WorkAverageMs { get; }
        public double FrameAverageMs { get; }
        public double WorkP95Ms { get; }
        public double FrameP95Ms { get; }
        public double FrameP99Ms { get; }
        public double WorkOverBudgetPercent { get; }
        public double FrameOverBudgetPercent { get; }

        public OglRegionFrame(string id, uint frame, float targetFrameSeconds,
            long sampledFrames = 0, double workAverageMs = 0, double frameAverageMs = 0,
            double workP95Ms = 0, double frameP95Ms = 0, double frameP99Ms = 0,
            double workOverBudgetPercent = 0, double frameOverBudgetPercent = 0)
        {
            Id = id ?? string.Empty;
            Frame = frame;
            TargetFrameSeconds = targetFrameSeconds;
            SampledFrames = sampledFrames;
            WorkAverageMs = workAverageMs;
            FrameAverageMs = frameAverageMs;
            WorkP95Ms = workP95Ms;
            FrameP95Ms = frameP95Ms;
            FrameP99Ms = frameP99Ms;
            WorkOverBudgetPercent = workOverBudgetPercent;
            FrameOverBudgetPercent = frameOverBudgetPercent;
        }
    }

    public sealed class OglPerformanceSampler
    {
        private const int MaximumReportedRegions = 32;
        private readonly object m_Sync = new object();
        private readonly Dictionary<string, RegionPosition> m_PreviousRegions =
            new Dictionary<string, RegionPosition>(StringComparer.Ordinal);
        private long m_LastTimestamp;
        private double m_LastProcessCpuSeconds;
        private double m_LastGcPauseSeconds;
        private long m_LastAllocatedBytes;

        private readonly struct RegionPosition
        {
            public uint Frame { get; }
            public long Timestamp { get; }

            public RegionPosition(uint frame, long timestamp)
            {
                Frame = frame;
                Timestamp = timestamp;
            }
        }

        public static double CalculateCpuPercent(
            double processCpuDeltaSeconds, double wallDeltaSeconds, int processorCount)
        {
            if (processCpuDeltaSeconds <= 0 || wallDeltaSeconds <= 0 || processorCount <= 0)
                return 0;

            return Math.Clamp(100.0 * processCpuDeltaSeconds /
                (wallDeltaSeconds * processorCount), 0, 100);
        }

        public static double CalculateHeartbeatHz(uint previous, uint current, double wallDeltaSeconds)
        {
            if (wallDeltaSeconds <= 0)
                return 0;

            // uint rollover is legitimate for a long-running simulator.
            return unchecked(current - previous) / wallDeltaSeconds;
        }

        public IReadOnlyDictionary<string, string> Capture(IReadOnlyList<OglRegionFrame> regions)
        {
            lock (m_Sync)
            {
                long timestamp = Stopwatch.GetTimestamp();
                double intervalSeconds = m_LastTimestamp == 0
                    ? 0
                    : (timestamp - m_LastTimestamp) / (double)Stopwatch.Frequency;

                using Process process = Process.GetCurrentProcess();
                double cpuSeconds = process.TotalProcessorTime.TotalSeconds;
                double gcPauseSeconds = GC.GetTotalPauseDuration().TotalSeconds;
                long allocatedBytes = GC.GetTotalAllocatedBytes(false);
                GCMemoryInfo memory = GC.GetGCMemoryInfo();

                ThreadPool.GetAvailableThreads(out int availableWorkers, out _);
                ThreadPool.GetMaxThreads(out int maxWorkers, out _);

                var data = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["perf_processor_count"] = Environment.ProcessorCount.ToString(CultureInfo.InvariantCulture),
                    ["perf_process_cpu_percent"] = CalculateCpuPercent(
                        cpuSeconds - m_LastProcessCpuSeconds, intervalSeconds,
                        Environment.ProcessorCount).ToString("F2", CultureInfo.InvariantCulture),
                    ["perf_process_private_bytes"] = process.PrivateMemorySize64.ToString(CultureInfo.InvariantCulture),
                    ["perf_managed_heap_bytes"] = GC.GetTotalMemory(false).ToString(CultureInfo.InvariantCulture),
                    ["perf_gc_heap_size_bytes"] = memory.HeapSizeBytes.ToString(CultureInfo.InvariantCulture),
                    ["perf_gc_fragmented_bytes"] = memory.FragmentedBytes.ToString(CultureInfo.InvariantCulture),
                    ["perf_gc_memory_load_bytes"] = memory.MemoryLoadBytes.ToString(CultureInfo.InvariantCulture),
                    ["perf_gc_total_available_bytes"] = memory.TotalAvailableMemoryBytes.ToString(CultureInfo.InvariantCulture),
                    ["perf_gc_gen0_collections"] = GC.CollectionCount(0).ToString(CultureInfo.InvariantCulture),
                    ["perf_gc_gen1_collections"] = GC.CollectionCount(1).ToString(CultureInfo.InvariantCulture),
                    ["perf_gc_gen2_collections"] = GC.CollectionCount(2).ToString(CultureInfo.InvariantCulture),
                    ["perf_gc_pause_percent"] = CalculateCpuPercent(
                        gcPauseSeconds - m_LastGcPauseSeconds, intervalSeconds, 1)
                        .ToString("F2", CultureInfo.InvariantCulture),
                    ["perf_allocated_bytes_per_second"] = (intervalSeconds > 0
                        ? Math.Max(0, allocatedBytes - m_LastAllocatedBytes) / intervalSeconds
                        : 0).ToString("F0", CultureInfo.InvariantCulture),
                    ["perf_threadpool_threads"] = ThreadPool.ThreadCount.ToString(CultureInfo.InvariantCulture),
                    ["perf_threadpool_pending_items"] = ThreadPool.PendingWorkItemCount.ToString(CultureInfo.InvariantCulture),
                    ["perf_threadpool_busy_workers"] = Math.Max(0, maxWorkers - availableWorkers)
                        .ToString(CultureInfo.InvariantCulture)
                };

                var regionStats = new List<object>();
                var seen = new HashSet<string>(StringComparer.Ordinal);
                var active = new HashSet<string>(StringComparer.Ordinal);
                int total = regions?.Count ?? 0;

                for (int i = 0; i < total; i++)
                {
                    OglRegionFrame region = regions[i];
                    if (string.IsNullOrEmpty(region.Id) || !active.Add(region.Id))
                        continue;
                    if (regionStats.Count >= MaximumReportedRegions)
                        continue;

                    double measuredHz = 0;
                    if (m_PreviousRegions.TryGetValue(region.Id, out RegionPosition previous))
                        measuredHz = CalculateHeartbeatHz(previous.Frame, region.Frame,
                            (timestamp - previous.Timestamp) / (double)Stopwatch.Frequency);

                    double targetHz = region.TargetFrameSeconds > 0
                        ? 1.0 / region.TargetFrameSeconds : 0;
                    regionStats.Add(new
                    {
                        region_id = region.Id,
                        heartbeat_hz = Math.Round(measuredHz, 2),
                        target_hz = Math.Round(targetHz, 2),
                        window_frames = region.SampledFrames,
                        work_avg_ms = Math.Round(region.WorkAverageMs, 2),
                        frame_avg_ms = Math.Round(region.FrameAverageMs, 2),
                        work_p95_ms = Math.Round(region.WorkP95Ms, 2),
                        frame_p95_ms = Math.Round(region.FrameP95Ms, 2),
                        frame_p99_ms = Math.Round(region.FrameP99Ms, 2),
                        work_over_budget_percent = Math.Round(region.WorkOverBudgetPercent, 2),
                        frame_over_budget_percent = Math.Round(region.FrameOverBudgetPercent, 2)
                    });
                    m_PreviousRegions[region.Id] = new RegionPosition(region.Frame, timestamp);
                }

                foreach (string id in m_PreviousRegions.Keys.ToArray())
                    if (!active.Contains(id))
                        m_PreviousRegions.Remove(id);

                data["perf_region_heartbeats_json"] = JsonSerializer.Serialize(regionStats);
                data["perf_region_sample_count"] = regionStats.Count.ToString(CultureInfo.InvariantCulture);
                data["perf_region_sample_omitted"] = Math.Max(0, active.Count - regionStats.Count)
                    .ToString(CultureInfo.InvariantCulture);

                m_LastTimestamp = timestamp;
                m_LastProcessCpuSeconds = cpuSeconds;
                m_LastGcPauseSeconds = gcPauseSeconds;
                m_LastAllocatedBytes = allocatedBytes;
                return data;
            }
        }
    }
}
