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

        // Idle regions still release expired RAM assets. This test never
        // queries the cache until the timer's eviction counter has changed.
        using (var idleCache = new OglBudgetedLruCache<string, byte[]>(
                   4096, 16, value => value.LongLength,
                   TimeSpan.FromMilliseconds(20)))
        {
            Require(idleCache.AddOrUpdate("idle-expiry", new byte[512],
                TimeSpan.FromMilliseconds(40)), "idle RAM asset was not cached");
            Require(SpinWait.SpinUntil(() => idleCache.ExpiredEvictions >= 1,
                TimeSpan.FromSeconds(3)),
                "idle sweep failed to release expired asset without a viewer request");
            Require(idleCache.EstimatedBytes == 0 && idleCache.Count == 0,
                "idle cache retained expired RAM payloads after sweep");
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

        // Separate real work pressure from pacing/scheduler sleep. A 55-FPS
        // normalized viewer readout is not a heartbeat timing measurement.
        var frameWindow = new OglRegionTimingWindow();
        frameWindow.Record(double.NaN, 1, 10); // invalid input must be ignored
        for (int i = 0; i < 95; ++i)
            frameWindow.Record(2.0, 8.5, 10.0);
        for (int i = 0; i < 5; ++i)
            frameWindow.Record(24.0, 25.0, 10.0);
        OglRegionTimingSample frameStats = frameWindow.TakeSnapshotAndReset();
        Require(frameStats.Frames == 100, "timing histogram lost frame observations");
        Require(Math.Abs(frameStats.WorkAverageMs - 3.10) < 0.001,
            "actual scene work average is incorrect");
        Require(frameStats.WorkP95Ms == 2 && frameStats.FrameP95Ms == 10,
            "P95 bucket edges must be conservative");
        Require(frameStats.FrameP99Ms == 30,
            "P99 must report long frames rather than normalize to 55 FPS");
        Require(frameStats.WorkOverBudgetPercent == 5 &&
                frameStats.FrameOverBudgetPercent == 5,
            "budget overrun percentage is incorrect");
        Require(frameWindow.TakeSnapshotAndReset().Frames == 0,
            "window must reset after a heartbeat export");
        var withFrameMetrics = sampler.Capture(new[]
        {
            new OglRegionFrame("timed-region", 125, 0.01f,
                frameStats.Frames, frameStats.WorkAverageMs,
                frameStats.FrameAverageMs, frameStats.WorkP95Ms,
                frameStats.FrameP95Ms, frameStats.FrameP99Ms,
                frameStats.WorkOverBudgetPercent,
                frameStats.FrameOverBudgetPercent)
        });
        using (JsonDocument histogramJson =
               JsonDocument.Parse(withFrameMetrics["perf_region_heartbeats_json"]))
        {
            JsonElement details = histogramJson.RootElement[0];
            Require(details.GetProperty("window_frames").GetInt64() == 100,
                "signed NodeAgent histogram lost the sample count");
            Require(details.GetProperty("frame_p99_ms").GetDouble() == 30,
                "signed NodeAgent histogram lost the P99 frame metric");
        }

        var concurrentFrameWindow = new OglRegionTimingWindow();
        System.Threading.Tasks.Parallel.For(0, 2000, i =>
            concurrentFrameWindow.Record(i % 2 == 0 ? 1 : 3, 5, 10));
        Require(concurrentFrameWindow.TakeSnapshotAndReset().Frames == 2000,
            "concurrent frame timing observations were lost");

        // Idle worker retirement is a race: enqueue right at the 1ms
        // timeout and verify that no accepted work is stranded forever.
        int idleProcessed = 0;
        using (var idleWorkerEngine = new ObjectJobEngine(_ =>
            Interlocked.Increment(ref idleProcessed), "OglIdleWorkerRegression",
            1, 1, 2))
        {
            for (int i = 1; i <= 200; i++)
            {
                Require(idleWorkerEngine.Enqueue(i), "idle worker rejected work");
                Require(SpinWait.SpinUntil(
                    () => Volatile.Read(ref idleProcessed) == i,
                    TimeSpan.FromSeconds(3)),
                    "accepted job stranded on an idle-worker retirement race");
                if ((i & 3) == 0)
                    Thread.Sleep(2);
            }
        }

        // Adaptive cache I/O must make progress without harming scene physics.
        using (var gate = new OglAdaptiveFileWriteGate(4))
        {
            Require(gate.TargetWorkers == 4, "adaptive I/O must start at configured capacity");
            gate.UpdatePressure(95, null);
            gate.UpdatePressure(95, null);
            gate.UpdatePressure(95, null);
            Require(gate.TargetWorkers == 1, "sustained CPU pressure must reduce I/O to one worker");
            for (int sample = 0; sample < 2; ++sample)
                gate.UpdatePressure(15, 0);
            Require(gate.TargetWorkers == 1, "recovery must require three healthy samples");
            gate.UpdatePressure(15, 0);
            Require(gate.TargetWorkers == 2, "healthy recovery should increase capacity gradually");
            gate.UpdatePressure(15, 30);
            Require(gate.TargetWorkers == 1, "region work overrun must reduce cache I/O concurrency");
            gate.UpdatePressure(15, 0);
            gate.UpdatePressure(15, 0);
            gate.UpdatePressure(15, 0);
            Require(gate.TargetWorkers == 2, "region recovery should restore capacity");

            int active = 0;
            int peakActive = 0;
            System.Threading.Tasks.Parallel.For(0, 100, i =>
            {
                if (!gate.TryAcquire(TimeSpan.FromSeconds(5), out var lease))
                    throw new InvalidOperationException("adaptive gate unexpectedly starved writer");

                try
                {
                    int current = Interlocked.Increment(ref active);
                    int observed;
                    do
                    {
                        observed = Volatile.Read(ref peakActive);
                        if (current <= observed) break;
                    }
                    while (Interlocked.CompareExchange(ref peakActive, current, observed) != observed);
                    Thread.SpinWait(8000);
                }
                finally
                {
                    Interlocked.Decrement(ref active);
                    lease.Dispose();
                }
            });
            Require(peakActive <= 2 && gate.ActiveWorkers == 0,
                "adaptive gate exceeded its target concurrent I/O budget");
        }

        // A waiting background writer is released when the gate shuts down.
        var stoppingGate = new OglAdaptiveFileWriteGate(1);
        Require(stoppingGate.TryAcquire(TimeSpan.Zero, out var heldPermit),
            "initial worker permit refused");
        var waitingWriter = System.Threading.Tasks.Task.Run(() =>
        {
            bool entered = stoppingGate.TryAcquire(TimeSpan.FromSeconds(5),
                out var waitingPermit);
            waitingPermit?.Dispose();
            return entered;
        });
        Require(!waitingWriter.Wait(TimeSpan.FromMilliseconds(30)),
            "waiter should be blocked while the only permit is held");
        stoppingGate.Dispose();
        Require(waitingWriter.Wait(TimeSpan.FromSeconds(5)) && !waitingWriter.Result,
            "stopping the adaptive gate did not unblock a waiting writer");
        heldPermit.Dispose();

        // Monotonic region counters must be available without consuming
        // the window exported to authenticated NodeAgent messages.
        var adaptiveWindow = new OglRegionTimingWindow();
        adaptiveWindow.Record(15, 18, 10);
        adaptiveWindow.Record(4, 9, 10);
        var lifetimeCounters = adaptiveWindow.GetLifetimeCounters();
        Require(lifetimeCounters.Frames == 2 && lifetimeCounters.WorkOverruns == 1,
            "independent cumulative region counters are incorrect");
        Require(adaptiveWindow.TakeSnapshotAndReset().Frames == 2,
            "independent pressure monitoring drained the NodeAgent histogram");
        Require(adaptiveWindow.GetLifetimeCounters().Frames == 2,
            "NodeAgent export reset the adaptive lifetime counters");

        // Shutdown must not dispose a queue while a worker is still executing.
        // Accepted but queued items must be discarded exactly once.
        using (var callbackStarted = new ManualResetEventSlim(false))
        using (var finishCallback = new ManualResetEventSlim(false))
        using (var callbackFinished = new ManualResetEventSlim(false))
        {
            int completed = 0;
            int discarded = 0;
            var engine = new ObjectJobEngine(o =>
            {
                callbackStarted.Set();
                Require(finishCallback.Wait(TimeSpan.FromSeconds(10)),
                    "callback unexpectedly timed out");
                Interlocked.Increment(ref completed);
                callbackFinished.Set();
            }, "OglDisposeDuringCallback", 1000, 1, 4, _ =>
            {
                Interlocked.Increment(ref discarded);
            });

            Require(engine.Enqueue(1), "active job rejected");
            Require(callbackStarted.Wait(TimeSpan.FromSeconds(10)),
                "callback did not start");
            Require(engine.Enqueue(2), "queued shutdown job rejected");
            engine.Dispose();  // must return while first callback is still active
            Require(engine.Count == 0 && !engine.Enqueue(3),
                "stopped queue still accepts/retains jobs");
            Require(Volatile.Read(ref discarded) == 1,
                "discard callback must release exactly one queued job");
            finishCallback.Set();
            Require(callbackFinished.Wait(TimeSpan.FromSeconds(10)),
                "active callback did not finish after Dispose");
            Require(Volatile.Read(ref completed) == 1 &&
                    Volatile.Read(ref discarded) == 1,
                "shutdown lost, duplicated or executed a discarded job");
        }

        // Concurrent producers and shutdown: each accepted job is either
        // completed or discarded, never both and never silently leaked.
        int acceptedConcurrent = 0;
        int executedConcurrent = 0;
        int discardedConcurrent = 0;
        var concurrentEngine = new ObjectJobEngine(_ =>
        {
            Thread.SpinWait(1500);
            Interlocked.Increment(ref executedConcurrent);
        }, "OglConcurrentStopStress", 20, 4, 64,
            _ => Interlocked.Increment(ref discardedConcurrent));
        using (var startProducers = new ManualResetEventSlim(false))
        {
            var producers = Enumerable.Range(0, 8).Select(worker =>
                System.Threading.Tasks.Task.Run(() =>
                {
                    Require(startProducers.Wait(TimeSpan.FromSeconds(10)),
                        "producer startup timed out");
                    for (int i = 0; i < 500; i++)
                        if (concurrentEngine.Enqueue(worker * 500 + i))
                            Interlocked.Increment(ref acceptedConcurrent);
                })).ToArray();
            startProducers.Set();
            Thread.Sleep(20);
            concurrentEngine.Dispose();
            Require(System.Threading.Tasks.Task.WaitAll(producers, TimeSpan.FromSeconds(10)),
                "concurrent producers did not finish");
            Require(SpinWait.SpinUntil(
                () => Volatile.Read(ref executedConcurrent) +
                      Volatile.Read(ref discardedConcurrent) ==
                      Volatile.Read(ref acceptedConcurrent),
                TimeSpan.FromSeconds(10)),
                "accepted background job was neither executed nor discarded");
            Require(concurrentEngine.Count == 0 && !concurrentEngine.Enqueue(42),
                "disposed job engine retained queued work or reopened");
        }

        Console.WriteLine("PASS: OGL G1 performance sampler, bounded job/byte queues, and RAM-budgeted LRU cache");
        return 0;
    }
}
