#!/usr/bin/env python3
"""Guard the deliberate G1 removal of inherited pCampBot load-testing binary."""
from pathlib import Path
import subprocess
import xml.etree.ElementTree as ET

root = Path(__file__).resolve().parents[2]
paths = subprocess.check_output(["git", "ls-files"], cwd=root, text=True).splitlines()
removed_prefixes = ("OpenSim/Tools/pCampBot/", "bin/pCampBot")
leftovers = [name for name in paths if name.startswith(removed_prefixes)]
assert not leftovers, "pCampBot files are still tracked: " + repr(leftovers)

prebuild = (root / "prebuild.xml").read_text(encoding="utf-8")
solution = (root / "OpenSim.sln").read_text(encoding="utf-8")
assert "pCampBot" not in prebuild, "Old bot still generated in prebuild"
assert "pCampBot" not in solution, "Old bot still included in .NET solution"
assert "28E92A53-0000-0000-0000-000000000000" not in solution, "Dangling bot GUID"
ET.fromstring(prebuild)  # Preserve valid prebuild XML after removing the project.

# Keep productive simulator runtime dependencies and dedicated NexVerse tools.
for required in (
    "OpenSim.Region.ScriptEngine.YEngine",
    "OpenSim.Region.CoreModules",
    "NexVerse.Core",
    "NexVerse.RegionModules",
    "OpenSim.Tools.Configger",
):
    assert required in prebuild, f"Required project lost from prebuild: {required}"
print("OGL G1 retired pCampBot source/build/binaries and preserved required modules: OK")
