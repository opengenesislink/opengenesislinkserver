/*
 * Copyright (c) Contributors, http://opensimulator.org/
 * See CONTRIBUTORS.TXT for a full list of copyright holders.
 *
 * Redistribution and use in source and binary forms, with or without
 * modification, are permitted provided that the following conditions are met:
 *     * Redistributions of source code must retain the above copyright
 *       notice, this list of conditions and the following disclaimer.
 *     * Redistributions in binary form must reproduce the above copyright
 *       notice, this list of conditions and the following disclaimer in the
 *       documentation and/or other materials provided with the distribution.
 *     * Neither the name of the OpenSimulator Project nor the
 *       names of its contributors may be used to endorse or promote products
 *       derived from this software without specific prior written permission.
 *
 * THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS'' AND ANY
 * EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
 * WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
 * DISCLAIMED. IN NO EVENT SHALL THE CONTRIBUTORS BE LIABLE FOR ANY
 * DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
 * (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
 * LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
 * ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
 * (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
 * SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */

using System;
using System.Diagnostics;
using System.IO;
using System.Collections.Generic;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Timers;
using log4net;
using Nini.Config;
using Mono.Addins;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Framework.Monitoring;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Server.Base;
using OpenSim.Services.Interfaces;
using System.Runtime.InteropServices;


namespace OpenSim.Region.CoreModules.Asset
{
    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "FlotsamAssetCache")]
    public class FlotsamAssetCache : ISharedRegionModule, IAssetCache, IAssetService, IOglAssetCacheMetricsProvider
    {
        private struct WriteAssetInfo
        {
            public string filename;
            public AssetBase asset;
            public bool replace;
            public OglInFlightByteLimiter.Reservation reservation;
        }

        private static readonly ILog m_log = LogManager.GetLogger( MethodBase.GetCurrentMethod().DeclaringType);

        private bool m_Enabled;
        private bool m_timerRunning;
        private bool m_cleanupRunning;

        private const string m_ModuleName = "FlotsamAssetCache";
        private string m_CacheDirectory = "assetcache";
        private string m_assetLoader;
        private string m_assetLoaderArgs;

        private readonly char[] m_InvalidChars;

        private int m_LogLevel = 0;
        private ulong m_HitRateDisplay = 100; // How often to display hit statistics, given in requests

        private ulong m_Requests;
        private ulong m_RequestsForInprogress;
        private ulong m_DiskHits;
        private ulong m_MemoryHits;
        private ulong m_weakRefHits;

        private static readonly HashSet<string> m_CurrentlyWriting = new();
        private static ObjectJobEngine m_assetFileWriteWorker = null;
        private static long m_CacheWritesRejected;
        private static HashSet<string> m_defaultAssets = new();

        private bool m_FileCacheEnabled = true;
        // File-cache I/O is independent of region-scene mutation. Defaults preserve G1 behavior.
        private int m_FileWriteConcurrency = 1;
        private int m_MaxPendingFileWrites = 0;
        // Optional payload budget across pending and active file-cache I/O.
        private int m_MaxInFlightFileWriteMiB = 0;
        private OglInFlightByteLimiter m_FileWriteByteLimiter;
        // Opt-in adaptive gate for disposable asset disk I/O; never scene/physics.
        private bool m_AdaptiveFileWrites;
        private int m_AdaptiveFileWriteSampleSeconds = 5;
        private OglAdaptiveFileWriteGate m_AdaptiveFileWriteGate;
        private System.Threading.Timer m_AdaptiveFileWriteTimer;
        private readonly object m_AdaptiveSamplingLock = new();
        private long m_LastAdaptiveTimestamp;
        private double m_LastAdaptiveCpuSeconds;
        private readonly OglRegionPressureTracker m_RegionPressureTracker = new();

        private ExpiringCacheOS<string, AssetBase> m_MemoryCache;
        private bool m_MemoryCacheEnabled = false;
        // Optional RAM byte/entry budget. Without it, preserve legacy TTL cache.
        private OglBudgetedLruCache<string, AssetBase> m_BudgetedMemoryCache;
        private int m_MemoryCacheMaxMiB = 0;
        private int m_MemoryCacheMaxEntries = 8192;
        // Optional RAM relief under high .NET GC memory load, disabled by default.
        private bool m_MemoryPressureTrimEnabled;
        private int m_MemoryPressureTrimThresholdPercent = 85;
        private int m_MemoryPressureTrimTargetPercent = 50;
        private System.Threading.Timer m_MemoryPressureTrimTimer;
        private long m_MemoryPressureTrimmed;

        private ExpiringKey<string> m_negativeCache;
        private bool m_negativeCacheEnabled = true;

        // Expiration is expressed in hours.
        private double m_MemoryExpiration = 0.016;
        private const double m_DefaultFileExpiration = 48;
        // Negative cache is in seconds
        private int m_negativeExpiration = 120;
        private TimeSpan m_FileExpiration = TimeSpan.FromHours(m_DefaultFileExpiration);
        private TimeSpan m_FileExpirationCleanupTimer = TimeSpan.FromHours(1.0);

        private static int m_CacheDirectoryTiers = 1;
        private static int m_CacheDirectoryTierLen = 3;
        private static int m_CacheWarnAt = 30000;

        private System.Timers.Timer m_CacheCleanTimer;

        private IAssetService m_AssetService;
        private readonly List<Scene> m_Scenes = new();
        private readonly object timerLock = new();

        private Dictionary<string,WeakReference> weakAssetReferences = new();
        private readonly object weakAssetReferencesLock = new();
        // Optional dead-key cleanup: weak targets are GC-managed, but dictionary
        // strings otherwise persist until a manual/deep cache purge.
        private int m_WeakReferencePruneSeconds;
        private System.Threading.Timer m_WeakReferencePruneTimer;
        private long m_WeakReferencesPruned;
        private static bool m_updateFileTimeOnCacheHit = false;

        private static ExpiringKey<string> m_lastFileAccessTimeChange = null;

        public FlotsamAssetCache()
        {
            List<char> invalidChars = new();
            invalidChars.AddRange(Path.GetInvalidPathChars());
            invalidChars.AddRange(Path.GetInvalidFileNameChars());
            m_InvalidChars = invalidChars.ToArray();
        }

        public Type ReplaceableInterface
        {
            get { return null; }
        }

        public string Name
        {
            get { return m_ModuleName; }
        }

        public void Initialise(IConfigSource source)
        {
            IConfig moduleConfig = source.Configs["Modules"];

            if (moduleConfig is not null)
            {
                string name = moduleConfig.GetString("AssetCaching", string.Empty);

                if (name == Name)
                {
                    m_negativeCache = new ExpiringKey<string>(2000);
                    m_Enabled = true;

                    m_log.Info($"[FLOTSAM ASSET CACHE]: {this.Name} enabled");

                    IConfig assetConfig = source.Configs["AssetCache"];
                    if (assetConfig is null)
                    {
                        m_log.Debug(
                           "[FLOTSAM ASSET CACHE]: AssetCache section missing from config (not copied config-include/FlotsamCache.ini.example?  Using defaults.");
                    }
                    else
                    {
                        m_FileCacheEnabled = assetConfig.GetBoolean("FileCacheEnabled", m_FileCacheEnabled);
                        m_FileWriteConcurrency = OglPerformanceWorkerPolicy.SelectIndependentIoWorkers(
                            assetConfig.GetInt("FileWriteConcurrency", m_FileWriteConcurrency),
                            Environment.ProcessorCount);
                        m_MaxPendingFileWrites = Math.Clamp(
                            assetConfig.GetInt("MaxPendingFileWrites", m_MaxPendingFileWrites),
                            0, 65536);
                        m_AdaptiveFileWrites = assetConfig.GetBoolean(
                            "AdaptiveFileWrites", m_AdaptiveFileWrites);
                        m_AdaptiveFileWriteSampleSeconds = Math.Clamp(
                            assetConfig.GetInt("AdaptiveFileWriteSampleSeconds",
                                m_AdaptiveFileWriteSampleSeconds), 5, 60);
                        int pruneSeconds = assetConfig.GetInt(
                            "WeakReferencePruneSeconds", m_WeakReferencePruneSeconds);
                        m_WeakReferencePruneSeconds = pruneSeconds <= 0
                            ? 0 : Math.Clamp(pruneSeconds, 60, 3600);
                        m_MaxInFlightFileWriteMiB = Math.Clamp(
                            assetConfig.GetInt("MaxInFlightFileWriteMiB", m_MaxInFlightFileWriteMiB),
                            0, 16384);
                        if (m_MaxInFlightFileWriteMiB > 0)
                            m_FileWriteByteLimiter = new OglInFlightByteLimiter(
                                (long)m_MaxInFlightFileWriteMiB * 1024 * 1024);
                        m_CacheDirectory = assetConfig.GetString("CacheDirectory", m_CacheDirectory);
                        m_CacheDirectory = Path.GetFullPath(m_CacheDirectory);

                        m_MemoryCacheEnabled = assetConfig.GetBoolean("MemoryCacheEnabled", m_MemoryCacheEnabled);
                        m_MemoryCacheMaxMiB = Math.Clamp(
                            assetConfig.GetInt("MemoryCacheMaxMiB", m_MemoryCacheMaxMiB), 0, 16384);
                        m_MemoryCacheMaxEntries = Math.Clamp(
                            assetConfig.GetInt("MemoryCacheMaxEntries", m_MemoryCacheMaxEntries), 1, 65536);
                        m_MemoryPressureTrimEnabled = assetConfig.GetBoolean(
                            "MemoryPressureTrimEnabled", m_MemoryPressureTrimEnabled);
                        m_MemoryPressureTrimThresholdPercent = Math.Clamp(
                            assetConfig.GetInt("MemoryPressureTrimThresholdPercent",
                                m_MemoryPressureTrimThresholdPercent), 70, 99);
                        m_MemoryPressureTrimTargetPercent = Math.Clamp(
                            assetConfig.GetInt("MemoryPressureTrimTargetPercent",
                                m_MemoryPressureTrimTargetPercent), 10, 90);
                        m_MemoryExpiration = assetConfig.GetDouble("MemoryCacheTimeout", m_MemoryExpiration);
                        m_MemoryExpiration *= 3600.0; // config in hours to seconds

                        m_negativeCacheEnabled = assetConfig.GetBoolean("NegativeCacheEnabled", m_negativeCacheEnabled);
                        m_negativeExpiration = assetConfig.GetInt("NegativeCacheTimeout", m_negativeExpiration);

                        m_updateFileTimeOnCacheHit = assetConfig.GetBoolean("UpdateFileTimeOnCacheHit", m_updateFileTimeOnCacheHit);
                        m_updateFileTimeOnCacheHit &= m_FileCacheEnabled;

                        m_LogLevel = assetConfig.GetInt("LogLevel", m_LogLevel);
                        m_HitRateDisplay = (ulong)assetConfig.GetLong("HitRateDisplay", (long)m_HitRateDisplay);

                        m_FileExpiration = TimeSpan.FromHours(assetConfig.GetDouble("FileCacheTimeout", m_DefaultFileExpiration));
                        m_FileExpirationCleanupTimer = TimeSpan.FromHours(
                                assetConfig.GetDouble("FileCleanupTimer", m_FileExpirationCleanupTimer.TotalHours));

                        m_CacheDirectoryTiers = assetConfig.GetInt("CacheDirectoryTiers", m_CacheDirectoryTiers);
                        m_CacheDirectoryTierLen = assetConfig.GetInt("CacheDirectoryTierLength", m_CacheDirectoryTierLen);

                        m_CacheWarnAt = assetConfig.GetInt("CacheWarnAt", m_CacheWarnAt);
                    }

                    if(m_updateFileTimeOnCacheHit)
                        m_lastFileAccessTimeChange = new ExpiringKey<string>(300000);

                    if (m_MemoryCacheEnabled)
                    {
                        if (m_MemoryCacheMaxMiB > 0)
                        {
                            m_BudgetedMemoryCache = new OglBudgetedLruCache<string, AssetBase>(
                                (long)m_MemoryCacheMaxMiB * 1024 * 1024,
                                m_MemoryCacheMaxEntries,
                                asset => (long)(asset.Data?.LongLength ?? 0) + 1024L);
                            m_log.InfoFormat(
                                "[FLOTSAM ASSET CACHE]: Memory cache LRU budget {0} MiB, {1} entries",
                                m_MemoryCacheMaxMiB, m_MemoryCacheMaxEntries);
                        }
                        else
                            m_MemoryCache = new ExpiringCacheOS<string, AssetBase>((int)m_MemoryExpiration * 500);
                    }

                    m_log.Info($"[FLOTSAM ASSET CACHE]: Cache Directory {m_CacheDirectory}");

                    if (m_CacheDirectoryTiers < 1)
                        m_CacheDirectoryTiers = 1;
                    else if (m_CacheDirectoryTiers > 3)
                        m_CacheDirectoryTiers = 3;

                    if (m_CacheDirectoryTierLen < 1)
                        m_CacheDirectoryTierLen = 1;
                    else if (m_CacheDirectoryTierLen > 4)
                        m_CacheDirectoryTierLen = 4;

                    m_negativeExpiration *= 1000;

                    assetConfig = source.Configs["AssetService"];
                    if(assetConfig is not null)
                    {
                        m_assetLoader = assetConfig.GetString("DefaultAssetLoader", string.Empty);
                        m_assetLoaderArgs = assetConfig.GetString("AssetLoaderArgs", string.Empty);
                        if (string.IsNullOrWhiteSpace(m_assetLoaderArgs))
                            m_assetLoader = string.Empty;
                    }

                    MainConsole.Instance.Commands.AddCommand("Assets", true, "fcache status", "fcache status", "Display cache status", HandleConsoleCommand);
                    MainConsole.Instance.Commands.AddCommand("Assets", true, "fcache clear",  "fcache clear [file] [memory]", "Remove all assets in the cache.  If file or memory is specified then only this cache is cleared.", HandleConsoleCommand);
                    MainConsole.Instance.Commands.AddCommand("Assets", true, "fcache assets", "fcache assets", "Attempt a deep scan and cache of all assets in all scenes", HandleConsoleCommand);
                    MainConsole.Instance.Commands.AddCommand("Assets", true, "fcache expire", "fcache expire <datetime(mm/dd/YYYY)>", "Purge cached assets older than the specified date/time", HandleConsoleCommand);
                    if (!string.IsNullOrWhiteSpace(m_assetLoader))
                    {
                        MainConsole.Instance.Commands.AddCommand("Assets", true, "fcache cachedefaultassets", "fcache cachedefaultassets", "loads local default assets to cache. This may override grid ones. use with care", HandleConsoleCommand);
                        MainConsole.Instance.Commands.AddCommand("Assets", true, "fcache deletedefaultassets", "fcache deletedefaultassets", "deletes default local assets from cache so they can be refreshed from grid. use with care", HandleConsoleCommand);
                    }
                }
            }
        }

        public void PostInitialise()
        {
        }

        public void Close()
        {
            lock (timerLock)
            {
                bool noRegions;
                lock (m_Scenes)
                    noRegions = m_Scenes.Count == 0;
                if (!noRegions)
                    return;

                m_cleanupRunning = false;
                if (m_timerRunning)
                {
                    m_timerRunning = false;
                    m_CacheCleanTimer.Stop();
                    m_CacheCleanTimer.Close();
                }
                if (m_assetFileWriteWorker != null)
                {
                    m_assetFileWriteWorker.Dispose();
                    m_assetFileWriteWorker = null;
                }
                // Stop all opt-in background sampling on final close.
                m_AdaptiveFileWriteTimer?.Dispose();
                m_AdaptiveFileWriteTimer = null;
                m_AdaptiveFileWriteGate?.Dispose();
                m_WeakReferencePruneTimer?.Dispose();
                m_WeakReferencePruneTimer = null;
                m_MemoryPressureTrimTimer?.Dispose();
                m_MemoryPressureTrimTimer = null;
                m_BudgetedMemoryCache?.Dispose();
            }
        }

        public void AddRegion(Scene scene)
        {
            if (m_Enabled)
            {
                scene.RegisterModuleInterface<IAssetCache>(this);
                lock (m_Scenes)
                    m_Scenes.Add(scene);
            }
        }

        public void RemoveRegion(Scene scene)
        {
            if (m_Enabled)
            {
                scene.UnregisterModuleInterface<IAssetCache>(this);
                lock (m_Scenes)
                    m_Scenes.Remove(scene);
                lock(timerLock)
                {
                    bool noRegions;
                    lock (m_Scenes)
                        noRegions = m_Scenes.Count == 0;
                    if (noRegions)
                    {
                        m_cleanupRunning = false;
                        if (m_timerRunning)
                        {
                            m_timerRunning = false;
                            m_CacheCleanTimer.Stop();
                            m_CacheCleanTimer.Close();
                        }
                        if (m_assetFileWriteWorker != null)
                        {
                            m_assetFileWriteWorker.Dispose();
                            m_assetFileWriteWorker = null;
                        }
                        // No active scenes: release the opt-in RAM cache now
                        // instead of pinning stale assets until the next TTL.
                        m_BudgetedMemoryCache?.Clear();
                        m_MemoryPressureTrimTimer?.Dispose();
                        m_MemoryPressureTrimTimer = null;
                        m_WeakReferencePruneTimer?.Dispose();
                        m_WeakReferencePruneTimer = null;
                        m_AdaptiveFileWriteTimer?.Dispose();
                        m_AdaptiveFileWriteTimer = null;
                        lock (m_AdaptiveSamplingLock)
                        {
                            m_LastAdaptiveTimestamp = 0;
                            m_RegionPressureTracker.Reset();
                        }
                    }
                }
            }
        }

        public void RegionLoaded(Scene scene)
        {
            if (m_Enabled)
            {
                m_AssetService ??= scene.RequestModuleInterface<IAssetService>();
                lock(timerLock)
                {
                    if(!m_timerRunning)
                    {
                        if (m_FileCacheEnabled && (m_FileExpiration > TimeSpan.Zero) && (m_FileExpirationCleanupTimer > TimeSpan.Zero))
                        {
                            m_CacheCleanTimer = new System.Timers.Timer(m_FileExpirationCleanupTimer.TotalMilliseconds)
                            {
                                AutoReset = false
                            };
                            m_CacheCleanTimer.Elapsed += CleanupExpiredFiles;
                            m_CacheCleanTimer.Start();
                            m_timerRunning = true;
                        }
                    }

                    if (m_FileCacheEnabled && m_assetFileWriteWorker == null)
                    {
                        if (m_AdaptiveFileWrites && m_FileWriteConcurrency > 1)
                        {
                            m_AdaptiveFileWriteGate ??=
                                new OglAdaptiveFileWriteGate(m_FileWriteConcurrency);
                        }

                        m_assetFileWriteWorker = new ObjectJobEngine(
                            ProcessWrites, "FlotsamCacheWriter", 1000,
                            m_FileWriteConcurrency, m_MaxPendingFileWrites,
                            DiscardFileWrite);
                        m_log.InfoFormat(
                            "[FLOTSAM ASSET CACHE]: File writers={0}, pending cache queue cap={1} (0=legacy unbounded), adaptive={2}",
                            m_FileWriteConcurrency, m_MaxPendingFileWrites,
                            m_AdaptiveFileWriteGate != null);
                    }

                    if (m_AdaptiveFileWriteGate != null &&
                        m_AdaptiveFileWriteTimer == null)
                    {
                        TimeSpan interval = TimeSpan.FromSeconds(m_AdaptiveFileWriteSampleSeconds);
                        m_AdaptiveFileWriteTimer = new System.Threading.Timer(
                            SampleAdaptiveFileWritePressure, null, interval, interval);
                    }

                    if (m_MemoryPressureTrimEnabled &&
                        m_BudgetedMemoryCache != null &&
                        m_MemoryPressureTrimTimer == null)
                    {
                        // Slow, independent of scene heartbeat and asset hot paths.
                        TimeSpan memoryInterval = TimeSpan.FromSeconds(30);
                        m_MemoryPressureTrimTimer = new System.Threading.Timer(
                            SampleMemoryPressure, null, memoryInterval, memoryInterval);
                    }

                    if (m_WeakReferencePruneSeconds > 0 &&
                        m_WeakReferencePruneTimer == null)
                    {
                        TimeSpan interval = TimeSpan.FromSeconds(m_WeakReferencePruneSeconds);
                        m_WeakReferencePruneTimer = new System.Threading.Timer(
                            _ =>
                            {
                                try
                                {
                                    PruneDeadWeakReferences();
                                }
                                catch (Exception error)
                                {
                                    // Optional maintenance must never crash a simulator.
                                    m_log.Warn("[FLOTSAM ASSET CACHE]: Weak-ref sweep skipped: " +
                                        error.Message);
                                }
                            }, null, interval, interval);
                    }

                    // Region membership may be changing while optional
                    // asset-loader initialization is running on another thread.
                    bool firstRegion;
                    lock (m_Scenes)
                        firstRegion = m_Scenes.Count > 0 &&
                            scene.RegionInfo.RegionID == m_Scenes[0].RegionInfo.RegionID;
                    if (!string.IsNullOrWhiteSpace(m_assetLoader) && firstRegion)
                    {
                        IAssetLoader assetLoader = ServerUtils.LoadPlugin<IAssetLoader>(m_assetLoader, Array.Empty<object>());
                        if (assetLoader is not null)
                        {
                            HashSet<string> ids = new();
                            assetLoader.ForEachDefaultXmlAsset(
                                m_assetLoaderArgs,
                                delegate (AssetBase a)
                                {
                                    Cache(a, true);
                                    ids.Add(a.ID);
                                });
                            m_defaultAssets = ids;
                        }
                    }
                }
            }
        }

        private static void DiscardFileWrite(object o)
        {
            if (o is WriteAssetInfo discarded)
            {
                discarded.reservation?.Dispose();
                lock (m_CurrentlyWriting)
                    m_CurrentlyWriting.Remove(discarded.filename);
            }
        }

        private void SampleMemoryPressure(object unused)
        {
            OglBudgetedLruCache<string, AssetBase> cache = m_BudgetedMemoryCache;
            if (!m_MemoryPressureTrimEnabled || cache == null)
                return;

            try
            {
                // No forced GC and no authority over the real asset store.
                GCMemoryInfo memory = GC.GetGCMemoryInfo();
                int trimmed = cache.TrimForMemoryPressure(
                    memory.MemoryLoadBytes, memory.TotalAvailableMemoryBytes,
                    m_MemoryPressureTrimThresholdPercent,
                    m_MemoryPressureTrimTargetPercent);
                if (trimmed > 0)
                    Interlocked.Add(ref m_MemoryPressureTrimmed, trimmed);
            }
            catch (Exception error)
            {
                m_log.Warn("[FLOTSAM ASSET CACHE]: Optional RAM-pressure sample skipped: " +
                    error.GetType().Name);
            }
        }

        private void SampleAdaptiveFileWritePressure(object unused)
        {
            if (m_AdaptiveFileWriteGate == null)
                return;

            try
            {
                lock (m_AdaptiveSamplingLock)
                {
                    long now = Stopwatch.GetTimestamp();
                    using Process process = Process.GetCurrentProcess();
                    double cpuSeconds = process.TotalProcessorTime.TotalSeconds;

                    Scene[] scenes;
                    lock (m_Scenes)
                        scenes = m_Scenes.ToArray();

                    var regionCounters = new List<OglRegionPressureCounter>(scenes.Length);
                    foreach (Scene scene in scenes)
                    {
                        if (scene.OglPerformanceTiming is null)
                            continue;
                        (long total, long overrun) =
                            scene.OglPerformanceTiming.GetLifetimeCounters();
                        regionCounters.Add(new OglRegionPressureCounter(
                            scene.RegionInfo.RegionID.ToString(), total, overrun));
                    }
                    double? regionOverrunPercent =
                        m_RegionPressureTracker.Sample(regionCounters);

                    if (m_LastAdaptiveTimestamp > 0 &&
                        now > m_LastAdaptiveTimestamp && scenes.Length > 0)
                    {
                        double wallSeconds =
                            (now - m_LastAdaptiveTimestamp) / (double)Stopwatch.Frequency;
                        double cpuPercent = Math.Clamp(
                            100.0 * Math.Max(0, cpuSeconds - m_LastAdaptiveCpuSeconds) /
                            (wallSeconds * Math.Max(1, Environment.ProcessorCount)), 0, 100);

                        m_AdaptiveFileWriteGate.UpdatePressure(cpuPercent, regionOverrunPercent);
                    }

                    m_LastAdaptiveTimestamp = now;
                    m_LastAdaptiveCpuSeconds = cpuSeconds;
                }
            }
            catch (Exception e)
            {
                m_log.Debug("[FLOTSAM ASSET CACHE]: Adaptive I/O sampling skipped: " + e.Message);
            }
        }

        private void ProcessWrites(object o)
        {
            WriteAssetInfo wai = (WriteAssetInfo)o;
            OglAdaptiveFileWriteGate.Permit permit = null;
            try
            {
                // Adaptive mode can temporarily lower concurrent disk I/O
                // when the CPU is busy or region work overruns its budget.
                // Workers wait briefly, then drop only expendable cache writes.
                if (m_AdaptiveFileWriteGate != null &&
                    !m_AdaptiveFileWriteGate.TryAcquire(
                        TimeSpan.FromMilliseconds(500), out permit))
                {
                    Interlocked.Increment(ref m_CacheWritesRejected);
                    lock (m_CurrentlyWriting)
                        m_CurrentlyWriting.Remove(wai.filename);
                    return;
                }

                WriteFileCache(wai.filename, wai.asset, wai.replace);
            }
            catch (Exception e)
            {
                m_log.Warn("[FLOTSAM ASSET CACHE]: Background disk write failed: " + e.Message);
            }
            finally
            {
                permit?.Dispose();
                wai.reservation?.Dispose();
            }
        }

        ////////////////////////////////////////////////////////////
        // IAssetCache
        //
        private int PruneDeadWeakReferences()
        {
            // No forced GC, only remove entries whose targets are already
            // collected. Never remove a live asset or touch authoritative data.
            int removed = 0;
            lock (weakAssetReferencesLock)
            {
                var deadKeys = new List<string>();
                foreach (KeyValuePair<string, WeakReference> entry in weakAssetReferences)
                {
                    if (entry.Value is null || !entry.Value.IsAlive)
                        deadKeys.Add(entry.Key);
                }

                foreach (string key in deadKeys)
                    if (weakAssetReferences.Remove(key))
                        ++removed;
            }

            if (removed > 0)
                Interlocked.Add(ref m_WeakReferencesPruned, removed);
            return removed;
        }

        private void UpdateWeakReference(string key, AssetBase asset)
        {
            lock(weakAssetReferencesLock)
            {
                ref WeakReference aref = ref CollectionsMarshal.GetValueRefOrAddDefault(weakAssetReferences, key, out bool ex);
                if(ex)
                    aref.Target = asset;
                else
                    aref = new WeakReference(asset);
            }
        }

        private void UpdateMemoryCache(string key, AssetBase asset)
        {
            if (m_BudgetedMemoryCache != null)
                m_BudgetedMemoryCache.AddOrUpdate(
                    key, asset, TimeSpan.FromSeconds(m_MemoryExpiration));
            else
                m_MemoryCache.AddOrUpdate(key, asset, m_MemoryExpiration);
        }

        private void UpdateFileCache(string key, AssetBase asset, bool replace = false)
        {
            if (m_assetFileWriteWorker is null)
                return;

            string filename = GetFileName(key);
            bool registered = false;
            bool queued = false;
            OglInFlightByteLimiter.Reservation reservation = null;

            try
            {
                // Avoid duplicate writes even with multiple independent disk workers.
                lock (m_CurrentlyWriting)
                {
                    if (!m_CurrentlyWriting.Add(filename))
                        return;
                    registered = true;
                }

                // Count both pending and currently executing writes. A queue
                // length alone cannot bound memory when assets have varied sizes.
                if (m_FileWriteByteLimiter != null &&
                    !m_FileWriteByteLimiter.TryReserve(
                        (long)(asset.Data?.LongLength ?? 0) + 1024L, out reservation))
                    return;

                WriteAssetInfo wai = new()
                {
                    filename = filename,
                    asset = asset,
                    replace = replace,
                    reservation = reservation
                };
                // Saturation skips only disposable disk-cache I/O.
                queued = m_assetFileWriteWorker?.Enqueue(wai) == true;
            }
            catch (Exception e)
            {
                m_log.Warn($"[FLOTSAM ASSET CACHE]: Failed to queue asset cache write for {asset.ID}: {e.Message}");
            }
            finally
            {
                if (!queued)
                {
                    reservation?.Dispose();
                    if (registered)
                    {
                        Interlocked.Increment(ref m_CacheWritesRejected);
                        lock (m_CurrentlyWriting)
                            m_CurrentlyWriting.Remove(filename);
                    }
                }
                // Accepted jobs release their reservation after I/O, or when
                // cancelled/abandoned by the ObjectJobEngine.
            }
        }

        public void Cache(AssetBase asset, bool replace = false)
        {
            if (asset is not null)
            {
                //m_log.DebugFormat("[FLOTSAM ASSET CACHE]: Caching asset with id {0}", asset.ID);
                UpdateWeakReference(asset.ID, asset);

                if (m_MemoryCacheEnabled)
                    UpdateMemoryCache(asset.ID, asset);

                if (m_FileCacheEnabled)
                    UpdateFileCache(asset.ID, asset, replace);

                if (m_negativeCacheEnabled)
                    m_negativeCache.Remove(asset.ID);
            }
        }

        public void CacheNegative(string id)
        {
            if (m_negativeCacheEnabled)
                m_negativeCache.Add(id, m_negativeExpiration);
        }

        /// <summary>
        /// Updates the cached file with the current time.
        /// </summary>
        /// <param name="filename">Filename.</param>
        /// <returns><c>true</c>, if the update was successful, false otherwise.</returns>
        private static bool CheckUpdateFileLastAccessTime(string filename)
        {
            try
            {
                File.SetLastAccessTime(filename, DateTime.Now);
                m_lastFileAccessTimeChange?.Add(filename, 900000);
                return true;
            }
            catch (FileNotFoundException)
            {
                return false;
            }
            catch
            {
                return true; // ignore other errors
            }
        }

        private static void UpdateFileLastAccessTime(string filename)
        {
            try
            {
                if(!m_lastFileAccessTimeChange.ContainsKey(filename))
                {
                    File.SetLastAccessTime(filename, DateTime.Now);
                    m_lastFileAccessTimeChange.Add(filename, 900000);
                }
            }
            catch
            {
            }
        }

        private AssetBase GetFromWeakReference(string id)
        {
            lock(weakAssetReferencesLock)
            {
                if (weakAssetReferences.TryGetValue(id, out WeakReference aref))
                {
                    if (aref.Target is AssetBase asset)
                    {
                        m_weakRefHits++;
                        return asset;
                    }
                }
            }
            return null;
        }

        /// <summary>
        /// Try to get an asset from the in-memory cache.
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        private AssetBase GetFromMemoryCache(string id)
        {
            AssetBase asset;
            bool found = m_BudgetedMemoryCache != null
                ? m_BudgetedMemoryCache.TryGetValue(id, out asset)
                : m_MemoryCache.TryGetValue(id, out asset);
            if (found)
            {
                m_MemoryHits++;
                return asset;
            }
            return null;
        }

        private bool CheckFromMemoryCache(string id)
        {
            return m_BudgetedMemoryCache != null
                ? m_BudgetedMemoryCache.Contains(id)
                : m_MemoryCache.Contains(id);
        }

        /// <summary>
        /// Try to get an asset from the file cache.
        /// </summary>
        /// <param name="id"></param>
        /// <returns>An asset retrieved from the file cache.  null if there was a problem retrieving an asset.</returns>
        private AssetBase GetFromFileCache(string id)
        {
            string filename = GetFileName(id);

            // Track how often we have the problem that an asset is requested while
            // it is still being downloaded by a previous request.
            lock (m_CurrentlyWriting)
            {
                if (m_CurrentlyWriting.Contains(filename))
                {
                    m_RequestsForInprogress++;
                    return null;
                }
            }

            AssetBase asset = null;

            try
            {
                using FileStream stream = File.Open(filename, FileMode.Open, FileAccess.Read, FileShare.Read);

                asset = FlotsamAssetCacheBinaryFormat.Read(stream, id);
                m_DiskHits++;
            }
            catch (FileNotFoundException)
            {
            }
            catch (DirectoryNotFoundException)
            {
            }
            catch (FlotsamAssetCacheBinaryFormat.LegacyCacheFormatException)
            {
                // Files written by .NET 8 BinaryFormatter cannot be read on .NET 10.
                // This is only a disposable simulator cache, NOT the asset store.
                // Silently drop the obsolete entry and fetch it again upstream.
                try { File.Delete(filename); }
                catch (IOException e) { m_log.Warn($"[FLOTSAM ASSET CACHE]: Cannot remove obsolete cache entry {filename}: {e.Message}"); }
                catch (UnauthorizedAccessException e) { m_log.Warn($"[FLOTSAM ASSET CACHE]: Cannot remove obsolete cache entry {filename}: {e.Message}"); }
            }
            catch (Exception e) when (e is InvalidDataException || e is EndOfStreamException || e is DecoderFallbackException)
            {
                m_log.Warn($"[FLOTSAM ASSET CACHE]: Discarding invalid cache entry {filename} for {id}: {e.Message}");
                try { File.Delete(filename); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
            catch (Exception e)
            {
                m_log.Warn($"[FLOTSAM ASSET CACHE]: Failed to get file {filename} for asset {id}: {e.Message}");
            }

        return asset;
        }

        private bool CheckFromFileCache(string id)
        {
            try
            {
                return File.Exists(GetFileName(id));
            }
            catch
            {
            }
            return false;
        }

        // For IAssetService
        public AssetBase Get(string id)
        {
            Get(id, out AssetBase asset);
            return asset;
        }

        public AssetBase Get(string id, string ForeignAssetService, bool dummy)
        {
            return null;
        }

        public bool Get(string id, out AssetBase asset)
        {
            asset = null;

            m_Requests++;

            if (id.Equals(UUID.ZeroString))
                return false;

            if (m_negativeCache.ContainsKey(id))
                return false;

            asset = GetFromWeakReference(id);
            if (asset is not null)
            {
                if(m_updateFileTimeOnCacheHit)
                    UpdateFileLastAccessTime(GetFileName(id));

                if (m_MemoryCacheEnabled)
                    UpdateMemoryCache(id, asset);
                return true;
            }

            if (m_MemoryCacheEnabled)
            {
                asset = GetFromMemoryCache(id);
                if(asset is not null)
                {
                    UpdateWeakReference(id,asset);
                    if (m_updateFileTimeOnCacheHit)
                        UpdateFileLastAccessTime(GetFileName(id));

                    return true;
                }
            }

            if (m_FileCacheEnabled)
            {
                asset = GetFromFileCache(id);
                if(asset is not null)
                {
                    UpdateWeakReference(id,asset);
                    if (m_MemoryCacheEnabled)
                        UpdateMemoryCache(id, asset);
                }
            }
            return true;
        }

        public bool GetFromMemory(string id, out AssetBase asset)
        {
            asset = null;

            m_Requests++;

            if (id.Equals(Util.UUIDZeroString))
                return false;

            if (m_negativeCache.ContainsKey(id))
                return false;

            asset = GetFromWeakReference(id);
            if (asset != null)
            {
                if (m_updateFileTimeOnCacheHit)
                {
                    string filename = GetFileName(id);
                    UpdateFileLastAccessTime(filename);
                }
                if (m_MemoryCacheEnabled)
                    UpdateMemoryCache(id, asset);
                return true;
            }

            if (m_MemoryCacheEnabled)
            {
                asset = GetFromMemoryCache(id);
                if (asset != null)
                {
                    UpdateWeakReference(id, asset);
                    if (m_updateFileTimeOnCacheHit)
                    {
                        string filename = GetFileName(id);
                        UpdateFileLastAccessTime(filename);
                    }
                    return true;
                }
            }
            return true;
        }

        public bool Check(string id)
        {
            if(GetFromWeakReference(id) is not null)
                return true;

            if (m_MemoryCacheEnabled && CheckFromMemoryCache(id))
                return true;

            if (m_FileCacheEnabled && CheckFromFileCache(id))
                return true;
            return false;
        }

        // does not check negative cache
        public AssetBase GetCached(string id)
        {
            m_Requests++;

            AssetBase asset = GetFromWeakReference(id);
            if (asset is not null)
            {
                if (m_updateFileTimeOnCacheHit)
                    UpdateFileLastAccessTime(GetFileName(id));

                if (m_MemoryCacheEnabled)
                    UpdateMemoryCache(id, asset);
                return asset;
            }

            if (m_MemoryCacheEnabled)
            {
                asset = GetFromMemoryCache(id);
                if (asset is not null)
                {
                    UpdateWeakReference(id, asset);
                    if (m_updateFileTimeOnCacheHit)
                        UpdateFileLastAccessTime(GetFileName(id));

                    return asset;
                }
            }

            if (m_FileCacheEnabled)
            {
                asset = GetFromFileCache(id);
                if (asset is not null)
                {
                    UpdateWeakReference(id, asset);
                    if (m_MemoryCacheEnabled)
                        UpdateMemoryCache(id, asset);
                }
            }
            return asset;
        }

        public void Expire(string id)
        {
            if (m_LogLevel >= 2)
                m_log.Debug($"[FLOTSAM ASSET CACHE]: Expiring Asset {id}");

            try
            {
                lock (weakAssetReferencesLock)
                    weakAssetReferences.Remove(id);

                if (m_MemoryCacheEnabled)
                {
                    if (m_BudgetedMemoryCache != null)
                        m_BudgetedMemoryCache.Remove(id);
                    else
                        m_MemoryCache.Remove(id);
                }

                if (m_negativeCacheEnabled)
                    m_negativeCache.Remove(id);

                if (m_FileCacheEnabled)
                    File.Delete(GetFileName(id));
            }
            catch (Exception e)
            {
                if (m_LogLevel >= 2)
                    m_log.Warn($"[FLOTSAM ASSET CACHE]: Failed to expire cached file {id}: {e.Message}");
            }
        }

        public void Clear()
        {
            if (m_LogLevel >= 2)
                m_log.Debug("[FLOTSAM ASSET CACHE]: Clearing caches.");

            if (m_FileCacheEnabled && Directory.Exists(m_CacheDirectory))
            {
                foreach (string dir in Directory.GetDirectories(m_CacheDirectory))
                {
                    try
                    {
                        Directory.Delete(dir, true);
                    }
                    catch { }
                }
            }

            if (m_MemoryCacheEnabled)
            {
                if (m_BudgetedMemoryCache != null)
                    m_BudgetedMemoryCache.Clear();
                else
                {
                    m_MemoryCache.Dispose();
                    m_MemoryCache = new ExpiringCacheOS<string, AssetBase>((int)m_MemoryExpiration * 500);
                }
            }
            if (m_negativeCacheEnabled)
            {
                m_negativeCache.Dispose();
                m_negativeCache = new ExpiringKey<string>(2000);
            }

            lock (weakAssetReferencesLock)
                weakAssetReferences = new Dictionary<string, WeakReference>();
        }

        private void CleanupExpiredFiles(object source, ElapsedEventArgs e)
        {
            lock (timerLock)
            {
                if (!m_timerRunning || m_cleanupRunning || !Directory.Exists(m_CacheDirectory))
                    return;
                m_cleanupRunning = true;
            }

            // Purge all files last accessed prior to this point
            DoCleanExpiredFiles(DateTime.Now - m_FileExpiration);
        }

        private void DoCleanExpiredFiles(DateTime purgeLine)
        {
            //long heap = 0;
            //if (m_LogLevel >= 2)
            //{
                m_log.Info($"[FLOTSAM ASSET CACHE]: Start background expiring files older than {purgeLine}");
                long heap = GC.GetTotalMemory(false);
            //}

            // An asset cache may contain local non-temporary assets that are not in the asset service.  Therefore,
            // before cleaning up expired files we must scan the objects in the scene to make sure that we retain
            // such local assets if they have not been recently accessed.
            Dictionary<UUID,sbyte> gids = GatherSceneAssets();

            int cooldown = 0;
            m_log.Info("[FLOTSAM ASSET CACHE] start asset files expire");
            foreach (string subdir in Directory.GetDirectories(m_CacheDirectory))
            {
                if(!m_cleanupRunning)
                    break;
                cooldown = CleanExpiredFiles(subdir, gids, purgeLine, cooldown);
                if (++cooldown >= 10)
                {
                    Thread.Sleep(120);
                    cooldown = 0;
                }
            }

            lock (weakAssetReferencesLock)
            {
                weakAssetReferences = new Dictionary<string, WeakReference>();
                m_weakRefHits=0;
            }

            lock (timerLock)
            {
                if (m_timerRunning)
                    m_CacheCleanTimer.Start();
                m_cleanupRunning = false;
            }
            //if (m_LogLevel >= 2)
            {
                heap = GC.GetTotalMemory(false) - heap;
                double fheap = Math.Round((double)(heap / (1024 * 1024)), 3);
                m_log.Info($"[FLOTSAM ASSET CACHE]: Finished expiring files, heap delta: {fheap}MB.");
            }
        }

        /// <summary>
        /// Recurses through specified directory checking for asset files last
        /// accessed prior to the specified purge line and deletes them.  Also
        /// removes empty tier directories.
        /// </summary>
        /// <param name="dir"></param>
        /// <param name="purgeTimeline"></param>
        private int CleanExpiredFiles(string dir, Dictionary<UUID, sbyte> gids, DateTime purgeTimeline, int cooldown)
        {
            try
            {
                if (!m_cleanupRunning)
                    return cooldown;

                int dirSize = 0;

                // Recurse into lower tiers
                foreach (string subdir in Directory.GetDirectories(dir))
                {
                    if (!m_cleanupRunning)
                        return cooldown;

                    ++dirSize;
                    cooldown = CleanExpiredFiles(subdir, gids, purgeTimeline, cooldown);
                    if (++cooldown > 10)
                    {
                        Thread.Sleep(60);
                        cooldown = 0;
                    }
                }

                foreach (string file in Directory.GetFiles(dir))
                {
                    if (!m_cleanupRunning)
                        return cooldown;

                    ++dirSize;
                    string id = Path.GetFileName(file);
                    if (string.IsNullOrEmpty(id))
                        continue; //??

                    if (m_defaultAssets.Contains(id) || (UUID.TryParse(id, out UUID uid) && gids.ContainsKey(uid)))
                    {
                        ++cooldown;
                        continue;
                    }

                    if (File.GetLastAccessTime(file) < purgeTimeline)
                    {
                        try
                        {
                            File.Delete(file);
                            lock (weakAssetReferencesLock)
                                weakAssetReferences.Remove(id);
                        }
                        catch { }
                        cooldown += 5;
                        --dirSize;
                    }

                    if (++cooldown >= 20)
                    {
                        Thread.Sleep(60);
                        cooldown = 0;
                    }
                }

                // Check if a tier directory is empty, if so, delete it
                if (m_cleanupRunning && dirSize == 0)
                {
                    try
                    {
                        Directory.Delete(dir);
                    }
                    catch { }

                    cooldown += 5;
                    if (cooldown >= 20)
                    {
                        Thread.Sleep(60);
                        cooldown = 0;
                    }
                }
                else if (dirSize >= m_CacheWarnAt)
                {
                    m_log.Warn(
                        $"[FLOTSAM ASSET CACHE]: Cache folder exceeded CacheWarnAt limit {dir} {dirSize}. Suggest increasing tiers, tier length, or reducing cache expiration");
                }
            }
            catch (DirectoryNotFoundException)
            {
                // If we get here, another node on the same box has
                // already removed the directory. Continue with next.
            }
            catch (Exception e)
            {
                m_log.Warn($"[FLOTSAM ASSET CACHE]: Could not complete clean of expired files in {dir}: {e.Message}");
            }
            return cooldown;
        }

        /// <summary>
        /// Determines the filename for an AssetID stored in the file cache
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        private string GetFileName(string id)
        {
            StringBuilder sb = osStringBuilderCache.Acquire();
            int indx = id.IndexOfAny(m_InvalidChars);
            if (indx >= 0)
            {
                sb.Append(id);
                int sublen = id.Length - indx;
                for(int i = 0; i < m_InvalidChars.Length; ++i)
                {
                    sb.Replace(m_InvalidChars[i], '_', indx, sublen);
                }
                id = sb.ToString();
                sb.Clear();
            }
            if(m_CacheDirectoryTiers == 1)
            {
                sb.Append(id.AsSpan(0, m_CacheDirectoryTierLen));
                sb.Append(Path.DirectorySeparatorChar);
            }
            else
            {
                for (int p = 0; p < m_CacheDirectoryTiers * m_CacheDirectoryTierLen; p += m_CacheDirectoryTierLen)
                {
                    sb.Append(id.AsSpan(p, m_CacheDirectoryTierLen));
                    sb.Append(Path.DirectorySeparatorChar);
                }
            }
            sb.Append(id);

            return Path.Combine(m_CacheDirectory, osStringBuilderCache.GetStringAndRelease(sb));
        }

        /// <summary>
        /// Writes a file to the file cache, creating any necessary
        /// tier directories along the way
        /// </summary>
        /// <param name="filename"></param>
        /// <param name="asset"></param>
        private static void WriteFileCache(string filename, AssetBase asset, bool replace)
        {
            try
            {
                // If the file is already cached, don't cache it, just touch it so access time is updated
                if (!replace && File.Exists(filename))
                {
                    if (m_updateFileTimeOnCacheHit)
                        UpdateFileLastAccessTime(filename);
                    return;
                }

                string directory = Path.GetDirectoryName(filename);
                string tempname = Path.Combine(directory, Path.GetRandomFileName());
                try
                {
                    if (!Directory.Exists(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }

                    using (Stream stream = File.Open(tempname, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    {
                        FlotsamAssetCacheBinaryFormat.Write(stream, asset);
                        stream.Flush();
                    }
                    m_lastFileAccessTimeChange?.Add(filename, 900000);
                }
                catch (Exception e)
                {
                    m_log.Warn(
                        $"[FLOTSAM ASSET CACHE]: Failed to write asset {asset.ID} to temporary location {tempname} (final {filename}) on cache in {directory}: {e.Message}");
                    try { File.Delete(tempname); }
                    catch (IOException) { }
                    catch (UnauthorizedAccessException) { }
                    return;
                }

                try
                {
                    if(replace)
                        File.Delete(filename);
                    File.Move(tempname, filename);
                }
                catch
                {
                    try
                    {
                        File.Delete(tempname);
                    }
                    catch{ }
                    // If we see an IOException here it's likely that some other competing thread has written the
                    // cache file first, so ignore.  Other IOException errors (e.g. filesystem full) should be
                    // signally by the earlier temporary file writing code.
                }
            }
            finally
            {
                // Even if the write fails with an exception, we need to make sure
                // that we release the lock on that file, otherwise it'll never get
                // cached
                lock (m_CurrentlyWriting)
                {
                    m_CurrentlyWriting.Remove(filename);
                }
            }
        }

        /// <summary>
        /// Scan through the file cache, and return number of assets currently cached.
        /// </summary>
        /// <param name="dir"></param>
        /// <returns></returns>
        private int GetFileCacheCount(string dir)
        {
            try
            {
                int count = 0;
                int cooldown = 0;
                foreach (string subdir in Directory.GetDirectories(dir))
                {
                    count += GetFileCacheCount(subdir);
                    ++cooldown;
                    if(cooldown > 50)
                    {
                        Thread.Sleep(100);
                        cooldown = 0;
                    }
                }
                return count + Directory.GetFiles(dir).Length;
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// This notes the last time the Region had a deep asset scan performed on it.
        /// </summary>
        /// <param name="regionID"></param>
        private void StampRegionStatusFile(UUID regionID)
        {
            string RegionCacheStatusFile = Path.Combine(m_CacheDirectory, $"RegionStatus_{regionID}.fac");

            try
            {
                if (File.Exists(RegionCacheStatusFile))
                {
                    File.SetLastWriteTime(RegionCacheStatusFile, DateTime.Now);
                }
                else
                {
                    File.WriteAllText(
                        RegionCacheStatusFile,
                        "Please do not delete this file unless you are manually clearing your Flotsam Asset Cache.");
                }
            }
            catch (Exception e)
            {
                m_log.Warn($"[FLOTSAM ASSET CACHE]: Could not stamp region status file for region {regionID}: {e. Message}");
            }
        }

        /// <summary>
        /// Iterates through all Scenes, doing a deep scan through assets
        /// to update the access time of all assets present in the scene or referenced by assets
        /// in the scene.
        /// </summary>
        /// <param name="tryGetUncached">
        /// If true, then assets scanned which are not found in cache are added to the cache.
        /// </param>
        /// <returns>Number of distinct asset references found in the scene.</returns>
        private int TouchAllSceneAssets(bool tryGetUncached)
        {
            m_log.Info("[FLOTSAM ASSET CACHE] start touch files of assets in use");

            Dictionary<UUID,sbyte> gatheredids = GatherSceneAssets();

            int cooldown = 0;
            foreach(UUID id in gatheredids.Keys)
            {
                if (!m_cleanupRunning)
                    break;

                string idstr = id.ToString();
                if (!CheckUpdateFileLastAccessTime(GetFileName(idstr)) && tryGetUncached)
                {
                    cooldown += 5;
                    m_AssetService.Get(idstr);
                }
                if (++cooldown > 50)
                {
                    Thread.Sleep(50);
                    cooldown = 0;
                }
            }
            return gatheredids.Count;
        }

        private Dictionary<UUID, sbyte> GatherSceneAssets()
        {
            m_log.Info("[FLOTSAM ASSET CACHE] gather assets in use");

            Dictionary<UUID, sbyte> gatheredids = new();
            UuidGatherer gatherer = new(m_AssetService, gatheredids);

            int cooldown = 0;
            foreach (Scene s in m_Scenes)
            {
                gatherer.AddGathered(s.RegionInfo.RegionSettings.TerrainTexture1, (sbyte)AssetType.Texture);
                gatherer.AddGathered(s.RegionInfo.RegionSettings.TerrainTexture2, (sbyte)AssetType.Texture);
                gatherer.AddGathered(s.RegionInfo.RegionSettings.TerrainTexture3, (sbyte)AssetType.Texture);
                gatherer.AddGathered(s.RegionInfo.RegionSettings.TerrainTexture4, (sbyte)AssetType.Texture);

                gatherer.AddGathered(s.RegionInfo.RegionSettings.TerrainPBR1, (sbyte)AssetType.Texture);
                gatherer.AddGathered(s.RegionInfo.RegionSettings.TerrainPBR2, (sbyte)AssetType.Texture);
                gatherer.AddGathered(s.RegionInfo.RegionSettings.TerrainPBR3, (sbyte)AssetType.Texture);
                gatherer.AddGathered(s.RegionInfo.RegionSettings.TerrainPBR4, (sbyte)AssetType.Texture);

                gatherer.AddGathered(s.RegionInfo.RegionSettings.TerrainImageID, (sbyte)AssetType.Texture);

                s.RegionEnvironment?.GatherAssets(gatheredids);

                if (s.LandChannel is not null)
                {
                    List<ILandObject> landObjects = s.LandChannel.AllParcels();
                    foreach (ILandObject lo in landObjects)
                    {
                        if (lo.LandData is not null && lo.LandData.Environment is not null)
                            lo.LandData.Environment.GatherAssets(gatheredids);
                    }
                }

                EntityBase[] entities = s.Entities.GetEntities();
                foreach (EntityBase entity in entities.AsSpan())
                {
                    if (!m_cleanupRunning)
                        break;

                    if (entity is SceneObjectGroup sog)
                    {
                        if (sog.IsDeleted)
                            continue;

                        gatherer.AddForInspection(sog);
                        while (gatherer.GatherNext())
                        {
                            if (++cooldown > 50)
                            {
                                Thread.Sleep(60);
                                cooldown = 0;
                            }
                        }
                        if (++cooldown > 25)
                        {
                            Thread.Sleep(60);
                            cooldown = 0;
                        }
                    }
                    else if( entity is ScenePresence sp)
                    {
                        if (sp.IsChildAgent || sp.IsDeleted || sp.Appearance is null)
                            continue;

                        Primitive.TextureEntry Texture = sp.Appearance.Texture;
                        if (Texture is null)
                            continue;

                        Primitive.TextureEntryFace[] FaceTextures = Texture.FaceTextures;
                        if (FaceTextures is null)
                            continue;

                        for (int it = 0; it < AvatarAppearance.BAKE_INDICES.Length; it++)
                        {
                            int idx = AvatarAppearance.BAKE_INDICES[it];
                            if(idx < FaceTextures.Length)
                            {
                                Primitive.TextureEntryFace face = FaceTextures[idx];
                                if (face is null)
                                    continue;
                                if (face.TextureID.IsZero() || face.TextureID.Equals(AppearanceManager.DEFAULT_AVATAR_TEXTURE))
                                    continue;
                                gatherer.AddGathered(face.TextureID, (sbyte)AssetType.Texture);
                            }
                        }
                    }
                }
                entities = null;
                if (!m_cleanupRunning)
                    break;

                StampRegionStatusFile(s.RegionInfo.RegionID);
            }

            gatherer.GatherAll();

            gatherer.FailedUUIDs.Clear();
            gatherer.UncertainAssetsUUIDs.Clear();

            m_log.Info($"[FLOTSAM ASSET CACHE]     found {gatheredids.Count} possible assets in use)");
            return gatheredids;
        }

        /// <summary>
        /// Deletes all cache contents
        /// </summary>
        private void ClearFileCache()
        {
            if(!Directory.Exists(m_CacheDirectory))
                return;

            foreach (string dir in Directory.GetDirectories(m_CacheDirectory))
            {
                try
                {
                    Directory.Delete(dir, true);
                }
                catch (Exception e)
                {
                    m_log.Warn($"[FLOTSAM ASSET CACHE]: Couldn't clear asset cache directory {dir} from {m_CacheDirectory}: {e.Message}");
                }
            }

            foreach (string file in Directory.GetFiles(m_CacheDirectory))
            {
                try
                {
                    File.Delete(file);
                }
                catch (Exception e)
                {
                    m_log.Warn($"[FLOTSAM ASSET CACHE]: Couldn't clear asset cache file {file} from {m_CacheDirectory}: {e.Message}");
                }
            }
        }

        private List<string> GenerateCacheHitReport()
        {
            List<string> outputLines = new();

            double invReq = 100.0 / m_Requests;

            double weakHitRate = m_weakRefHits * invReq;
            int weakEntriesAlive = 0;
            lock(weakAssetReferencesLock)
            {
                foreach(WeakReference aref in weakAssetReferences.Values)
                {
                    if (aref.IsAlive)
                        ++weakEntriesAlive;
                }
            }
            int weakEntries = weakAssetReferences.Count;

            double fileHitRate = m_DiskHits * invReq;
            double TotalHitRate = weakHitRate + fileHitRate;

            outputLines.Add($"Total requests: {m_Requests}");
            outputLines.Add($"unCollected Hit Rate: {weakHitRate:0.00}% ({weakEntries} entries {weakEntriesAlive} alive)");
            outputLines.Add($"File Hit Rate: {fileHitRate:0.00}%");

            if (m_MemoryCacheEnabled)
            {
                double HitRate = m_MemoryHits * invReq;
                outputLines.Add($"Memory Hit Rate: {HitRate:0.00}%");

                TotalHitRate += HitRate;
            }
            outputLines.Add($"Total Hit Rate: {TotalHitRate:0.00}%");

            outputLines.Add($"Requests overlap during file writing: {m_RequestsForInprogress}");

            return outputLines;
        }

        // Sampled on the existing signed NodeAgent heartbeat. No new network
        // listener, forced GC or cache mutation. Numbers are approximate
        // concurrent snapshots of Flotsam's existing counters.
        public IReadOnlyDictionary<string, long> CaptureAssetCacheMetrics()
        {
            if (!m_Enabled)
                return new Dictionary<string, long>();
            static long SafeCount(ulong value) =>
                value > (ulong)long.MaxValue ? long.MaxValue : (long)value;
            var lru = m_BudgetedMemoryCache;
            var limiter = m_FileWriteByteLimiter;
            var gate = m_AdaptiveFileWriteGate;
            return new Dictionary<string, long>(StringComparer.Ordinal)
            {
                ["perf_asset_requests"] = SafeCount(m_Requests),
                ["perf_asset_memory_hits"] = SafeCount(m_MemoryHits),
                ["perf_asset_disk_hits"] = SafeCount(m_DiskHits),
                ["perf_asset_weak_hits"] = SafeCount(m_weakRefHits),
                ["perf_asset_memory_enabled"] = m_MemoryCacheEnabled ? 1 : 0,
                ["perf_asset_ram_entries"] = lru?.Count ?? 0,
                ["perf_asset_ram_bytes"] = lru?.EstimatedBytes ?? 0,
                ["perf_asset_ram_limit_bytes"] = lru == null ? 0 : (long)m_MemoryCacheMaxMiB * 1024 * 1024,
                ["perf_asset_memory_trims"] = Interlocked.Read(ref m_MemoryPressureTrimmed),
                ["perf_asset_file_writers"] = m_FileWriteConcurrency,
                ["perf_asset_writer_target"] = gate?.TargetWorkers ?? m_FileWriteConcurrency,
                ["perf_asset_writer_active"] = gate?.ActiveWorkers ?? 0,
                ["perf_asset_writer_pending_jobs"] = m_assetFileWriteWorker?.Count ?? 0,
                ["perf_asset_writes_rejected"] = Interlocked.Read(ref m_CacheWritesRejected),
                ["perf_asset_inflight_bytes"] = limiter?.InFlightBytes ?? 0,
                ["perf_asset_inflight_limit_bytes"] = limiter?.LimitBytes ?? 0
            };
        }

        #region Console Commands
        private void HandleConsoleCommand(string module, string[] cmdparams)
        {
            ICommandConsole con = MainConsole.Instance;

            if (cmdparams.Length >= 2)
            {
                string cmd = cmdparams[1];

                switch (cmd)
                {
                    case "status":
                    {
                        WorkManager.RunInThreadPool(delegate
                        {
                            if (m_MemoryCacheEnabled && m_BudgetedMemoryCache != null)
                                con.Output(
                                    "[FLOTSAM ASSET CACHE] Memory Cache (LRU): {0} assets / ~{1} MiB; budget {2} MiB",
                                    m_BudgetedMemoryCache.Count,
                                    m_BudgetedMemoryCache.EstimatedBytes / (1024 * 1024),
                                    m_MemoryCacheMaxMiB);
                            if (m_MemoryPressureTrimEnabled && m_BudgetedMemoryCache != null)
                                con.Output(
                                    "[FLOTSAM ASSET CACHE] RAM pressure trim: enabled at {0}% GC load, LRU target {1}% of budget; {2} assets evicted",
                                    m_MemoryPressureTrimThresholdPercent,
                                    m_MemoryPressureTrimTargetPercent,
                                    Interlocked.Read(ref m_MemoryPressureTrimmed));
                            if (m_MemoryCacheEnabled && m_BudgetedMemoryCache == null)
                                con.Output("[FLOTSAM ASSET CACHE] Memory Cache: {0} assets", m_MemoryCache.Count);
                            else if (!m_MemoryCacheEnabled)
                                con.Output("[FLOTSAM ASSET CACHE] Memory cache disabled");

                            if (m_FileCacheEnabled)
                            {
                                con.Output(
                                    "[FLOTSAM ASSET CACHE] Disk writer queue: {0} pending; {1} cache writes rejected since start",
                                    m_assetFileWriteWorker?.Count ?? 0,
                                    Interlocked.Read(ref m_CacheWritesRejected));
                                if (m_FileWriteByteLimiter != null)
                                    con.Output(
                                        "[FLOTSAM ASSET CACHE] File payloads in flight: ~{0} MiB / {1} MiB (peak ~{2} MiB, byte-budget rejects {3})",
                                        m_FileWriteByteLimiter.InFlightBytes / (1024 * 1024),
                                        m_MaxInFlightFileWriteMiB,
                                        m_FileWriteByteLimiter.HighWaterBytes / (1024 * 1024),
                                        m_FileWriteByteLimiter.RejectedCount);
                                if (m_AdaptiveFileWriteGate != null)
                                    con.Output(
                                        "[FLOTSAM ASSET CACHE] Adaptive file I/O: {0}/{1} target workers; {2} active; {3} adaptive skip(s)",
                                        m_AdaptiveFileWriteGate.TargetWorkers,
                                        m_AdaptiveFileWriteGate.MaxWorkers,
                                        m_AdaptiveFileWriteGate.ActiveWorkers,
                                        m_AdaptiveFileWriteGate.RejectedWrites);
                                if (m_WeakReferencePruneSeconds > 0)
                                    con.Output(
                                        "[FLOTSAM ASSET CACHE] Dead weak-ref sweep: every {0}s, {1} stale keys removed",
                                        m_WeakReferencePruneSeconds,
                                        Interlocked.Read(ref m_WeakReferencesPruned));
                                bool doingscan;
                                lock (timerLock)
                                {
                                    doingscan = m_cleanupRunning;
                                }
                                if(doingscan)
                                {
                                    con.Output("[FLOTSAM ASSET CACHE] a deep scan is in progress, skipping file cache assets count");
                                }
                                else
                                {
                                    con.Output("[FLOTSAM ASSET CACHE] counting file cache assets");
                                    int fileCount = GetFileCacheCount(m_CacheDirectory);
                                    con.Output("[FLOTSAM ASSET CACHE]   File Cache: {0} assets", fileCount);
                                }
                            }
                            else
                            {
                                con.Output("[FLOTSAM ASSET CACHE] File cache disabled");
                            }

                            GenerateCacheHitReport().ForEach(l => con.Output(l));

                            if (m_FileCacheEnabled)
                            {
                                con.Output("[FLOTSAM ASSET CACHE] Deep scans have previously been performed on the following regions:");

                                foreach (string s in Directory.GetFiles(m_CacheDirectory, "*.fac"))
                                {
                                    int start = s.IndexOf('_');
                                    int end = s.IndexOf('.');
                                    if(start > 0 && end > 0)
                                    {
                                        string RegionID = s.Substring(start + 1, end - start);
                                        DateTime RegionDeepScanTMStamp = File.GetLastWriteTime(s);
                                        con.Output("[FLOTSAM ASSET CACHE] Region: {0}, {1}", RegionID, RegionDeepScanTMStamp.ToString("MM/dd/yyyy hh:mm:ss"));
                                    }
                                }
                            }
                        }, null, "CacheStatus", false);

                        break;
                    }
                    case "clear":
                        if (cmdparams.Length < 2)
                        {
                            con.Output("Usage is fcache clear [file] [memory]");
                            break;
                        }

                        bool clearMemory = false, clearFile = false;

                        if (cmdparams.Length == 2)
                        {
                            clearMemory = true;
                            clearFile = true;
                        }
                        foreach (string s in cmdparams)
                        {
                            if (s.ToLower() == "memory")
                                clearMemory = true;
                            else if (s.ToLower() == "file")
                                clearFile = true;
                        }

                        if (clearMemory)
                        {
                            if (m_MemoryCacheEnabled)
                            {
                                if (m_BudgetedMemoryCache != null)
                                    m_BudgetedMemoryCache.Clear();
                                else
                                    m_MemoryCache.Clear();
                                con.Output("Memory cache cleared.");
                            }
                            else
                            {
                                con.Output("Memory cache not enabled.");
                            }
                        }

                        if (clearFile)
                        {
                            if (m_FileCacheEnabled)
                            {
                                ClearFileCache();
                                con.Output("File cache cleared.");
                            }
                            else
                            {
                                con.Output("File cache not enabled.");
                            }
                        }

                        break;

                    case "assets":
                        lock (timerLock)
                        {
                            if (m_cleanupRunning)
                            {
                                con.Output("Flotsam assets check already running");
                                return;
                            }
                            m_cleanupRunning = true;
                        }

                        con.Output("Flotsam Ensuring assets are cached for all scenes.");

                        WorkManager.RunInThreadPool(delegate
                        {
                            bool wasRunning= false;
                            lock(timerLock)
                            {
                                if(m_timerRunning)
                                {
                                    m_CacheCleanTimer.Stop();
                                    m_timerRunning = false;
                                    wasRunning = true;
                                }
                            }

                            if (wasRunning)
                                Thread.Sleep(120);

                            int assetReferenceTotal = TouchAllSceneAssets(true);

                            lock(timerLock)
                            {
                                if(wasRunning)
                                {
                                    m_CacheCleanTimer.Start();
                                    m_timerRunning = true;
                                }
                                m_cleanupRunning = false;
                            }
                            con.Output("Completed check with {0} assets.", assetReferenceTotal);
                        }, null, "TouchAllSceneAssets", false);

                        break;

                    case "expire":
                        lock (timerLock)
                        {
                            if (m_cleanupRunning)
                            {
                                con.Output("Flotsam assets check already running");
                                return;
                            }
                            m_cleanupRunning = true;
                        }

                        if (cmdparams.Length < 3)
                        {
                            con.Output("Invalid parameters for Expire, please specify a valid date & time");
                            m_cleanupRunning = false;
                            break;
                        }

                        string s_expirationDate = "";
                        DateTime expirationDate;

                        if (cmdparams.Length > 3)
                        {
                            s_expirationDate = string.Join(" ", cmdparams, 2, cmdparams.Length - 2);
                        }
                        else
                        {
                            s_expirationDate = cmdparams[2];
                        }
                        if(s_expirationDate.Equals("now", StringComparison.InvariantCultureIgnoreCase))
                            expirationDate = DateTime.Now;
                        else
                        {
                            if (!DateTime.TryParse(s_expirationDate, out expirationDate))
                            {
                                con.Output("{0} is not a valid date & time", cmd);
                                m_cleanupRunning = false;
                                break;
                            }
                            if (expirationDate >= DateTime.Now)
                            {
                                con.Output("{0} date & time must be in past", cmd);
                                m_cleanupRunning = false;
                                break;
                            }
                        }
                        if (m_FileCacheEnabled)
                        {
                            WorkManager.RunInThreadPool(delegate
                            {
                                bool wasRunning = false;
                                lock (timerLock)
                                {
                                    if (m_timerRunning)
                                    {
                                        m_CacheCleanTimer.Stop();
                                        m_timerRunning = false;
                                        wasRunning = true;
                                    }
                                }

                                if(wasRunning)
                                    Thread.Sleep(120);

                                DoCleanExpiredFiles(expirationDate);

                                lock (timerLock)
                                {
                                    if (wasRunning)
                                    {
                                        m_CacheCleanTimer.Start();
                                        m_timerRunning = true;
                                    }
                                    m_cleanupRunning = false;
                                }
                            }, null, "ExpireSceneAssets", false);
                        }
                        else
                            con.Output("File cache not active, not clearing.");

                        break;
                    case "cachedefaultassets":
                        HandleLoadDefaultAssets();
                        break;
                    case "deletedefaultassets":
                        HandleDeleteDefaultAssets();
                        break;
                    default:
                        con.Output("Unknown command {0}", cmd);
                        break;
                }
            }
            else if (cmdparams.Length == 1)
            {
                con.Output("fcache assets - Attempt a deep cache of all assets in all scenes");
                con.Output("fcache expire <datetime> - Purge assets older than the specified date & time");
                con.Output("fcache clear [file] [memory] - Remove cached assets");
                con.Output("fcache status - Display cache status");
                con.Output("fcache cachedefaultassets - loads default assets to cache replacing existent ones, this may override grid assets. Use with care");
                con.Output("fcache deletedefaultassets - deletes default local assets from cache so they can be refreshed from grid");
            }
        }

        #endregion

        #region IAssetService Members

        public AssetMetadata GetMetadata(string id)
        {
            Get(id, out AssetBase asset);
            if (asset == null)
                return null;
            return asset.Metadata;
        }

        public byte[] GetData(string id)
        {
            Get(id, out AssetBase asset);
            if (asset == null)
                return null;
            return asset.Data;
        }

        public bool Get(string id, object sender, AssetRetrieved handler)
        {
            if (!Get(id, out AssetBase asset))
                return false;
            handler(id, sender, asset);
            return true;
        }

        public void Get(string id, string ForeignAssetService, bool StoreOnLocalGrid, SimpleAssetRetrieved callBack)
        {
        }

        public bool[] AssetsExist(string[] ids)
        {
            bool[] exist = new bool[ids.Length];

            for (int i = 0; i < ids.Length; i++)
            {
                exist[i] = Check(ids[i]);
            }

            return exist;
        }

        public string Store(AssetBase asset)
        {
            if (asset.FullID.IsZero())
            {
                asset.FullID = UUID.Random();
            }

            Cache(asset);

            return asset.ID;
        }

        public bool UpdateContent(string id, byte[] data)
        {
            if (!Get(id, out AssetBase asset))
                return false;
            asset.Data = data;
            Cache(asset, true);
            return true;
        }

        public bool Delete(string id)
        {
            Expire(id);
            return true;
        }

        private void HandleLoadDefaultAssets()
        {
            if (string.IsNullOrWhiteSpace(m_assetLoader))
            {
                m_log.Info("[FLOTSAM ASSET CACHE] default assets loader not defined");
                return;
            }

            IAssetLoader assetLoader = ServerUtils.LoadPlugin<IAssetLoader>(m_assetLoader, Array.Empty<object>());
            if (assetLoader == null)
            {
                m_log.Info("[FLOTSAM ASSET CACHE] default assets loader not found");
                return;
            }

            m_log.Info("[FLOTSAM ASSET CACHE] start loading local default assets");

            int count = 0;
            HashSet<string> ids = new();
            assetLoader.ForEachDefaultXmlAsset(
                    m_assetLoaderArgs,
                    delegate (AssetBase a)
                    {
                        Cache(a, true);
                        ids.Add(a.ID);
                        ++count;
                    });
            m_defaultAssets = ids;
            m_log.Info($"[FLOTSAM ASSET CACHE] loaded {count} local default assets");
        }

        private void HandleDeleteDefaultAssets()
        {
            if (string.IsNullOrWhiteSpace(m_assetLoader))
            {
                m_log.Info("[FLOTSAM ASSET CACHE] default assets loader not defined");
                return;
            }

            IAssetLoader assetLoader = ServerUtils.LoadPlugin<IAssetLoader>(m_assetLoader, Array.Empty<object>());
            if (assetLoader is null)
            {
                m_log.Info("[FLOTSAM ASSET CACHE] default assets loader not found");
                return;
            }

            m_log.Info("[FLOTSAM ASSET CACHE] started deleting local default assets");
            int count = 0;
            assetLoader.ForEachDefaultXmlAsset(
                    m_assetLoaderArgs,
                    delegate (AssetBase a)
                    {
                        Expire(a.ID);
                        ++count;
                    });
            m_defaultAssets = new HashSet<string>();
            m_log.Info($"[FLOTSAM ASSET CACHE] deleted {count} local default assets");
        }
        #endregion
    }
    /// <summary>
    /// Versioned, bounded, integrity-checked local simulator cache encoding.
    /// This format never invokes BinaryFormatter or deserializes executable types.
    /// The file cache is disposable: old BinaryFormatter entries are discarded
    /// and re-fetched through the configured authoritative asset service.
    /// </summary>
    internal static class FlotsamAssetCacheBinaryFormat
    {
        private const uint Magic = 0x31434C46; // FLC1 in little-endian
        private const int Version = 1;
        private const int MaxStringBytes = 1024 * 1024;
        private const int MaxAssetBytes = 512 * 1024 * 1024;
        private const int MaxHashBytes = 128;

        internal sealed class LegacyCacheFormatException : IOException
        {
            internal LegacyCacheFormatException()
                : base("Obsolete BinaryFormatter cache entry; re-fetch from asset service") { }
        }

        private static void WriteBytes(BinaryWriter writer, byte[] bytes, int limit)
        {
            if (bytes is null)
            {
                writer.Write(-1);
                return;
            }

            if (bytes.Length > limit)
                throw new InvalidDataException("Cache field exceeds configured size limit");

            writer.Write(bytes.Length);
            writer.Write(bytes);
        }

        private static byte[] ReadBytes(BinaryReader reader, int limit)
        {
            int length = reader.ReadInt32();
            if (length == -1)
                return null;
            if (length < 0 || length > limit || length > reader.BaseStream.Length - reader.BaseStream.Position)
                throw new InvalidDataException("Invalid cache field length");

            byte[] bytes = reader.ReadBytes(length);
            if (bytes.Length != length)
                throw new EndOfStreamException("Truncated cache field");
            return bytes;
        }

        private static void WriteString(BinaryWriter writer, string value)
        {
            WriteBytes(writer, value is null ? null : Encoding.UTF8.GetBytes(value), MaxStringBytes);
        }

        private static string ReadString(BinaryReader reader)
        {
            byte[] data = ReadBytes(reader, MaxStringBytes);
            return data is null ? null : new UTF8Encoding(false, true).GetString(data);
        }

        internal static void Write(Stream stream, AssetBase asset)
        {
            if (asset is null || asset.Metadata is null)
                throw new InvalidDataException("Missing asset or metadata");

            using BinaryWriter writer = new(stream, Encoding.UTF8, leaveOpen: true);
            writer.Write(Magic);
            writer.Write(Version);

            AssetMetadata metadata = asset.Metadata;
            WriteString(writer, metadata.FullID.ToString());
            WriteString(writer, metadata.ID);
            WriteString(writer, metadata.Name);
            WriteString(writer, metadata.Description);
            WriteString(writer, metadata.CreatorID);
            WriteString(writer, metadata.ContentType);
            writer.Write(metadata.Type);
            writer.Write((int)metadata.Flags);
            writer.Write(metadata.Local);
            writer.Write(metadata.Temporary);
            writer.Write(metadata.CreationDate.ToBinary());
            WriteBytes(writer, metadata.SHA1, MaxHashBytes);
            writer.Write(asset.UploadAttempts);
            WriteBytes(writer, asset.Data, MaxAssetBytes);
            writer.Write(SHA256.HashData(asset.Data ?? Array.Empty<byte>()));
            writer.Flush();
        }

        internal static AssetBase Read(Stream stream, string expectedId)
        {
            if (!stream.CanSeek)
                throw new InvalidDataException("File cache stream must be seekable");

            using BinaryReader reader = new(stream, new UTF8Encoding(false, true), leaveOpen: true);
            if (reader.ReadUInt32() != Magic)
                throw new LegacyCacheFormatException();
            if (reader.ReadInt32() != Version)
                throw new InvalidDataException("Unsupported Flotsam cache version");

            string fullId = ReadString(reader);
            string id = ReadString(reader);
            if (!string.Equals(id, expectedId, StringComparison.Ordinal))
                throw new InvalidDataException("Asset ID does not match cache key");

            string name = ReadString(reader);
            string description = ReadString(reader);
            string creatorId = ReadString(reader);
            string contentType = ReadString(reader);
            sbyte type = reader.ReadSByte();
            AssetFlags flags = (AssetFlags)reader.ReadInt32();
            bool local = reader.ReadBoolean();
            bool temporary = reader.ReadBoolean();
            DateTime created = DateTime.FromBinary(reader.ReadInt64());
            byte[] sha1 = ReadBytes(reader, MaxHashBytes);
            int uploadAttempts = reader.ReadInt32();
            byte[] payload = ReadBytes(reader, MaxAssetBytes);
            byte[] checksum = reader.ReadBytes(32);

            if (checksum.Length != 32 ||
                !CryptographicOperations.FixedTimeEquals(checksum, SHA256.HashData(payload ?? Array.Empty<byte>())) ||
                reader.BaseStream.Position != reader.BaseStream.Length)
                throw new InvalidDataException("Cache payload checksum or length mismatch");

            AssetMetadata metadata = new();
            if (UUID.TryParse(fullId, out UUID uuid))
                metadata.FullID = uuid;
            metadata.ID = id;
            metadata.Name = name;
            metadata.Description = description;
            metadata.CreatorID = creatorId;
            if (contentType is not null)
                metadata.ContentType = contentType;
            metadata.Type = type; // Preserve original type after ContentType setter.
            metadata.Flags = flags;
            metadata.Local = local;
            metadata.Temporary = temporary;
            metadata.CreationDate = created;
            metadata.SHA1 = sha1;

            return new AssetBase
            {
                Metadata = metadata,
                Data = payload,
                UploadAttempts = uploadAttempts
            };
        }
    }

}
