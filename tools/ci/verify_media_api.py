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

# /media/assets/{assetId} is now intentionally available ONLY to uploads
# recorded in the authoritative MariaDB publication catalog.
assert '"/api/v1/media/assets/{assetId}"' in openapi
managed = Path("NexVerse/Server/Api/NexManagedMediaApi.cs").read_text()
store = Path("NexVerse/Server/Api/NexMediaCatalogStore.cs").read_text()
keys = Path("NexVerse/Core/Security/NexApiKeyStore.cs").read_text()
security = Path("NexVerse/Core/Security/NexSecurity.cs").read_text()
for route in (
    "/api/v1/media/uploads",
    "/api/v1/media/assets/{assetId}",
    "/api/v1/media/inventory/{itemId}/image",
    "/api/v1/media/marketplace/{listingId}/image",
):
    assert route in openapi, f"missing managed route: {route}"
for marker in (
    'm_Catalog.FindUpload(assetId)',
    'item.Owner != user.PrincipalID',
    'm_Auth.TryAuthenticate(request, NexScopes.MediaPublish',
    'principal.Subject.StartsWith("api-key:"',
    'm_Catalog.PublishListing(',
    'm_Catalog.UnpublishListing(',
    'm_Groups.UpdateGroup(',
    'm_Profiles.UpdateAvatarProperties(',
    'OpenJPEG.EncodeFromImage(',
    'AssetType.Texture',
    'MaxUploadBytes = 4 * 1024 * 1024',
    'record.FounderID != user.PrincipalID',
):
    assert marker in managed, f"missing managed-media ownership/codec validation: {marker}"
assert 'CREATE TABLE IF NOT EXISTS ogl_media_uploads' in store
assert 'CREATE TABLE IF NOT EXISTS ogl_media_marketplace' in store
assert 'FOREIGN KEY(asset_id)' in store
assert 'NexScopes.MediaPublish' in keys
assert 'public const string MediaPublish' in security
role_scopes = security.split('private static readonly HashSet<string> s_AssignableScopes', 1)[1]
assert 'NexScopes.MediaPublish' not in role_scopes
assert 'ConfigureManaged(managedMedia)' in connector
assert 'new NexMediaCatalogStore(databaseConnectionString)' in connector
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
