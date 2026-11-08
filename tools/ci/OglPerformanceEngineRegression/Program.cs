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

        using (var cache = new OglBudgetedLruCache<string, byte[]>(
                   1200, 2, value => value.LongLength))
        {
            Require(cache.AddOrUpdate("a", new byte[600], TimeSpan.FromMinutes(1)),
                "LRU first insert");
            Require(cache.AddOrUpdate("b", new byte[600], TimeSpan.FromMinutes(1)),
                "LRU second insert");
            Require(cache.TryGetValue("a", out _), "LRU cache read");
            Require(cache.AddOrUpdate("c", new byte[600], TimeSpan.FromMinutes(1)),
                "LRU third insert");
            Require(cache.Contains("a") && cache.Contains("c") && !cache.Contains("b"),
                "LRU must evict the least recently accessed asset");
            Require(cache.Count == 2 && cache.EstimatedBytes == 1200,
                "LRU byte/entry budget enforcement");
            Require(!cache.AddOrUpdate("oversized", new byte[2400], TimeSpan.FromMinutes(1)),
                "oversized asset must not be pinned in RAM");
            Require(cache.AddOrUpdate("short", new byte[100], TimeSpan.FromMilliseconds(20)),
                "short-lived asset insert");
            Thread.Sleep(55);
            Require(!cache.TryGetValue("short", out _), "expired asset must be released");
            cache.Clear();
            Require(cache.Count == 0 && cache.EstimatedBytes == 0, "cache clear");
            Require(cache.AddOrUpdate("no-expiry", new byte[100], TimeSpan.Zero),
                "legacy zero TTL must be accepted");
            Thread.Sleep(30);
            Require(cache.Contains("no-expiry"),
                "zero TTL must preserve non-expiring legacy semantics");
        }

        using (var cache = new OglBudgetedLruCache<int, byte[]>(
                   4096, 8, value => value.LongLength))
        {
            System.Threading.Tasks.Parallel.For(0, 600, i =>
            {
                cache.AddOrUpdate(i, new byte[256], TimeSpan.FromMinutes(1));
                cache.TryGetValue(i, out _);
            });
            Require(cache.Count <= 8 && cache.EstimatedBytes <= 4096,
                "parallel inserts exceeded LRU limits");
        }

        Require(OglPerformanceWorkerPolicy.SelectIndependentIoWorkers(0, 1) == 1,
            "auto worker policy must not oversubscribe a single core");
        Require(OglPerformanceWorkerPolicy.SelectIndependentIoWorkers(0, 2) == 1,
            "auto worker policy must retain headroom on two cores");
        Require(OglPerformanceWorkerPolicy.SelectIndependentIoWorkers(0, 8) == 4,
            "auto worker policy should use up to half of eight cores");
        Require(OglPerformanceWorkerPolicy.SelectIndependentIoWorkers(0, 32) == 4,
            "auto worker policy needs a four-worker ceiling");
        Require(OglPerformanceWorkerPolicy.SelectIndependentIoWorkers(8, 2) == 2,
            "explicit worker count must respect CPU quota");
        Require(OglPerformanceWorkerPolicy.SelectIndependentIoWorkers(5, 12) == 5,
            "explicit worker count should be honored when within CPU cap");
        Require(OglPerformanceWorkerPolicy.SelectIndependentIoWorkers(-1, 8) == 1,
            "invalid worker count must fail safe to one");

        var limiter = new OglInFlightByteLimiter(1024);
        Require(limiter.TryReserve(800, out var large), "first byte reservation");
        Require(!limiter.TryReserve(300, out var denied) && denied == null,
            "must reject when the in-flight byte budget is exhausted");
        Require(limiter.InFlightBytes == 800 && limiter.RejectedCount == 1,
            "incorrect byte budget occupancy/rejected counter");
        large.Dispose();
        large.Dispose(); // never count releases twice
        Require(limiter.InFlightBytes == 0 && limiter.HighWaterBytes == 800,
            "byte reservation leaked or double release underflowed");

        // Multiple workers must never overcommit a shared byte budget.
        var parallelLimiter = new OglInFlightByteLimiter(4096);
        System.Threading.Tasks.Parallel.For(0, 1000, i =>
        {
            if (parallelLimiter.TryReserve(512, out var slot))
            {
                try { Thread.SpinWait(3000); }
                finally { slot.Dispose(); }
            }
        });
        Require(parallelLimiter.InFlightBytes == 0 &&
                parallelLimiter.HighWaterBytes <= parallelLimiter.LimitBytes,
            "parallel in-flight budget exceeded or leaked");

        // Discarding accepted queued jobs on cancellation must release
        // their memory reservations; the executing job releases its own.
        var cancelLimiter = new OglInFlightByteLimiter(1024);
        using (var started = new ManualResetEventSlim(false))
        using (var unblock = new ManualResetEventSlim(false))
        using (var workerCompleted = new ManualResetEventSlim(false))
        {
            Require(cancelLimiter.TryReserve(256, out var executing), "executing reservation");
            Require(cancelLimiter.TryReserve(256, out var abandoned), "queued reservation");
            using (var queue = new ObjectJobEngine(o =>
                   {
                       started.Set();
                       if (!unblock.Wait(TimeSpan.FromSeconds(10)))
                           throw new InvalidOperationException("worker was not released");
                       ((OglInFlightByteLimiter.Reservation)o!).Dispose();
                       workerCompleted.Set();
                   }, "OglByteBudgetCancellationRegression", 1000, 1, 2,
                   o => ((OglInFlightByteLimiter.Reservation)o).Dispose()))
            {
                Require(queue.Enqueue(executing), "executing job not queued");
                Require(started.Wait(TimeSpan.FromSeconds(10)), "worker not started");
                Require(queue.Enqueue(abandoned), "second job not queued");
                queue.Cancel();
                Require(cancelLimiter.InFlightBytes == 256,
                    "abandoned queued job was not released by discard callback");
                unblock.Set();
                Require(workerCompleted.Wait(TimeSpan.FromSeconds(10)),
                    "executing job did not finish");
            }
            Require(cancelLimiter.InFlightBytes == 0,
                "cancellation leaked active or queued reservations");
        }

        Console.WriteLine("PASS: OGL G1 performance sampler, bounded job/byte queues, and RAM-budgeted LRU cache");
        return 0;
    }
}
