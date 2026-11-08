using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using NexVerse.Core.Observability;
using OpenSim.Framework;

static class Program
{
    static void Require(bool ok, string message)
    {
        if (!ok) throw new InvalidOperationException(message);
    }

    static int Main()
    {
        Require(Math.Abs(OglPerformanceSampler.CalculateCpuPercent(2, 1, 4) - 50) < 0.001,
            "CPU utilization must normalize across available cores");
        Require(OglPerformanceSampler.CalculateCpuPercent(-1, 1, 4) == 0, "negative CPU delta");
        Require(OglPerformanceSampler.CalculateCpuPercent(5, 1, 1) == 100, "CPU percent cap");
        Require(Math.Abs(OglPerformanceSampler.CalculateHeartbeatHz(100, 130, 1.5) - 20) < 0.001,
            "real simulator tick rate calculation");
        Require(OglPerformanceSampler.CalculateHeartbeatHz(uint.MaxValue - 1, 2, 1) == 4,
            "frame counter rollover");
        Require(OglPerformanceSampler.CalculateHeartbeatHz(0, 0, 0) == 0, "zero interval");

        var sampler = new OglPerformanceSampler();
        IReadOnlyDictionary<string, string> first =
            sampler.Capture(new[] { new OglRegionFrame("region-test", 100, 0.0909f) });
        Require(first.ContainsKey("perf_gc_heap_size_bytes"), "missing GC heap metric");
        Require(first.ContainsKey("perf_threadpool_pending_items"), "missing threadpool metric");
        Require(first.ContainsKey("perf_process_cpu_percent"), "missing CPU metric");
        using (JsonDocument json = JsonDocument.Parse(first["perf_region_heartbeats_json"]))
        {
            Require(json.RootElement.GetArrayLength() == 1, "expected one region");
            Require(json.RootElement[0].GetProperty("heartbeat_hz").GetDouble() == 0,
                "first sample must be a baseline");
        }
        Thread.Sleep(150);
        var second = sampler.Capture(new[] { new OglRegionFrame("region-test", 103, 0.0909f) });
        using (JsonDocument json = JsonDocument.Parse(second["perf_region_heartbeats_json"]))
            Require(json.RootElement[0].GetProperty("heartbeat_hz").GetDouble() > 0,
                "heartbeat rate must be measured from samples");
        sampler.Capture(Array.Empty<OglRegionFrame>());

        using var firstStarted = new ManualResetEventSlim(false);
        using var releaseFirst = new ManualResetEventSlim(false);
        using var secondDone = new ManualResetEventSlim(false);
        int processed = 0;
        using (var queue = new ObjectJobEngine(obj =>
               {
                   if ((int)obj! == 1)
                   {
                       firstStarted.Set();
                       if (!releaseFirst.Wait(TimeSpan.FromSeconds(10)))
                           throw new InvalidOperationException("blocked work did not resume");
                   }
                   Interlocked.Increment(ref processed);
                   if ((int)obj! == 2) secondDone.Set();
               }, "OglPerformanceRegression", 1000, 1, 1))
        {
            Require(queue.Enqueue(1), "first job should be accepted");
            Require(firstStarted.Wait(TimeSpan.FromSeconds(10)), "worker did not start");
            Require(queue.Enqueue(2), "one queued item expected");
            Require(!queue.Enqueue(3), "capacity must reject the third item without blocking");
            releaseFirst.Set();
            Require(secondDone.Wait(TimeSpan.FromSeconds(10)), "queued job was lost");
            Require(Volatile.Read(ref processed) == 2, "rejected job must not run");
        }

        Console.WriteLine("PASS: OGL G1 performance sampling, per-region tick rates, and bounded background queue");
        return 0;
    }
}
