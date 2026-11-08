// SPDX-License-Identifier: MPL-2.0
// OGL Performance Engine: opt-in, allocation-free heartbeat timing on the hot path.
using System;
using System.Threading;

namespace OpenSim.Framework
{
    /// <summary>
    /// Drainable approximate histograms for a region's work time and total
    /// frame duration. Thread-safe, without locks or allocations in Record().
    /// Quantiles are rounded UP to the next bucket edge. Window boundaries
    /// are approximate because a capture can race with an active heartbeat.
    /// </summary>
    public sealed class OglRegionTimingWindow
    {
        // Millisecond bucket ceilings, including sub-10ms intervals needed
        // for future high-frequency simulators. Last bucket means >1000 ms.
        private static readonly double[] s_Limits =
        {
            1, 2, 4, 6, 8, 10, 12, 16, 20, 30, 45, 60, 90, 120,
            180, 250, 400, 650, 1000, double.PositiveInfinity
        };

        private readonly long[] m_WorkHistogram = new long[s_Limits.Length];
        private readonly long[] m_FrameHistogram = new long[s_Limits.Length];
        private long m_Frames;
        private long m_WorkOverruns;
        private long m_FrameOverruns;
        private long m_TotalWorkMicroseconds;
        private long m_TotalFrameMicroseconds;

        public void Record(double workMs, double frameMs, double budgetMs)
        {
            if (!double.IsFinite(workMs) || !double.IsFinite(frameMs) ||
                !double.IsFinite(budgetMs) || budgetMs <= 0 ||
                workMs < 0 || frameMs < 0)
                return;

            Interlocked.Increment(ref m_WorkHistogram[Bucket(workMs)]);
            Interlocked.Increment(ref m_FrameHistogram[Bucket(frameMs)]);
            Interlocked.Add(ref m_TotalWorkMicroseconds, ToMicroseconds(workMs));
            Interlocked.Add(ref m_TotalFrameMicroseconds, ToMicroseconds(frameMs));
            if (workMs > budgetMs)
                Interlocked.Increment(ref m_WorkOverruns);
            if (frameMs > budgetMs)
                Interlocked.Increment(ref m_FrameOverruns);
            Interlocked.Increment(ref m_Frames);
        }

        public OglRegionTimingSample TakeSnapshotAndReset()
        {
            long[] workHistogram = new long[s_Limits.Length];
            long[] frameHistogram = new long[s_Limits.Length];
            for (int i = 0; i < s_Limits.Length; ++i)
            {
                workHistogram[i] = Interlocked.Exchange(ref m_WorkHistogram[i], 0);
                frameHistogram[i] = Interlocked.Exchange(ref m_FrameHistogram[i], 0);
            }

            long workMicros = Interlocked.Exchange(ref m_TotalWorkMicroseconds, 0);
            long frameMicros = Interlocked.Exchange(ref m_TotalFrameMicroseconds, 0);
            long workOverruns = Interlocked.Exchange(ref m_WorkOverruns, 0);
            long frameOverruns = Interlocked.Exchange(ref m_FrameOverruns, 0);
            long frames = Interlocked.Exchange(ref m_Frames, 0);

            if (frames <= 0)
                return default;

            return new OglRegionTimingSample(
                frames,
                workMicros / (1000.0 * frames),
                frameMicros / (1000.0 * frames),
                Quantile(workHistogram, 0.95),
                Quantile(frameHistogram, 0.95),
                Quantile(frameHistogram, 0.99),
                Math.Clamp(workOverruns * 100.0 / frames, 0, 100),
                Math.Clamp(frameOverruns * 100.0 / frames, 0, 100));
        }

        private static int Bucket(double ms)
        {
            for (int i = 0; i < s_Limits.Length; i++)
                if (ms <= s_Limits[i])
                    return i;
            return s_Limits.Length - 1;
        }

        private static long ToMicroseconds(double milliseconds)
        {
            return (long)Math.Min(milliseconds * 1000.0, long.MaxValue / 4.0);
        }

        private static double Quantile(long[] histogram, double percentile)
        {
            long total = 0;
            foreach (long count in histogram)
                total += count;
            if (total == 0)
                return 0;

            long target = (long)Math.Ceiling(total * percentile);
            long countSoFar = 0;
            for (int i = 0; i < histogram.Length; i++)
            {
                countSoFar += histogram[i];
                if (countSoFar >= target)
                    return double.IsPositiveInfinity(s_Limits[i]) ? 1000 : s_Limits[i];
            }
            return 1000;
        }
    }

    public readonly struct OglRegionTimingSample
    {
        public long Frames { get; }
        public double WorkAverageMs { get; }
        public double FrameAverageMs { get; }
        public double WorkP95Ms { get; }
        public double FrameP95Ms { get; }
        public double FrameP99Ms { get; }
        public double WorkOverBudgetPercent { get; }
        public double FrameOverBudgetPercent { get; }

        public OglRegionTimingSample(long frames, double workAverageMs,
            double frameAverageMs, double workP95Ms, double frameP95Ms, double frameP99Ms,
            double workOverBudgetPercent, double frameOverBudgetPercent)
        {
            Frames = frames;
            WorkAverageMs = workAverageMs;
            FrameAverageMs = frameAverageMs;
            WorkP95Ms = workP95Ms;
            FrameP95Ms = frameP95Ms;
            FrameP99Ms = frameP99Ms;
            WorkOverBudgetPercent = workOverBudgetPercent;
            FrameOverBudgetPercent = frameOverBudgetPercent;
        }
    }
}
