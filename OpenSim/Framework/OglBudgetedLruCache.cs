// SPDX-License-Identifier: MPL-2.0
// Opt-in, lock-protected bounded LRU cache for disposable simulator assets.
// Does not replace the authoritative asset store or the legacy expiring cache.
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace OpenSim.Framework
{
    public sealed class OglBudgetedLruCache<TKey, TValue> : IDisposable where TKey : notnull
    {
        private sealed class Entry
        {
            public TValue Value;
            public long EstimatedBytes;
            public long ExpiresAt;
            public LinkedListNode<TKey> Node;
        }

        private readonly object m_Sync = new object();
        private readonly Dictionary<TKey, Entry> m_Entries = new Dictionary<TKey, Entry>();
        private readonly LinkedList<TKey> m_Lru = new LinkedList<TKey>();
        private readonly Func<TValue, long> m_EstimateBytes;
        private readonly long m_MaxBytes;
        private readonly int m_MaxEntries;
        private long m_Bytes;
        private bool m_Disposed;

        public OglBudgetedLruCache(long maxBytes, int maxEntries, Func<TValue, long> estimateBytes)
        {
            if (maxBytes < 1) throw new ArgumentOutOfRangeException(nameof(maxBytes));
            if (maxEntries < 1) throw new ArgumentOutOfRangeException(nameof(maxEntries));
            m_EstimateBytes = estimateBytes ?? throw new ArgumentNullException(nameof(estimateBytes));
            m_MaxBytes = maxBytes;
            m_MaxEntries = maxEntries;
        }

        public int Count
        {
            get { lock (m_Sync) { PruneExpired(Stopwatch.GetTimestamp()); return m_Entries.Count; } }
        }

        public long EstimatedBytes
        {
            get { lock (m_Sync) { PruneExpired(Stopwatch.GetTimestamp()); return m_Bytes; } }
        }

        public bool AddOrUpdate(TKey key, TValue value, TimeSpan lifetime)
        {
            long estimatedBytes = Math.Max(1, m_EstimateBytes(value));
            // Don't pin an individual asset larger than the entire budget.
            lock (m_Sync)
            {
                if (m_Disposed) return false;
                RemoveCore(key);
                if (estimatedBytes > m_MaxBytes)
                    return false;

                long now = Stopwatch.GetTimestamp();
                // Non-positive TTL means no expiration, matching Flotsam's
                // existing expiring cache behavior. The byte/entry cap remains.
                long expiresAt = long.MaxValue;
                if (lifetime > TimeSpan.Zero)
                {
                    long duration = (long)Math.Clamp(
                        lifetime.TotalSeconds * Stopwatch.Frequency,
                        1.0, (double)long.MaxValue / 4);
                    expiresAt = now >= long.MaxValue - duration ? long.MaxValue : now + duration;
                }
                var entry = new Entry
                {
                    Value = value,
                    EstimatedBytes = estimatedBytes,
                    ExpiresAt = expiresAt,
                    Node = m_Lru.AddFirst(key)
                };
                m_Entries.Add(key, entry);
                m_Bytes += estimatedBytes;
                // Keep the hot insertion path O(1). Expired non-tail entries
                // are removed lazily on access, eviction, or status inspection.
                while (m_Lru.Last != null &&
                       m_Entries[m_Lru.Last.Value].ExpiresAt <= now)
                    RemoveCore(m_Lru.Last.Value);
                while (m_Entries.Count > m_MaxEntries || m_Bytes > m_MaxBytes)
                {
                    if (m_Lru.Last == null) break;
                    RemoveCore(m_Lru.Last.Value);
                }
                return m_Entries.ContainsKey(key);
            }
        }

        public bool TryGetValue(TKey key, out TValue value)
        {
            lock (m_Sync)
            {
                if (!m_Disposed && m_Entries.TryGetValue(key, out Entry entry))
                {
                    if (entry.ExpiresAt > Stopwatch.GetTimestamp())
                    {
                        m_Lru.Remove(entry.Node);
                        m_Lru.AddFirst(entry.Node);
                        value = entry.Value;
                        return true;
                    }
                    RemoveCore(key);
                }
                value = default;
                return false;
            }
        }

        public bool Contains(TKey key)
        {
            return TryGetValue(key, out _);
        }

        public bool Remove(TKey key)
        {
            lock (m_Sync)
                return RemoveCore(key);
        }

        public void Clear()
        {
            lock (m_Sync)
            {
                m_Entries.Clear();
                m_Lru.Clear();
                m_Bytes = 0;
            }
        }

        public void Dispose()
        {
            lock (m_Sync)
            {
                m_Disposed = true;
                m_Entries.Clear();
                m_Lru.Clear();
                m_Bytes = 0;
            }
        }

        private void PruneExpired(long now)
        {
            // Called only under the cache lock. No perpetual timer/thread needed.
            LinkedListNode<TKey> node = m_Lru.Last;
            while (node != null)
            {
                LinkedListNode<TKey> previous = node.Previous;
                if (m_Entries[node.Value].ExpiresAt <= now)
                    RemoveCore(node.Value);
                node = previous;
            }
        }

        private bool RemoveCore(TKey key)
        {
            if (!m_Entries.Remove(key, out Entry previous))
                return false;

            m_Lru.Remove(previous.Node);
            m_Bytes -= previous.EstimatedBytes;
            return true;
        }
    }
}
