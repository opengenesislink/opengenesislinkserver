using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
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
            Console.WriteLine("PASS: Flotsam .NET10 binary cache roundtrip, integrity, bounded legacy rejection and refetch");
            return 0;
        }
        finally
        {
            Directory.Delete(root, recursive: true);
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
        catch (TargetInvocationException ex) when (ex.InnerException is IOException)
        {
        }
    }
}
