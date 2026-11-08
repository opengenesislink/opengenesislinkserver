// SPDX-License-Identifier: MPL-2.0
// OGL Performance Engine: optional backpressure for disposable cache I/O only.
using System;
using System.Diagnostics;
using System.Threading;

namespace OpenSim.Framework
{
    /// <summary>
    /// An adaptive concurrency gate for independent background asset file writes.
    /// Never controls Physics, Scene.Update, scripts or authoritative asset saves.
    /// Pressure updates use hysteresis to avoid oscillating the worker budget.
    /// </summary>
    public sealed class OglAdaptiveFileWriteGate : IDisposable
    {
        private readonly object m_Sync = new object();
        private readonly int m_MaxWorkers;
        private int m_TargetWorkers;
        private int m_ActiveWorkers;
        private int m_HealthySamples;
        private long m_Rejected;
        private bool m_Disposed;

        public OglAdaptiveFileWriteGate(int maxWorkers)
        {
            if (maxWorkers < 1 || maxWorkers > 8)
                throw new ArgumentOutOfRangeException(nameof(maxWorkers));
            m_MaxWorkers = maxWorkers;
            m_TargetWorkers = maxWorkers;
        }

        public int MaxWorkers => m_MaxWorkers;
        public int TargetWorkers { get { lock (m_Sync) return m_TargetWorkers; } }
        public int ActiveWorkers { get { lock (m_Sync) return m_ActiveWorkers; } }
        public long RejectedWrites => Interlocked.Read(ref m_Rejected);

        /// <summary>
        /// Work-overrun rate can be null when region timings are disabled.
        /// A sustained healthy state restores concurrency one worker at a time.
        /// </summary>
        public void UpdatePressure(double cpuPercent, double? regionWorkOverrunPercent)
        {
            if (!double.IsFinite(cpuPercent))
                return;

            cpuPercent = Math.Clamp(cpuPercent, 0, 100);
            double? lag = regionWorkOverrunPercent;
            if (lag.HasValue && !double.IsFinite(lag.Value))
                lag = null;

            lock (m_Sync)
            {
                if (m_Disposed)
                    return;

                if (cpuPercent >= 85 || (lag.HasValue && lag.Value >= 15))
                {
                    m_TargetWorkers = Math.Max(1, m_TargetWorkers - 1);
                    m_HealthySamples = 0;
                }
                else if (cpuPercent <= 65 && (!lag.HasValue || lag.Value <= 5))
                {
                    if (++m_HealthySamples >= 3)
                    {
                        m_TargetWorkers = Math.Min(m_MaxWorkers, m_TargetWorkers + 1);
                        m_HealthySamples = 0;
                    }
                }
                else
                {
                    m_HealthySamples = 0;
                }

                Monitor.PulseAll(m_Sync);
            }
        }

        public bool TryAcquire(TimeSpan timeout, out Permit permit)
        {
            permit = null;
            if (timeout < TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(timeout));

            long start = Stopwatch.GetTimestamp();
            lock (m_Sync)
            {
                while (!m_Disposed && m_ActiveWorkers >= m_TargetWorkers)
                {
                    double remainingMs = timeout.TotalMilliseconds -
                        (Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency;
                    if (remainingMs <= 0)
                    {
                        Interlocked.Increment(ref m_Rejected);
                        return false;
                    }
                    Monitor.Wait(m_Sync, Math.Max(1, (int)Math.Min(remainingMs, 250)));
                }

                if (m_Disposed)
                {
                    Interlocked.Increment(ref m_Rejected);
                    return false;
                }

                ++m_ActiveWorkers;
                permit = new Permit(this);
                return true;
            }
        }

        private void Release()
        {
            lock (m_Sync)
            {
                --m_ActiveWorkers;
                Monitor.PulseAll(m_Sync);
            }
        }

        public void Dispose()
        {
            lock (m_Sync)
            {
                m_Disposed = true;
                Monitor.PulseAll(m_Sync);
            }
        }

        public sealed class Permit : IDisposable
        {
            private OglAdaptiveFileWriteGate m_Owner;
            internal Permit(OglAdaptiveFileWriteGate owner)
            {
                m_Owner = owner;
            }

            public void Dispose()
            {
                Interlocked.Exchange(ref m_Owner, null)?.Release();
            }
        }
    }
}
