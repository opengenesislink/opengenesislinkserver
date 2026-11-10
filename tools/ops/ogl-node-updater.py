#!/usr/bin/env python3
# SPDX-License-Identifier: MPL-2.0
"""Independent, loopback-only OGL Generation 1 live update supervisor.

Install this program and its shell payload outside the checkout/bin directory.
Never accepts an arbitrary command, path, branch, git URL or HTTP target.
"""
import hmac
import json
import os
from pathlib import Path
import re
import subprocess
import sys
import threading
import time
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from urllib.parse import urlsplit
from uuid import UUID

NODE_PATTERN = re.compile(r"^[A-Za-z0-9][A-Za-z0-9._-]{0,63}$")
PHASE_PATTERN = re.compile(r"^\[([1-7])/7\]")
TOKEN = os.environ.get("OGL_UPDATER_TOKEN", "")
NODE_ID = os.environ.get("OGL_UPDATER_NODE_ID", "")
PORT = int(os.environ.get("OGL_UPDATER_PORT", "19090"))
BASE = Path(os.environ.get("OGL_UPDATER_REPO_ROOT", "/opt/robust"))
BACKUPS = Path(os.environ.get("OGL_UPDATER_BACKUP_ROOT", "/opt/ogl-backups"))
STATE_DIR = Path(os.environ.get("OGL_UPDATER_STATE_DIR", "/var/lib/ogl-node-updater"))
SCRIPT = Path(__file__).resolve().with_name("ogl-node-update-live.sh")
LOCK = threading.RLock()
STATE_FILE = STATE_DIR / "current.json"
STATUS = {"operation_id": "", "node_id": "", "state": "idle",
          "phase": 0, "message": "no_update", "updated_at": 0}


def check_paths() -> None:
    """The repository root MUST be the parent of the directory with OpenSim.dll."""
    if len(TOKEN.encode("utf-8")) < 32 or not NODE_PATTERN.fullmatch(NODE_ID):
        raise RuntimeError("Missing or unsafe updater token / node ID.")
    if not 1 <= PORT <= 65535:
        raise RuntimeError("Invalid loopback port.")
    for path in (BASE, BACKUPS, STATE_DIR):
        if path.is_symlink() or str(path) == "/":
            raise RuntimeError("Root or symlink path is forbidden.")
    live = BASE.resolve(strict=True)
    binary = live / "bin"
    if binary.is_symlink() or not binary.is_dir() or not (binary / "OpenSim.dll").is_file():
        raise RuntimeError("Expected <checkout>/bin/OpenSim.dll not found.")
    if not (live / ".git").is_dir() or not SCRIPT.is_file():
        raise RuntimeError("Trusted Git checkout or installed update payload missing.")
    backup = BACKUPS.resolve()
    state = STATE_DIR.resolve()
    if (backup == live or live in backup.parents or
            state == live or live in state.parents or
            backup in live.parents or state in live.parents):
        raise RuntimeError("Updater storage cannot overlap the live checkout.")
    if (SCRIPT == live or live in SCRIPT.parents):
        raise RuntimeError("Updater executable must live outside the checkout.")
    BACKUPS.mkdir(parents=True, exist_ok=True, mode=0o700)
    STATE_DIR.mkdir(parents=True, exist_ok=True, mode=0o700)
    os.chmod(STATE_DIR, 0o700)


def persist() -> None:
    payload = json.dumps(STATUS, sort_keys=True)
    temp = STATE_DIR / ("current." + str(os.getpid()) + ".tmp")
    with temp.open("w", encoding="utf-8") as handle:
        os.chmod(temp, 0o600)
        handle.write(payload)
        handle.flush()
        os.fsync(handle.fileno())
    temp.replace(STATE_FILE)


def change(**updates) -> None:
    with LOCK:
        STATUS.update(updates)
        STATUS["updated_at"] = int(time.time())
        persist()


def start(operation_id: str, node_id: str):
    with LOCK:
        if STATUS["operation_id"] == operation_id:
            return (202 if STATUS["state"] in ("queued", "running") else 200,
                    dict(STATUS))
        if STATUS["state"] in ("queued", "running"):
            return (409, {"error": "update_already_running"})
        STATUS.update({"operation_id": operation_id, "node_id": node_id,
                       "state": "queued", "phase": 0, "message": "waiting_for_worker",
                       "updated_at": int(time.time())})
        persist()
        worker = threading.Thread(target=run_update, args=(operation_id,),
                                  daemon=True, name="ogl-live-updater")
        worker.start()
        return (202, dict(STATUS))


def run_update(operation_id: str) -> None:
    log_path = STATE_DIR / ("update-" + operation_id + ".log")
    try:
        change(state="running", message="preflight")
        with log_path.open("a", encoding="utf-8") as logfile:
            os.chmod(log_path, 0o600)
            process = subprocess.Popen(
                ["bash", str(SCRIPT), str(BASE.resolve()), str(BACKUPS.resolve())],
                cwd="/", stdin=subprocess.DEVNULL, stdout=subprocess.PIPE,
                stderr=subprocess.STDOUT, text=True, bufsize=1,
                env={**os.environ, "GIT_TERMINAL_PROMPT": "0"},
                start_new_session=True)
            if process.stdout is None:
                raise RuntimeError("Updater output stream missing.")
            for line in process.stdout:
                logfile.write(line)
                logfile.flush()
                match = PHASE_PATTERN.match(line)
                if match:
                    change(phase=int(match.group(1)), message="step_" + match.group(1))
            ret = process.wait()
        if ret == 0:
            change(state="completed", phase=7,
                   message="update_completed_manual_simulator_restart_required")
        else:
            change(state="failed", message="update_script_exit_" + str(ret))
    except Exception as exc:
        # Do not leak secrets or shell output to callers.
        print("OGL Updater worker failed:", type(exc).__name__, file=sys.stderr)
        change(state="failed", message="internal_updater_error")


class Handler(BaseHTTPRequestHandler):
    server_version = "OGLNodeUpdater/1.0"
    sys_version = ""

    def log_message(self, format, *args):
        # Do not log HTTP Authorization headers or user-supplied bodies.
        print("[OGL UPDATER] " + format % args, file=sys.stderr)

    def reply(self, status: int, payload: dict) -> None:
        body = json.dumps(payload).encode("utf-8")
        self.send_response(status)
        self.send_header("Content-Type", "application/json; charset=utf-8")
        self.send_header("Cache-Control", "no-store")
        self.send_header("X-Content-Type-Options", "nosniff")
        self.send_header("Content-Length", str(len(body)))
        self.end_headers()
        self.wfile.write(body)

    def authenticate(self) -> bool:
        # Defense in depth: HTTP listener itself binds only to 127.0.0.1.
        if self.client_address[0] != "127.0.0.1":
            self.reply(403, {"error": "loopback_only"})
            return False
        authorization = self.headers.get("Authorization", "")
        expected = "Bearer " + TOKEN
        if len(authorization) > 512 or not hmac.compare_digest(
                authorization.encode("utf-8"), expected.encode("utf-8")):
            self.reply(401, {"error": "unauthorized"})
            return False
        return True

    def do_GET(self):
        if not self.authenticate():
            return
        url = urlsplit(self.path)
        if url.path == "/v1/status" and not url.query:
            with LOCK:
                self.reply(200, dict(STATUS))
            return
        self.reply(404, {"error": "not_found"})

    def do_POST(self):
        if not self.authenticate():
            return
        if urlsplit(self.path).path != "/v1/updates" or urlsplit(self.path).query:
            self.reply(404, {"error": "not_found"})
            return
        try:
            size = int(self.headers.get("Content-Length", "0"))
            if size < 1 or size > 4096:
                self.reply(413, {"error": "invalid_request_length"})
                return
            request = json.loads(self.rfile.read(size))
            if (not isinstance(request, dict) or
                    set(request) != {"node_id", "operation_id"} or
                    request["node_id"] != NODE_ID):
                self.reply(400, {"error": "invalid_request"})
                return
            operation_id = str(UUID(request["operation_id"]))
            if operation_id == "00000000-0000-0000-0000-000000000000":
                raise ValueError()
        except (ValueError, TypeError, UnicodeDecodeError, json.JSONDecodeError, KeyError):
            self.reply(400, {"error": "invalid_request"})
            return
        status, payload = start(operation_id, NODE_ID)
        self.reply(status, payload)


def main() -> None:
    check_paths()
    if STATE_FILE.exists():
        try:
            previous = json.loads(STATE_FILE.read_text(encoding="utf-8"))
            if previous.get("state") in ("running", "queued"):
                STATUS.update(previous)
                change(state="interrupted", message="supervisor_restarted_check_private_log")
            else:
                STATUS.update(previous)
        except (ValueError, OSError):
            change(state="failed", message="invalid_previous_status")
    server = ThreadingHTTPServer(("127.0.0.1", PORT), Handler)
    print("[OGL UPDATER] Ready on loopback for node " + NODE_ID, flush=True)
    try:
        server.serve_forever(poll_interval=0.5)
    finally:
        server.server_close()


if __name__ == "__main__":
    main()
