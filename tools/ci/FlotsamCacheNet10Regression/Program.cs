using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using OpenSim.Framework;
using OpenSim.Region.CoreModules.Asset;

internal static class Program
{
    private static int Main()
    {
        string root = Path.Combine(Path.GetTempPath(), "ogl-flotsam-net10-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            Type format = typeof(FlotsamAssetCache).Assembly.GetType(
                "OpenSim.Region.CoreModules.Asset.FlotsamAssetCacheBinaryFormat", throwOnError: true)!;
            MethodInfo write = format.GetMethod("Write", BindingFlags.Static | BindingFlags.NonPublic)!;
            MethodInfo read = format.GetMethod("Read", BindingFlags.Static | BindingFlags.NonPublic)!;
            if (write is null || read is null)
                throw new Exception("Missing versioned Flotsam codec");

            const string id = "fee03dc5-1f4d-46cc-8456-e19b85699c69";
            AssetBase original = new();
            original.ID = id;
            original.Name = "Net10 cache fixture";
            original.Type = 7;
            original.CreatorID = "creator-test";
            original.Description = "Structured cache metadata roundtrip";
            original.Metadata.CreationDate = new DateTime(2026, 11, 4, 12, 30, 0, DateTimeKind.Utc);
            original.Metadata.ContentType = "application/vnd.ll.notecard";
            original.Type = 7;
            original.Metadata.SHA1 = Enumerable.Range(0, 20).Select(i => (byte)i).ToArray();
            original.Local = true;
            original.Temporary = true;
            original.Flags = AssetFlags.Collectable;
            original.UploadAttempts = 3;
            original.Data = Enumerable.Range(0, 256).Select(i => (byte)i).ToArray();

            using MemoryStream encoded = new();
            write.Invoke(null, new object[] { encoded, original });
            if (encoded.Length <= original.Data.Length)
                throw new Exception("Invalid cache encoding");
            encoded.Position = 0;
            AssetBase restored = (AssetBase)read.Invoke(null, new object[] { encoded, id })!;
            Check(restored.ID == original.ID, "ID mismatch");
            Check(restored.Name == original.Name, "Name mismatch");
            Check(restored.Description == original.Description, "Description mismatch");
            Check(restored.CreatorID == original.CreatorID, "Creator mismatch");
            Check(restored.Type == original.Type, "Asset Type mismatch");
            Check(restored.Metadata.ContentType == original.Metadata.ContentType, "Content type mismatch");
            Check(restored.Flags == original.Flags, "Flags mismatch");
            Check(restored.Local && restored.Temporary, "Local/Temporary lost");
            Check(restored.UploadAttempts == original.UploadAttempts, "UploadAttempts mismatch");
            Check(restored.Metadata.CreationDate == original.Metadata.CreationDate, "Creation time mismatch");
            Check(restored.Metadata.SHA1!.SequenceEqual(original.Metadata.SHA1!), "SHA1 mismatch");
            Check(restored.Data.SequenceEqual(original.Data), "Payload mismatch");

            byte[] payload = encoded.ToArray();
            byte[] tampered = (byte[])payload.Clone();
            tampered[^33] ^= 0x11;
            MustReject(read, tampered, id, "corrupt payload hash");
            MustReject(read, payload.Take(payload.Length - 6).ToArray(), id, "truncated file");
            MustReject(read, payload, "11111111-2222-3333-4444-555555555555", "wrong asset ID");
            MustReject(read, new byte[] { 0, 1, 0, 0, 0, 0, 0, 0 }, id, "legacy format");

            var cache = new FlotsamAssetCache();
            typeof(FlotsamAssetCache).GetField("m_CacheDirectory", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(cache, root);
            var filename = typeof(FlotsamAssetCache).GetMethod("GetFileName", BindingFlags.Instance | BindingFlags.NonPublic)!;
            var getFile = typeof(FlotsamAssetCache).GetMethod("GetFromFileCache", BindingFlags.Instance | BindingFlags.NonPublic)!;
            string path = (string)filename.Invoke(cache, new object[] { id })!;
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllBytes(path, new byte[] { 0, 1, 2, 3, 4, 5, 6, 7 });
            Check(getFile.Invoke(cache, new object[] { id }) is null, "Legacy cache returned an asset");
            Check(!File.Exists(path), "Legacy cache entry was not discarded");
            File.WriteAllBytes(path, payload);
            var fromFile = (AssetBase)getFile.Invoke(cache, new object[] { id })!;
            Check(fromFile is not null && fromFile.Data.SequenceEqual(original.Data), "File cache could not read new versioned format");

            TestConcurrentRealFileCacheWrites(root);
            TestDeadWeakReferencePruning();

            Console.WriteLine("PASS: Flotsam .NET10 codec, concurrent file-cache writes, byte budget, and dead weak-key reclamation");
            return 0;
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static void TestDeadWeakReferencePruning()
    {
        // Do not force a GC in the test. A weak reference with a null target
        // is known dead; a strongly held AssetBase must remain available.
        var cache = new FlotsamAssetCache();
        Type type = typeof(FlotsamAssetCache);
        var weakField = type.GetField("weakAssetReferences",
            BindingFlags.Instance | BindingFlags.NonPublic)!;
        var prune = type.GetMethod("PruneDeadWeakReferences",
            BindingFlags.Instance | BindingFlags.NonPublic)!;
        var liveAsset = new AssetBase { ID = Guid.NewGuid().ToString("D") };
        var weak = (Dictionary<string, WeakReference>)weakField.GetValue(cache)!;
        weak["dead-1"] = new WeakReference(null);
        weak["dead-2"] = new WeakReference(null);
        weak["live"] = new WeakReference(liveAsset);

        Check((int)prune.Invoke(cache, Array.Empty<object>())! == 2,
            "dead weak-reference keys were not reclaimed");
        Check(weak.Count == 1 && weak.ContainsKey("live"),
            "weak-reference pruning discarded live asset metadata");
        Check(ReferenceEquals(weak["live"].Target, liveAsset),
            "weak-reference pruning changed a live asset target");
        Check((int)prune.Invoke(cache, Array.Empty<object>())! == 0,
            "weak-reference pruning must be idempotent");
        GC.KeepAlive(liveAsset);
    }

    private static void TestConcurrentRealFileCacheWrites(string root)
    {
        Type type = typeof(FlotsamAssetCache);
        var cache = new FlotsamAssetCache();
        string cacheRoot = Path.Combine(root, "parallel");
        Directory.CreateDirectory(cacheRoot);

        type.GetField("m_CacheDirectory", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(cache, cacheRoot);

        var filename = type.GetMethod("GetFileName", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var updateFile = type.GetMethod("UpdateFileCache", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var readFile = type.GetMethod("GetFromFileCache", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var processWrites = type.GetMethod("ProcessWrites", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var discardWrite = type.GetMethod("DiscardFileWrite", BindingFlags.Static | BindingFlags.NonPublic)!;
        var workerField = type.GetField("m_assetFileWriteWorker", BindingFlags.Static | BindingFlags.NonPublic)!;
        var limiterField = type.GetField("m_FileWriteByteLimiter", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var writingField = type.GetField("m_CurrentlyWriting", BindingFlags.Static | BindingFlags.NonPublic)!;

        var limiter = new OglInFlightByteLimiter(8 * 1024 * 1024);
        limiterField.SetValue(cache, limiter);

        WaitCallback callback = (WaitCallback)Delegate.CreateDelegate(
            typeof(WaitCallback), cache, processWrites);
        Action<object> discard = (Action<object>)Delegate.CreateDelegate(
            typeof(Action<object>), discardWrite);

        using var worker = new ObjectJobEngine(callback, "RealFlotsamFileCacheCI",
            100, 4, 128, discard);
        Check(workerField.GetValue(null) == null,
            "Flotsam shared writer was unexpectedly initialized");
        workerField.SetValue(null, worker);

        try
        {
            var assets = Enumerable.Range(0, 64).Select(i =>
            {
                var asset = new AssetBase
                {
                    ID = Guid.NewGuid().ToString("D"),
                    Name = "Parallel file cache " + i,
                    CreatorID = "cache-ci",
                    Type = 7,
                    Data = Enumerable.Range(0, 8192).Select(k => (byte)(k + i)).ToArray()
                };
                asset.Metadata.ContentType = "application/vnd.ll.notecard";
                asset.Metadata.SHA1 = Enumerable.Range(0, 20).Select(k => (byte)(k + i)).ToArray();
                return asset;
            }).ToArray();

            Parallel.ForEach(assets, asset =>
                updateFile.Invoke(cache, new object[] { asset.ID, asset, false }));

            bool allWritten = SpinWait.SpinUntil(() =>
                limiter.InFlightBytes == 0 &&
                assets.All(asset => File.Exists(
                    (string)filename.Invoke(cache, new object[] { asset.ID })!)),
                TimeSpan.FromSeconds(15));
            Check(allWritten, "Parallel Flotsam writes were lost or retained in the byte budget");
            Check(worker.Count == 0 && limiter.InFlightBytes == 0,
                "Background cache writer did not drain its queue or release RAM");

            foreach (AssetBase asset in assets)
            {
                var stored = (AssetBase)readFile.Invoke(cache, new object[] { asset.ID })!;
                Check(stored != null && stored.ID == asset.ID &&
                      stored.Data.SequenceEqual(asset.Data),
                    "Concurrent disk-cache serialization lost or damaged an asset");
            }

            object writing = writingField.GetValue(null)!;
            lock (writing)
                Check(((HashSet<string>)writing).Count == 0,
                    "Concurrent file cache left permanent in-progress markers");

            // Force the opt-in adaptive cache I/O gate to saturate. The writer
            // must skip only the optional disk cache and release its in-flight
            // byte reservation and filename marker within the 500ms deadline.
            var adaptiveField = type.GetField("m_AdaptiveFileWriteGate",
                BindingFlags.Instance | BindingFlags.NonPublic)!;
            using (var adaptive = new OglAdaptiveFileWriteGate(1))
            {
                adaptiveField.SetValue(cache, adaptive);
                Check(adaptive.TryAcquire(TimeSpan.Zero, out var held),
                    "adaptive gate failed to reserve its single worker");

                try
                {
                    var skipped = new AssetBase
                    {
                        ID = Guid.NewGuid().ToString("D"),
                        Name = "Adaptive saturated cache fixture",
                        Type = 7,
                        Data = new byte[8192]
                    };
                    updateFile.Invoke(cache, new object[] { skipped.ID, skipped, false });
                    Check(SpinWait.SpinUntil(() =>
                        adaptive.RejectedWrites > 0 && limiter.InFlightBytes == 0,
                        TimeSpan.FromSeconds(5)),
                        "saturated adaptive cache I/O did not release its byte reservation");
                    string skippedPath = (string)filename.Invoke(cache,
                        new object[] { skipped.ID })!;
                    Check(!File.Exists(skippedPath),
                        "saturated adaptive writer should skip optional disk-cache I/O");
                    lock (writing)
                        Check(!((HashSet<string>)writing).Contains(skippedPath),
                            "saturated adaptive writer retained an in-progress marker");
                }
                finally
                {
                    held.Dispose();
                    adaptiveField.SetValue(cache, null);
                }
            }
        }
        finally
        {
            worker.Dispose();
            workerField.SetValue(null, null);
        }
    }

    private static void Check(bool ok, string error)
    {
        if (!ok)
            throw new InvalidOperationException(error);
    }

    private static void MustReject(MethodInfo read, byte[] contents, string id, string label)
    {
        using MemoryStream stream = new(contents);
        try
        {
            read.Invoke(null, new object[] { stream, id });
            throw new InvalidOperationException("Invalid cache accepted: " + label);
        }
        catch (TargetInvocationException ex) when (ex.InnerException is InvalidDataException || ex.InnerException is IOException)
        {
        }
    }
}
