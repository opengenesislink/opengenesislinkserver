// Offline, data-only migration of historical KeyframeMotion NRBF records.
// Never invoke BinaryFormatter, load serialized types or write to a database.
using System;
using System.Collections.Generic;
using System.Formats.Nrbf;
using System.IO;
using System.Security.Cryptography;
using OpenMetaverse;
using OpenSim.Region.Framework.Scenes;

internal static class Program
{
    private const int MaxBytes = 4 * 1024 * 1024;
    private const int MaxFrames = 8192;
    private static readonly byte[] Magic = { 0x4f, 0x47, 0x4b, 0x46 };

    private static int Main(string[] args)
    {
        if (args.Length == 3 && args[0] == "--batch-hex")
            return ConvertBatch(args[1], args[2]);

        if (args.Length != 2 || args[0] is "-h" or "--help")
        {
            Console.Error.WriteLine("Usage: dotnet run --project tools/migration/KeyframeLegacyConverter -- legacy.bin migrated.ogkf");
            Console.Error.WriteLine("Input is a single exported binary KeyframeMotion record. Output must NOT already exist.");
            return 2;
        }

        try
        {
            string source = Path.GetFullPath(args[0]);
            string destination = Path.GetFullPath(args[1]);
            if (string.Equals(source, destination, StringComparison.Ordinal))
                throw new InvalidDataException("Input and output must be different.");
            FileInfo input = new FileInfo(source);
            if (!input.Exists || input.Length == 0 || input.Length > MaxBytes)
                throw new InvalidDataException("Input size must be within 1..4 MiB.");
            byte[] legacy = File.ReadAllBytes(source);
            byte[] converted = ConvertVerified(legacy);

            using FileStream target = new FileStream(destination, FileMode.CreateNew,
                FileAccess.Write, FileShare.None);
            target.Write(converted);
            target.Flush(true);
            Console.WriteLine("PASS: converted one legacy NRBF keyframe record to OGKF v1");
            Console.WriteLine("Original record preserved: " + source);
            Console.WriteLine("New verified record: " + destination);
            return 0;
        }
        catch (Exception error) when (error is IOException or InvalidDataException or
            InvalidOperationException or ArgumentException or NotSupportedException)
        {
            Console.Error.WriteLine("Legacy keyframe migration rejected: " + error.Message);
            return 1;
        }
    }

    // Read-only MariaDB/MySQL export path: a TSV of UUID and HEX(KeyframeMotion)
    // becomes a TSV of UUID, original SHA-256 and verified OGKF HEX. The source
    // database is never contacted. Neither input nor output is overwritten.
    // A malformed or unsupported row aborts the WHOLE batch and emits no file.
    private static int ConvertBatch(string inputPath, string outputPath)
    {
        string input = Path.GetFullPath(inputPath);
        string output = Path.GetFullPath(outputPath);
        if (string.Equals(input, output, StringComparison.Ordinal))
        {
            Console.Error.WriteLine("Batch input and output must be different.");
            return 1;
        }

        string? directory = Path.GetDirectoryName(output);
        if (directory is null || !Directory.Exists(directory) || File.Exists(output))
        {
            Console.Error.WriteLine("Batch output directory must exist and output must not exist.");
            return 1;
        }

        string temporary = Path.Combine(directory, "." + Path.GetFileName(output) +
            "." + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            HashSet<Guid> seen = new HashSet<Guid>();
            int count = 0;
            using (FileStream file = new FileStream(temporary, FileMode.CreateNew,
                       FileAccess.Write, FileShare.None))
            using (StreamWriter writer = new StreamWriter(file))
            using (StreamReader reader = new StreamReader(input))
            {
                string? line;
                while ((line = reader.ReadLine()) is not null)
                {
                    count++;
                    if (count > 100000)
                        throw new InvalidDataException("More than 100000 records in keyframe batch.");

                    string[] columns = line.Split('\t');
                    if (columns.Length != 2 ||
                        !Guid.TryParseExact(columns[0], "D", out Guid objectId) ||
                        !seen.Add(objectId))
                        throw new InvalidDataException("Invalid or duplicate object UUID at line " + count);

                    string hex = columns[1];
                    if (hex.Length < 18 || hex.Length % 2 != 0 ||
                        hex.Length > MaxBytes * 2)
                        throw new InvalidDataException("Invalid BLOB length at line " + count);

                    byte[] original;
                    try { original = System.Convert.FromHexString(hex); }
                    catch (FormatException error)
                    {
                        throw new InvalidDataException("Invalid exported HEX at line " + count, error);
                    }

                    byte[] converted;
                    try { converted = ConvertVerified(original); }
                    catch (Exception error) when (error is InvalidDataException or
                        InvalidOperationException or ArgumentException or NotSupportedException)
                    {
                        throw new InvalidDataException("Invalid NRBF movement at line " + count, error);
                    }

                    writer.Write(objectId.ToString("D"));
                    writer.Write('\t');
                    writer.Write(System.Convert.ToHexString(SHA256.HashData(original)));
                    writer.Write('\t');
                    writer.WriteLine(System.Convert.ToHexString(converted));
                }
                if (count == 0)
                    throw new InvalidDataException("Batch export is empty.");
                writer.Flush();
                file.Flush(true);
            }
            File.Move(temporary, output); // will not overwrite an existing file
            Console.WriteLine("PASS: converted " + count + " legacy keyframes, hashes retained.");
            Console.WriteLine("Input retained: " + input);
            Console.WriteLine("Output (UUID, original SHA-256, verified OGKF HEX): " + output);
            return 0;
        }
        catch (Exception error) when (error is IOException or InvalidDataException or
            InvalidOperationException or ArgumentException or NotSupportedException)
        {
            Console.Error.WriteLine("Legacy keyframe batch rejected; no output installed: " +
                error.Message);
            return 1;
        }
        finally
        {
            if (File.Exists(temporary))
                File.Delete(temporary);
        }
    }

    private static byte[] ConvertVerified(byte[] legacy)
    {
        byte[] converted = Convert(legacy);
        // The target runtime itself must accept the output; never store an
        // unvalidated conversion as a replacement for a region's record.
        KeyframeMotion? tested = KeyframeMotion.FromData(null, converted);
        if (tested is null || tested.HasLegacySerialization ||
            !converted.AsSpan().SequenceEqual(tested.Serialize()))
            throw new InvalidDataException("Generated OGKF payload failed runtime validation.");
        return converted;
    }

    private static byte[] Convert(byte[] bytes)
    {
        using MemoryStream source = new MemoryStream(bytes, false);
        if (!NrbfDecoder.StartsWithPayloadHeader(source))
            throw new InvalidDataException("Not a BinaryFormatter NRBF payload.");
        source.Position = 0;
        ClassRecord root = NrbfDecoder.DecodeClassRecord(source);
        RequireType(root, typeof(KeyframeMotion));

        int mode = EnumValue(root, "m_mode");
        int flags = EnumValue(root, "m_data");
        if (mode is < 0 or > 3 || (flags & ~3) != 0)
            throw new InvalidDataException("Unknown legacy play mode or data flags.");

        Vector3 serializedPosition = ReadVector(NotNull(root.GetClassRecord("m_serializedPosition"), "m_serializedPosition"));
        Vector3 basePosition = ReadVector(NotNull(root.GetClassRecord("m_basePosition"), "m_basePosition"));
        Quaternion baseRotation = ReadQuaternion(NotNull(root.GetClassRecord("m_baseRotation"), "m_baseRotation"));
        bool running = root.GetBoolean("m_running");
        int iterations = root.GetInt32("m_iterations");
        int skipLoops = root.GetInt32("m_skipLoops");
        KeyframeMotion.Keyframe currentFrame = ReadFrame(NotNull(root.GetClassRecord("m_currentFrame"), "m_currentFrame"));

        KeyframeMotion.Keyframe[]? keyframes = ReadArray(root.GetArrayRecord("m_keyframes"));
        ClassRecord list = NotNull(root.GetClassRecord("m_frames"), "m_frames");
        if (!list.TypeName.FullName.StartsWith("System.Collections.Generic.List", StringComparison.Ordinal))
            throw new InvalidDataException("Unexpected legacy m_frames container.");
        int frameCount = list.GetInt32("_size");
        if (frameCount < 0 || frameCount > MaxFrames)
            throw new InvalidDataException("Invalid pending frame count.");
        KeyframeMotion.Keyframe[] storage = ReadArray(list.GetArrayRecord("_items"))
            ?? throw new InvalidDataException("Missing list backing array.");
        if (frameCount > storage.Length)
            throw new InvalidDataException("Pending frame count exceeds backing array.");

        using MemoryStream output = new MemoryStream();
        using (BinaryWriter writer = new BinaryWriter(output, System.Text.Encoding.UTF8, true))
        {
            writer.Write(Magic);
            writer.Write((byte)1);
            writer.Write(mode);
            writer.Write(flags);
            WriteVector(writer, serializedPosition);
            WriteVector(writer, basePosition);
            WriteQuaternion(writer, baseRotation);
            writer.Write(running);
            writer.Write(iterations);
            writer.Write(skipLoops);
            WriteFrame(writer, currentFrame);
            writer.Write(keyframes is null ? -1 : keyframes.Length);
            if (keyframes is not null)
                foreach (KeyframeMotion.Keyframe frame in keyframes) WriteFrame(writer, frame);
            writer.Write(frameCount);
            for (int i = 0; i < frameCount; i++) WriteFrame(writer, storage[i]);
        }
        if (output.Length > MaxBytes)
            throw new InvalidDataException("Converted keyframe exceeds the maximum size.");
        return output.ToArray();
    }

    private static T NotNull<T>(T? record, string name) where T : class =>
        record ?? throw new InvalidDataException("Missing legacy field " + name);

    private static void RequireType(SerializationRecord record, Type expected)
    {
        if (!record.TypeNameMatches(expected))
            throw new InvalidDataException("Unexpected legacy type: " + record.TypeName.FullName);
    }

    private static int EnumValue(ClassRecord record, string field)
    {
        object? raw = record.GetRawValue(field);
        return raw switch
        {
            int value => value,
            PrimitiveTypeRecord<int> value => value.Value,
            ClassRecord enumRecord when enumRecord.HasMember("value__")
                => enumRecord.GetInt32("value__"),
            _ => throw new InvalidDataException("Unexpected enum field: " + field)
        };
    }

    private static float Finite(float value)
    {
        if (!float.IsFinite(value))
            throw new InvalidDataException("Non-finite legacy keyframe component.");
        return value;
    }

    private static Vector3 ReadVector(ClassRecord c)
    {
        RequireType(c, typeof(Vector3));
        return new Vector3(Finite(c.GetSingle("X")), Finite(c.GetSingle("Y")),
                           Finite(c.GetSingle("Z")));
    }

    private static Quaternion ReadQuaternion(ClassRecord c)
    {
        RequireType(c, typeof(Quaternion));
        return new Quaternion(Finite(c.GetSingle("X")), Finite(c.GetSingle("Y")),
            Finite(c.GetSingle("Z")), Finite(c.GetSingle("W")));
    }

    private static KeyframeMotion.Keyframe ReadFrame(ClassRecord c)
    {
        RequireType(c, typeof(KeyframeMotion.Keyframe));
        ClassRecord? position = c.GetClassRecord("Position");
        ClassRecord? rotation = c.GetClassRecord("Rotation");
        return new KeyframeMotion.Keyframe
        {
            Position = position is null ? null : ReadVector(position),
            Rotation = rotation is null ? null : ReadQuaternion(rotation),
            StartRotation = ReadQuaternion(NotNull(c.GetClassRecord("StartRotation"), "StartRotation")),
            TimeMS = c.GetInt32("TimeMS"),
            TimeTotal = c.GetInt32("TimeTotal"),
            AngularVelocity = ReadVector(NotNull(c.GetClassRecord("AngularVelocity"), "AngularVelocity")),
            StartPosition = ReadVector(NotNull(c.GetClassRecord("StartPosition"), "StartPosition"))
        };
    }

    private static KeyframeMotion.Keyframe[]? ReadArray(ArrayRecord? record)
    {
        if (record is null) return null;
        if (record.Rank != 1 || record.Lengths.Length != 1 ||
            record.Lengths[0] < 0 || record.Lengths[0] > MaxFrames)
            throw new InvalidDataException("Invalid legacy keyframe array dimensions.");
        SerializationRecord[] serialized =
            (SerializationRecord[])record.GetArray(typeof(KeyframeMotion.Keyframe[]), allowNulls: false);
        KeyframeMotion.Keyframe[] output = new KeyframeMotion.Keyframe[serialized.Length];
        for (int i = 0; i < output.Length; i++)
            output[i] = ReadFrame(serialized[i] as ClassRecord ??
                throw new InvalidDataException("Unexpected array record"));
        return output;
    }

    private static void WriteVector(BinaryWriter w, Vector3 v)
    {
        w.Write(Finite(v.X)); w.Write(Finite(v.Y)); w.Write(Finite(v.Z));
    }

    private static void WriteQuaternion(BinaryWriter w, Quaternion q)
    {
        w.Write(Finite(q.X)); w.Write(Finite(q.Y));
        w.Write(Finite(q.Z)); w.Write(Finite(q.W));
    }

    private static void WriteFrame(BinaryWriter w, KeyframeMotion.Keyframe f)
    {
        w.Write(f.Position.HasValue);
        if (f.Position.HasValue) WriteVector(w, f.Position.Value);
        w.Write(f.Rotation.HasValue);
        if (f.Rotation.HasValue) WriteQuaternion(w, f.Rotation.Value);
        WriteQuaternion(w, f.StartRotation);
        w.Write(f.TimeMS);
        w.Write(f.TimeTotal);
        WriteVector(w, f.AngularVelocity);
        WriteVector(w, f.StartPosition);
    }
}
