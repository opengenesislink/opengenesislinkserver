#!/usr/bin/env python3
"""Offline integration smoke: launch multiple real OpenSim regions and close via console.

This uses isolated SQLite files and a temporary regions directory; no production
grid, NAS, database or credentials are contacted.
"""
from __future__ import annotations
import argparse
import os
from pathlib import Path
import pty
import re
import select
import shutil
import signal
import subprocess
import tempfile
import time
import uuid

ROOT = Path(__file__).resolve().parents[2]
BIN = ROOT / "bin"
COMMON = BIN / "config-include" / "StandaloneCommon.ini"
COMMON_TEMPLATE = ROOT / "tools/ci/NexLoginPlacement/StandaloneCommon.ini"
INI_TEMPLATE = BIN / "OpenSim.NexVerseLoginPlacement.Tests.ini"
REGION_TEMPLATE = ROOT / "tools/ci/NexLoginPlacement/Regions/NexVerse-CI.ini"
ESTATES = ROOT / "tools/ci/NexLoginPlacement/Estates"
def run_smoke(workdir: Path, region_count: int, expect_timeout: bool = False) -> None:
    names = [f"OGL Shutdown CI {n}" for n in range(1, region_count + 1)]
    orig_common = COMMON.read_bytes() if COMMON.exists() else None
    state = workdir / "state"
    regions = workdir / "regions"
    regions.mkdir()
    for i, name in enumerate(names, 1):
        code = REGION_TEMPLATE.read_text()
        code = code.replace("NexVerse CI Landing", name)
        code = code.replace(
            "1c6aa9e1-6cef-4c01-a4e4-6b4d19449ef1",
            str(uuid.uuid5(uuid.NAMESPACE_DNS, "ogl-shutdown-ci-" + str(i))),
        )
        code = code.replace("Location = 1200,1200", f"Location = {1200 + i},1200")
        code = code.replace("InternalPort = 19101", f"InternalPort = {19210 + i}")
        (regions / f"shutdown-{i}.ini").write_text(code)

    ini = INI_TEMPLATE.read_text()
    ini = ini.replace("background = true", "background = false")
    ini = ini.replace('regionload_regionsdir = "../tools/ci/NexLoginPlacement/Regions"',
                      f'regionload_regionsdir = "{regions}"')
    ini = ini.replace('regionload_estatesdir = "../tools/ci/NexLoginPlacement/Estates"',
                      f'regionload_estatesdir = "{ESTATES}"')
    ini = ini.replace("19100", "19210").replace("19102", "19212")
    ini += ("\n[OGLShutdown]\nRegionCloseTimeoutSeconds = "
            + ("8" if expect_timeout else "30") + "\n")
    # OpenSim resolves includes relative to its own bin/config directory.
    config = BIN / "OpenSim.OglShutdownSmoke.Tests.ini"
    config.write_text(ini)

    proc = None
    master = None
    logs = ""
    try:
        COMMON.write_text(COMMON_TEMPLATE.read_text().replace(
            "/tmp/nexverse-login-placement", str(state)))
        master, slave = pty.openpty()
        env = dict(os.environ, TERM="xterm")
        try:
            proc = subprocess.Popen(
                ["dotnet", "OpenSim.dll", "-inifile", config.name],
                cwd=BIN, stdin=slave, stdout=slave, stderr=slave,
                start_new_session=True, close_fds=True, env=env,
            )
        finally:
            os.close(slave)

        def capture(seconds: float, stop_if=None):
            nonlocal logs
            end = time.monotonic() + seconds
            while time.monotonic() < end:
                if proc.poll() is not None:
                    break
                ready, _, _ = select.select([master], [], [], 0.2)
                if ready:
                    try:
                        data = os.read(master, 65536)
                    except OSError:
                        break
                    if not data:
                        break
                    logs += data.decode("utf-8", "replace")
                    if stop_if and stop_if(logs):
                        return
                elif stop_if and stop_if(logs):
                    return

        capture(max(120, region_count * 20), lambda output: all(
            "INITIALIZATION COMPLETE FOR " + name in output for name in names))
        if not all("INITIALIZATION COMPLETE FOR " + name in logs for name in names):
            raise AssertionError(f"Simulator did not initialize all {region_count} shutdown regions")

        # Give the foreground console time to open its interactive prompt.
        capture(1)
        os.write(master, b"shutdown\r")
        capture(max(90, region_count * 8))
        # Environment.Exit closes the PTY before Popen.poll() necessarily sees
        # the process exit. Reap the process instead of racing its final close.
        try:
            proc.wait(timeout=10)
        except subprocess.TimeoutExpired as exc:
            raise AssertionError(
                "All region close callbacks may have finished, but OpenSim process did not exit"
            ) from exc
        # Region log output is ANSI-colored when the simulator owns a PTY.
        plain_logs = re.sub(r"\\x1b\\[[0-9;?]*[A-Za-z]", "", logs)
        if expect_timeout:
            if proc.returncode == 0:
                raise AssertionError("Timed-out region incorrectly reported successful shutdown")
            if "[OGL SHUTDOWN]: TIMEOUT" not in plain_logs or "OGL Shutdown CI 2" not in plain_logs:
                raise AssertionError("Expected stuck region timeout log not found")
            if f"Region OGL Shutdown CI 1 closed in" not in logs or (
                    f"Region OGL Shutdown CI 3 closed in" not in logs):
                raise AssertionError("Other regions did not finish closing after one timeout")
            if "region close operations failed or timed out" not in plain_logs:
                raise AssertionError("Nonzero failure summary not found")
            print("PASS: injected blocked region reported timeout, other regions"
                  " closed, OpenSim exited nonzero")
        else:
            if proc.returncode != 0:
                raise AssertionError(f"Simulator exit status was {proc.returncode}")
            for name in names:
                if f"Region {name} closed in" not in logs:
                    raise AssertionError("Missing clean close confirmation for " + name)
            if f"All {region_count} region close operations completed" not in logs:
                raise AssertionError("Missing complete shutdown summary")
            print(f"PASS: {region_count} real regions started, console shutdown completed, exit code 0")
    except Exception:
        print("----- Shutdown simulator output (last 160 lines) -----")
        print("\n".join(logs.splitlines()[-160:]))
        raise
    finally:
        if proc is not None and proc.poll() is None:
            try:
                os.killpg(proc.pid, signal.SIGKILL)
            except ProcessLookupError:
                pass
        if proc is not None:
            proc.wait(timeout=10)
        if master is not None:
            os.close(master)
        if orig_common is None:
            COMMON.unlink(missing_ok=True)
        else:
            COMMON.write_bytes(orig_common)
        config.unlink(missing_ok=True)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--regions", type=int, default=3, help="number of test regions (1..42)")
    parser.add_argument("--expect-timeout", action="store_true",
                        help="expect CI-injected region 2 close hang and nonzero exit")
    args = parser.parse_args()
    if args.expect_timeout and args.regions < 3:
        parser.error("--expect-timeout requires at least three regions")
    if not (1 <= args.regions <= 42):
        parser.error("--regions must be between 1 and 42")
    with tempfile.TemporaryDirectory(prefix="ogl-g1-shutdown-ci-") as path:
        run_smoke(Path(path), args.regions, args.expect_timeout)


if __name__ == "__main__":
    main()
