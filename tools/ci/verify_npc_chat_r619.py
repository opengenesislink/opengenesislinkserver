#!/usr/bin/env python3
"""R620 safety regression for existing-MariaDB-only owner NPC IM archive."""
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
 ("consent-gated InnoDB storage",store,("using MySql.Data.MySqlClient","ENGINE=InnoDB","INSERT IGNORE INTO npc_chat_messages","FROM npc_chat_consent WHERE owner=@owner AND enabled=1","ON DUPLICATE KEY UPDATE","UNIQUE KEY uq_npc_chat_owner_event","DELETE FROM npc_chat_messages","MaxPerOwner")),
 ("authenticated and source-verified IM ingest",ingress,("HMACSHA256","FixedTimeEquals","TryOwned(owner","m_Store.IsEnabled(owner)","m_Store.Append","8192")),
 ("owner-only API",api,("NexScopes.RelationshipsRead","NexScopes.RelationshipsWrite","owner != account.PrincipalID.Guid","owned.Any(n => n.npc_id == npcID)","acknowledge_private_owner_npc_im","m_Chat.DeleteHistory(owner)","complete=false")),
 ("private IM only",npc,("OnOwnerDirectMessage","im.fromAgentID == m_ownerID.Guid","target == m_ownerID","!im.fromGroup")),
 ("separate signing credential",publisher,("NexNpcChatPublisher","NexNPC","HMACSHA256","Uri.UriSchemeHttps")),
 ("disabled by default",module,('GetBoolean("NexNPCChatEnabled", false)',"OnOwnerDirectMessage += m_ChatPublisher.Send")),
 ("isolated World archive",connector,('config.Configs["NexNPCChat"]','GetBoolean("Enabled", false)','NEX_NPC_CHAT_SECRET','NexNpcChatIngress','databaseStorageProvider,','databaseConnectionString,')),
 ("OpenAPI",routes,('"/api/v1/npcs/chat/settings"','"/api/v1/npcs/chat/history"')),
 ("simulator build",project,("World\\NPC\\NexNpcChatPublisher.cs",)),
):
    for marker in needles:
        assert marker in source, name+": "+marker
    print("NPC_CHAT_R619_PASS",name)
assert 'Mono.Data.Sqlite' not in store and 'SqliteConnection' not in store
assert 'StoragePath' not in connector[connector.index('NexNpcChatStore npcChatStore'):connector.index('NexNpcWorldApi npcWorldApi')]
assert 'OnChatToNPC' not in module, "No public/local chat capture allowed"
assert 'NexCitizenImStore' not in ingress and 'NexCitizenImStore' not in api
print("NPC_CHAT_R620_MARIADB_SAFETY_PASS")
