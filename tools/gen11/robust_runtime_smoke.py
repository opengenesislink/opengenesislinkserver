#!/usr/bin/env python3
"""Offline .NET 10 ROBUST process/HTTP startup smoke, no database or live grid.

Runs the actual rebuilt Robust.dll with an isolated temporary INI, no service
connectors, and a loopback probe. Never uses checked-in active configurations.
This is a runtime gate, not a MariaDB/Hypergrid/full-service integration test.
"""
from __future__ import annotations

import argparse
import json
import os
import pathlib
import signal
import socket
import subprocess
import sys
import tempfile
import time
import urllib.error
import urllib.request

ROOT = pathlib.Path(__file__).resolve().parents[2]
BIN = ROOT / "bin"


def assert_net10_config(path: pathlib.Path) -> None:
    data = json.loads(path.read_text(encoding="utf-8-sig"))["runtimeOptions"]
    if data.get("tfm") != "net10.0" or not str(data.get("framework", {}).get("version", "")).startswith("10."):
        raise RuntimeError(f"Invalid .NET 10 runtime configuration: {path.name}")


def choose_port() -> int:
    with socket.socket(socket.AF_INET, socket.SOCK_STREAM) as sock:
        sock.bind(("127.0.0.1", 0))
        return sock.getsockname()[1]


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--bin", type=pathlib.Path, default=BIN)
    parser.add_argument("--deadline", type=float, default=35)
    args = parser.parse_args()
    bin_dir = args.bin.resolve()
    for name in ("Robust.dll", "Robust.runtimeconfig.json", "OpenSim.dll", "OpenSim.runtimeconfig.json"):
        if not (bin_dir / name).is_file():
            raise RuntimeError(f"Missing built .NET 10 host: {name}")
    assert_net10_config(bin_dir / "Robust.runtimeconfig.json")
    assert_net10_config(bin_dir / "OpenSim.runtimeconfig.json")

    runtime_list = subprocess.run(["dotnet", "--list-runtimes"], capture_output=True, text=True, check=True).stdout
    if not any(line.startswith("Microsoft.NETCore.App 10.") for line in runtime_list.splitlines()):
        raise RuntimeError(".NET 10 runtime not installed")
    port = choose_port()

    with tempfile.TemporaryDirectory(prefix="ogl-gen11-runtime-") as temporary:
        temp = pathlib.Path(temporary)
        ini = temp / "robust-smoke.ini"
        ini.write_text(
            "[Startup]\n"
            "console = local\n"
            "NoVerifyCertChain = false\n"
            "NoVerifyCertHostname = false\n"
            "EnableRobustSelfsignedCertSupport = false\n"
            "RobustCertRenewOnStartup = false\n"
            "RegistryLocation = .\n"
            "PIDFile = \n"
            "ConsoleHistoryFileEnabled = false\n"
            "\n[ServiceList]\n"
            "\n[Network]\n"
            f"port = {port}\n"
            "https_main = false\n"
            "https_listener = false\n"
            "ConsolePort = 0\n"
            "\n[OGLAssetServiceAdmin]\n"
            "Enabled = false\n",
            encoding="utf-8",
        )
        logfile = temp / "robust.log"
        env = os.environ.copy()
        # Stay on .NET 10 but accept its latest security/servicing patch.
        env["DOTNET_ROLL_FORWARD"] = "LatestPatch"
        env["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1"
        with logfile.open("w+", encoding="utf-8", errors="replace") as log, subprocess.Popen(
            ["dotnet", "Robust.dll", "-inifile=" + str(ini)],
            cwd=bin_dir,
            stdin=subprocess.PIPE,
            stdout=log,
            stderr=subprocess.STDOUT,
            env=env,
            text=True,
        ) as process:
            deadline = time.monotonic() + args.deadline
            started = False
            try:
                while time.monotonic() < deadline:
                    code = process.poll()
                    if code is not None:
                        raise RuntimeError(f"ROBUST exited during startup (code {code})")
                    try:
                        with urllib.request.urlopen(
                            urllib.request.Request(
                                f"http://127.0.0.1:{port}/",
                                headers={"User-Agent": "OGL-Gen11-Net10-CI"},
                            ), timeout=1.2
                        ) as response:
                            if 100 <= response.status <= 599:
                                started = True
                                break
                    except urllib.error.HTTPError:
                        # 404 is expected with no service connectors.
                        started = True
                        break
                    except (urllib.error.URLError, TimeoutError, ConnectionError):
                        pass
                    time.sleep(0.4)
                if not started:
                    raise RuntimeError("ROBUST process alive but HTTP listener was not reachable")

                # Catch early crashes and prove it did not simply bind and exit.
                time.sleep(1.0)
                if process.poll() is not None:
                    raise RuntimeError("ROBUST exited after listener became reachable")
                print(f"PASS: .NET 10 ROBUST host started, HTTP listener accepted a loopback request (port {port}).")
                print("Offline isolated smoke only: no DB services, no real Login/HG/Viewer traffic.")
                return 0
            except Exception:
                log.flush()
                logfile_text = logfile.read_text(encoding="utf-8", errors="replace")
                print("ROBUST startup diagnostic (last 5000 chars):\n" + logfile_text[-5000:], file=sys.stderr)
                raise
            finally:
                if process.poll() is None:
                    process.terminate()
                    try:
                        process.wait(timeout=7)
                    except subprocess.TimeoutExpired:
                        process.kill()
                        process.wait(timeout=5)


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except Exception as exc:
        print(f"FAIL: .NET 10 ROBUST runtime smoke: {exc}", file=sys.stderr)
        raise SystemExit(1)
