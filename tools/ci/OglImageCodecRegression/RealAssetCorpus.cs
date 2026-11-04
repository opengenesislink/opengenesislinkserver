/* SPDX-License-Identifier: MPL-2.0
 * Read-only, local G1.1 JPEG2000 legacy asset acceptance gate.
 * The script does not upload or modify real texture assets.
 */
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using OpenMetaverse.Imaging;
using OpenSim.Region.CoreModules.Agent.TextureSender;

internal static class RealAssetCorpus
{
    private const int MaxFiles = 2000;
    private static readonly HashSet<string> Extensions =
        new(StringComparer.OrdinalIgnoreCase) { ".j2c", ".j2k", ".jp2" };

    internal static int Run(string directory)
    {
        if (!Directory.Exists(directory))
        {
            Console.Error.WriteLine("Real asset corpus directory not found.");
            return 2;
        }

        var native = new OglOpenJpeg2CodecProvider();
        if (!native.NativeAvailable)
        {
            Console.Error.WriteLine("Native OpenJPEG2 validator unavailable. Build the platform shim first.");
            return 3;
        }

        var candidates = Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories)
            .Where(path => Extensions.Contains(Path.GetExtension(path)))
            .Take(MaxFiles + 1)
            .ToArray();
        if (candidates.Length == 0)
        {
            Console.Error.WriteLine("No .j2c, .j2k or .jp2 files found.");
            return 4;
        }
        if (candidates.Length > MaxFiles)
        {
            Console.Error.WriteLine($"Corpus exceeds {MaxFiles} assets; use bounded batches.");
            return 5;
        }

        Array.Sort(candidates, StringComparer.Ordinal);
        int passed = 0, failed = 0, baselineDifferences = 0;
        long totalBytes = 0;
        var stopwatch = Stopwatch.StartNew();
        var legacy = new OglCsJ2KCodecProvider();

        foreach (string path in candidates)
        {
            string fingerprint = "unavailable";
            try
            {
                var file = new FileInfo(path);
                if (file.Length < 12 || file.Length > OglImageCodecEngine.DefaultMaxAssetBytes)
                    throw new InvalidDataException("Codestream too small or exceeds the 32 MiB input gate");
                byte[] data = File.ReadAllBytes(path);
                totalBytes += data.Length;
                fingerprint = Convert.ToHexString(SHA256.HashData(data)).Substring(0, 16);
                if (!native.TryDecodeLayerBoundaries(data, out OpenJPEG.J2KLayerInfo[] layers,
                        out int components) ||
                    !OglImageCodecEngine.AreLayerBoundariesValid(layers, data.Length) ||
                    components < 1 || components > 5)
                    throw new InvalidDataException("Native OpenJPEG2 + CSJ2K validation failed");

                // Compare with the existing managed path when it can parse
                // raw codestreams. JP2 offsets include box bytes, so those
                // values cannot be compared directly without adjustment.
                bool difference = false;
                if (!path.EndsWith(".jp2", StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        if (legacy.TryDecodeLayerBoundaries(data, out var oldLayers, out _) &&
                            OglImageCodecEngine.AreLayerBoundariesValid(oldLayers, data.Length))
                        {
                            difference = oldLayers.Length != layers.Length ||
                                oldLayers.Where((oldLayer, i) =>
                                    i >= layers.Length ||
                                    oldLayer.Start != layers[i].Start ||
                                    oldLayer.End != layers[i].End).Any();
                        }
                    }
                    catch (Exception)
                    {
                        // A historical asset may not be parseable by the old
                        // baseline. This cannot invalidate a native-validated
                        // codestream, but it is documented by separate tests.
                    }
                }

                if (difference)
                {
                    baselineDifferences++;
                    throw new InvalidDataException("Progressive byte layer positions differ from CSJ2K baseline");
                }

                passed++;
                Console.WriteLine($"PASS sha256:{fingerprint} length:{data.Length} layers:{layers.Length} components:{components}");
            }
            catch (Exception ex)
            {
                failed++;
                Console.Error.WriteLine($"FAIL sha256:{fingerprint} ({ex.GetType().Name}: {ex.Message})");
            }
        }

        stopwatch.Stop();
        Console.WriteLine(
            $"OGL G1.1 real-asset corpus: {passed}/{candidates.Length} pass; {failed} fail; " +
            $"{baselineDifferences} layer mismatches; {totalBytes} bytes; {stopwatch.ElapsedMilliseconds} ms.");
        Console.WriteLine("This is a read-only decode/quality-layer test, not an image pixel, Viewer or Hypergrid certification.");
        return failed == 0 ? 0 : 1;
    }
}
