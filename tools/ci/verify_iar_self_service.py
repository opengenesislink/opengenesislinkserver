#!/usr/bin/env python3
"""Self-service IAR: resident ownership, bounded transfer, safe defaults."""
from pathlib import Path
ROOT = Path(__file__).resolve().parents[2]
def text(path): return (ROOT / path).read_text(encoding="utf-8-sig")
api = text("NexVerse/Server/Api/OglIarSelfServiceApi.cs")
core = text("NexVerse/Core/Archives/OglIarTransferPolicy.cs")
connector = text("NexVerse/Server/Api/NexVerseWorldApiConnector.cs")
node = text("NexVerse/RegionModules/NodeAgent/NexVerseNodeAgentModule.cs")
cfgs = [text(p) for p in (
    "bin/Robust.HG.ini", "bin/Robust.HG.ini.example",
    "bin/Robust.ini.example")]
for marker in (
    '"/api/v1/iar/me"', "apiGate.Wrap(iarSelfService.Handle)",
    "new OglIarSelfServiceApi(", "jobStore, eventBus, featureControl"
):
    assert marker in connector, marker
assert connector.index('"/api/v1/iar/me"') < connector.index('"/api/v1/iar"')
for cfg in cfgs:
    assert cfg.count("[OglIarSelfService]") == 1
    assert "Enabled = false" in cfg.split("[OglIarSelfService]")[-1]
    assert 'SharedStorageRoot = ""' in cfg.split("[OglIarSelfService]")[-1]

for marker in (
    '"Enabled", false', '"SharedStorageRoot", ""',
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
assert "Path.GetFileName(" not in api, "Never let upload choose a filesystem name"
assert "new byte[take]" in api and "new byte[65536]" in api
assert "File.ReadAllBytes(" not in api
assert "request.InputStream.Read(" not in api or "remaining" in api
assert "m_Jobs.List(1000).Any(" in api
assert "inventory_path" in api
assert '["self_service"] = "true"' in api
assert '["checkPermissions"] = "CT"' in node, "Self-service export requires copy+transfer"

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
    'GetString("NasHost", "")', 'GetInt("RetentionDays", 30)',
    'string.Equals(m_NasHost, "10.78.0.4"',
    "m_CleanupTimer = new Timer(", "TimeSpan.FromHours(1)",
    "OglIarNasStorage.RequireMounted(m_SharedRoot, m_NasHost)",
    "CleanupExpiredArchives()", "File.Delete(file)",
    "!IsExpired(job)", "OglIarNasStorage.IsManagedIar(",
    "FileAttributes.ReparsePoint", "job.State is OglJobState.Running or OglJobState.Queued",
    '["nas_host"] = m_NasHost',
):
    assert marker in api, marker
for cfg in cfgs:
    block = cfg.split("[OglIarSelfService]")[-1]
    assert 'NasHost = "10.78.0.4"' in block
    assert "RetentionDays = 30" in block
assert "OglIarNasStorage.RequireMounted(operations.StorageRoot, nasHost)" in node
assert '"10.78.0.4"' in node
assert "string StorageRoot { get; }" in text(
    "NexVerse/RegionModules/Archives/OglIarOperationManager.cs")
assert "OglIarNasStorage.cs" in text(
    "tools/ci/OglIarSelfServiceRegression/OglIarSelfServiceRegression.csproj")
assert "10.78.0.4" in text("doc/NexVerse/OGL_IAR_SELF_SERVICE.md")
print("OGL IAR resident self-service static contracts PASS")
