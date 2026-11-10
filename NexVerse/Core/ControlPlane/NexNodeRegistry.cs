// SPDX-License-Identifier: MPL-2.0

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using NexVerse.Core.Messaging;

namespace NexVerse.Core.ControlPlane
{
    public sealed class NexNodeRegionSnapshot
    {
        public NexNodeRegionSnapshot(
            string regionId,
            string name,
            string serverUri,
            int sizeX,
            int sizeY,
            int agentCount,
            DateTimeOffset lastSeen,
            int npcCount = 0)
        {
            NpcCount = Math.Clamp(npcCount, 0, Math.Max(0, agentCount));
            RegionId = regionId ?? string.Empty;
            Name = name ?? string.Empty;
            ServerUri = serverUri ?? string.Empty;
            SizeX = sizeX;
            SizeY = sizeY;
            AgentCount = agentCount;
            LastSeen = lastSeen;
        }

        public string RegionId { get; }
        public string Name { get; }
        public string ServerUri { get; }
        public int SizeX { get; }
        public int SizeY { get; }
        public int AgentCount { get; }
        public int NpcCount { get; }
        public DateTimeOffset LastSeen { get; }
    }

    public sealed class NexRegionPerformanceSnapshot
    {
        public string RegionId { get; init; } = string.Empty;
        public double HeartbeatHz { get; init; }
        public double TargetHz { get; init; }
        public long WindowFrames { get; init; }
        public double WorkP95Ms { get; init; }
        public double FrameP95Ms { get; init; }
        public double FrameP99Ms { get; init; }
        public double WorkOverBudgetPercent { get; init; }
        public double FrameOverBudgetPercent { get; init; }
    }

    public sealed class NexHostCpuCoreSnapshot
    {
        public int CoreId { get; init; }
        public double UsagePercent { get; init; }
    }

    public sealed class NexNodeSnapshot
    {
        public NexNodeSnapshot(
            string nodeId,
            string hostname,
            string serverVersion,
            long uptimeSeconds,
            int processId,
            long workingSetBytes,
            double cpuSeconds,
            long diskFreeBytes,
            long diskTotalBytes,
            int regionCount,
            int agentCount,
            bool managedRegionCommands,
            bool crossRegionObjectMessaging,
            bool crossOwnerObjectMessaging,
            int objectMessagesPerSecond,
            int objectMessageMaxAgeSeconds,
            string migrationStorageId,
            bool maintenanceMode,
            bool draining,
            int lunaTextureDiagnosticCount,
            long lunaTextureOccurrenceCount,
            int lunaTextureRegionsAffected,
            DateTimeOffset? lunaTextureLastSeen,
            IReadOnlyDictionary<string, long> lunaTextureClassifications,
            string state,
            DateTimeOffset lastSeen,
            DateTimeOffset lastEventAt,
            IReadOnlyList<NexNodeRegionSnapshot> regions,
            string updateState = "",
            string updateOperationId = "",
            DateTimeOffset? updateAt = null,
            int npcCount = 0,
            IReadOnlyDictionary<string, double> performanceMetrics = null,
            IReadOnlyList<NexRegionPerformanceSnapshot> regionPerformance = null,
            IReadOnlyList<NexHostCpuCoreSnapshot> hostCpuCores = null)
        {
            NpcCount = Math.Clamp(npcCount, 0, Math.Max(0, agentCount));
            NodeId = nodeId ?? string.Empty;
            Hostname = hostname ?? string.Empty;
            ServerVersion = serverVersion ?? string.Empty;
            UptimeSeconds = uptimeSeconds;
            ProcessId = processId;
            WorkingSetBytes = workingSetBytes;
            CpuSeconds = cpuSeconds;
            DiskFreeBytes = diskFreeBytes;
            DiskTotalBytes = diskTotalBytes;
            RegionCount = regionCount;
            AgentCount = agentCount;
            ManagedRegionCommands = managedRegionCommands;
            CrossRegionObjectMessaging = crossRegionObjectMessaging;
            CrossOwnerObjectMessaging = crossOwnerObjectMessaging;
            ObjectMessagesPerSecond = Math.Max(0, objectMessagesPerSecond);
            ObjectMessageMaxAgeSeconds = Math.Max(0, objectMessageMaxAgeSeconds);
            MigrationStorageId = migrationStorageId ?? string.Empty;
            MaintenanceMode = maintenanceMode;
            Draining = draining;
            LunaTextureDiagnosticCount = Math.Max(0, lunaTextureDiagnosticCount);
            LunaTextureOccurrenceCount = Math.Max(0, lunaTextureOccurrenceCount);
            LunaTextureRegionsAffected = Math.Max(0, lunaTextureRegionsAffected);
            LunaTextureLastSeen = lunaTextureLastSeen;
            LunaTextureClassifications =
                lunaTextureClassifications ??
                new Dictionary<string, long>(
                    StringComparer.OrdinalIgnoreCase);
            State = state ?? "unknown";
            LastSeen = lastSeen;
            LastEventAt = lastEventAt;
            Regions = regions ?? Array.Empty<NexNodeRegionSnapshot>();
            UpdateState = updateState ?? string.Empty;
            UpdateOperationId = updateOperationId ?? string.Empty;
            UpdateAt = updateAt;
            PerformanceMetrics = performanceMetrics ?? new Dictionary<string, double>(StringComparer.Ordinal);
            RegionPerformance = regionPerformance ?? Array.Empty<NexRegionPerformanceSnapshot>();
            HostCpuCores = hostCpuCores ?? Array.Empty<NexHostCpuCoreSnapshot>();
        }

        public string NodeId { get; }
        public string Hostname { get; }
        public string ServerVersion { get; }
        public long UptimeSeconds { get; }
        public int ProcessId { get; }
        public long WorkingSetBytes { get; }
        public double CpuSeconds { get; }
        public long DiskFreeBytes { get; }
        public long DiskTotalBytes { get; }
        public int RegionCount { get; }
        public int AgentCount { get; }
        public int NpcCount { get; }
        public bool ManagedRegionCommands { get; }
        public bool CrossRegionObjectMessaging { get; }
        public bool CrossOwnerObjectMessaging { get; }
        public int ObjectMessagesPerSecond { get; }
        public int ObjectMessageMaxAgeSeconds { get; }
        public string MigrationStorageId { get; }
        public bool MaintenanceMode { get; }
        public bool Draining { get; }
        public int LunaTextureDiagnosticCount { get; }
        public long LunaTextureOccurrenceCount { get; }
        public int LunaTextureRegionsAffected { get; }
        public DateTimeOffset? LunaTextureLastSeen { get; }
        public IReadOnlyDictionary<string, long> LunaTextureClassifications { get; }
        public string State { get; }
        public DateTimeOffset LastSeen { get; }
        public DateTimeOffset LastEventAt { get; }
        public IReadOnlyList<NexNodeRegionSnapshot> Regions { get; }
        public string UpdateState { get; }
        public string UpdateOperationId { get; }
        public DateTimeOffset? UpdateAt { get; }
        // Only allowlisted, validated read-only process metrics from signed NodeAgent heartbeats.
        public IReadOnlyDictionary<string, double> PerformanceMetrics { get; }
        public IReadOnlyList<NexRegionPerformanceSnapshot> RegionPerformance { get; }
        public IReadOnlyList<NexHostCpuCoreSnapshot> HostCpuCores { get; }
    }

    /// <summary>
    /// Robust-side projection of simulator NodeAgent lifecycle and heartbeat events.
    /// State is intentionally in-memory: NodeAgents continuously rebuild it after
    /// Robust restarts and stale detection is based on the local receive clock.
    /// </summary>
    public sealed class NexNodeRegistry : IDisposable
    {
        // Keep remote telemetry bounded: omit high-cardinality region JSON and non-finite numbers.
        private static readonly string[] s_PerformanceFields =
        {
            "perf_processor_count",
            "perf_process_cpu_percent",
            "perf_process_private_bytes",
            "perf_managed_heap_bytes",
            "perf_gc_heap_size_bytes",
            "perf_gc_fragmented_bytes",
            "perf_gc_memory_load_bytes",
            "perf_gc_total_available_bytes",
            "perf_gc_pause_percent",
            "perf_allocated_bytes_per_second",
            "perf_threadpool_threads",
            "perf_threadpool_pending_items",
            "perf_threadpool_busy_workers",
            "perf_region_sample_count",
            "perf_region_sample_omitted",
            "perf_host_cpu_cores_omitted",
            "perf_asset_requests",
            "perf_asset_memory_hits",
            "perf_asset_disk_hits",
            "perf_asset_weak_hits",
            "perf_asset_memory_enabled",
            "perf_asset_ram_entries",
            "perf_asset_ram_bytes",
            "perf_asset_ram_limit_bytes",
            "perf_asset_memory_trims",
            "perf_asset_file_writers",
            "perf_asset_writer_target",
            "perf_asset_writer_active",
            "perf_asset_writer_pending_jobs",
            "perf_asset_writes_rejected",
            "perf_asset_inflight_bytes",
            "perf_asset_inflight_limit_bytes"
        };

        private readonly ConcurrentDictionary<string, NodeRecord> m_Nodes =
            new ConcurrentDictionary<string, NodeRecord>(
                StringComparer.OrdinalIgnoreCase);

        private readonly IDisposable m_Subscription;
        private readonly TimeSpan m_StaleAfter;
        private int m_Disposed;

        public int StaleAfterSeconds =>
            (int)m_StaleAfter.TotalSeconds;

        public NexNodeRegistry(
            INexEventBus eventBus,
            int staleAfterSeconds = 90)
        {
            if (eventBus == null)
                throw new ArgumentNullException(nameof(eventBus));

            m_StaleAfter =
                TimeSpan.FromSeconds(
                    Math.Max(10, staleAfterSeconds));

            m_Subscription =
                eventBus.Subscribe(
                    "*",
                    HandleEvent);
        }

        public IReadOnlyList<NexNodeSnapshot> List()
        {
            DateTimeOffset now =
                DateTimeOffset.UtcNow;

            return m_Nodes.Values
                .Select(record =>
                    Snapshot(record, now))
                .OrderBy(
                    snapshot => snapshot.NodeId,
                    StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        public NexNodeSnapshot Get(
            string nodeId)
        {
            if (string.IsNullOrWhiteSpace(nodeId) ||
                !m_Nodes.TryGetValue(
                    nodeId.Trim(),
                    out NodeRecord record))
            {
                return null;
            }

            return Snapshot(
                record,
                DateTimeOffset.UtcNow);
        }

        public NexNodeSnapshot FindNodeForRegion(
            string regionId)
        {
            if (string.IsNullOrWhiteSpace(regionId))
                return null;

            string normalized =
                regionId.Trim();

            DateTimeOffset now =
                DateTimeOffset.UtcNow;

            return m_Nodes.Values
                .Where(record =>
                {
                    lock (record.Sync)
                    {
                        return record.Regions.ContainsKey(
                            normalized);
                    }
                })
                .Select(record =>
                    Snapshot(record, now))
                .OrderBy(snapshot =>
                    StateOrder(snapshot.State))
                .ThenByDescending(snapshot =>
                    snapshot.LastSeen)
                .FirstOrDefault();
        }

        private void HandleEvent(
            NexEvent nexEvent)
        {
            if (nexEvent == null)
                return;

            switch (nexEvent.Name?.Trim().ToLowerInvariant())
            {
                case "node.online":
                case "node.heartbeat":
                    ApplyNodeStatus(
                        nexEvent,
                        false);
                    break;

                case "node.offline":
                    ApplyNodeStatus(
                        nexEvent,
                        true);
                    break;

                case "node.update.stage.accepted":
                case "node.update.stage.completed":
                case "node.update.stage.failed":
                case "node.update.live.accepted":
                case "node.update.live.running":
                case "node.update.live.completed":
                case "node.update.live.failed":
                    ApplyNodeUpdateState(nexEvent);
                    break;

                case "region.online":
                    ApplyRegionOnline(
                        nexEvent);
                    break;
                case "region.presence":
                    ApplyRegionPresence(nexEvent);
                    break;

                case "region.offline":
                    ApplyRegionOffline(
                        nexEvent);
                    break;
            }
        }

        private void ApplyNodeUpdateState(NexEvent ev)
        {
            if (!string.Equals(ev.Source, "nexverse.simulator", StringComparison.Ordinal) ||
                !TryData(ev, "node_id", out string nodeId) ||
                !TryData(ev, "operation_id", out string operationId) ||
                !Guid.TryParse(operationId, out _))
                return;

            NodeRecord record = m_Nodes.GetOrAdd(nodeId, id => new NodeRecord(id));
            lock (record.Sync)
            {
                record.UpdateState = ev.Name.StartsWith("node.update.live.", StringComparison.Ordinal)
                    ? "live." + ev.Name.Substring("node.update.live.".Length)
                    : ev.Name.Substring("node.update.stage.".Length);
                record.UpdateOperationId = operationId;
                record.UpdateAt = DateTimeOffset.UtcNow;
                record.LastEventAt = record.UpdateAt.Value;
            }
        }

        private void ApplyNodeStatus(
            NexEvent nexEvent,
            bool explicitOffline)
        {
            if (!TryData(
                    nexEvent,
                    "node_id",
                    out string nodeId))
            {
                return;
            }

            DateTimeOffset receivedAt =
                DateTimeOffset.UtcNow;

            NodeRecord record =
                m_Nodes.GetOrAdd(
                    nodeId,
                    id => new NodeRecord(id));

            lock (record.Sync)
            {
                record.ExplicitOffline =
                    explicitOffline;
                record.LastSeen =
                    receivedAt;
                record.LastEventAt =
                    nexEvent.Timestamp;

                AssignString(
                    nexEvent,
                    "hostname",
                    value => record.Hostname = value);
                AssignString(
                    nexEvent,
                    "server_version",
                    value => record.ServerVersion = value);
                AssignLong(
                    nexEvent,
                    "uptime_seconds",
                    value => record.UptimeSeconds = value);
                AssignInt(
                    nexEvent,
                    "process_id",
                    value => record.ProcessId = value);
                AssignLong(
                    nexEvent,
                    "working_set_bytes",
                    value => record.WorkingSetBytes = value);
                AssignDouble(
                    nexEvent,
                    "cpu_seconds",
                    value => record.CpuSeconds = value);
                AssignLong(nexEvent, "disk_free_bytes", value => record.DiskFreeBytes = value);
                AssignLong(nexEvent, "disk_total_bytes", value => record.DiskTotalBytes = value);
                AssignInt(
                    nexEvent,
                    "region_count",
                    value => record.RegionCount = value);
                AssignInt(
                    nexEvent,
                    "agent_count",
                    value => record.AgentCount = value);
                AssignInt(nexEvent, "npc_count", value =>
                    record.NpcCount = Math.Clamp(value, 0, Math.Max(0, record.AgentCount)));
                AssignBool(nexEvent, "maintenance_mode", value => record.MaintenanceMode = value);
                AssignBool(nexEvent, "draining", value => record.Draining = value);
                AssignBool(
                    nexEvent,
                    "managed_region_commands",
                    value => record.ManagedRegionCommands = value);
                AssignBool(
                    nexEvent,
                    "cross_region_object_messaging",
                    value => record.CrossRegionObjectMessaging = value);
                AssignBool(
                    nexEvent,
                    "cross_owner_object_messaging",
                    value => record.CrossOwnerObjectMessaging = value);
                AssignInt(
                    nexEvent,
                    "object_messages_per_second",
                    value => record.ObjectMessagesPerSecond = Math.Max(0, value));
                AssignInt(
                    nexEvent,
                    "object_message_max_age_seconds",
                    value => record.ObjectMessageMaxAgeSeconds = Math.Max(0, value));
                AssignString(
                    nexEvent,
                    "migration_storage_id",
                    value => record.MigrationStorageId = value);
                AssignInt(
                    nexEvent,
                    "luna_texture_diagnostic_count",
                    value => record.LunaTextureDiagnosticCount = Math.Max(0, value));
                AssignLong(
                    nexEvent,
                    "luna_texture_occurrence_count",
                    value => record.LunaTextureOccurrenceCount = Math.Max(0, value));
                AssignInt(
                    nexEvent,
                    "luna_texture_regions_affected",
                    value => record.LunaTextureRegionsAffected = Math.Max(0, value));
                AssignNullableDateTimeOffset(
                    nexEvent,
                    "luna_texture_last_seen_utc",
                    value => record.LunaTextureLastSeen = value);
                AssignLongDictionary(
                    nexEvent,
                    "luna_texture_classifications_json",
                    value => record.LunaTextureClassifications = value);

                // Missing opt-in metrics clear the previous sample on the next heartbeat.
                // Only explicitly allowlisted finite scalars are retained.
                Dictionary<string, double> metrics = new Dictionary<string, double>(StringComparer.Ordinal);
                foreach (string field in s_PerformanceFields)
                {
                    if (TryParseDouble(nexEvent, field, out double value) &&
                        double.IsFinite(value) && value >= 0 && value <= 1e15 &&
                        (!field.EndsWith("_percent", StringComparison.Ordinal) || value <= 100))
                        metrics[field] = value;
                }
                record.PerformanceMetrics = metrics;

                if (TryData(
                        nexEvent,
                        "regions",
                        out string regionsRaw,
                        allowEmpty: true))
                {
                    SynchronizeRegions(
                        record,
                        regionsRaw,
                        receivedAt);
                }

                // Signed opt-in telemetry is a replacement snapshot, never an
                // append-only history. Bound the decoded arrays and input size.
                record.RegionPerformance = ParseRegionPerformance(
                    nexEvent, record.Regions);
                record.HostCpuCores = ParseHostCpuCores(nexEvent);
            }
        }

        private void ApplyRegionOnline(
            NexEvent nexEvent)
        {
            if (!TryData(
                    nexEvent,
                    "node_id",
                    out string nodeId) ||
                !TryData(
                    nexEvent,
                    "region_id",
                    out string regionId))
            {
                return;
            }

            DateTimeOffset receivedAt =
                DateTimeOffset.UtcNow;

            NodeRecord record =
                m_Nodes.GetOrAdd(
                    nodeId,
                    id => new NodeRecord(id));

            lock (record.Sync)
            {
                record.ExplicitOffline = false;
                record.LastSeen = receivedAt;
                record.LastEventAt = nexEvent.Timestamp;

                record.Regions.TryGetValue(
                    regionId,
                    out RegionRecord region);

                region ??=
                    new RegionRecord(regionId);

                if (TryData(
                        nexEvent,
                        "region_name",
                        out string name,
                        allowEmpty: true))
                {
                    region.Name = name;
                }

                if (TryData(
                        nexEvent,
                        "server_uri",
                        out string serverUri,
                        allowEmpty: true))
                {
                    region.ServerUri = serverUri;
                }

                if (TryParseInt(
                        nexEvent,
                        "size_x",
                        out int sizeX))
                {
                    region.SizeX = sizeX;
                }

                if (TryParseInt(
                        nexEvent,
                        "size_y",
                        out int sizeY))
                {
                    region.SizeY = sizeY;
                }

                if (TryParseInt(
                        nexEvent,
                        "agent_count",
                        out int agentCount))
                {
                    region.AgentCount = Math.Max(0, agentCount);
                }
                if (TryParseInt(nexEvent, "npc_count", out int npcCount))
                    region.NpcCount = Math.Clamp(npcCount, 0, region.AgentCount);

                region.LastSeen =
                    receivedAt;

                record.Regions[regionId] =
                    region;
                record.RegionCount =
                    record.Regions.Count;

                record.AgentCount =
                    Math.Max(
                        record.AgentCount,
                        record.Regions.Values.Sum(x =>
                            Math.Max(0, x.AgentCount)));
            }
        }

        // A compact, current presence update delivered alongside each node heartbeat.
        // Never resurrect regions which were removed or mark offline nodes online.
        private void ApplyRegionPresence(NexEvent ev)
        {
            if (!TryData(ev, "node_id", out string nodeId) ||
                !TryData(ev, "region_id", out string regionId) ||
                !TryParseInt(ev, "agent_count", out int agents) ||
                !TryParseInt(ev, "npc_count", out int npcs) ||
                !m_Nodes.TryGetValue(nodeId, out NodeRecord record))
                return;
            lock (record.Sync)
            {
                if (record.ExplicitOffline || !record.Regions.TryGetValue(regionId, out RegionRecord region))
                    return;
                region.AgentCount = Math.Max(0, agents);
                region.NpcCount = Math.Clamp(npcs, 0, region.AgentCount);
                region.LastSeen = DateTimeOffset.UtcNow;
            }
        }

        private void ApplyRegionOffline(
            NexEvent nexEvent)
        {
            if (!TryData(
                    nexEvent,
                    "node_id",
                    out string nodeId) ||
                !TryData(
                    nexEvent,
                    "region_id",
                    out string regionId))
            {
                return;
            }

            DateTimeOffset receivedAt =
                DateTimeOffset.UtcNow;

            NodeRecord record =
                m_Nodes.GetOrAdd(
                    nodeId,
                    id => new NodeRecord(id));

            lock (record.Sync)
            {
                record.ExplicitOffline = false;
                record.LastSeen = receivedAt;
                record.LastEventAt = nexEvent.Timestamp;
                record.Regions.Remove(regionId);
                record.RegionCount =
                    record.Regions.Count;
                record.AgentCount =
                    record.Regions.Values.Sum(x =>
                        Math.Max(0, x.AgentCount));
            }
        }

        private static void SynchronizeRegions(
            NodeRecord record,
            string raw,
            DateTimeOffset receivedAt)
        {
            Dictionary<string, string> current =
                new Dictionary<string, string>(
                    StringComparer.OrdinalIgnoreCase);

            if (!string.IsNullOrWhiteSpace(raw))
            {
                foreach (string entry in raw.Split(
                             ';',
                             StringSplitOptions.RemoveEmptyEntries))
                {
                    int separator =
                        entry.IndexOf('|');

                    string regionId =
                        separator >= 0
                            ? entry.Substring(0, separator)
                            : entry;

                    string name =
                        separator >= 0
                            ? entry.Substring(separator + 1)
                            : string.Empty;

                    regionId =
                        regionId.Trim();

                    if (regionId.Length == 0)
                        continue;

                    current[regionId] =
                        name.Trim();
                }
            }

            foreach (string existingId in
                     record.Regions.Keys.ToArray())
            {
                if (!current.ContainsKey(existingId))
                    record.Regions.Remove(existingId);
            }

            foreach (KeyValuePair<string, string> item in current)
            {
                if (!record.Regions.TryGetValue(
                        item.Key,
                        out RegionRecord region))
                {
                    region =
                        new RegionRecord(item.Key);
                }

                if (!string.IsNullOrWhiteSpace(item.Value))
                    region.Name = item.Value;

                region.LastSeen =
                    receivedAt;

                record.Regions[item.Key] =
                    region;
            }

            record.RegionCount =
                record.Regions.Count;
        }

        private NexNodeSnapshot Snapshot(
            NodeRecord record,
            DateTimeOffset now)
        {
            lock (record.Sync)
            {
                string state =
                    record.ExplicitOffline
                        ? "offline"
                        : now - record.LastSeen >
                          m_StaleAfter
                            ? "stale"
                            : "online";

                NexNodeRegionSnapshot[] regions =
                    record.Regions.Values
                        .OrderBy(
                            region => region.Name,
                            StringComparer.OrdinalIgnoreCase)
                        .ThenBy(
                            region => region.RegionId,
                            StringComparer.OrdinalIgnoreCase)
                        .Select(region =>
                            new NexNodeRegionSnapshot(
                                region.RegionId,
                                region.Name,
                                region.ServerUri,
                                region.SizeX,
                                region.SizeY,
                                region.AgentCount,
                                region.LastSeen,
                                region.NpcCount))
                        .ToArray();

                return new NexNodeSnapshot(
                    record.NodeId,
                    record.Hostname,
                    record.ServerVersion,
                    record.UptimeSeconds,
                    record.ProcessId,
                    record.WorkingSetBytes,
                    record.CpuSeconds,
                    record.DiskFreeBytes,
                    record.DiskTotalBytes,
                    record.RegionCount,
                    record.AgentCount,
                    record.ManagedRegionCommands,
                    record.CrossRegionObjectMessaging,
                    record.CrossOwnerObjectMessaging,
                    record.ObjectMessagesPerSecond,
                    record.ObjectMessageMaxAgeSeconds,
                    record.MigrationStorageId,
                    record.MaintenanceMode,
                    record.Draining,
                    record.LunaTextureDiagnosticCount,
                    record.LunaTextureOccurrenceCount,
                    record.LunaTextureRegionsAffected,
                    record.LunaTextureLastSeen,
                    new Dictionary<string, long>(
                        record.LunaTextureClassifications,
                        StringComparer.OrdinalIgnoreCase),
                    state,
                    record.LastSeen,
                    record.LastEventAt,
                    regions,
                    record.UpdateState,
                    record.UpdateOperationId,
                    record.UpdateAt,
                    record.NpcCount,
                    new Dictionary<string, double>(record.PerformanceMetrics, StringComparer.Ordinal),
                    record.RegionPerformance.ToArray(),
                    record.HostCpuCores.ToArray());
            }
        }

        private static int StateOrder(
            string state)
        {
            return state switch
            {
                "online" => 0,
                "stale" => 1,
                "offline" => 2,
                _ => 3
            };
        }

        private static bool TryFinite(JsonElement item, string name,
            double max, out double value)
        {
            value = 0;
            if (item.ValueKind != JsonValueKind.Object ||
                !item.TryGetProperty(name, out JsonElement e) ||
                e.ValueKind != JsonValueKind.Number ||
                !e.TryGetDouble(out double parsed) ||
                !double.IsFinite(parsed) || parsed < 0 || parsed > max)
                return false;
            value = parsed;
            return true;
        }

        private static NexRegionPerformanceSnapshot[] ParseRegionPerformance(
            NexEvent nexEvent, Dictionary<string, RegionRecord> liveRegions)
        {
            if (!TryData(nexEvent, "perf_region_heartbeats_json", out string json) ||
                json.Length > 24576)
                return Array.Empty<NexRegionPerformanceSnapshot>();
            try
            {
                using JsonDocument doc = JsonDocument.Parse(json,
                    new JsonDocumentOptions { MaxDepth = 8 });
                if (doc.RootElement.ValueKind != JsonValueKind.Array ||
                    doc.RootElement.GetArrayLength() > 32)
                    return Array.Empty<NexRegionPerformanceSnapshot>();
                var result = new List<NexRegionPerformanceSnapshot>();
                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (JsonElement entry in doc.RootElement.EnumerateArray())
                {
                    if (entry.ValueKind != JsonValueKind.Object ||
                        !entry.TryGetProperty("region_id", out JsonElement id) ||
                        id.ValueKind != JsonValueKind.String)
                        continue;
                    string regionId = id.GetString();
                    if (regionId == null || !Guid.TryParse(regionId, out Guid uuid))
                        continue;
                    regionId = uuid.ToString("D");
                    if (!liveRegions.ContainsKey(regionId) || !seen.Add(regionId))
                        continue;
                    if (!TryFinite(entry, "heartbeat_hz", 1000, out double heartbeat) ||
                        !TryFinite(entry, "target_hz", 1000, out double target) ||
                        !TryFinite(entry, "window_frames", 1e8, out double frames) ||
                        !TryFinite(entry, "work_p95_ms", 60000, out double workP95) ||
                        !TryFinite(entry, "frame_p95_ms", 60000, out double frameP95) ||
                        !TryFinite(entry, "frame_p99_ms", 60000, out double frameP99) ||
                        !TryFinite(entry, "work_over_budget_percent", 100, out double workOver) ||
                        !TryFinite(entry, "frame_over_budget_percent", 100, out double frameOver))
                        continue;
                    result.Add(new NexRegionPerformanceSnapshot
                    {
                        RegionId = regionId,
                        HeartbeatHz = heartbeat,
                        TargetHz = target,
                        WindowFrames = (long)frames,
                        WorkP95Ms = workP95,
                        FrameP95Ms = frameP95,
                        FrameP99Ms = frameP99,
                        WorkOverBudgetPercent = workOver,
                        FrameOverBudgetPercent = frameOver
                    });
                }
                return result.ToArray();
            }
            catch (JsonException)
            {
                return Array.Empty<NexRegionPerformanceSnapshot>();
            }
        }

        private static NexHostCpuCoreSnapshot[] ParseHostCpuCores(NexEvent nexEvent)
        {
            if (!TryData(nexEvent, "perf_host_cpu_cores_json", out string json) ||
                json.Length > 12288)
                return Array.Empty<NexHostCpuCoreSnapshot>();
            try
            {
                using JsonDocument doc = JsonDocument.Parse(json,
                    new JsonDocumentOptions { MaxDepth = 6 });
                if (doc.RootElement.ValueKind != JsonValueKind.Array ||
                    doc.RootElement.GetArrayLength() > 128)
                    return Array.Empty<NexHostCpuCoreSnapshot>();
                var result = new List<NexHostCpuCoreSnapshot>();
                var seen = new HashSet<int>();
                foreach (JsonElement item in doc.RootElement.EnumerateArray())
                {
                    if (!TryFinite(item, "core_id", 4095, out double idValue) ||
                        Math.Truncate(idValue) != idValue ||
                        !TryFinite(item, "usage_percent", 100, out double load))
                        continue;
                    int core = (int)idValue;
                    if (seen.Add(core))
                        result.Add(new NexHostCpuCoreSnapshot
                            { CoreId = core, UsagePercent = load });
                }
                return result.OrderBy(x => x.CoreId).ToArray();
            }
            catch (JsonException)
            {
                return Array.Empty<NexHostCpuCoreSnapshot>();
            }
        }

        private static bool TryData(
            NexEvent nexEvent,
            string key,
            out string value,
            bool allowEmpty = false)
        {
            value = string.Empty;

            if (nexEvent?.Data == null ||
                !nexEvent.Data.TryGetValue(
                    key,
                    out string raw) ||
                raw == null)
            {
                return false;
            }

            value =
                raw.Trim();

            return allowEmpty ||
                   value.Length > 0;
        }

        private static bool TryParseInt(
            NexEvent nexEvent,
            string key,
            out int value)
        {
            value = 0;

            return
                TryData(
                    nexEvent,
                    key,
                    out string raw) &&
                int.TryParse(
                    raw,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out value);
        }

        private static bool TryParseLong(
            NexEvent nexEvent,
            string key,
            out long value)
        {
            value = 0;

            return
                TryData(
                    nexEvent,
                    key,
                    out string raw) &&
                long.TryParse(
                    raw,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out value);
        }

        private static bool TryParseDouble(
            NexEvent nexEvent,
            string key,
            out double value)
        {
            value = 0;

            return
                TryData(
                    nexEvent,
                    key,
                    out string raw) &&
                double.TryParse(
                    raw,
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out value);
        }

        private static void AssignString(
            NexEvent nexEvent,
            string key,
            Action<string> assign)
        {
            if (TryData(
                    nexEvent,
                    key,
                    out string value,
                    allowEmpty: true))
            {
                assign(value);
            }
        }

        private static void AssignInt(
            NexEvent nexEvent,
            string key,
            Action<int> assign)
        {
            if (TryParseInt(
                    nexEvent,
                    key,
                    out int value))
            {
                assign(value);
            }
        }

        private static void AssignLong(
            NexEvent nexEvent,
            string key,
            Action<long> assign)
        {
            if (TryParseLong(
                    nexEvent,
                    key,
                    out long value))
            {
                assign(value);
            }
        }

        private static void AssignBool(
            NexEvent nexEvent,
            string key,
            Action<bool> assign)
        {
            if (TryData(
                    nexEvent,
                    key,
                    out string raw) &&
                bool.TryParse(
                    raw,
                    out bool value))
            {
                assign(value);
            }
        }

        private static void AssignDouble(
            NexEvent nexEvent,
            string key,
            Action<double> assign)
        {
            if (TryParseDouble(
                    nexEvent,
                    key,
                    out double value))
            {
                assign(value);
            }
        }

        private static void AssignNullableDateTimeOffset(
            NexEvent nexEvent,
            string key,
            Action<DateTimeOffset?> assign)
        {
            if (!TryData(
                    nexEvent,
                    key,
                    out string raw,
                    allowEmpty: true))
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(raw))
            {
                assign(null);
                return;
            }

            if (DateTimeOffset.TryParse(
                    raw,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out DateTimeOffset value))
            {
                assign(value);
            }
        }

        private static void AssignLongDictionary(
            NexEvent nexEvent,
            string key,
            Action<Dictionary<string, long>> assign)
        {
            if (!TryData(
                    nexEvent,
                    key,
                    out string raw,
                    allowEmpty: true))
            {
                return;
            }

            Dictionary<string, long> parsed =
                new Dictionary<string, long>(
                    StringComparer.OrdinalIgnoreCase);

            if (!string.IsNullOrWhiteSpace(raw))
            {
                try
                {
                    Dictionary<string, long> values =
                        JsonSerializer.Deserialize<Dictionary<string, long>>(raw);

                    if (values != null)
                    {
                        foreach (KeyValuePair<string, long> item in values)
                        {
                            string name =
                                item.Key?.Trim().ToLowerInvariant();

                            if (string.IsNullOrWhiteSpace(name))
                                continue;

                            parsed[name] =
                                Math.Max(0, item.Value);
                        }
                    }
                }
                catch (JsonException)
                {
                    return;
                }
            }

            assign(parsed);
        }

        public void Dispose()
        {
            if (System.Threading.Interlocked.Exchange(
                    ref m_Disposed,
                    1) != 0)
            {
                return;
            }

            m_Subscription?.Dispose();
            m_Nodes.Clear();
        }

        private sealed class NodeRecord
        {
            public NodeRecord(
                string nodeId)
            {
                NodeId = nodeId;
                LastSeen = DateTimeOffset.UtcNow;
                LastEventAt = LastSeen;
            }

            public object Sync { get; } =
                new object();

            public string NodeId { get; }
            public string Hostname = string.Empty;
            public string ServerVersion = string.Empty;
            public long UptimeSeconds;
            public int ProcessId;
            public long WorkingSetBytes;
            public double CpuSeconds;
            public long DiskFreeBytes;
            public long DiskTotalBytes;
            public int RegionCount;
            public int AgentCount;
            public int NpcCount;
            public bool ManagedRegionCommands;
            public bool CrossRegionObjectMessaging;
            public bool CrossOwnerObjectMessaging;
            public int ObjectMessagesPerSecond;
            public int ObjectMessageMaxAgeSeconds;
            public string MigrationStorageId = string.Empty;
            public bool MaintenanceMode;
            public bool Draining;
            public int LunaTextureDiagnosticCount;
            public long LunaTextureOccurrenceCount;
            public int LunaTextureRegionsAffected;
            public DateTimeOffset? LunaTextureLastSeen;
            public Dictionary<string, long> LunaTextureClassifications =
                new Dictionary<string, long>(
                    StringComparer.OrdinalIgnoreCase);
            public bool ExplicitOffline;
            public string UpdateState = string.Empty;
            public string UpdateOperationId = string.Empty;
            public DateTimeOffset? UpdateAt;
            public Dictionary<string, double> PerformanceMetrics =
                new Dictionary<string, double>(StringComparer.Ordinal);
            public DateTimeOffset LastSeen;
            public DateTimeOffset LastEventAt;
            public NexRegionPerformanceSnapshot[] RegionPerformance =
                Array.Empty<NexRegionPerformanceSnapshot>();
            public NexHostCpuCoreSnapshot[] HostCpuCores =
                Array.Empty<NexHostCpuCoreSnapshot>();

            public Dictionary<string, RegionRecord> Regions { get; } =
                new Dictionary<string, RegionRecord>(
                    StringComparer.OrdinalIgnoreCase);
        }

        private sealed class RegionRecord
        {
            public RegionRecord(
                string regionId)
            {
                RegionId = regionId;
                LastSeen = DateTimeOffset.UtcNow;
            }

            public string RegionId { get; }
            public string Name = string.Empty;
            public string ServerUri = string.Empty;
            public int SizeX;
            public int SizeY;
            public int AgentCount;
            public int NpcCount;
            public DateTimeOffset LastSeen;
        }
    }
}
