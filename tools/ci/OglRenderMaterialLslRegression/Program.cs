// SPDX-License-Identifier: MPL-2.0
using System;
using OpenMetaverse;
using OpenMetaverse.StructuredData;
using OpenSim.Framework;
using static OpenMetaverse.Primitive.RenderMaterials;

internal static class Program
{
    private static void Require(bool ok, string why)
    {
        if (!ok) throw new InvalidOperationException("Render material LSL regression: " + why);
    }

    private static void Main()
    {
        var shape = new PrimitiveBaseShape();
        UUID first = UUID.Random();
        UUID second = UUID.Random();
        Require(OglRenderMaterialFaces.Get(shape, 0, 6) == UUID.Zero,
            "empty face must return NULL_KEY");
        Require(!OglRenderMaterialFaces.Set(shape, 6, 6, first) &&
                !OglRenderMaterialFaces.Set(shape, -2, 6, first),
            "invalid faces must not mutate materials");
        Require(OglRenderMaterialFaces.Set(shape, -1, 6, first) &&
                shape.RenderMaterials.entries.Length == 6,
            "ALL_SIDES must assign all six faces");
        Require(OglRenderMaterialFaces.Get(shape, -1, 6) == first &&
                OglRenderMaterialFaces.Get(shape, 5, 6) == first,
            "SL ALL_SIDES getter reads face zero");
        Require(!OglRenderMaterialFaces.Set(shape, 3, 6, first),
            "unchanged material must not schedule an update");
        Require(OglRenderMaterialFaces.Set(shape, 3, 6, second) &&
                OglRenderMaterialFaces.Get(shape, 3, 6) == second &&
                OglRenderMaterialFaces.Get(shape, 4, 6) == first,
            "a single face must not mutate adjacent faces");

        var old = new OSDMap
        {
            ["bc"] = new OSDArray { OSD.FromReal(0.5) },
            ["ti"] = new OSDArray { new OSDMap { ["r"] = OSD.FromReal(0.6) } }
        };
        shape.RenderMaterials.overrides = new[]
        {
            new RenderMaterialOverrideEntry
            {
                te_index = 3,
                data = OSDParser.SerializeLLSDNotation(old)
            }
        };
        Require(OglRenderMaterialFaces.Set(shape, 3, 6, UUID.Zero) &&
                OglRenderMaterialFaces.Get(shape, 3, 6) == UUID.Zero,
            "NULL_KEY must remove assigned material");
        Require(shape.RenderMaterials.overrides is { Length: 1 },
            "UV transform must survive material replacement");
        var retained = OSDParser.DeserializeLLSDNotation(
            shape.RenderMaterials.overrides[0].data) as OSDMap;
        Require(retained != null && retained.ContainsKey("ti") &&
                !retained.ContainsKey("bc"),
            "base color override must be reset while preserving UV rotation");

        Require(!OglRenderMaterialFaces.Set(shape, 3, 0, first) &&
                !OglRenderMaterialFaces.Set(shape, 3, 257, first),
            "invalid side budgets must not mutate state");
        Console.WriteLine("OGL render material LSL face/UV regression: OK");
    }
}
