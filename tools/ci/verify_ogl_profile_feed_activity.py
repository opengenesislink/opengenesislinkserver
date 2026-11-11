#!/usr/bin/env python3
"""Gate personal opt-in, authoritative discovery, durable dedup and deletion."""
from pathlib import Path
root=Path(__file__).resolve().parents[2]
read=lambda name:(root/name).read_text(encoding="utf-8")
store=read("NexVerse/Server/Api/OglProfileFeedStore.cs")
auto=read("NexVerse/Server/Api/OglProfileFeedAutoPublisher.cs")
discovery=read("NexVerse/Server/Api/NexDiscoveryApi.cs")
connector=read("NexVerse/Server/Api/NexVerseWorldApiConnector.cs")
account=read("OpenSim/Services/UserAccountService/UserAccountService.cs")
api=read("NexVerse/Server/Api/OglProfileFeedApi.cs")
web=read("NexVerse/Server/Api/OglProfileFeedWebApp.cs")
migration=read("doc/OpenGenesisLINK/migrations/20261111_ogl_profile_feed_activity.sql")
contract=read("NexVerse/Server/Api/NexVerseWorldApiHandlers.cs")
for text in (store,migration):
    for name in ("ogl_profile_feed_activity_settings",
                 "ogl_profile_feed_activity_receipts","auto_events",
                 "auto_classifieds","ON DELETE CASCADE"):
        # settings, receipts and cascade must be consistent in migrations
        assert name in text,name
assert "auto_events TINYINT(1) NOT NULL DEFAULT 0" in store
assert "auto_classifieds TINYINT(1) NOT NULL DEFAULT 0" in store
assert "GetActivitySettings(UUID owner)" in store
assert "SetActivitySettings(UUID owner, bool events, bool classifieds)" in store
assert "TryCreateActivityPost(" in store
assert "LockExistingResidents(db,tx,owner)" in store
assert "INSERT IGNORE INTO ogl_profile_feed_activity_receipts" in store
assert "marker.ExecuteNonQuery()!=1" in store
assert "tx.Commit();" in store

for needle in ("ev.Source != \"nexverse.world-api\"",
               "m_Discovery.GetEvent(id)", "m_Discovery.GetClassified(id)",
               "record.State != NexDiscoveryPublicationState.Approved",
               "account.LocalToGrid", "properties.PublishProfile",
               "TryCreateActivityPost(owner,kind,id,body)"):
    assert needle in auto, needle
for needle in ("PublishApprovedActivity(\"event\"", "PublishApprovedActivity(\"classified\"",
               "saved.State == NexDiscoveryPublicationState.Approved && !wasApproved",
               '["entity_id"] = entityId.ToString("D")'):
    assert needle in discovery,needle
assert "s_FeedActivityPublisher" in connector and "new OglProfileFeedAutoPublisher(" in connector
assert "teleportBaseUri,\n                        eventBus);" in connector
for needle in ("p[4] == \"activities\"", "NexScopes.ProfileWrite",
               "GetActivitySettings(resident)", "SetActivitySettings(resident",
               "TryActivitySettings("):
    assert needle in api,needle
for needle in ("ogl-auto-events", "ogl-auto-classifieds",
               "ogl-auto-settings", "saveActivitySettings()",
               "loadActivitySettings()", "'PUT'"):
    assert needle in web,needle
assert '["/api/v1/feed/me/activities"]' in contract
for needle in ("ogl_profile_feed_activity_receipts", "ogl_profile_feed_activity_settings",
               "Partial OGL Profile Feed activity schema"):
    assert needle in account,needle
print("OGL Feed automatic activity privacy, consent, dedup and deletion contract: PASS")
