#!/usr/bin/env python3
"""R619 safety regression for owner-consented NPC direct IM archive."""
from pathlib import Path
root=Path(__file__).resolve().parents[2]
read=lambda name: (root/name).read_text(encoding="utf-8")
store=read("NexVerse/Server/Api/NexNpcChatStore.cs")
ingress=read("NexVerse/Server/Api/NexNpcChatIngress.cs")
api=read("NexVerse/Server/Api/NexNpcWorldApi.cs")
publisher=read("OpenSim/Region/OptionalModules/World/NPC/NexNpcChatPublisher.cs")
npc=read("OpenSim/Region/OptionalModules/World/NPC/NPCAvatar.cs")
module=read("OpenSim/Region/OptionalModules/World/NPC/NPCModule.cs")
connector=read("NexVerse/Server/Api/NexVerseWorldApiConnector.cs")
routes=read("NexVerse/Server/Api/NexVerseWorldApiHandlers.cs")
project=read("OpenSim/Region/OptionalModules/OpenSim.Region.OptionalModules.csproj")
for name,source,needles in (
 ("no pre-consent storage",store,("if (!Enabled(db, owner, tx)) return false;","UNIQUE(owner,event_id)","DELETE FROM npc_chat_messages","MaxPerOwner")),
 ("authenticated and source-verified IM ingest",ingress,("HMACSHA256","FixedTimeEquals","TryOwned(owner","m_Store.IsEnabled(owner)","m_Store.Append","8192")),
 ("owner-only API",api,("NexScopes.RelationshipsRead","NexScopes.RelationshipsWrite","owner != account.PrincipalID.Guid","owned.Any(n => n.npc_id == npcID)","acknowledge_private_owner_npc_im","m_Chat.DeleteHistory(owner)","complete=false")),
 ("private IM only",npc,("OnOwnerDirectMessage","im.fromAgentID == m_ownerID.Guid","target == m_ownerID","!im.fromGroup")),
 ("separate signing credential",publisher,("NexNpcChatPublisher","NexNPC","HMACSHA256","Uri.UriSchemeHttps")),
 ("disabled by default",module,('GetBoolean("NexNPCChatEnabled", false)',"OnOwnerDirectMessage += m_ChatPublisher.Send")),
 ("isolated World archive",connector,('config.Configs["NexNPCChat"]','GetBoolean("Enabled", false)','NEX_NPC_CHAT_SECRET','NexNpcChatIngress')),
 ("OpenAPI",routes,('"/api/v1/npcs/chat/settings"','"/api/v1/npcs/chat/history"')),
 ("simulator build",project,("World\\NPC\\NexNpcChatPublisher.cs",)),
):
    for marker in needles:
        assert marker in source, name+": "+marker
    print("NPC_CHAT_R619_PASS",name)
assert 'OnChatToNPC' not in module, "No public/local chat capture allowed"
assert 'NexCitizenImStore' not in ingress and 'NexCitizenImStore' not in api
print("NPC_CHAT_R619_SAFETY_PASS")
