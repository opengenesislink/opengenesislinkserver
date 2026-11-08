#!/usr/bin/env python3
"""Regression: obsolete stand-alone launcher and 2007 Perl audit stay retired."""
from pathlib import Path
import subprocess

root = Path(__file__).resolve().parents[2]
paths = subprocess.check_output(["git", "ls-files"], cwd=root, text=True).splitlines()
retired = [
    path for path in paths
    if path.startswith("OpenSim/Tools/LaunchSLClient/")
    or path in ("bin/LaunchSLClient.ini", "OpenSim/Tools/classaudit.pl")
]
assert not retired, "Unexpected tracked legacy tooling: " + repr(retired)
for name in ("OpenSim.sln", "prebuild.xml"):
    contents = (root / name).read_text(encoding="utf-8")
    assert "LaunchSLClient" not in contents, f"{name} still references obsolete launcher"
    assert "pCampBot" not in contents, f"{name} regressed retired pCampBot"
assert (root / "OpenSim/Region/ScriptEngine/YEngine").is_dir()
assert (root / "OpenSim/Region/PhysicsModules/BulletS").is_dir()
assert (root / "OpenSim/Region/CoreModules/World/Warp3DMap").is_dir()
assert (root / "doc/NexVerse/LEGACY_COMPONENTS.md").exists()
print("OGL G1 legacy launcher, presets and classaudit cleanup: PASS")
