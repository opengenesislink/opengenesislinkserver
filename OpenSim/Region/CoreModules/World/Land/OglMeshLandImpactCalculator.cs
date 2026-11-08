// SPDX-License-Identifier: BSD-3-Clause
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using OpenMetaverse;
using OpenMetaverse.StructuredData;
using OpenSim.Framework;
using OpenSim.Region.Framework.Scenes;

namespace OpenSim.Region.CoreModules.World.Land
{
    /// <summary>
    /// Optional, region-scoped mesh land-impact estimator. Reads the same
    /// compressed LOD lengths as the model upload cost calculator. Does not
    /// alter linkset part counts, script link numbers, or persisted assets.
    /// </summary>
    internal sealed class OglMeshLandImpactCalculator
    {
        private const int MaxCachedMeshAssets = 8192;
        private const int MaxMeshBytesToInspect = 64 * 1024 * 1024;
        private const int MaxConvexBytesToDecode = 128 * 1024;
        private const int MaxDecodedConvexBytes = 256 * 1024;
        private readonly Scene m_scene;
        private readonly Dictionary<UUID, MeshMetrics> m_cache = new();
        private readonly object m_cacheLock = new();

        internal OglMeshLandImpactCalculator(Scene scene)
        {
            m_scene = scene ?? throw new ArgumentNullException(nameof(scene));
        }

        internal int GetLandImpact(SceneObjectGroup group)
        {
            GetWeights(group, out bool containsMesh, out double streaming,
                out double physics, out double server);
            return OglMeshLandImpactMath.LandImpact(
                group?.PrimCount ?? 0, containsMesh, streaming, physics, server);
        }

        internal void GetWeights(SceneObjectGroup group, out bool containsMesh,
            out double streaming, out double physics, out double server)
        {
            containsMesh = false;
            streaming = physics = server = 0;
            if (group is null || group.IsDeleted || group.IsAttachment)
                return;

            SceneObjectPart[] parts = group.Parts;
            if (parts is null || parts.Length == 0)
                return;

            // The legacy primitive accounting remains unchanged when no mesh
            // is present in a linkset.
            foreach (SceneObjectPart part in parts)
            {
                if (IsMesh(part))
                {
                    containsMesh = true;
                    break;
                }
            }
            if (!containsMesh)
            {
                streaming = physics = server = parts.Length;
                return;
            }

            server = OglMeshLandImpactMath.ServerWeight(
                parts.Length, group.ScriptCount(), group.UsesPhysics);
            foreach (SceneObjectPart part in parts)
            {
                if (part is null || !IsMesh(part))
                {
                    // Classic prims in a mixed linkset retain conservative cost.
                    streaming += 1.0;
                    physics += 1.0;
                    continue;
                }

                MeshMetrics metrics = GetMeshMetrics(part.Shape.SculptTexture);
                if (!metrics.Valid)
                {
                    // Unknown, unavailable, oversize, or malformed mesh assets
                    // never gain an automatic land quota discount.
                    streaming += 1.0;
                    physics += 1.0;
                    continue;
                }

                double streamingWeight = OglMeshLandImpactMath.StreamingWeight(
                    metrics.High, metrics.Medium, metrics.Low, metrics.Lowest,
                    part.Scale.LengthSquared());
                streaming += double.IsFinite(streamingWeight)
                    ? Math.Max(0.01, streamingWeight) : 1.0;

                if (part.PhysicsShapeType == (byte)PhysShapeType.none)
                    continue;

                if (part.PhysicsShapeType == (byte)PhysShapeType.convex)
                {
                    // Same per-vertex convex weighting as the existing mesh
                    // uploader, with actual physics actor weight as a floor.
                    double hull = metrics.ConvexVertices > 0
                        ? 0.04 * (metrics.ConvexVertices + 1) : 1.0;
                    physics += Math.Max(hull, SafePhysicsCost(part));
                }
                else
                {
                    // Prim physics can use a detailed triangle collision
                    // shape. Never give it the low convex-hull discount.
                    physics += Math.Max(1.0, SafePhysicsCost(part));
                }
            }
        }

        internal static bool IsMesh(SceneObjectPart part)
        {
            return part?.Shape is not null &&
                part.Shape.SculptType == (byte)SculptType.Mesh &&
                part.Shape.SculptTexture.IsNotZero();
        }

        private static double SafePhysicsCost(SceneObjectPart part)
        {
            float cost = part.PhysicsCost;
            return float.IsFinite(cost) && cost >= 0 ? cost : 1.0;
        }

        private MeshMetrics GetMeshMetrics(UUID assetId)
        {
            if (assetId.IsZero())
                return default;

            lock (m_cacheLock)
            {
                if (m_cache.TryGetValue(assetId, out MeshMetrics cached))
                    return cached;
            }

            MeshMetrics metrics = default;
            try
            {
                AssetBase asset = m_scene.AssetService?.Get(assetId.ToString());
                if (asset?.Data is byte[] data &&
                    data.Length > 0 && data.Length <= MaxMeshBytesToInspect)
                    metrics = ParseMesh(data);
            }
            catch (Exception) // Unavailable asset or malformed LLSD: fail closed.
            {
                metrics = default;
            }

            lock (m_cacheLock)
            {
                if (m_cache.Count >= MaxCachedMeshAssets)
                    m_cache.Clear();
                m_cache[assetId] = metrics;
            }
            return metrics;
        }

        private static MeshMetrics ParseMesh(byte[] data)
        {
            using MemoryStream stream = new(data, writable: false);
            if (OSDParser.DeserializeLLSDBinary(stream) is not OSDMap root)
                return default;
            long payloadOffset = stream.Position;
            if (!TryReadSection(root, "high_lod", payloadOffset, data.Length,
                out int high, out _, out _))
                return default;

            int medium = ReadOptionalSection(root, "medium_lod", payloadOffset, data.Length);
            int low = ReadOptionalSection(root, "low_lod", payloadOffset, data.Length);
            int lowest = ReadOptionalSection(root, "lowest_lod", payloadOffset, data.Length);
            int vertices = ReadConvexVertices(root, payloadOffset, data);
            return new MeshMetrics(true, high, medium, low, lowest, vertices);
        }

        private static int ReadOptionalSection(OSDMap root, string name,
            long baseOffset, int dataLength)
        {
            return TryReadSection(root, name, baseOffset, dataLength,
                out int size, out _, out _) ? size : 0;
        }

        private static bool TryReadSection(OSDMap root, string name,
            long baseOffset, int dataLength, out int size,
            out int offset, out int absoluteOffset)
        {
            size = offset = absoluteOffset = 0;
            if (!root.TryGetValue(name, out OSD section) || section is not OSDMap map ||
                !map.TryGetValue("offset", out OSD offsetValue) ||
                !map.TryGetValue("size", out OSD sizeValue))
                return false;
            offset = offsetValue.AsInteger();
            size = sizeValue.AsInteger();
            long absolute = baseOffset + offset;
            if (offset < 0 || size <= 0 || absolute < 0 ||
                absolute + size > dataLength)
                return false;
            absoluteOffset = (int)absolute;
            return true;
        }

        private static int ReadConvexVertices(OSDMap root, long baseOffset, byte[] data)
        {
            if (!TryReadSection(root, "physics_convex", baseOffset, data.Length,
                out int size, out _, out int absoluteOffset) ||
                size > MaxConvexBytesToDecode)
                return 0;
            try
            {
                using MemoryStream compressed = new(data, absoluteOffset, size, writable: false);
                using ZLibStream zlib = new(compressed, CompressionMode.Decompress);
                using MemoryStream decompressed = new();
                byte[] buffer = new byte[4096];
                int read;
                while ((read = zlib.Read(buffer, 0, buffer.Length)) > 0)
                {
                    if (decompressed.Length + read > MaxDecodedConvexBytes)
                        return 0;
                    decompressed.Write(buffer, 0, read);
                }
                decompressed.Position = 0;
                if (OSDParser.DeserializeLLSDBinary(decompressed) is OSDMap convex &&
                    convex.TryGetValue("BoundingVerts", out OSD verts))
                {
                    byte[] dataVerts = verts.AsBinary();
                    if (dataVerts is not null && dataVerts.Length >= 6 &&
                        dataVerts.Length % 6 == 0)
                        return dataVerts.Length / 6;
                }
            }
            catch (Exception)
            {
                // Missing/invalid convex hull means a conservative physics
                // cost, not a truncated or unvalidated cheap collision shape.
            }
            return 0;
        }

        private readonly struct MeshMetrics
        {
            internal readonly bool Valid;
            internal readonly int High;
            internal readonly int Medium;
            internal readonly int Low;
            internal readonly int Lowest;
            internal readonly int ConvexVertices;

            internal MeshMetrics(bool valid, int high, int medium,
                int low, int lowest, int convexVertices)
            {
                Valid = valid;
                High = high;
                Medium = medium;
                Low = low;
                Lowest = lowest;
                ConvexVertices = convexVertices;
            }
        }
    }
}
