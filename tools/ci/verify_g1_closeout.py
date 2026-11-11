#!/usr/bin/env python3
"""Read-only G1 closeout integration source contract.

This verifies that the four independently reviewed feature sets survived
the integration branch. Runtime, database and viewer checks remain separate.
"""
from pathlib import Path

root = Path(__file__).resolve().parents[2]

def source(path):
    return (root / path).read_text(encoding="utf-8-sig")

def has(path, *tokens):
    content = source(path)
    for token in tokens:
        assert token in content, f"{path}: missing {token}"

# Data integrity (two SQL dialects, PostgreSQL transaction and PBR)
for sql in ("MySQL", "PGSQL"):
    has(f"OpenSim/Data/{sql}/{sql}GroupsData.cs",
        'm_Membership.Get("PrincipalID", principalID)',
        'new[] { "GroupID", "PrincipalID" }')
has("OpenSim/Data/PGSQL/PGSQLEstateData.cs",
    "conn.BeginTransaction()", "transaction.Commit()")
has("OpenSim/Data/PGSQL/PGSQLUserProfilesData.cs",
    "ON CONFLICT (useruuid, targetuuid) DO UPDATE")
has("OpenSim/Region/OptionalModules/Materials/MaterialsModule.cs",
    "CanApplyMaterialOverride", "RequireFiniteColorFactor",
    "Too many PBR material changes in one request")

# LSL API, independent helper, YEngine scripting wrappers, built PBR tests
for method in ("llSetRenderMaterial", "llSetLinkRenderMaterial", "llGetRenderMaterial"):
    for path in (
        "OpenSim/Region/ScriptEngine/Shared/Api/Interface/ILSL_Api.cs",
        "OpenSim/Region/ScriptEngine/Shared/Api/Runtime/LSL_Stub.cs",
        "OpenSim/Region/ScriptEngine/Shared/Api/Implementation/LSL_Api.cs",
    ):
        has(path, method)
has("OpenSim/Framework/OglRenderMaterialFaces.cs",
    "public static bool Set(", "KeepUvTransforms(", "public static UUID Get(")
has("OpenSim/Region/ScriptEngine/Shared/Api/Implementation/LSL_Api.cs",
    "case ScriptBaseClass.PRIM_RENDER_MATERIAL:")

# Performance and safe legacy defaults
has("OpenSim/Region/CoreModules/World/Warp3DMap/Warp3DImageModule.cs",
    "lock (m_RenderLock)", "if (m_ForceFullGC)", "if (m_WriteMapDiagnosticPng)",
    "renderer.Scene.destroy();")
for path in ("bin/OpenSim.ini", "bin/OpenSim.ini.example", "bin/OpenSimDefaults.ini"):
    has(path, "Warp3DForceFullGC = true", "Warp3DWriteMapPng = true")

# Refactored NodeAgent collector and CSS, existing signed routing preserved
has("NexVerse/RegionModules/NodeAgent/NexVerseNodeAgentModule.cs",
    "OglNodePerformanceHeartbeat.Append(", '"node.heartbeat"')
has("NexVerse/RegionModules/NodeAgent/OglNodePerformanceHeartbeat.cs",
    "TakeSnapshotAndReset()", "IOglAssetCacheMetricsProvider")
has("NexVerse/Server/Api/NexApiDocsPage.cs",
    "NexApiDocsStyles.Content", "NexOglBranding.DataUri", "oglPerformanceDetails")
has("NexVerse/Server/Api/NexApiDocsStyles.cs",
    "OGL Control Center 5.0", "color-scheme:dark")

print("G1 integrated data / SL-PBR / Warp3D / NodeAgent / ROBUST CSS contract: OK")
