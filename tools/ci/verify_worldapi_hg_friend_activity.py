#!/usr/bin/env python3
"""Fail-closed source contract for private World API friend activity.

Runnable C# model test covers parsing and elapsed-time edge cases.
Source checks tie it to the authenticated REST route and backend.
"""
from pathlib import Path
ROOT = Path(__file__).resolve().parents[2]
def read(path):
    return (ROOT / path).read_text(encoding="utf-8-sig")

api = read("NexVerse/Server/Api/NexSocialGraphApi.cs")
model = read("NexVerse/Server/Api/NexRelationshipPresenceModel.cs")
connector = read("NexVerse/Server/Api/NexVerseWorldApiConnector.cs")
docs = read("doc/NexVerse/PORTAL_SOCIAL_PROFILE.md")
tests = read("tools/ci/NexRelationshipPresenceRegression/Program.cs")
workflow = read(".github/workflows/nexjast-ci.yml")
assert '"GET", StringComparison.OrdinalIgnoreCase)' in api
assert "NexScopes.RelationshipsRead" in api
assert "if (!Owns(principal, owner))" in api
assert "m_Friends.GetFriends(owner)" in api
assert "NexRelationshipPresenceModel.TryParseIdentity(" in api
assert "m_GridUsers.GetGridUserInfo(" in api
assert "identity.IsHypergrid ? identity.ForeignName" in api
assert "identity.IsHypergrid" in api
assert "canSeeOnline = confirmed && (f.TheirFlags & 1) != 0" in api
assert "canSeeOnline && presenceKnown && !online" in api
assert "identity.IsHypergrid ? f.Friend : id.ToString()" in api
assert "TryGetOnline(UUID id, out bool online)" in api
assert 'last_online_scope = activity.LastOnlineAtUtc == null' in api
for field in (
    "is_hypergrid", "home_grid", "online_status", "online_scope",
    "last_online_at_utc", "last_online_minutes", "last_online_hours",
    "last_online_label", "last_online_scope",
):
    assert field in api and field in docs, field
assert '"local_grid"' in api and '"unknown"' in api
assert '"local_grid_logout"' in api
assert "Cache-Control" in api and "no-store" in api
assert "response.RawBuffer = Encoding.UTF8.GetBytes" in api
assert "m_Accounts.GetUserAccount(UUID.Zero, id)" in api
assert "new NexSocialGraphApi(userAccounts, friendsService, authenticator," in connector
assert "auditSink, presence, gridUsers)" in connector
assert "LoadOptionalService<IGridUserService>(config, \"GridUserService\")" in connector
assert "value.Split(';', 4)" in model
assert "Guid.TryParseExact" in model
assert "home.UserInfo" in model
assert "TrimEnd('/')" in model
assert "parts[2].Trim()" in model
assert "parts[3]" not in model, "HG federation secrets must never be read"
assert "LastLogout(" in model
assert "DateTimeOffset.FromUnixTimeSeconds(0)" in model
assert "if (!authorized || online" in model
assert "for (int minutes = 0; minutes < 61 * 24; minutes++)" in tests
assert "HG federation secret" in tests
assert "Regression-test World API Hypergrid friends and last online" in workflow
assert "friends.Select(Project)" not in api, "unparsed HG UUI entries"
print("World API HG friends and private last-online source contracts PASS")
