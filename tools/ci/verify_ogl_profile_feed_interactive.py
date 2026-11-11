#!/usr/bin/env python3
"""Non-bypassable OAuth/PKCE and privacy release contracts for interactive feed."""
from pathlib import Path
import os
import subprocess
import tempfile

root = Path(__file__).resolve().parents[2]
read = lambda file: (root / file).read_text(encoding="utf-8")
api = read("NexVerse/Server/Api/OglProfileFeedApi.cs")
store = read("NexVerse/Server/Api/OglProfileFeedStore.cs")
web = read("NexVerse/Server/Api/OglProfileFeedWebApp.cs")
connector = read("NexVerse/Server/Api/NexVerseWorldApiConnector.cs")
contract = read("NexVerse/Server/Api/NexVerseWorldApiHandlers.cs")
docs = read("doc/OpenGenesisLINK/OGL_PROFILE_FEED_INTERACTIVE_V2.md")

# Authenticated identity and owner-bound write paths.
for needle in ("p[3]==\"me\"", "p[3]==\"timeline\"",
               "NexScopes.ProfileRead", "NexScopes.RelationshipsRead",
               "viewerAccount.PrincipalID!=me", "ListFollowingPosts(",
               "!properties.PublishProfile", "visibleIds.Add(",
               "ListLikedFollowingPostIds(",
               "OglProfileFeedWebApp.Controls()", "OglProfileFeedWebApp.Script(",
               "OglProfileFeedWebApp.CallbackPage("):
    assert needle in api, needle
assert api.index("if(p.Length==4 && (p[3]==\"me\" || p[3]==\"timeline\"))") < api.index("!UUID.TryParse(p[3]"), "route ordering"
for needle in ("ListFollowingPosts(", "IsFollowing(", "ListLikedPostIds(",
               "ListLikedFollowingPostIds(", "p.created_at>=@cutoff",
               "f.follower=@follower"):
    assert needle in store, needle
assert "profile.PublishProfile ? m_Store.ListPosts(" in api, "SSR private-profile leak"
for needle in ('"ProfileFeedOAuthClientId", string.Empty',
               "publicBaseUrl.TrimEnd('/') + \"/feed\""):
    assert needle in connector, needle
assert "X-Frame-Options" in api
assert "script-src 'nonce-" in api and "connect-src 'self'" in api
assert "RandomNumberGenerator.GetBytes(16)" in api
assert "No OAuth" not in web
for needle in ("code_challenge_method: 'S256'", "crypto.getRandomValues",
               "crypto.subtle.digest('SHA-256'", "sessionStorage.setItem(pendingKey",
               "params.get('state') !== pending.state", "history.replaceState",
               "grant_type: 'authorization_code'", "credentials: 'omit'",
               "sessionStorage.removeItem(tokenKey)", "textContent",
               "/oauth/authorize", "/oauth/token", "/oauth/revoke"):
    assert needle in web, needle
for forbidden in ("localStorage", "document.cookie", "innerHTML", "client_secret"):
    assert forbidden not in web, forbidden
assert "profile:read profile:write relationships:read relationships:write" in web
oauth_store = read("NexVerse/Core/Security/NexOAuthStore.cs")
assert "EnsureProfileFeedPublicClient(" in oauth_store
assert "ClientType = NexOAuthClientTypes.Public" in oauth_store
assert "existing.Enabled" in oauth_store
assert "ProfileFeedOAuthClientId" in docs
assert "EnableNativeTokens = true" in docs
assert "EnsureProfileFeedPublicClient(" in connector
for path in ("bin/Robust.HG.ini", "bin/Robust.HG.ini.example", "bin/Robust.ini.example"):
    config = read(path)
    assert "ProfileFeedOAuthClientId = \"\"" in config, path
    assert "EnableProfileFeed = true" in config, path
for needle in ('["/api/v1/feed/me"]', '["/api/v1/feed/timeline"]'):
    assert needle in contract

# Parse the JavaScript sent to the actual viewer rather than accepting a
# merely textual C# signature. GitHub Actions ubuntu-latest provides node.
script = web.split('private const string ScriptBody = """', 1)[1].rsplit('""";', 1)[0]
with tempfile.TemporaryDirectory() as directory:
    source = Path(directory) / "feed-webapp.js"
    source.write_text(script, encoding="utf-8")
    subprocess.run(["node", "--check", os.fspath(source)], check=True)
print("Interactive OGL Feed v2 OAuth, timeline, privacy and browser JS: PASS")
