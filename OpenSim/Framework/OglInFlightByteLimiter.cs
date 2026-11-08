// SPDX-License-Identifier: MPL-2.0
// OpenGenesisLINK: optional non-blocking RAM backpressure for independent I/O tasks.
using System;
using System.Threading;

namespace OpenSim.Framework
{
    /// <summary>
    /// Bounds the estimated bytes held by queued and currently executing work.
    /// Every successful reservation must be disposed exactly once; disposal is idempotent.
    /// This is an estimated payload budget, NOT a process-wide RSS limit.
    /// </summary>
    public sealed class OglInFlightByteLimiter
    {
        private readonly long m_LimitBytes;
        private long m_InFlightBytes;
        private long m_HighWaterBytes;
        private long m_Rejected;

        public long LimitBytes => m_LimitBytes;
        public long InFlightBytes => Interlocked.Read(ref m_InFlightBytes);
        public long HighWaterBytes => Interlocked.Read(ref m_HighWaterBytes);
        public long RejectedCount => Interlocked.Read(ref m_Rejected);

        public OglInFlightByteLimiter(long limitBytes)
        {
            if (limitBytes < 1)
                throw new ArgumentOutOfRangeException(nameof(limitBytes));
            m_LimitBytes = limitBytes;
        }

        public bool TryReserve(long estimatedBytes, out Reservation reservation)
        {
            reservation = null;
            if (estimatedBytes <= 0)
                throw new ArgumentOutOfRangeException(nameof(estimatedBytes));

            if (estimatedBytes > m_LimitBytes)
            {
                Interlocked.Increment(ref m_Rejected);
                return false;
            }

            while (true)
            {
                long current = Interlocked.Read(ref m_InFlightBytes);
                if (current > m_LimitBytes - estimatedBytes)
                {
                    Interlocked.Increment(ref m_Rejected);
                    return false;
                }

                long next = current + estimatedBytes;
                if (Interlocked.CompareExchange(ref m_InFlightBytes, next, current) == current)
                {
                    UpdateHighWater(next);
                    reservation = new Reservation(this, estimatedBytes);
                    return true;
                }
            }
        }

        private void UpdateHighWater(long value)
        {
            while (true)
            {
                long current = Interlocked.Read(ref m_HighWaterBytes);
                if (value <= current ||
                    Interlocked.CompareExchange(ref m_HighWaterBytes, value, current) == current)
                    return;
            }
        }

        private void Release(long estimatedBytes)
        {
            Interlocked.Add(ref m_InFlightBytes, -estimatedBytes);
        }

        public sealed class Reservation : IDisposable
        {
            private OglInFlightByteLimiter m_Owner;
            private readonly long m_Bytes;

            internal Reservation(OglInFlightByteLimiter owner, long bytes)
            {
                m_Owner = owner;
                m_Bytes = bytes;
            }

            public void Dispose()
            {
                Interlocked.Exchange(ref m_Owner, null)?.Release(m_Bytes);
            }
        }
    }
}
