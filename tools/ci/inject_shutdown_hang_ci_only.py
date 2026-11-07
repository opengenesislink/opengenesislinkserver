#!/usr/bin/env python3
"""CI-only: inject a deliberately blocked Scene.Close() for shutdown fault testing.

This rewrites the *ephemeral GitHub Actions checkout only*, AFTER normal smoke.
Do not run on a live host. Production source and PR diff contain no hang.
"""
import os
from pathlib import Path

if os.environ.get("GITHUB_ACTIONS") != "true" or os.environ.get("OGL_CI_INJECT_HANG") != "1":
    raise SystemExit("Refusing fault injection outside explicit GitHub Actions CI environment")

source = (Path(__file__).resolve().parents[2] /
          "OpenSim/Region/Framework/Scenes/Scene.cs")
before = source.read_text()
target = """        public override void Close()
        {
            if (Interlocked.Exchange(ref m_oglCloseStarted, 1) != 0)"""
injected = """        public override void Close()
        {
            // TEMPORARY CI FAULT INJECTION: not committed to simulator code.
            if (Name == "OGL Shutdown CI 2")
            {
                m_log.Warn("[OGL SHUTDOWN CI]: Deliberately blocking region 2 close");
                System.Threading.Thread.Sleep(20000);
            }
            if (Interlocked.Exchange(ref m_oglCloseStarted, 1) != 0)"""
if before.count(target) != 1:
    raise SystemExit("Scene.Close method signature changed; abort fault injection")
source.write_text(before.replace(target, injected))
print("PASS: inserted test-only 20-second blocking close for OGL Shutdown CI 2")
