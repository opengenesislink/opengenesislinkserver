#!/usr/bin/env python3
"""Fail-closed release contract for local OGL Profile Feed account deletion."""
from pathlib import Path

root = Path(__file__).resolve().parents[2]
read = lambda name: (root / name).read_text(encoding="utf-8")
account = read("OpenSim/Services/UserAccountService/UserAccountService.cs")
store = read("NexVerse/Server/Api/OglProfileFeedStore.cs")
api = read("NexVerse/Server/Api/OglProfileFeedApi.cs")
connector = read("NexVerse/Server/Api/NexVerseWorldApiConnector.cs")
docs = read("doc/OpenGenesisLINK/OGL_PROFILE_FEED_ROLLOUT.md")

assert "ValidateLocalFeedDeletionStore(" in account
assert "LockLocalAccountRow(" in account
assert "PurgeLocalProfileFeed(" in account
self_delete = account.split("public bool TryDeleteLocalAvatar(", 1)[1].split(
    "private bool ValidateLocalFeedDeletionStore(", 1)[0]
admin_delete = account.split("Administrative core avatar purge committed", 1)[0]
for needle in ("LockLocalAccountRow(db, tx, avatarId)",
               "PurgeLocalProfileFeed(db, tx, avatarId, mysql, feedEnabled)"):
    assert needle in self_delete, needle
    assert self_delete.index(needle) < self_delete.index("tx.Commit()"), needle
for needle in ("LockLocalAccountRow(connection, deletionTransaction, userId)",
               "PurgeLocalProfileFeed(connection, deletionTransaction, userId, mysql, feedEnabled)"):
    assert needle in admin_delete, needle

purge = account.split("private static void PurgeLocalProfileFeed(", 1)[1].split(
    "private string DeletionStorageSetting(", 1)[0]
for needle in (
    "information_schema.tables",
    "if (feedEnabled && feedTables != 4)",
    "if (feedTables != 0 && feedTables != 4)",
    "ogl_profile_feed_comments",
    "ogl_profile_feed_likes",
    "ogl_profile_feed_follows",
    "ogl_profile_feed_posts",
    "DELETE FROM",
):
    assert needle in purge, needle
assert purge.index("ogl_profile_feed_comments") < purge.rfind("DELETE FROM `ogl_profile_feed_posts`")
assert "same MySQL datastore" in account
assert "same MariaDB provider/connection" in connector or "SAME MariaDB provider/connection" in connector
assert 'GetBoolean("EnableProfileFeed", false)' in connector

assert "LockExistingResidents(" in store
assert "SELECT `PrincipalID` FROM `UserAccounts`" in store
assert "FOR UPDATE" in store
for method in ("CreatePost(", "AddComment(", "Like(", "Follow("):
    fragment = store.split("internal bool " + method, 1)[1].split("\n        }", 1)[0]
    assert "LockExistingResidents" in fragment, method
    assert "tx.Commit()" in fragment, method

for needle in ("ProfileFeedRetentionDays", "profileConnectionString"):
    assert needle in connector
for needle in ("PruneExpired()", "Cutoff", "created_at>=@cutoff"):
    assert needle in store
assert api.count("m_Store.PruneExpired();") == 2

for path in ("bin/Robust.HG.ini", "bin/Robust.HG.ini.example",
             "bin/Robust.ini.example"):
    cfg = read(path)
    assert "EnableProfileFeed = false" in cfg, path
    assert "ProfileFeedRetentionDays = 180" in cfg, path
assert "Rollback" in docs and "Migrations" in docs
print("OGL Profile Feed G1 deletion and retention rollout contracts: PASS")
