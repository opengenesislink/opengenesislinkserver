// SPDX-License-Identifier: MPL-2.0
using System;
using System.Collections.Generic;
using OpenMetaverse;
using OpenMetaverse.StructuredData;
using static OpenMetaverse.Primitive.RenderMaterials;

namespace OpenSim.Framework
{
    /// <summary>
    /// Atomic, side-bounded glTF material assignment shared by LSL and
    /// regression tests. Edits only the supplied shape (never the asset store).
    /// Keeping an empty RenderMaterials object signals removal to viewers.
    /// </summary>
    public static class OglRenderMaterialFaces
    {
        public static UUID Get(PrimitiveBaseShape shape, int face, int sides)
        {
            if (shape?.RenderMaterials?.entries is null || sides <= 0 ||
                (face != -1 && (face < 0 || face >= sides)))
                return UUID.Zero;

            int index = face == -1 ? 0 : face;
            foreach (RenderMaterialEntry entry in shape.RenderMaterials.entries)
            {
                if (entry.te_index == index)
                    return entry.id;
            }
            return UUID.Zero;
        }

        // Preserve only the four-slot texture-transform field (ti); all
        // color, metallic, emissive and alpha overrides are reset. Fail closed
        // on malformed previous data, without interpreting arbitrary objects.
        public static string KeepUvTransforms(string previous)
        {
            try
            {
                if (string.IsNullOrEmpty(previous) ||
                    OSDParser.DeserializeLLSDNotation(previous) is not OSDMap old ||
                    !old.TryGetValue("ti", out OSD ti) ||
                    ti is not OSDArray transforms || transforms.Count > 4)
                    return string.Empty;

                return OSDParser.SerializeLLSDNotation(new OSDMap
                {
                    ["ti"] = transforms
                });
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }

        public static bool Set(PrimitiveBaseShape shape, int face, int sides, UUID materialId)
        {
            if (shape is null || sides <= 0 || sides > 256 ||
                (face != -1 && (face < 0 || face >= sides)))
                return false;

            // Work on private copies first. The publicly visible shape is
            // replaced only after all targeted faces have been processed.
            var old = shape.RenderMaterials;
            var entries = new List<RenderMaterialEntry>();
            var overrides = new List<RenderMaterialOverrideEntry>();
            if (old?.entries is not null)
                entries.AddRange(old.entries);
            if (old?.overrides is not null)
                overrides.AddRange(old.overrides);

            bool changed = false;
            int first = face == -1 ? 0 : face;
            int last = face == -1 ? sides - 1 : face;
            for (int target = first; target <= last; ++target)
            {
                int pos = entries.FindIndex(x => x.te_index == target);
                if (materialId.IsZero())
                {
                    if (pos >= 0)
                    {
                        entries.RemoveAt(pos);
                        changed = true;
                    }
                }
                else if (pos >= 0)
                {
                    if (entries[pos].id.NotEqual(materialId))
                    {
                        entries[pos] = new RenderMaterialEntry
                        {
                            te_index = (byte)target,
                            id = materialId
                        };
                        changed = true;
                    }
                }
                else
                {
                    entries.Add(new RenderMaterialEntry
                    {
                        te_index = (byte)target,
                        id = materialId
                    });
                    changed = true;
                }

                // SL retains repeats, offsets and rotation while resetting
                // other per-face glTF overrides on material replacement.
                int existingOverride = overrides.FindIndex(x => x.te_index == target);
                if (existingOverride >= 0)
                {
                    string retainedTransforms = KeepUvTransforms(overrides[existingOverride].data);
                    if (retainedTransforms.Length == 0)
                        overrides.RemoveAt(existingOverride);
                    else
                    {
                        overrides[existingOverride] = new RenderMaterialOverrideEntry
                        {
                            te_index = (byte)target,
                            data = retainedTransforms
                        };
                    }
                    changed = true;
                }
            }

            if (!changed)
                return false;

            var next = new Primitive.RenderMaterials
            {
                entries = entries.Count == 0 ? null : entries.ToArray(),
                overrides = overrides.Count == 0 ? null : overrides.ToArray()
            };
            shape.RenderMaterials = next;
            return true;
        }
    }
}
