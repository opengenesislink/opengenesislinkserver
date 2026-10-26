#!/usr/bin/env python3
"""Guard OGL Profile Feed v1 privacy, routing, write ownership and viewer integration."""
from pathlib import Path
root=Path(__file__).resolve().parents[2]
api=(root/"NexVerse/Server/Api/OglProfileFeedApi.cs").read_text()
store=(root/"NexVerse/Server/Api/OglProfileFeedStore.cs").read_text()
connector=(root/"NexVerse/Server/Api/NexVerseWorldApiConnector.cs").read_text()
contract=(root/"NexVerse/Server/Api/NexVerseWorldApiHandlers.cs").read_text()
docs=(root/"doc/OpenGenesisLINK/OGL_PROFILE_FEED_V1.md").read_text()
for key in ("EnableProfileFeed", '"\/api\/v1\/feed"', '"\/feed"',
            "new OglProfileFeedStore", "apiGate.Wrap(feedApi.HandleApi)",
            "apiGate.Wrap(feedApi.HandleWeb)", "profileStorageProvider"):
    key=key.replace("\\/","/")
    assert key in connector, key
for key in ("profile.PublishProfile", "AllowRead(", "LocalToGrid",
            "actorAccount.PrincipalID!=actorId", "actorId!=ownerId",
            "NexScopes.ProfileWrite", "NexScopes.RelationshipsWrite",
            "m_Store.IsPostOwned(ownerId,postId)",
            "WebUtility.HtmlEncode(post.Text)", "WebUtility.HtmlEncode(comment.Text)",
            "Content-Security-Policy", "no-store", "TryText(request",
            "m_Store.DeleteComment", "m_Store.Like", "m_Store.Follow"):
    assert key in api, key
for key in ("FOREIGN KEY(post_id)", "ON DELETE CASCADE", "owner=@owner",
            "author=@actor", "created_at>=@minute", "CREATE TABLE IF NOT EXISTS"):
    assert key in store, key
for key in ('["/api/v1/feed/{principalId}"]',
            '["/api/v1/feed/{principalId}/{postId}/comments"]',
            '["/api/v1/feed/{principalId}/{postId}/likes"]'):
    assert key in contract,key
for name in ("Robust.HG.ini.example","Robust.ini.example"):
    cfg=(root/"bin"/name).read_text()
    assert "EnableProfileFeed = false" in cfg
    assert "; web_profile_url = https://world.stadt-nexverse.de/feed?name=[AGENT_NAME]" in cfg
assert "Keine automatische" in docs and "Datenschutz" in docs
print("OGL Profile Feed: opt-in, privacy, owner write, scoped interactions, viewer web route OK")
