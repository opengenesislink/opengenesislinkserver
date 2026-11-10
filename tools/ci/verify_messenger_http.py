#!/usr/bin/env python3
"""Smoke test: unconfigured Messenger must fail closed but serve the PWA shell."""
import os
import pathlib
import subprocess
import time
import tempfile
import urllib.error
import urllib.request

root = pathlib.Path(__file__).resolve().parents[2]
project = root / "OpenSim" / "Addons" / "OGLMessenger" / "Server"
port = 54579
env = dict(os.environ)
env["ASPNETCORE_URLS"] = f"http://127.0.0.1:{port}"
env["ASPNETCORE_ENVIRONMENT"] = "Production"
env.pop("Messenger__WorldApiBaseUrl", None)
env.pop("Messenger__PublicBaseUrl", None)
env.pop("Messenger__ClientId", None)
output = tempfile.TemporaryFile(mode="w+t")
process = subprocess.Popen(
    ["dotnet", "run", "--no-build", "--configuration", "Release",
     "--no-launch-profile", "--project", "OGLMessenger.Server.csproj",
     "--", "--urls", f"http://127.0.0.1:{port}"],
    cwd=project, env=env,
    stdout=output, stderr=subprocess.STDOUT,
)
def get(route):
    try:
        with urllib.request.urlopen(
            f"http://127.0.0.1:{port}{route}", timeout=2) as res:
            return res.status, res.read().decode("utf-8")
    except urllib.error.HTTPError as error:
        return error.code, error.read().decode("utf-8")

try:
    for _ in range(100):
        if process.poll() is not None:
            output.seek(0)
            raise RuntimeError("Messenger failed to start: " + output.read()[-3000:])
        try:
            status, body = get("/healthz")
            if status == 200:
                break
        except Exception:
            pass
        time.sleep(.15)
    else:
        output.seek(0)
        raise RuntimeError("Messenger health endpoint did not start: " + output.read()[-3000:])

    assert get("/")[0] == 200, "PWA shell is not served"
    assert "NexVerse Messenger" in get("/")[1], "PWA wrong static root"
    assert get("/manifest.webmanifest")[0] == 200
    assert get("/app.js")[0] == 200
    assert get("/api/v1/auth/me")[0] == 503, "anonymous user API must fail closed"
    assert get("/auth/start")[0] == 503, "OAuth must require operator configuration"
    print("PASS: Messenger static PWA, health, production fail-closed authentication")
finally:
    process.terminate()
    try:
        process.wait(timeout=10)
    except subprocess.TimeoutExpired:
        process.kill()
        process.wait()
    output.close()
