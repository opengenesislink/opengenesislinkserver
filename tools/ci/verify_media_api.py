#!/usr/bin/env python3
"""Fail CI on OpenGenesisLINK Media API routing/privacy regressions."""
from pathlib import Path

media = Path("NexVerse/Server/Api/NexMediaApi.cs").read_text()
connector = Path("NexVerse/Server/Api/NexVerseWorldApiConnector.cs").read_text()
groups = Path("NexVerse/Server/Api/NexGroupsApi.cs").read_text()
profile = Path("NexVerse/Server/Api/NexWebProfileV3Api.cs").read_text()
openapi = Path("NexVerse/Server/Api/NexVerseWorldApiHandlers.cs").read_text()
tests = Path("tools/ci/NexGroupsHttpRegression/Program.cs").read_text()
doc = Path("doc/NexVerse/WORLD_API_MEDIA_1_0.md").read_text()

for path in (
    "/api/v1/media/groups/{groupId}/image",
    "/api/v1/media/profiles/{principalId}/image",
    "/api/v1/media/profiles/{principalId}/first-life",
):
    assert path in openapi and path in doc, f"media route missing: {path}"

for marker in (
    'TryParseMediaPath(parts, out bool group',
    'TryParseSize(request.QueryString?["size"]',
    'm_Authenticator.TryAuthenticate(request, NexScopes.GroupsRead',
    'm_Authenticator.TryAuthenticate(request, NexScopes.ProfileRead',
    'if (firstLife || !profile.PublishProfile)',
    'subject != avatarId',
    'm_Groups.GetGroupRecord(requester, groupId)',
    'profile.FirstLifeImageId : profile.ImageId',
    'assets.Get(assetId.ToString())',
    'AssetType.Texture',
    'OpenJPEG.DecodeToImage',
    'Cache-Control", "private, no-store"',
    'MaxAssetBytes = 8 * 1024 * 1024',
    'MaxDimension = 2048',
    'size == 64 || size == 128 || size == 256 || size == 512',
):
    assert marker in media, f"Media API missing safety constraint: {marker}"

assert '"/api/v1/media/assets"' not in connector
assert '"/api/v1/media/assets' not in openapi
assert '"/api/v1/media"' in connector
assert 'apiGate.Wrap(mediaApi.Handle)' in connector
assert 'AssetServiceConnector.GetLocalService(config, "AssetService")' in connector
assert 'new NexMediaApi(' in connector
assert 'payload["image_url"]' in groups
assert 'payload["insignia_id"]' in groups
assert 'media_image_url =' in profile
assert 'first_life_image_url =' in profile
assert 'profile_image_url =' in profile, "legacy image URL must remain"
assert 'new SimpleStreamHandler("/api/v1/media", mediaHandler' in tests
assert 'GroupMediaAssetProxy' in tests
assert 'mediaPath + "?size=999"' in tests
assert 'HttpStatusCode.Unauthorized' in tests
assert 'HttpStatusCode.Forbidden' in tests
assert 'imageReply.Content.Headers.ContentType' in tests
assert '"/api/v1/media/assets/" + insignia' in tests
assert '["image/png"]' in openapi and 'format = "binary"' in openapi
print("OpenGenesisLINK Media API 1.0 routing/privacy/HTTP regression coverage: OK")
