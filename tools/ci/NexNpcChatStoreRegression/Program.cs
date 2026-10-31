// SPDX-License-Identifier: MPL-2.0
// Real MariaDB regression; uses the same database service as OpenGenesisLINK.
using System;
using NexVerse.Server.Api;

string connection = Environment.GetEnvironmentVariable("NEX_NPC_CHAT_MARIADB");
if (string.IsNullOrWhiteSpace(connection))
    throw new InvalidOperationException("NEX_NPC_CHAT_MARIADB is required (real SQL test).");
Guid owner = Guid.NewGuid(), other = Guid.NewGuid();
Guid npc = Guid.NewGuid(), otherNpc = Guid.NewGuid();
NexNpcChatStore store = new NexNpcChatStore("OpenSim.Data.MySQL.dll", connection, 30);
try
{
    Check(!store.IsEnabled(owner) && !store.IsEnabled(other), "no implicit consent");
    Check(!store.Append(Guid.NewGuid(), owner, npc, "npc", "before opt-in"),
        "cannot record before opt-in");
    Check(store.Read(owner,npc,0,100).Messages.Count==0,"pre-consent archive empty");

    store.SetEnabled(owner,true);
    Check(store.IsEnabled(owner), "opt-in stored");
    Guid id = Guid.NewGuid();
    Check(store.Append(id,owner,npc,"npc","hello"), "NPC IM inserted");
    Check(store.Append(id,owner,npc,"npc","replay"), "idempotent replay acknowledged");
    Check(store.Read(owner,npc,0,100).Messages.Count==1,"replay not duplicated");
    Check(store.Append(Guid.NewGuid(),owner,npc,"owner","reply"),"owner reply stored");
    Check(!store.Append(Guid.NewGuid(),other,npc,"npc","wrong owner"),
        "unconsented owner cannot write");
    Check(!store.Append(Guid.NewGuid(),owner,npc,"visitor","third party"),
        "third party blocked");
    Check(store.Append(Guid.NewGuid(),owner,otherNpc,"npc","other NPC"),
        "different NPC separate");
    Check(store.Read(owner,npc,0,100).Messages.Count==2,"NPC filter");
    Check(store.Read(other,npc,0,100).Messages.Count==0,"cross-owner isolation");

    var first=store.Read(owner,npc,0,1);
    Check(first.Messages.Count==1 && first.NextCursor!="","pagination first page");
    var second=store.Read(owner,npc,long.Parse(first.NextCursor),1);
    Check(second.Messages.Count==1 && second.NextCursor=="","pagination final page");

    var restarted = new NexNpcChatStore("OpenSim.Data.MySQL.dll", connection, 30);
    Check(restarted.IsEnabled(owner) &&
        restarted.Read(owner,npc,0,100).Messages.Count==2,"durability");
    restarted.DeleteHistory(owner);
    Check(restarted.IsEnabled(owner) &&
        restarted.Read(owner,npc,0,100).Messages.Count==0,"delete preserves consent");
    restarted.Append(Guid.NewGuid(),owner,npc,"npc","to delete");
    restarted.SetEnabled(owner,false);
    Check(!restarted.IsEnabled(owner) &&
        restarted.Read(owner,npc,0,100).Messages.Count==0,"opt-out purges messages");
    Console.WriteLine("NPC_CHAT_R620_MARIADB_PASS");
}
finally
{
    store.SetEnabled(owner,false);
    store.SetEnabled(other,false);
}
static void Check(bool good,string message)
{
    if(!good)throw new InvalidOperationException("NPC_CHAT_MARIADB_FAILED: "+message);
}
