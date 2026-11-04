using System;
using System.Drawing;
using System.IO;
using OpenMetaverse.Imaging;
using OpenSim.Region.CoreModules.Agent.TextureSender;

static class Program
{
    private sealed class FakeCodec : IOglImageCodecProvider
    {
        public string Name => "fake";
        public int Calls;
        public bool Throw;
        public bool ReturnValidLayers;
        public bool ReturnInvalidLayers;
        public bool TryDecodeImage(byte[] data, out Image image)
        {
            Calls++;
            image = null;
            if (Throw) throw new InvalidDataException("damaged codestream");
            return false;
        }
        public bool TryDecodeLayerBoundaries(byte[] data, out OpenJPEG.J2KLayerInfo[] layers, out int components)
        {
            Calls++;
            layers = Array.Empty<OpenJPEG.J2KLayerInfo>();
            components = 0;
            if (Throw) throw new InvalidDataException("damaged codestream");
            if (ReturnValidLayers)
            {
                layers = new [] { new OpenJPEG.J2KLayerInfo { Start = 0, End = data.Length } };
                components = 3;
                return true;
            }
            if (ReturnInvalidLayers)
            {
                layers = new [] { new OpenJPEG.J2KLayerInfo { Start = 0, End = data.Length + 1 } };
                return true;
            }
            return false;
        }
    }

    private static int assertions;
    private static void Assert(bool value, string message)
    {
        assertions++;
        if (!value) throw new InvalidOperationException(message);
    }

    private static int Main(string[] args)
    {
        if (args.Length == 2 && args[0] == "--corpus")
            return RealAssetCorpus.Run(args[1]);

        byte[] valid = new byte[12];
        valid[0] = 0xFF;
        valid[1] = 0x4F;
        byte[] original = (byte[])valid.Clone();
        var primary = new FakeCodec();
        var fallback = new FakeCodec { ReturnValidLayers = true };
        var engine = new OglImageCodecEngine(primary, fallback, 16);

        Assert(!engine.TryDecodeLayers(null, out var layers, out _, out var failure) &&
            failure == OglImageDecodeFailure.EmptyInput && layers.Length == 0, "null must fail");
        Assert(!engine.TryDecodeLayers(Array.Empty<byte>(), out layers, out _, out failure) &&
            failure == OglImageDecodeFailure.EmptyInput, "empty must fail");
        Assert(!engine.TryDecodeLayers(new byte[17], out layers, out _, out failure) &&
            failure == OglImageDecodeFailure.InputTooLarge, "max size gate");
        Assert(primary.Calls == 0 && fallback.Calls == 0, "invalid input must not reach native providers");

        Assert(engine.TryDecodeLayers(valid, out layers, out int components, out failure) &&
            failure == OglImageDecodeFailure.None && components == 3 &&
            layers.Length == 1 && layers[0].End == valid.Length, "legacy fallback must preserve layer bounds");
        Assert(primary.Calls == 1 && fallback.Calls == 1, "fallback must run once");
        Assert(Convert.ToHexString(valid) == Convert.ToHexString(original), "input must remain immutable");

        primary.ReturnValidLayers = true;
        Assert(engine.TryDecodeLayers(valid, out _, out _, out failure) &&
            failure == OglImageDecodeFailure.None, "primary must win");
        Assert(fallback.Calls == 1, "successful primary must not invoke fallback");

        primary.ReturnValidLayers = false;
        primary.ReturnInvalidLayers = true;
        Assert(engine.TryDecodeLayers(valid, out _, out _, out failure) &&
            failure == OglImageDecodeFailure.None, "invalid primary boundaries should fall back");

        primary.Throw = true;
        primary.ReturnInvalidLayers = false;
        Assert(engine.TryDecodeLayers(valid, out _, out _, out failure) &&
            failure == OglImageDecodeFailure.None, "provider exceptions must not abort fallback");
        Assert(!engine.TryDecodeImage(valid, out _, out failure) &&
            failure == OglImageDecodeFailure.DecoderFailed, "failed pixel decode must remain failure");
        Assert(!engine.TryDecodeImage(new byte[17], out _, out failure) &&
            failure == OglImageDecodeFailure.InputTooLarge, "pixel input size guard");

        var invalidPrimary = new FakeCodec { ReturnInvalidLayers = true };
        var none = new FakeCodec();
        var strict = new OglImageCodecEngine(invalidPrimary, none);
        Assert(!strict.TryDecodeLayers(valid, out layers, out components, out failure) &&
            failure == OglImageDecodeFailure.InvalidLayers &&
            layers.Length == 0 && components == 0, "invalid layers must not be returned or cached");

        Assert(!OglImageCodecEngine.AreLayerBoundariesValid(null, 12), "null layers");
        Assert(!OglImageCodecEngine.AreLayerBoundariesValid(Array.Empty<OpenJPEG.J2KLayerInfo>(), 12), "empty layers");
        Assert(!OglImageCodecEngine.AreLayerBoundariesValid(
            new[] {new OpenJPEG.J2KLayerInfo {Start=0, End=13}}, 12), "out of bounds layer");
        Assert(!OglImageCodecEngine.AreLayerBoundariesValid(
            new[] {new OpenJPEG.J2KLayerInfo {Start=4, End=2}}, 12), "reversed layer");
        Assert(!OglImageCodecEngine.AreLayerBoundariesValid(
            new[] {new OpenJPEG.J2KLayerInfo {Start=5, End=8},
                   new OpenJPEG.J2KLayerInfo {Start=4, End=12}}, 12), "nonmonotonic layer starts");
        Assert(OglImageCodecEngine.AreLayerBoundariesValid(
            new[] {new OpenJPEG.J2KLayerInfo {Start=0, End=12}}, 12), "valid boundary");

        if (args.Length == 2 && args[0] == "--hybrid")
        {
            byte[] codestream = File.ReadAllBytes(args[1]);
            var hybrid = new OglOpenJpeg2CodecProvider();
            Assert(hybrid.NativeAvailable, "OpenJPEG2 native shim not loaded");

            Assert(hybrid.TryDecodeLayerBoundaries(codestream, out var hybridLayers,
                    out int hybridComponents),
                "OpenJPEG2 decode + managed packet offsets must agree");
            Assert(hybridComponents >= 1 && hybridComponents <= 5 &&
                   hybridLayers.Length == 3,
                "unexpected components or JPEG2000 layer count");
            Assert(OglImageCodecEngine.AreLayerBoundariesValid(hybridLayers, codestream.Length),
                "hybrid layer offsets must be strictly in bounds");

            var hybridEngine = new OglImageCodecEngine(hybrid, new OglCsJ2KCodecProvider());
            Assert(hybridEngine.TryDecodeLayers(codestream, out var pipelineLayers,
                    out int pipelineComponents, out var hybridFailure),
                "hybrid pipeline should decode JPEG2000 layers: " + hybridFailure);
            Assert(pipelineComponents == hybridComponents &&
                   pipelineLayers.Length == hybridLayers.Length,
                "hybrid pipeline must preserve validated decoder metadata");

            byte[] corrupt = (byte[])codestream.Clone();
            Array.Clear(corrupt, 0, corrupt.Length);
            Assert(!hybrid.TryDecodeLayerBoundaries(corrupt, out var corruptLayers, out _)
                   && corruptLayers.Length == 0, "invalid JPEG2000 input must be rejected");

            byte[] truncated = new byte[Math.Min(24, codestream.Length)];
            Array.Copy(codestream, truncated, truncated.Length);
            Assert(!hybrid.TryDecodeLayerBoundaries(truncated, out _, out _),
                "incomplete codestream must not produce valid layers");
            Console.WriteLine($"OpenJPEG 2.x validated hybrid PASS: {hybridLayers.Length} layers, " +
                $"{hybridComponents} components, {assertions} assertions");
            return 0;
        }

        if (args.Length > 0)
        {
            // An actual codestream produced by opj_compress (J2C or JP2):
            // verifies the native provider rather than a fake implementation.
            byte[] fixture = File.ReadAllBytes(args[0]);
            var native = new OglOpenJpegCodecProvider();
            bool nativeSucceeded = false;
            try
            {
                nativeSucceeded = native.TryDecodeLayerBoundaries(fixture,
                    out var nativeLayers, out int nativeComponents)
                    && nativeLayers != null && nativeLayers.Length > 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("OpenJPEG native-provider diagnostic: " + ex);
            }

            var fallbackEngine = new OglImageCodecEngine(native, new OglCsJ2KCodecProvider());
            bool fallbackSucceeded = fallbackEngine.TryDecodeLayers(fixture, out var realLayers,
                out int realComponents, out var realFailure);
            Console.WriteLine($"Native OpenJPEG success={nativeSucceeded}, codec pipeline success={fallbackSucceeded}, " +
                $"layers={realLayers.Length}, components={realComponents}, failure={realFailure}");
            Assert(fallbackSucceeded && realLayers.Length > 0 &&
                OglImageCodecEngine.AreLayerBoundariesValid(realLayers, fixture.Length),
                "OpenJPEG/CSJ2K pipeline failed to decode generated J2C layers");
            Assert(nativeSucceeded,
                "legacy native OpenJPEG cannot parse valid generated JPEG2000 quality layers; needs replacement");
        }

        Console.WriteLine($"OGL Image Codec Engine regression PASS ({assertions} assertions)");
        return 0;
    }
}
