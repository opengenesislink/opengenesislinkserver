/* SPDX-License-Identifier: MPL-2.0
 * OpenGenesisLINK Generation 1.1 native OpenJPEG2 + managed layer offsets.
 */
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using CSJ2K;
using OpenMetaverse.Imaging;

namespace OpenSim.Region.CoreModules.Agent.TextureSender
{
    /// <summary>
    /// OpenJPEG 2.x validates and fully decodes an LRCP codestream in native
    /// code; CSJ2K obtains progressive packet offsets only after that check.
    /// The OpenJPEG 2.x public decoder index does not supply packet positions,
    /// so neither the shim nor this adapter invents such information.
    /// Unsupported platforms or absent native dependencies return false and
    /// leave fallback policy to OglImageCodecEngine.
    /// </summary>
    public sealed class OglOpenJpeg2CodecProvider : IOglImageCodecProvider
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct NativeInfo
        {
            public int Width;
            public int Height;
            public int Components;
            public int Layers;
            public int Progression;
        }

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int NativeInspect(
            [In] byte[] codestream, int length, out NativeInfo metadata);

        private static readonly Lazy<NativeInspect> Native = new Lazy<NativeInspect>(LoadNative, true);

        public string Name => "OpenJPEG2+CSJ2K";

        public bool NativeAvailable => Native.Value != null;

        private static NativeInspect LoadNative()
        {
            string filename;
            if (OperatingSystem.IsLinux() && RuntimeInformation.ProcessArchitecture == Architecture.X64)
                filename = "libogl-openjpeg2-x86_64.so";
            else if (OperatingSystem.IsLinux() && RuntimeInformation.ProcessArchitecture == Architecture.Arm64)
                filename = "libogl-openjpeg2-arm64.so";
            else
                return null;

            string overrideDir = Environment.GetEnvironmentVariable("OGL_IMAGE_CODEC_NATIVE_DIR");
            string[] directories = {
                !string.IsNullOrWhiteSpace(overrideDir) && Path.IsPathFullyQualified(overrideDir)
                    ? overrideDir : null,
                Path.Combine(AppContext.BaseDirectory, "lib64")
            };

            foreach (string directory in directories)
            {
                if (directory == null)
                    continue;
                string path = Path.Combine(directory, filename);
                try
                {
                    if (!File.Exists(path) || !NativeLibrary.TryLoad(path, out IntPtr library))
                        continue;
                    if (NativeLibrary.TryGetExport(library, "ogl_openjpeg2_inspect", out IntPtr symbol))
                        return Marshal.GetDelegateForFunctionPointer<NativeInspect>(symbol);
                    NativeLibrary.Free(library);
                }
                catch (Exception)
                {
                    // A missing or invalid optional native package is not
                    // fatal to the existing G1 or CSJ2K compatibility path.
                }
            }
            return null;
        }

        private bool TryInspect(byte[] data, out NativeInfo metadata)
        {
            metadata = default;
            if (!NativeAvailable || data == null || data.Length == 0 ||
                data.Length > OglImageCodecEngine.DefaultMaxAssetBytes)
                return false;
            return Native.Value(data, data.Length, out metadata) == 1 &&
                metadata.Components >= 1 && metadata.Components <= 5 &&
                metadata.Layers >= 1 && metadata.Layers <= 1024 &&
                metadata.Width > 0 && metadata.Height > 0 &&
                metadata.Progression == 0; // OPJ_LRCP
        }

        public bool TryDecodeImage(byte[] data, out Image image)
        {
            image = null;
            if (!TryInspect(data, out _))
                return false;
            // Pixel presentation stays managed until the native pixel-ABI
            // and color/alpha handling have their own cross-platform tests.
            image = J2kImage.FromBytes(data);
            return image != null;
        }

        public bool TryDecodeLayerBoundaries(byte[] data,
            out OpenJPEG.J2KLayerInfo[] layers, out int components)
        {
            layers = Array.Empty<OpenJPEG.J2KLayerInfo>();
            components = 0;
            if (!TryInspect(data, out NativeInfo metadata))
                return false;

            List<int> starts;
            using (MemoryStream stream = new MemoryStream(data, false))
                starts = J2kImage.GetLayerBoundaries(stream);
            if (starts == null || starts.Count != metadata.Layers)
                return false;

            OpenJPEG.J2KLayerInfo[] candidate =
                new OpenJPEG.J2KLayerInfo[metadata.Layers];
            for (int i = 0; i < starts.Count; ++i)
            {
                candidate[i].Start = i == 0 ? 0 : starts[i];
                candidate[i].End = i == starts.Count - 1
                    ? data.Length : starts[i + 1] - 1;
            }
            if (!OglImageCodecEngine.AreLayerBoundariesValid(candidate, data.Length))
                return false;

            layers = candidate;
            components = metadata.Components;
            return true;
        }
    }
}
