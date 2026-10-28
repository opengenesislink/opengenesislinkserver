// SPDX-License-Identifier: MPL-2.0
using System;
using System.Collections.Generic;

namespace OpenSim.Region.CoreModules.Scripting.DynamicTexture
{
    /// <summary>
    /// Keeps a limited number of superseded dynamic assets available briefly so
    /// an in-flight viewer texture request cannot immediately lose its old UUID.
    /// This does not guarantee that the viewer retains the old image on a new
    /// texture UUID; it only protects the server cache handoff.
    /// </summary>
    internal sealed class DynamicTextureRetirementQueue
    {
        private readonly object m_Sync = new object();
        private readonly Queue<RetiredAsset> m_Queue = new Queue<RetiredAsset>();
        private readonly TimeSpan m_Retention;
        private readonly int m_MaxPending;
        private readonly Func<DateTime> m_UtcNow;
        private readonly Action<Exception> m_OnError;

        private sealed class RetiredAsset
        {
            public DateTime Expiry;
            public Action Expire;
        }

        public DynamicTextureRetirementQueue(int seconds, int maxPending,
            Func<DateTime> utcNow = null, Action<Exception> onError = null)
        {
            m_Retention = TimeSpan.FromSeconds(Math.Clamp(seconds, 0, 120));
            m_MaxPending = Math.Clamp(maxPending, 1, 2048);
            m_UtcNow = utcNow ?? (() => DateTime.UtcNow);
            m_OnError = onError;
        }

        public int PendingCount
        {
            get { lock (m_Sync) return m_Queue.Count; }
        }

        public void Retire(Action expire)
        {
            if (expire == null)
                return;

            List<Action> due = new List<Action>();
            lock (m_Sync)
            {
                DateTime now = m_UtcNow();
                CollectDue(now, false, due);
                if (m_Retention == TimeSpan.Zero)
                    due.Add(expire);
                else
                {
                    // The queue is bounded: when overloaded, age out the oldest
                    // superseded asset rather than retaining unlimited texture data.
                    if (m_Queue.Count >= m_MaxPending)
                        due.Add(m_Queue.Dequeue().Expire);
                    m_Queue.Enqueue(new RetiredAsset
                    {
                        Expiry = now + m_Retention,
                        Expire = expire
                    });
                }
            }
            Execute(due);
        }

        public void ExpireDue()
        {
            List<Action> due = new List<Action>();
            lock (m_Sync)
                CollectDue(m_UtcNow(), false, due);
            Execute(due);
        }

        public void Flush()
        {
            List<Action> due = new List<Action>();
            lock (m_Sync)
                CollectDue(m_UtcNow(), true, due);
            Execute(due);
        }

        private void CollectDue(DateTime now, bool all, List<Action> due)
        {
            while (m_Queue.Count > 0 &&
                   (all || m_Queue.Peek().Expiry <= now))
                due.Add(m_Queue.Dequeue().Expire);
        }

        private void Execute(List<Action> callbacks)
        {
            // Never run potentially blocking cache expiration under m_Sync.
            foreach (Action callback in callbacks)
            {
                try { callback(); }
                catch (Exception error) { m_OnError?.Invoke(error); }
            }
        }
    }
}
