// SPDX-License-Identifier: MPL-2.0
// R617: pure thread-safe NPC source index. No HTTP or simulator dependency.
using System;
using System.Collections.Generic;
using System.Linq;

namespace NexVerse.Server.Api
{
    internal sealed class NexNpcOwnerEntry
    {
        public Guid npc_id { get; set; }
        public Guid owner_principal_id { get; set; }
        public string name { get; set; }
        public string region_name { get; set; }
    }

    internal sealed class NexNpcSnapshotIndex
    {
        private sealed class Region
        {
            internal long Sequence;
            internal DateTimeOffset LastSeen;
            internal List<NexNpcOwnerEntry> Npcs;
        }

        private readonly object m_Guard = new object();
        private readonly Dictionary<Guid, Region> m_Regions =
            new Dictionary<Guid, Region>();
        private long m_Version;
        private HashSet<Guid> m_LastExpectedRegionIds;
        private readonly int m_RequiredRegions;
        // Optional trusted NodeAgent inventory. Never infer expected regions
        // from whichever NPC snapshots happen to have arrived.
        private readonly Func<IReadOnlyCollection<Guid>> m_ExpectedRegionIds;
        private static readonly TimeSpan FreshFor = TimeSpan.FromSeconds(100);

        internal NexNpcSnapshotIndex(int requiredRegions,
            Func<IReadOnlyCollection<Guid>> expectedRegionIds = null)
        {
            if (requiredRegions <= 0 || requiredRegions > 1024)
                throw new ArgumentOutOfRangeException(nameof(requiredRegions));
            m_RequiredRegions = requiredRegions;
            m_ExpectedRegionIds = expectedRegionIds;
        }

        // The presence of an expired snapshot never implies "zero NPCs".
        // Restart starts unready until a live simulator proves its own state.
        internal bool TryOwned(Guid owner, out List<NexNpcOwnerEntry> owned,
            out long revision)
        {
            owned = new List<NexNpcOwnerEntry>();
            revision = 0;
            // NodeRegistry is populated by authenticated simulator events, not
            // by NPC snapshots. If it reports zero regions, fail closed.
            IReadOnlyCollection<Guid> expected = m_ExpectedRegionIds?.Invoke();
            if (m_ExpectedRegionIds != null && (expected == null ||
                expected.Count < m_RequiredRegions || expected.Count > 1024 ||
                expected.Contains(Guid.Empty)))
                return false;
            lock (m_Guard)
            {
                DateTimeOffset now = DateTimeOffset.UtcNow;
                List<Region> fresh;
                if (m_ExpectedRegionIds != null)
                {
                    HashSet<Guid> unique = new HashSet<Guid>(expected);
                    if (unique.Count != expected.Count) return false;
                    // A region going online/offline changes the cursor
                    // generation even before new NPC snapshots arrive.
                    if (m_LastExpectedRegionIds == null ||
                        !m_LastExpectedRegionIds.SetEquals(unique))
                    {
                        m_LastExpectedRegionIds = unique;
                        ++m_Version;
                    }
                    fresh = new List<Region>(unique.Count);
                    foreach (Guid regionId in unique)
                    {
                        if (!m_Regions.TryGetValue(regionId, out Region snapshot) ||
                            now - snapshot.LastSeen > FreshFor)
                            return false;
                        fresh.Add(snapshot);
                    }
                }
                else
                {
                    fresh = m_Regions.Values.Where(x => now - x.LastSeen <= FreshFor).ToList();
                    if (fresh.Count < m_RequiredRegions || fresh.Count != m_Regions.Count)
                        return false;
                }
                revision = m_Version;
                // A conflicting owner claim from different reporting regions
                // invalidates the directory rather than exposing an NPC to
                // both accounts.
                var owners = new Dictionary<Guid,Guid>();
                foreach (Region region in fresh)
                    foreach (NexNpcOwnerEntry npc in region.Npcs)
                    {
                        if (owners.TryGetValue(npc.npc_id,out Guid previous)
                            && previous != npc.owner_principal_id)
                            return false;
                        owners[npc.npc_id] = npc.owner_principal_id;
                    }
                var seen = new HashSet<Guid>();
                foreach (Region region in fresh)
                    foreach (NexNpcOwnerEntry npc in region.Npcs)
                        if (npc.owner_principal_id == owner && seen.Add(npc.npc_id))
                            owned.Add(npc);
                owned.Sort((a,b) => a.npc_id.CompareTo(b.npc_id));
                return true;
            }
        }

        // An authorized *complete* snapshot replaces one simulator region;
        // monotonic timestamps reject duplicated/replayed state changes.
        internal bool Replace(Guid regionID, long sequence,
            List<NexNpcOwnerEntry> entries)
        {
            if (regionID == Guid.Empty || sequence <= 0 || entries == null ||
                entries.Count > 1000 ||
                entries.Any(n => n == null || n.npc_id == Guid.Empty ||
                    n.owner_principal_id == Guid.Empty ||
                    string.IsNullOrWhiteSpace(n.name) || n.name.Length > 120 ||
                    (n.region_name ?? "").Length > 120) ||
                entries.GroupBy(n => n.npc_id).Any(g => g.Count() != 1))
                return false;

            lock (m_Guard)
            {
                if (m_Regions.TryGetValue(regionID, out Region last))
                {
                    if (sequence <= last.Sequence) return false;
                }
                else if (m_Regions.Count >= 1024) return false;
                m_Regions[regionID] = new Region
                {
                    Sequence = sequence,
                    LastSeen = DateTimeOffset.UtcNow,
                    Npcs = entries.ToList()
                };
                ++m_Version;
            }
            return true;
        }
    }

 }
