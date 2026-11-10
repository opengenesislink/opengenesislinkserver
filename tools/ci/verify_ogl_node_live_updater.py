#!/usr/bin/env python3
"""OGL updater regression: zero remote command inputs and independent worker."""
import importlib.util
import json
import os
from pathlib import Path
import subprocess
import sys
import tempfile
import threading
import time
import urllib.error
import urllib.request
import uuid

REPO = Path(__file__).resolve().parents[2]
SUPERVISOR = REPO / "tools/ops/ogl-node-updater.py"
PAYLOAD = REPO / "tools/ops/ogl-node-update-live.sh"
INSTALLER = REPO / "tools/ops/install-ogl-node-updater.sh"
UNIT = REPO / "tools/ops/ogl-node-updater.service"
NODE = (REPO / "NexVerse/RegionModules/NodeAgent/NexVerseNodeAgentModule.cs").read_text()
API = (REPO / "NexVerse/Server/Api/NexNodeApi.cs").read_text()
UI = (REPO / "NexVerse/Server/Api/NexApiDocsPage.cs").read_text()
PROTOCOL = (REPO / "NexVerse/Core/Messaging/NexBusProtocol.cs").read_text()

for script in (PAYLOAD, INSTALLER):
    subprocess.run(["bash", "-n", str(script)], check=True)
for needle in ("git fetch --no-tags origin main",
               "git reset --hard origin/main", "bash ./compile.sh",
               "bash ./runprebuild.sh", "configs.tar.gz",
               "NexVerse-Deutschland.ini", "bin/OpenSim.dll",
               "Microsoft.NETCore.App 10.", "python3 tools/gen11/verify_net10.py",
               "OpenGenesisLINK.Addons."):
    assert needle in PAYLOAD.read_text(), needle
for needle in ("node.update.live.requested", "SignLiveRequest(",
               'action == "update"', "EnsureStageAuthorized(response)"):
    assert needle in API, needle
for needle in ("VerifyNodeLiveRequest", "OGL_NODE_UPDATER_TOKEN",
               "127.0.0.1", 'node.update.live.completed',
               "manual_opensim_restart_required"):
    assert needle in NODE, needle
for needle in ("ogl-node-live-v1", "SignNodeLiveRequest",
               "VerifyNodeLiveRequest"):
    assert needle in PROTOCOL, needle
assert "updateSimulatorNode(" in UI and "window.confirm(message)" in UI
assert "User=oglupdate" in UNIT.read_text()
assert "/usr/local/libexec/ogl-node-updater" in INSTALLER.read_text()

with tempfile.TemporaryDirectory(prefix="ogl-live-updater-ci-") as temp:
    sandbox = Path(temp)
    live = sandbox / "checkout"
    (live / ".git").mkdir(parents=True)
    (live / "bin").mkdir()
    (live / "bin/OpenSim.dll").write_bytes(b"fake-assembly")
    backups = sandbox / "backups"
    state = sandbox / "state"
    script = sandbox / "trusted-stub.sh"
    script.write_text("#!/usr/bin/env bash\n"
                      "set -e\n"
                      "echo '[1/7] prerequisites'\n"
                      "sleep 0.3\n"
                      "echo '[7/7] complete'\n")
    script.chmod(0o755)
    spec = importlib.util.spec_from_file_location("ogl_updater_test", SUPERVISOR)
    module = importlib.util.module_from_spec(spec)
    assert spec.loader
    spec.loader.exec_module(module)
    module.NODE_ID = "test-sim-1"
    module.TOKEN = "T" * 64
    module.BASE = live
    module.BACKUPS = backups
    module.STATE_DIR = state
    module.STATE_FILE = state / "current.json"
    module.SCRIPT = script
    module.check_paths()

    server = module.ThreadingHTTPServer(("127.0.0.1", 0), module.Handler)
    port = server.server_address[1]
    runner = threading.Thread(target=server.serve_forever, daemon=True)
    runner.start()

    def call(path, method="GET", payload=None, key="T" * 64):
        data = None if payload is None else json.dumps(payload).encode("utf-8")
        req = urllib.request.Request(
            f"http://127.0.0.1:{port}{path}", data=data, method=method,
            headers={"Authorization": "Bearer " + key,
                     "Content-Type": "application/json"})
        try:
            with urllib.request.urlopen(req, timeout=4) as response:
                return response.status, json.load(response)
        except urllib.error.HTTPError as response:
            return response.code, json.load(response)

    try:
        assert call("/v1/status", key="wrong")[0] == 401
        assert call("/v1/updates", "POST", {"operation_id": str(uuid.uuid4()),
                "node_id": module.NODE_ID, "command": "rm -rf /"})[0] == 400
        assert call("/v1/updates", "POST", {"operation_id": str(uuid.uuid4()),
                "node_id": "other-node"})[0] == 400
        op = str(uuid.uuid4())
        request = {"operation_id": op, "node_id": module.NODE_ID}
        code, first = call("/v1/updates", "POST", request)
        assert code == 202 and first["operation_id"] == op, (code, first)
        assert call("/v1/updates", "POST", request)[0] == 202
        assert call("/v1/updates", "POST", {"operation_id": str(uuid.uuid4()),
               "node_id": module.NODE_ID})[0] == 409
        for _ in range(100):
            status_code, status = call("/v1/status")
            if status["state"] == "completed":
                break
            assert status["state"] in ("running", "queued"), status
            time.sleep(0.05)
        else:
            raise AssertionError("independent worker did not complete")
        assert status["phase"] == 7 and status["operation_id"] == op
        assert json.loads(module.STATE_FILE.read_text())["state"] == "completed"
        assert (state / ("update-" + op + ".log")).is_file()
        assert call("/v1/updates", "POST", request)[0] == 200
        assert (live / "bin/OpenSim.dll").read_bytes() == b"fake-assembly"
    finally:
        server.shutdown()
        server.server_close()
        runner.join(timeout=3)

print("PASS: isolated loopback update service, HMAC source contract, no arbitrary commands, idempotent operations and manual restart")
