#!/usr/bin/env python3
"""Self-service IAR: resident ownership, bounded transfer, safe defaults."""
from pathlib import Path
ROOT = Path(__file__).resolve().parents[2]
def text(path): return (ROOT / path).read_text(encoding="utf-8-sig")
api = text("NexVerse/Server/Api/OglIarSelfServiceApi.cs")
core = text("NexVerse/Core/Archives/OglIarTransferPolicy.cs")
connector = text("NexVerse/Server/Api/NexVerseWorldApiConnector.cs")
node = text("NexVerse/RegionModules/NodeAgent/NexVerseNodeAgentModule.cs")
live_robust = text("bin/Robust.HG.ini")
live_sim = text("bin/OpenSim.ini")
cfgs = [live_robust, text("bin/Robust.HG.ini.example"),
        text("bin/Robust.ini.example")]
for marker in (
    '"/api/v1/iar/me"', "apiGate.Wrap(iarSelfService.Handle)",
    "new OglIarSelfServiceApi(", "jobStore, eventBus, featureControl"
):
    assert marker in connector, marker
assert connector.index('"/api/v1/iar/me"') < connector.index('"/api/v1/iar"')
# Production G1 configuration is deliberately persisted in Git; example
# templates remain safe, disabled defaults for other operators.
def iar_section(ini, section):
    marker = "[" + section + "]"
    assert ini.count(marker) == 1, section
    return ini.split(marker, 1)[1].split("\n[", 1)[0]

ROBUST_IAR = iar_section(live_robust, "OglIarSelfService")
SIM_IAR = iar_section(live_sim, "OpenGenesisLINKIAR")
EXPECTED_LIVE = (
    "Enabled = true",
    'StorageBackend = "WebDAV"',
    'NasHost = "10.78.0.4"',
    'WebDavBaseUrl = "https://10.78.0.4:5006/Server/Benutzer/Weiss_Joerg/NexVerse/IAR-Backups/"',
    'WebDavUsername = "ogl-iar"',
    'WebDavPasswordFile = "/etc/opengenesislink/iar-webdav.pass"',
)
for block in (ROBUST_IAR, SIM_IAR):
    for item in EXPECTED_LIVE:
        assert item in block, "Live IAR Git configuration drifted: " + item

for cfg in cfgs:
    block = iar_section(cfg, "OglIarSelfService")
    assert 'StorageBackend = "WebDAV"' in block
    assert 'SharedStorageRoot = ""' in block
for example_path in ("bin/Robust.HG.ini.example", "bin/Robust.ini.example"):
    block = iar_section(text(example_path), "OglIarSelfService")
    assert "Enabled = false" in block
    assert 'WebDavPasswordFile = ""' in block
    assert 'WebDavBaseUrl = ""' in block

for marker in (
    '"Enabled", false', '"SharedStorageRoot",',
    "m_SharedRoot = m_Enabled ?", "maximumTotal",
    "NexScopes.InventoryRead", "NexScopes.InventoryWrite",
    "resident_token_required", "principal.Subject",
    "user.PrincipalID", "OwnJob(user, id)", "self_service",
    'Actor(user)', '"region_id"', '"sha256"', '"expected_bytes"',
    "OglIarTransferPolicy.AcceptChunk(", "FileMode.CreateNew",
    "FileShare.None", "m_Locks.GetOrAdd", "stream.Flush(true)",
    '"uploading"', "EtaSeconds(", "EtaFromJob(",
    "sha256_mismatch", 'Digest(path, SHA256.Create)',
    "m_Bus.Publish(new NexEvent", '"archive.iar.requested"',
    "FileFor(job)", "new FileInfo(path).Length",
    "res.RawBuffer = data;", '"Content-Range"', "HttpStatusCode.PartialContent",
    "OglIarTransferPolicy.MaximumChunkBytes",
    "MD5.Create", "SHA256.Create", "download_ready",
    "archive_operation_conflict", "NexScopes.InventoryRead",
    "ExpireStaleUploads(user)", "private void DeleteArchive(",
    "m_Jobs.Fail(job.JobId, \"sha256_mismatch\")", "m_Jobs.Cancel(job.JobId",
    "TimeSpan.FromHours(24)"
):
    assert marker in api or marker in core, marker
assert "TryReadyExport(job" in api
assert '"user_id"] = user.PrincipalID.ToString()' in api, "Never trust client user IDs"
assert '"user_id"] = job.Metadata["user_id"]' in api
assert "ArchiveInventory(" not in api and "DearchiveInventory(" not in api
assert 'ReadString(root, "file_name")' not in api, "Never let upload choose a filesystem name"
assert "new byte[take]" in api and "new byte[65536]" in api
assert "File.ReadAllBytes(" not in api
assert "request.InputStream.Read(" not in api or "remaining" in api
assert "m_Jobs.List(1000).Any(" in api
assert "inventory_path" in api
assert '["self_service"] = "true"' in api
# The resident IAR backup includes inventory regardless of Copy/Transfer bits.
# Native permissions remain serialized in the archive; no privileged rights are granted.
manager = text("NexVerse/RegionModules/Archives/OglIarOperationManager.cs")
writer = text("OpenSim/Region/CoreModules/Avatar/Inventory/Archiver/InventoryArchiveWriteRequest.cs")
assert 'operations.StartExport(operationId, user, inventoryPath, fileName)' in node
assert '["checkPermissions"] = "CT"' not in node, "Self-service must not filter Copy/Transfer"
assert 'options ?? new Dictionary<string, object>()' in manager
assert 'FilterContent = null;' in writer and 'if (FilterContent == null)' in writer
assert 'UserInventoryItemSerializer.Serialize(inventoryItem, options, userAccountService)' in writer
assert 'UserAccount user = scene.UserAccountService.GetUserAccount(' in node
assert 'OwnJob(user, id)' in api, "Never expose another avatar's IAR jobs"

for marker in (
    "MaximumChunkBytes = 4 * 1024 * 1024",
    "DefaultMaximumArchiveBytes = 2L * 1024 * 1024 * 1024",
    "Path.GetFullPath(sharedRoot)", 'jobId.ToString("D") + ".iar"',
    "offset == currentBytes", "bytes <= MaximumChunkBytes",
    "now <= createdAt", "return null",
):
    assert marker in core, marker
assert "OglIarTransferPolicy.cs" in text(
    "tools/ci/OglIarSelfServiceRegression/OglIarSelfServiceRegression.csproj")
workflow = text(".github/workflows/nexjast-ci.yml")
assert "Verify IAR self-service bounded transfer, ownership and ETA" in workflow
assert "dotnet run --configuration Release --project tools/ci/OglIarSelfServiceRegression/OglIarSelfServiceRegression.csproj" in workflow
assert "/api/v1/iar/me/operations/{id}" in text("doc/NexVerse/OGL_IAR_SELF_SERVICE.md")
assert "mountOglIarSelfService" in text("doc/NexVerse/examples/ogl-iar-self-service-widget.js")
assert "setInterval(" in text("doc/NexVerse/examples/ogl-iar-self-service-widget.js")
assert "showSaveFilePicker" in text("doc/NexVerse/examples/ogl-iar-self-service-widget.js")
nas = text("NexVerse/Core/Archives/OglIarNasStorage.cs")
for marker in (
    "MaximumRetentionDays = 30", "IsExpectedNfsMount(", "RequireMounted(",
    '"/proc/self/mountinfo"', '"nfs4"', '"nfs"',
    "IsExpired(", "IsManagedIar(", "Path.GetFullPath(",
):
    assert marker in nas, marker
for marker in (
    'GetString("NasHost", "10.78.0.4")', 'GetInt("RetentionDays", 30)',
    'string.Equals(m_NasHost, "10.78.0.4"',
    "m_CleanupTimer = new Timer(", "TimeSpan.FromHours(1)",
    "OglIarNasStorage.RequireMounted(m_SharedRoot, m_NasHost)",
    "CleanupExpiredArchives()", "File.Delete(file)",
    "!IsExpired(job)", "OglIarNasStorage.IsManagedIar(",
    "FileAttributes.ReparsePoint", "job.State is OglJobState.Running or OglJobState.Queued",
    "ogl-retention", "retention_marker_failed", "DeleteMarker(file)",
    'retention_days = m_RetentionDays', 'expires_at_utc',
    '["nas_host"] = m_NasHost',
):
    assert marker in api, marker
for cfg in cfgs:
    block = cfg.split("[OglIarSelfService]")[-1]
    assert 'NasHost = "10.78.0.4"' in block
    assert "RetentionDays = 30" in block
assert "OglIarNasStorage.RequireMounted(" in node
assert "Path.GetDirectoryName(operations.StorageRoot), nasHost)" in node
assert '"10.78.0.4"' in node
assert "string StorageRoot { get; }" in text(
    "NexVerse/RegionModules/Archives/OglIarOperationManager.cs")
assert "OglIarNasStorage.cs" in text(
    "tools/ci/OglIarSelfServiceRegression/OglIarSelfServiceRegression.csproj")
assert "10.78.0.4" in text("doc/NexVerse/OGL_IAR_SELF_SERVICE.md")
print("OGL IAR resident self-service static contracts PASS")

# WebDAV is the default storage contract; NFS remains opt-in and fail-closed.
dav = text("NexVerse/Core/Archives/OglIarWebDavStorage.cs")
for marker in ('BackendName = "WebDAV"', 'ConnectCallback', '10.78.0.4',
               'AllowAutoRedirect = false', 'UseProxy = false',
               'Uri.UriSchemeHttps', 'File.GetUnixFileMode',
               'Request("MKCOL"', 'Request("PUT"', 'Request("HEAD"',
               'Request("GET"', 'Request("DELETE"', 'Request("PROPFIND"',
               'Content-Range', 'HttpCompletionOption.ResponseHeadersRead'):
    assert marker in dav, marker
for marker in ('m_WebDav.Upload(', 'm_WebDav.DownloadChunk(',
               'm_WebDav.Digest(', 'm_WebDav.Delete(', 'm_WebDav.Size(',
               'm_UseWebDav ? "WebDAV" : "NFS"', 'TemporaryStorageRoot',
               'if (!m_UseWebDav)', 'CleanupExpiredWebDav'):
    assert marker in api, marker
for marker in ('m_IarWebDav.Download(', 'm_IarWebDav.Upload(',
               'm_IarWebDavMaximumBytes', 'File.Delete(stage)',
               '"storage_backend"', 'OglIarNasStorage.RequireMounted('):
    assert marker in node, marker
for p in ("bin/OpenSim.ini", "bin/OpenSim.ini.example"):
    ini = text(p)
    block = iar_section(ini, "OpenGenesisLINKIAR")
    assert 'StorageBackend = "WebDAV"' in block
example_sim = iar_section(text("bin/OpenSim.ini.example"), "OpenGenesisLINKIAR")
assert 'WebDavPasswordFile = ""' in example_sim
assert 'WebDavBaseUrl = ""' in example_sim
assert 'WebDavUsername = ""' in example_sim
# Never accidentally store password material or inline credential values in Git.
for block in (ROBUST_IAR, SIM_IAR):
    assert 'WebDavPassword = ' not in block
    assert 'WebDavPasswordFile = "/etc/opengenesislink/iar-webdav.pass"' in block

# Never report native archiver failures as WebDAV transfer failures.
assert 'remoteUploadFailed ? "iar_webdav_transfer_failed" : operation.Error' in node
assert 'state == "failed" ? "iar_webdav_transfer_failed" : operation.Error' not in node
assert '"iar_webdav_not_configured"' in node
assert 'Archive staging failed for operation ' in node
assert 'WebDAV-Upload bestaetigt' in node
assert 'validiertes Archiv' in node
assert 'm_IarWebDav.Upload(' in node
# WebDAV export startup must identify the failing NAS operation without
# exposing internal paths, credentials or exceptions to the resident portal.
assert '"iar_webdav_region_unavailable"' in api
assert '"iar_webdav_marker_unavailable"' in api
assert "WebDAV MKCOL region directory preflight" in api
assert "WebDAV PUT retention marker preflight" in api
assert "Self-service archive I/O failed." in api
preflight = dav.split("public void EnsureRegion(Guid region)", 1)[1].split(
    "public void CreateMarker(Guid region", 1)[0]
assert "HttpStatusCode.Conflict" not in preflight, "MKCOL HTTP 409 must fail"
assert "HttpStatusCode.MethodNotAllowed" in preflight
