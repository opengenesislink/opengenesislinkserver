// SPDX-License-Identifier: MPL-2.0
using System;
using System.IO;
using NexVerse.Server.Api;

string dir=Path.Combine(Path.GetTempPath(),"npc-chat-r619-"+Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(dir);
try
{
    string db=Path.Combine(dir,"chat.sqlite");
    Guid owner=Guid.NewGuid(),otherOwner=Guid.NewGuid();
    Guid npc=Guid.NewGuid(),otherNpc=Guid.NewGuid();
    NexNpcChatStore store=new NexNpcChatStore(db,30);
    Check(!store.IsEnabled(owner),"owner has no implicit consent");
    Check(!store.Append(Guid.NewGuid(),owner,npc,"npc","pre-consent"),
        "no IM recording before opt-in");
    Check(store.Read(owner,npc,0,100).Messages.Count==0,"no pre-consent records");
    store.SetEnabled(owner,true);
    Check(store.IsEnabled(owner),"explicit consent saved");
    Guid first=Guid.NewGuid();
    Check(store.Append(first,owner,npc,"npc","NPC response"),"NPC message written");
    Check(store.Append(first,owner,npc,"npc","duplicate"),"replay is idempotently accepted");
    Check(store.Read(owner,npc,0,100).Messages.Count==1,"replay is not duplicated");
    Check(store.Append(Guid.NewGuid(),owner,npc,"owner","owner reply"),"owner reply");
    Check(!store.Append(Guid.NewGuid(),otherOwner,npc,"npc","private"),
        "other owner cannot record without opt-in");
    Check(!store.Append(Guid.NewGuid(),owner,npc,"visitor","third party"),
        "other senders never accepted");
    Check(store.Append(Guid.NewGuid(),owner,otherNpc,"npc","other npc"),
        "different NPC belongs to same owner in ingest contract");
    Check(store.Read(owner,npc,0,100).Messages.Count==2,"owner sees own NPC only");
    Check(store.Read(otherOwner,npc,0,100).Messages.Count==0,"other owner invisible");
    var page=store.Read(owner,npc,0,1);
    Check(page.Messages.Count==1 && page.NextCursor.Length>0,"cursor page");
    var end=store.Read(owner,npc,long.Parse(page.NextCursor),1);
    Check(end.Messages.Count==1 && end.NextCursor=="","complete final page");
    NexNpcChatStore reopened=new NexNpcChatStore(db,30);
    Check(reopened.IsEnabled(owner) && reopened.Read(owner,npc,0,100).Messages.Count==2,
        "durable consent and history");
    reopened.DeleteHistory(owner);
    Check(reopened.IsEnabled(owner) && reopened.Read(owner,npc,0,100).Messages.Count==0,
        "delete history preserves consent");
    reopened.Append(Guid.NewGuid(),owner,npc,"npc","to erase");
    reopened.SetEnabled(owner,false);
    Check(!reopened.IsEnabled(owner) && reopened.Read(owner,npc,0,100).Messages.Count==0,
        "withdrawal deletes all owner's records");
    Console.WriteLine("NPC_CHAT_R619_SQLITE_PASS");
}
finally{try{Directory.Delete(dir,true);}catch(IOException){}}
static void Check(bool assertion,string message)
{
    if(!assertion)throw new InvalidOperationException("NPC_CHAT_TEST_FAILED: "+message);
}
