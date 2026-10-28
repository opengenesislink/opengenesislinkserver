#!/usr/bin/env python3
"""Static contract for opt-in, authenticated cross-estate llGridSay."""
from pathlib import Path
root = Path(__file__).resolve().parents[2]
def read(path):
    return (root / path).read_text(encoding="utf-8-sig")

api = read("OpenSim/Region/ScriptEngine/Shared/Api/Interface/ILSL_Api.cs")
stub = read("OpenSim/Region/ScriptEngine/Shared/Api/Runtime/LSL_Stub.cs")
lsl = read("OpenSim/Region/ScriptEngine/Shared/Api/Implementation/LSL_Api.cs")
router = read("OpenSim/Region/Framework/Interfaces/IGridScriptMessageRouter.cs")
node = read("NexVerse/RegionModules/NodeAgent/NexVerseNodeAgentModule.cs")
workflow = read(".github/workflows/nexjast-ci.yml")
demo = read("tools/diagnostics/ogl_grid_beep_cubes.lsl")
proj = read("OpenSim/Region/Framework/OpenSim.Region.Framework.csproj")

assert "void llGridSay(int channelID, string text);" in api
assert "public void llGridSay(int channelID, string text)" in stub
assert "m_LSL_Functions.llGridSay(channelID, text);" in stub
assert "public void llGridSay(int channelID, string text)" in lsl
assert 'channelID == 0 || channelID == ScriptBaseClass.DEBUG_CHANNEL' in lsl
assert 'World.RequestModuleInterface<IGridScriptMessageRouter>()' in lsl
assert "TryBroadcastGridMessage(" in router
assert "TryBroadcastGridMessage(" in node
assert 'GridScriptMessaging", false' in node, "must default OFF"
assert "m_GridScriptMessagingEnabled" in node
assert "scene.RegisterModuleInterface<IGridScriptMessageRouter>(this);" in node
assert "scene.UnregisterModuleInterface<IGridScriptMessageRouter>(this);" in node
assert "m_GridScriptMessageSubscription?.Dispose();" in node
assert '"grid.script.message.requested"' in node
assert "HandleGridScriptMessage" in node

sender = node.split("public bool TryBroadcastGridMessage(", 1)[1].split("private void HandleGridScriptMessage(", 1)[0]
receiver = node.split("private void HandleGridScriptMessage(", 1)[1].split("private void HandleEstateScriptMessage(", 1)[0]
for term in (
    "sourcePart.OwnerID != sourceOwnerId",
    "sourcePart.IsDeleted",
    "sourceEstate.IsEstateManagerOrOwner(sourceOwnerId)",
    "TryTakeObjectMessageRateSlot(m_ObjectMessageOutboundRates",
    "channel == 0",
    "channel == DebugChannel",
    "m_Scenes.Values",
    "IWorldComm",
    "ChatTypeEnum.Region",
    'Publish(new NexEvent(',
):
    assert term in sender, "missing send authorization: "+term

for term in (
    "nexEvent.Timestamp",
    "m_ObjectMessageMaxAgeSeconds",
    'string.Equals(nexEvent.Source, "nexverse.simulator"',
    "TryTakeObjectMessageRateSlot(m_ObjectMessageInboundRates",
    '"source_node_id"',
    '"source_region_id"',
    '"source_object_id"',
    '"source_owner_id"',
    "m_Scenes.Values",
    "IWorldComm",
    "ChatTypeEnum.Region",
):
    assert term in receiver, "missing remote guard: "+term

assert 'Interfaces\\IGridScriptMessageRouter.cs' in proj
assert "llGridSay(CHANNEL, SIGNAL);" in demo
assert "llListen(CHANNEL" in demo
assert "llSetTimerEvent(FLASH_SECONDS)" in demo
for path, state in (
    ("bin/OpenSimDefaults.ini", "false"),
    ("bin/OpenSim.ini", "true"),
    ("bin/OpenSim.ini.example", "true"),
):
    config = read(path)
    assert "GridScriptMessaging = " + state in config, path

assert "Verify grid-wide cross-estate LSL messaging" in workflow
print("Generation 1 llGridSay: API, local broadcast, signed NexBus, permissions, rate limit and opt-in config OK")
