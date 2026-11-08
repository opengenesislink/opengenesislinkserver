// SPDX-License-Identifier: MPL-2.0
// Conservative workload sizing for independent background I/O only.
// The simulator's scene graph, physics and YEngine remain serial where required.
using System;

namespace OpenSim.Framework
{
    public static class OglPerformanceWorkerPolicy
    {
        /// <summary>
        /// Explicit 0 opts in to a CPU-aware mode: at most four workers,
        /// using no more than half of the process-visible logical processors.
        /// Positive values respect an eight-worker ceiling and the CPU quota.
        /// Legacy default is 1, not auto.
        /// </summary>
        public static int SelectIndependentIoWorkers(int requestedWorkers, int visibleProcessors)
        {
            int processors = Math.Max(1, visibleProcessors);
            int hardCap = Math.Min(8, processors);

            if (requestedWorkers == 0)
                return Math.Min(4, Math.Max(1, processors / 2));

            return Math.Clamp(requestedWorkers, 1, hardCap);
        }
    }
}
