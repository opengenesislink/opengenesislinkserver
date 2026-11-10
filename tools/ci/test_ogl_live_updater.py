#!/usr/bin/env python3
"""Offline signed updater and seven-step source update regression, no network."""
import hashlib
import hmac
import http.server
import importlib.util
import json
import os
from pathlib import Path
import subprocess
import sys
import tarfile
import tempfile
import threading
import time
import unittest
import urllib.error
import urllib.request
import uuid

ROOT = Path(__file__).resolve().parents[2]
SHELL = ROOT / "tools/ops/ogl-node-live-update.sh"
spec = importlib.util.spec_from_file_location("ogl_live_updater",
                                              ROOT / "tools/ops/ogl-node-live-updater.py")
updater = importlib.util.module_from_spec(spec)
sys.modules[spec.name] = updater
spec.loader.exec_module(updater)
KEY = "live-control-test-" + "q" * 48


def approve(node, operation, when):
    return hmac.new(KEY.encode(), updater.signed_payload(node, operation, when),
                    hashlib.sha256).hexdigest()


class TestUpdater(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory(prefix="ogl-node-update-ci-")
        self.addCleanup(self.tmp.cleanup)
        self.parent = Path(self.tmp.name)
        self.root = self.parent / "checkout"
        (self.root / ".git").mkdir(parents=True)
        (self.root / "bin").mkdir()
        (self.root / "bin/OpenSim.dll").write_bytes(b"fake dll")
        (self.root / "compile.sh").write_text(":\n")
        standalone = self.parent / "standalone"
        standalone.mkdir()
        self.stub = standalone / "ogl-node-live-update.sh"
        self.stub.write_text("#!/bin/bash\nset -e\nsleep 0.3\n")
        self.service = updater.UpdateService(
            node_id="simulator-1", bin_dir=self.root / "bin",
            script=self.stub, state_dir=self.parent / "state",
            backup_dir=self.parent / "backups", key=KEY)

    def test_csharp_python_hmac_protocol_vector(self):
        fixed = updater.signed_payload(
            "simulator-1", "00000000-0000-0000-0000-000000000001", 1700000000)
        result = hmac.new(("U" * 64).encode(), fixed, hashlib.sha256).hexdigest()
        self.assertEqual(result,
            "ddb60ee3ba682022b4176869024b04de7607e702b73c61987adca2975d81e193")

    def test_security_replay_persistence(self):
        op = str(uuid.uuid4())
        now = int(time.time())
        self.assertFalse(updater.verify_approval(
            "simulator-1", op, now, "0" * 64, KEY))
        self.assertFalse(updater.verify_approval(
            "simulator-1", op, now - 301,
            approve("simulator-1", op, now - 301), KEY, now))
        self.assertFalse(updater.verify_approval(
            "simulator-2", op, now, approve("simulator-1", op, now), KEY, now))
        status, result = self.service.submit(
            op, now, approve("simulator-1", op, now))
        self.assertEqual(status, 202)
        self.assertEqual(result["operation_id"], op)
        self.assertEqual(self.service.submit(
            op, now, approve("simulator-1", op, now))[0], 200)
        other = str(uuid.uuid4())
        self.assertEqual(self.service.submit(
            other, now, approve("simulator-1", other, now))[0], 409)
        for i in range(100):
            if self.service.snapshot()["state"] in ("completed", "failed"):
                break
            time.sleep(.05)
        self.assertEqual(self.service.snapshot()["state"], "completed")
        restarted = updater.UpdateService(
            node_id="simulator-1", bin_dir=self.root / "bin",
            script=self.stub, state_dir=self.parent / "state",
            backup_dir=self.parent / "backups", key=KEY)
        self.assertEqual(restarted.snapshot()["state"], "completed")

    def test_local_http_rejects_unauthorized(self):
        server = http.server.ThreadingHTTPServer(
            ("127.0.0.1", 0), updater.handler_factory(self.service))
        server.daemon_threads = True
        thread = threading.Thread(target=server.serve_forever, daemon=True)
        thread.start()
        self.addCleanup(server.server_close)
        self.addCleanup(server.shutdown)
        base = f"http://127.0.0.1:{server.server_port}"
        def query(path, body=None, headers=None):
            req = urllib.request.Request(base + path, data=body,
                headers=headers or {}, method="POST" if body else "GET")
            try:
                with urllib.request.urlopen(req, timeout=4) as res:
                    return res.status, json.load(res)
            except urllib.error.HTTPError as err:
                return err.code, json.load(err)
        self.assertEqual(query("/healthz")[0], 200)
        self.assertEqual(query("/v1/status")[0], 403)
        self.assertEqual(query("/v1/update", b"{}")[0], 400)
        op, now = str(uuid.uuid4()), int(time.time())
        body = json.dumps({"node_id": "simulator-1", "operation_id": op,
            "issued_at": now, "approval_hmac": approve("simulator-1", op, now)}).encode()
        self.assertEqual(query("/v1/update", body)[0], 202)
        self.assertEqual(query("/v1/status", headers={
            "X-OGL-Timestamp": str(now),
            "X-OGL-Signature": approve("simulator-1", op, now)})[0], 200)

    def test_seven_step_backup_restore_and_region_deletion(self):
        fake = self.parent / "mock-bin"
        fake.mkdir()
        git = fake / "git"
        git.write_text("""#!/usr/bin/env bash
case "$1" in
 branch) echo "$FAKE_BRANCH";;
 remote) echo "https://github.com/opengenesislink/opengenesislinkserver.git";;
 status) :;;
 rev-parse) echo "0123456789abcdef0123456789abcdef01234567";;
 -c) :;;
 reset) echo "UPSTREAM=overwritten" > bin/OpenSim.ini;;
 *) exit 3;;
esac
""")
        dotnet = fake / "dotnet"
        dotnet.write_text("""#!/usr/bin/env bash
if [[ "$1" == "--list-sdks" ]]; then
  echo "10.0.100 [/usr/share/dotnet/sdk]"; exit 0; fi
if [[ "$1" == "--list-runtimes" ]]; then
  echo "Microsoft.NETCore.App 10.0.0 [/usr/share/dotnet/shared]"; exit 0; fi
exit 3
""")
        py = fake / "python3"
        py.write_text("#!/bin/bash\nexit 0\n")
        for file in (git, dotnet, py):
            file.chmod(0o755)
        (self.root / "runprebuild.sh").write_text("#!/bin/bash\ntouch project-generated\n")
        (self.root / "compile.sh").write_text("""#!/usr/bin/env bash
printf 'built-robust' > bin/Robust.dll
printf 'built-opensim' > bin/OpenSim.dll
for addon in NexTraffic NexTransit NexSmartLight OGLWeather OGLEnergy; do
  printf 'built-addon' > "bin/OpenGenesisLINK.Addons.$addon.dll"
done
""")
        (self.root / "bin/OpenSim.ini").write_text("[Grid]\nSECRET=keep\n")
        (self.root / "bin/Regions").mkdir()
        (self.root / "bin/Regions/NexVerse-Deutschland.ini").write_text("delete")
        (self.root / "bin/Regions/Hamburg.ini").write_text("retain")
        (self.root / "bin/Estates").mkdir()
        (self.root / "bin/Estates/estate.ini").write_text("retain")
        (self.root / "bin/config-include").mkdir()
        (self.root / "bin/config-include/custom.ini").write_text("retain")
        env = {**os.environ, "PATH": str(fake) + os.pathsep + os.environ["PATH"],
               "FAKE_BRANCH": "feature"}
        command = ["bash", str(SHELL), str(self.root), str(self.parent / "snapshots")]
        reject = subprocess.run(command, env=env, capture_output=True, text=True)
        self.assertNotEqual(reject.returncode, 0)
        self.assertTrue((self.root / "bin/Regions/NexVerse-Deutschland.ini").exists())
        env["FAKE_BRANCH"] = "main"
        result = subprocess.run(command, env=env, capture_output=True, text=True)
        self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
        self.assertFalse((self.root / "bin/Regions/NexVerse-Deutschland.ini").exists())
        self.assertIn("SECRET=keep", (self.root / "bin/OpenSim.ini").read_text())
        for file in ["bin/Regions/Hamburg.ini", "bin/Estates/estate.ini",
                     "bin/config-include/custom.ini"]:
            self.assertTrue((self.root / file).exists(), file)
        snapshot = next((self.parent / "snapshots").iterdir())
        with tarfile.open(snapshot / "configs.tar.gz", "r:gz") as archive:
            self.assertIn("bin/Regions/NexVerse-Deutschland.ini", archive.getnames())
        self.assertTrue((self.root / "project-generated").exists())


if __name__ == "__main__":
    unittest.main()
