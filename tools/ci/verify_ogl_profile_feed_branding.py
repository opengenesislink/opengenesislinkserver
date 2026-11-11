#!/usr/bin/env python3
"""Protect Stadt NexVerse ownership of the profile feed presentation."""
from pathlib import Path

root = Path(__file__).resolve().parents[2]
api = (root / "NexVerse/Server/Api/OglProfileFeedApi.cs").read_text(encoding="utf-8")
web = (root / "NexVerse/Server/Api/OglProfileFeedWebApp.cs").read_text(encoding="utf-8")

# Both the server-first HTML fallback and the hydrated browser UI must be
# styled. Nothing may rely on external fonts, remote images or JS CDNs.
for needle in (
    "Stadt NexVerse · Profilfeed",
    "ogl-shell", "ogl-brandbar", "ogl-hero", "ogl-cover",
    "ogl-portrait", "ogl-stream-title", "ogl-post-header",
    "ogl-reactions", "ogl-footer",
    'Powered by <strong>OpenGenesisLINK</strong>',
    "WebUtility.HtmlEncode(account.Name)",
    "WebUtility.HtmlEncode(post.Text)",
    "WebUtility.HtmlEncode(comment.Text)",
    "OglProfileFeedWebApp.Styles()",
):
    assert needle in api, needle

# This string must never appear as a competing brand beside NexVerse.
assert "STADT NEXVERSE · OPEN GENESIS LINK" not in api
assert api.count("Powered by <strong>OpenGenesisLINK</strong>") == 1

for needle in (
    "Dein virtuelles Zuhause", "ogl-profile", "ogl-compose",
    "ogl-compose-heading", "ogl-stream-heading",
    "ogl-post-header", "ogl-post-avatar", "ogl-reactions",
    "ogl-comment-compose", ".ogl-follow-controls", "ogl-liked",
    "@media(max-width:560px)", "@media(prefers-reduced-motion:reduce)",
    "aria-live", "aria-label", "textContent",
):
    assert needle in web or needle in api, needle
for forbidden in (
    "fonts.googleapis.com", "cdn.jsdelivr.net", "unpkg.com",
    "innerHTML", "document.write", "localStorage",
):
    assert forbidden not in web and forbidden not in api, forbidden
assert "script-src 'nonce-" in api
assert "frame-ancestors 'none'" in api
print("OGL Feed Stadt NexVerse branding, modern responsive shell and CSP: PASS")
