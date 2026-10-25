#!/usr/bin/env python3
"""Regression guard for admin operator UI, daily local/remote logs and SQL safety."""
from pathlib import Path
import re
import shutil
import subprocess
import tempfile
import xml.etree.ElementTree as ET

root=Path(__file__).resolve().parents[2]
page=(root/"NexVerse/Server/Api/NexApiDocsPage.cs").read_text()
login=(root/"NexVerse/Server/Api/OglApiDocsAccess.cs").read_text()
reader=(root/"NexVerse/Server/Api/NexOperatorLogsApi.cs").read_text()
relay=(root/"NexVerse/Server/Api/NexBusRemoteLogArchive.cs").read_text()
node=(root/"NexVerse/RegionModules/NodeAgent/NexVerseNodeAgentModule.cs").read_text()
endpoint=(root/"NexVerse/Server/Api/NexBusHttpTransport.cs").read_text()
bus=(root/"NexVerse/Core/Messaging/DistributedNexEventBus.cs").read_text()
connector=(root/"NexVerse/Server/Api/NexVerseWorldApiConnector.cs").read_text()
openapi=(root/"NexVerse/Server/Api/NexVerseWorldApiHandlers.cs").read_text()
catalog=(root/"NexVerse/Server/Api/NexChatGptInstructionDownloads.cs").read_text()
doc=(root/"doc/OpenGenesisLINK/WORLD_API_SQL_OPERATIONS_LOGS_AUDIT_2026-10-25.md").read_text()

for name in ("Robust", "OpenSim"):
    cfg=ET.parse(root/"bin"/f"{name}.exe.config")
    appender=cfg.find(".//appender[@name='LogFileAppender']")
    assert appender is not None
    assert appender.attrib["type"]=="log4net.Appender.RollingFileAppender"
    assert appender.find("file").attrib["value"]==f"logs/{name}_"
    assert appender.find("rollingStyle").attrib["value"]=="Date"
    assert appender.find("datePattern").attrib["value"]=="dd_MM_yyyy'.log'"
    assert appender.find("staticLogFileName").attrib["value"]=="false"

for item in ('"/api/v1/admin/logs"', '"/api/v1/admin/logs/{service}/{date}/entries"'):
    assert item in openapi
assert "GET /api/v1/admin/logs" in doc
assert "/{yyyy-MM-dd}/entries" in doc
assert 'new NexOperatorLogsApi(authenticator)' in connector
assert 'apiGate.Wrap(operatorLogs.Handle)' in connector
for marker in ("NexScopes.AdminAll", "account.UserLevel < 200", "account.LocalToGrid",
  's_FileName.Match(basename)', 'node_id', 'TryParseExact',
  '"logs"', 'MaxPage = 1000', 'FileShare.ReadWrite | FileShare.Delete',
  's_Secret.Replace', 'NexBusRemoteLogArchive.SafeNode.IsMatch(nodeId)'):
    assert marker in reader, marker

for marker in ('m_Nodes.Get(node) == null','SafeNode.IsMatch(node)',
  '"OpenSim_" + node + "_"','offset > file.Length',
  'FixedTimeEquals(previous, bytes)', 'file.Flush();',
  'File.SetUnixFileMode', 'internal bool TryReceive(NexEvent ev)'):
    assert marker in relay, marker

for marker in ('LogForwardingEnabled','NexBusProtocol.Serialize(envelope)',
  'NexBusProtocol.Sign(payload, m_SharedKey)','"node.log.chunk"',
  'HttpStatusCode.Accepted','result.Content.ReadAsByteArrayAsync()',
  'cursor += count','Interlocked.CompareExchange(ref m_LogForwardRunning',
  'm_LogForwardOffsets[dayId] = cursor', 'peer.Scheme == Uri.UriSchemeHttps'):
    assert marker in node, marker

assert 'm_Bus.ReceiveLogChunk?.Invoke(nexEvent)' in endpoint
assert 'm_Bus.Receive(nexEvent)' in endpoint
assert 'isLogChunk && !accepted ? HttpStatusCode.Conflict' in endpoint
assert 'public Func<NexEvent, bool> ReceiveLogChunk' in bus
assert 'signedLogBus.ReceiveLogChunk = remoteLogs.TryReceive;' in connector

for marker in ('id="page-logs"','id="page-audit"',
  'id="oglUserCreate"','id="oglUserSearch"',
  'function oglLoadLogIndex()','function oglLoadAudit(next=false)',
  'function oglActionEditor(domain,entry)', 'function oglCreateUser(event)',
  'function oglSearchUsers()', 'new URLSearchParams',
  'DEAKTIVIEREN','AUSFÜHREN',
  'file.node_id?', 'encodeURIComponent(file.node_id)'):
    assert marker in page, marker
assert 'OGL_LOGO_DATA_URI' in login
assert 'UserLevel 200' in login
assert "63 benannte Tabellen" in doc
assert "19 mit ~0 Zeilen" in doc
assert "keine" in doc.lower()
assert "DROP TABLE" in doc

# Syntax-check actual browser programs, not disconnected sample files.
if shutil.which("node") is None:
    raise RuntimeError("Node is necessary for browser admin regression")
for code,filename in [(page,"panel.mjs"),(login,"login.mjs")]:
    match=re.search(r"<script>\s*('use strict';.*?)\s*</script>", code, re.S)
    assert match,filename
    with tempfile.TemporaryDirectory() as temp:
        path=Path(temp)/filename
        path.write_text(match.group(1))
        check=subprocess.run(["node","--check",str(path)],capture_output=True,text=True)
        assert check.returncode==0,filename+": "+check.stderr

print("Operator Center: daily ROBUST/OpenSim and NexBus NodeId archives, admin CRUD, audit, XML & JavaScript OK")
