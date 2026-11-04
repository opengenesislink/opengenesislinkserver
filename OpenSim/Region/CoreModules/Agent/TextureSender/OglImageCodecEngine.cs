/* SPDX-License-Identifier: MPL-2.0
 * OpenGenesisLINK Generation 1.1 JPEG2000 codec-provider foundation.
 * Keeps existing JPEG2000 assets and IJ2KDecoder wire contracts unchanged.
 */
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using OpenMetaverse.Imaging;
using CSJ2K;

namespace OpenSim.Region.CoreModules.Agent.TextureSender
{
    /// <summary>A bounded, provider-independent JPEG2000 decode contract.</summary>
    public interface IOglImageCodecProvider
    {
        string Name { get; }
        bool TryDecodeImage(byte[] data, out Image image);
        bool TryDecodeLayerBoundaries(byte[] data, out OpenJPEG.J2KLayerInfo[] layers, out int components);
    }

    public enum OglImageDecodeFailure
    {
        None,
        EmptyInput,
        InputTooLarge,
        DecoderFailed,
        InvalidLayers
    }

    /// <summary>
    /// Guarded OpenJPEG-first pipeline, with a configurable, compatible fallback.
    /// Decode failures are never reported as valid layers and never mutate assets.
    /// </summary>
    public sealed class OglImageCodecEngine
    {
        public const int DefaultMaxAssetBytes = 32 * 1024 * 1024;

        private readonly IOglImageCodecProvider m_primary;
        private readonly IOglImageCodecProvider m_fallback;
        private readonly int m_maxAssetBytes;

        public OglImageCodecEngine(IOglImageCodecProvider primary, IOglImageCodecProvider fallback,
            int maxAssetBytes = DefaultMaxAssetBytes)
        {
            m_primary = primary ?? throw new ArgumentNullException(nameof(primary));
            m_fallback = fallback;
            if (maxAssetBytes <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxAssetBytes));
            m_maxAssetBytes = maxAssetBytes;
        }

        private OglImageDecodeFailure ValidateInput(byte[] data)
        {
            if (data == null || data.Length == 0)
                return OglImageDecodeFailure.EmptyInput;
            if (data.Length > m_maxAssetBytes)
                return OglImageDecodeFailure.InputTooLarge;
            return OglImageDecodeFailure.None;
        }

        public bool TryDecodeImage(byte[] data, out Image image, out OglImageDecodeFailure failure)
        {
            image = null;
            failure = ValidateInput(data);
            if (failure != OglImageDecodeFailure.None)
                return false;

            if (TryProviderImage(m_primary, data, out image))
                return true;
            if (m_fallback != null && TryProviderImage(m_fallback, data, out image))
                return true;
            failure = OglImageDecodeFailure.DecoderFailed;
            return false;
        }

        private static bool TryProviderImage(IOglImageCodecProvider provider, byte[] data, out Image image)
        {
            image = null;
            try
            {
                if (provider.TryDecodeImage(data, out image) && image != null)
                    return true;
                image?.Dispose();
                image = null;
                return false;
            }
            catch (Exception)
            {
                image?.Dispose();
                image = null;
                return false;
            }
        }

        public bool TryDecodeLayers(byte[] data, out OpenJPEG.J2KLayerInfo[] layers,
            out int components, out OglImageDecodeFailure failure)
        {
            layers = Array.Empty<OpenJPEG.J2KLayerInfo>();
            components = 0;
            failure = ValidateInput(data);
            if (failure != OglImageDecodeFailure.None)
                return false;

            bool invalid = false;
            if (TryProviderLayers(m_primary, data, out layers, out components, ref invalid))
                return true;
            if (m_fallback != null && TryProviderLayers(m_fallback, data, out layers, out components, ref invalid))
                return true;

            layers = Array.Empty<OpenJPEG.J2KLayerInfo>();
            components = 0;
            failure = invalid ? OglImageDecodeFailure.InvalidLayers : OglImageDecodeFailure.DecoderFailed;
            return false;
        }

        private static bool TryProviderLayers(IOglImageCodecProvider provider, byte[] data,
            out OpenJPEG.J2KLayerInfo[] layers, out int components, ref bool invalid)
        {
            layers = Array.Empty<OpenJPEG.J2KLayerInfo>();
            components = 0;
            try
            {
                if (!provider.TryDecodeLayerBoundaries(data, out OpenJPEG.J2KLayerInfo[] candidate, out int count))
                    return false;
                if (count < 0 || !AreLayerBoundariesValid(candidate, data.Length))
                {
                    invalid = true;
                    return false;
                }
                layers = candidate;
                components = count;
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>Defensive bounds only; preserves decoder-supplied quality-layer structure.</summary>
        public static bool AreLayerBoundariesValid(OpenJPEG.J2KLayerInfo[] layers, int length)
        {
            if (length <= 0 || layers == null || layers.Length == 0 || layers.Length > 1024)
                return false;
            int previousStart = -1;
            foreach (OpenJPEG.J2KLayerInfo layer in layers)
            {
                if (layer.Start < 0 || layer.End < layer.Start ||
                    layer.End > length || layer.Start < previousStart)
                    return false;
                previousStart = layer.Start;
            }
            return true;
        }
    }

    /// <summary>OpenJPEG adapter; the asset format remains J2C/JP2, not HTJ2K.</summary>
    public sealed class OglOpenJpegCodecProvider : IOglImageCodecProvider
    {
        public string Name => "OpenJPEG";
        public bool TryDecodeImage(byte[] data, out Image image)
        {
            return OpenJPEG.DecodeToImage(data, out ManagedImage unused, out image);
        }
        public bool TryDecodeLayerBoundaries(byte[] data, out OpenJPEG.J2KLayerInfo[] layers, out int components)
        {
            return OpenJPEG.DecodeLayerBoundaries(data, out layers, out components);
        }
    }

    /// <summary>Legacy compatibility adapter for historical assets rejected by OpenJPEG.</summary>
    public sealed class OglCsJ2KCodecProvider : IOglImageCodecProvider
    {
        public string Name => "CSJ2K";
        public bool TryDecodeImage(byte[] data, out Image image)
        {
            image = J2kImage.FromBytes(data);
            return image != null;
        }
        public bool TryDecodeLayerBoundaries(byte[] data, out OpenJPEG.J2KLayerInfo[] layers, out int components)
        {
            layers = Array.Empty<OpenJPEG.J2KLayerInfo>();
            components = 0;
            List<int> boundaries;
            using (MemoryStream stream = new MemoryStream(data, false))
                boundaries = J2kImage.GetLayerBoundaries(stream);
            if (boundaries == null || boundaries.Count == 0)
                return false;
            layers = new OpenJPEG.J2KLayerInfo[boundaries.Count];
            for (int i = 0; i < boundaries.Count; i++)
                layers[i] = new OpenJPEG.J2KLayerInfo
                {
                    Start = i == 0 ? 0 : boundaries[i],
                    End = i == boundaries.Count - 1 ? data.Length : boundaries[i + 1] - 1
                };
            return true;
        }
    }
}
