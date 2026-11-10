#!/usr/bin/env python3
"""Standalone localhost-only OpenGenesisLINK Gen1 source updater.

Run separately from Robust/OpenSim under systemd. This process never loads
OpenSim assemblies, never accepts a command/path/repository from HTTP and
never restarts a simulator. The caller must prove a signed, recent ROBUST
deploy approval using the separate NEXVERSE_UPDATE_CONTROL_KEY.
"""
from __future__ import annotations

import fcntl
import hashlib
import hmac
import http.server
import ipaddress
import json
import os
from pathlib import Path
import re
import signal
import subprocess
import sys
import threading
import time
import uuid

MAX_BODY = 2048
MAX_STATUS = 4096
NODE_RE = re.compile(r"^[A-Za-z0-9][A-Za-z0-9._-]{0,63}$")


def signed_payload(node_id: str, operation_id: str, issued_at: int) -> bytes:
    parsed = str(uuid.UUID(operation_id))
    normalized = node_id.strip().lower()
    if not NODE_RE.fullmatch(node_id) or len(normalized) != len(node_id):
        raise ValueError("invalid node")
    text = (f"ogl-node-deploy-v1\\n{len(normalized)}:{normalized}\\n"
            f"{parsed}\\n{issued_at}")
    return text.encode("utf-8")


def verify_approval(node: str, operation: str, timestamp: int,
                    signature: str, key: str, now: int | None = None) -> bool:
    now = int(time.time()) if now is None else now
    if not key or len(key.encode("utf-8")) < 32 or timestamp > now + 60 or timestamp < now - 300:
        return False
    if not isinstance(signature, str) or len(signature) != 64:
        return False
    try:
        supplied = bytes.fromhex(signature)
        expected = hmac.new(key.encode("utf-8"),
                            signed_payload(node, operation, timestamp),
                            hashlib.sha256).digest()
        return hmac.compare_digest(expected, supplied)
    except (ValueError, TypeError, OverflowError):
        return False


class UpdateService:
    def __init__(self, *, node_id: str, bin_dir: Path, script: Path,
                 state_dir: Path, backup_dir: Path, key: str):
        if not NODE_RE.fullmatch(node_id):
            raise ValueError("invalid configured node ID")
        if len(key.encode("utf-8")) < 32 or key.startswith(
                ("SET_THE_SAME", "REPLACE_", "CHANGE_ME")):
            raise ValueError("A separate random NEXVERSE_UPDATE_CONTROL_KEY (>=32 bytes) is required")
        self.node_id = node_id
        self.key = key
        self.bin_dir = Path(os.path.realpath(bin_dir))
        self.root = self.bin_dir.parent
        self.script = Path(os.path.realpath(script))
        self.state_dir = Path(os.path.realpath(state_dir))
        self.backup_dir = Path(os.path.realpath(backup_dir))
        if self.bin_dir.name != "bin" or not (self.bin_dir / "OpenSim.dll").is_file():
            raise ValueError("target must be a real bin folder with OpenSim.dll")
        if not (self.root / ".git").exists() or not (self.root / "compile.sh").is_file():
            raise ValueError("target must be an OpenGenesisLINK git source checkout")
        if (self.script == self.root or self.root in self.script.parents or
                self.script == self.bin_dir or
                self.root == self.state_dir or self.root in self.state_dir.parents or
                self.root == self.backup_dir or self.root in self.backup_dir.parents or
                self.backup_dir in self.root.parents or
                self.state_dir in self.root.parents):
            raise ValueError("updater script, backups and service state must be outside target tree")
        if not self.script.is_file():
            raise ValueError("standalone update shell script missing")
        self.state_dir.mkdir(mode=0o700, parents=True, exist_ok=True)
        self.state_dir.chmod(0o700)
        self.backup_dir.mkdir(mode=0o700, parents=True, exist_ok=True)
        self.backup_dir.chmod(0o700)
        self._lock = threading.RLock()
        self._active = False
        self.state_path = self.state_dir / "status.json"
        self._status = self._read_status()
        if self._status.get("state") in ("accepted", "running"):
            self._status.update(state="failed", phase="interrupted",
                                message="Updater service restarted during operation",
                                finished_at=int(time.time()))
            self._save_status(self._status)

    def _read_status(self) -> dict:
        try:
            data = json.loads(self.state_path.read_text(encoding="utf-8"))
            if isinstance(data, dict) and data.get("node_id") == self.node_id:
                return data
        except (OSError, ValueError):
            pass
        return {"node_id": self.node_id, "operation_id": "",
                "state": "idle", "phase": "none", "message": "no_update_requested"}

    def _save_status(self, item: dict) -> None:
        path = self.state_dir / "status.tmp"
        with path.open("w", encoding="utf-8") as output:
            os.chmod(path, 0o600)
            json.dump(item, output, sort_keys=True)
            output.flush()
            os.fsync(output.fileno())
        os.replace(path, self.state_path)

    def snapshot(self) -> dict:
        with self._lock:
            return dict(self._status)

    def submit(self, operation: str, timestamp: int, signature: str) -> tuple[int, dict]:
        if not verify_approval(self.node_id, operation, timestamp,
                               signature, self.key):
            return 403, {"error": "invalid_update_approval"}
        with self._lock:
            if operation == self._status.get("operation_id"):
                return 200, self.snapshot()
            if self._active:
                return 409, {"error": "node_update_already_running"}
            self._active = True
            self._status = {
                "node_id": self.node_id,
                "operation_id": operation,
                "state": "accepted", "phase": "queued",
                "message": "update_queued",
                "started_at": int(time.time()),
                "manual_restart_required": True,
            }
            self._save_status(self._status)
            thread = threading.Thread(target=self._execute, args=(operation,),
                                      name="ogl-live-update", daemon=True)
            thread.start()
            return 202, self.snapshot()

    def _execute(self, operation: str) -> None:
        lock_path = self.state_dir / "update.lock"
        try:
            with lock_path.open("a+b") as lock:
                fcntl.flock(lock.fileno(), fcntl.LOCK_EX | fcntl.LOCK_NB)
                with self._lock:
                    self._status.update(state="running", phase="git_build",
                                        message="updating_source_and_compiling")
                    self._save_status(self._status)
                log_path = self.state_dir / f"{operation}.log"
                with log_path.open("wb") as output:
                    os.chmod(log_path, 0o600)
                    # Args come solely from operator-owned service config, not from the API.
                    with subprocess.Popen(["bash", str(self.script),
                                           str(self.root), str(self.backup_dir)],
                                          cwd=str(self.root), stdout=output,
                                          stderr=subprocess.STDOUT, close_fds=True,
                                          start_new_session=True,
                                          env={**os.environ, "GIT_TERMINAL_PROMPT": "0"}) as process:
                        # Independent service has a bounded execution budget.
                        # A timed-out build must not leave orphan git/dotnet jobs.
                        timeout = min(14400, max(300, int(os.environ.get(
                            "OGL_UPDATER_TIMEOUT_SECONDS", "3600"))))
                        try:
                            returncode = process.wait(timeout=timeout)
                        except subprocess.TimeoutExpired:
                            try:
                                os.killpg(process.pid, signal.SIGKILL)
                            except ProcessLookupError:
                                pass
                            process.wait()
                            returncode = 124
                with self._lock:
                    self._status.update(
                        state="completed" if returncode == 0 else "failed",
                        phase="manual_restart_required" if returncode == 0 else "update_failed",
                        message=("build_completed_restart_simulator_manually"
                                 if returncode == 0 else "check_private_update_log"),
                        exit_code=returncode,
                        finished_at=int(time.time()))
                    self._save_status(self._status)
        except (OSError, subprocess.SubprocessError) as err:
            with self._lock:
                self._status.update(state="failed", phase="updater_error",
                                    message=type(err).__name__,
                                    finished_at=int(time.time()))
                self._save_status(self._status)
        finally:
            with self._lock:
                self._active = False


def handler_factory(service: UpdateService):
    class Handler(http.server.BaseHTTPRequestHandler):
        server_version = "OGL-Updater/1"
        def log_message(self, fmt, *args):
            # Never log signatures or bearer-like headers to stdout.
            sys.stderr.write("[OGL UPDATER] %s\\n" % (fmt % args))

        def reply(self, status: int, item: dict):
            payload = json.dumps(item, separators=(",", ":")).encode("utf-8")
            self.send_response(status)
            self.send_header("Content-Type", "application/json; charset=utf-8")
            self.send_header("Cache-Control", "no-store")
            self.send_header("X-Content-Type-Options", "nosniff")
            self.send_header("Content-Length", str(len(payload)))
            self.end_headers()
            self.wfile.write(payload)

        def do_GET(self):
            if self.path == "/healthz":
                return self.reply(200, {"service": "OGL Node Updater", "ready": True})
            if self.path != "/v1/status":
                return self.reply(404, {"error": "not_found"})
            try:
                timestamp = int(self.headers.get("X-OGL-Timestamp", ""))
            except ValueError:
                return self.reply(403, {"error": "unauthorized"})
            status = service.snapshot()
            operation = status.get("operation_id", "")
            signature = self.headers.get("X-OGL-Signature", "")
            if not operation or not verify_approval(service.node_id, operation,
                                                     timestamp, signature, service.key):
                return self.reply(403, {"error": "unauthorized"})
            return self.reply(200, status)

        def do_POST(self):
            if self.path != "/v1/update":
                return self.reply(404, {"error": "not_found"})
            try:
                size = int(self.headers.get("Content-Length", "-1"))
                if not 0 < size <= MAX_BODY:
                    return self.reply(413, {"error": "invalid_request_size"})
                data = json.loads(self.rfile.read(size))
                if not isinstance(data, dict) or set(data) != {
                    "node_id", "operation_id", "issued_at", "approval_hmac"}:
                    return self.reply(400, {"error": "invalid_fields"})
                if data["node_id"] != service.node_id or not isinstance(data["issued_at"], int):
                    return self.reply(403, {"error": "invalid_node"})
                code, status = service.submit(
                    data["operation_id"], data["issued_at"], data["approval_hmac"])
                return self.reply(code, status)
            except (ValueError, TypeError, UnicodeError):
                return self.reply(400, {"error": "invalid_json"})

    return Handler


def main() -> int:
    node_id = os.environ.get("OGL_UPDATER_NODE_ID", "")
    bin_dir = Path(os.environ.get("OGL_UPDATER_BIN", "/opt/robust/bin"))
    service_dir = Path(__file__).resolve().parent
    script = service_dir / "ogl-node-live-update.sh"
    state_dir = Path(os.environ.get("OGL_UPDATER_STATE", "/var/lib/opengenesislink-updater"))
    backup_dir = Path(os.environ.get("OGL_UPDATER_BACKUP_DIR", "/opt/ogl-backups"))
    key = os.environ.get("NEXVERSE_UPDATE_CONTROL_KEY", "")
    host = os.environ.get("OGL_UPDATER_BIND", "127.0.0.1")
    port = int(os.environ.get("OGL_UPDATER_PORT", "8097"))
    if not ipaddress.ip_address(host).is_loopback:
        raise SystemExit("Updater must bind only to loopback")
    if os.environ.get("OGL_UPDATER_ENABLE", "0") != "1":
        raise SystemExit("Updater requires explicit OGL_UPDATER_ENABLE=1")
    service = UpdateService(node_id=node_id, bin_dir=bin_dir, script=script,
                            state_dir=state_dir, backup_dir=backup_dir, key=key)
    server = http.server.ThreadingHTTPServer((host, port), handler_factory(service))
    server.daemon_threads = True
    print(f"[OGL UPDATER] node={node_id} bind={host}:{port} root={service.root}", flush=True)
    server.serve_forever()
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
