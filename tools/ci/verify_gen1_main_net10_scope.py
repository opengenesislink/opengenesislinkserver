#!/usr/bin/env python3
"""Gate the Generation 1 main .NET 10 upgrade to framework/build/runtime only."""
from pathlib import Path
import json

root = Path(__file__).resolve().parents[2]
core = (root / "OpenSim/Region/CoreModules/OpenSim.Region.CoreModules.csproj").read_text()
assert "<TargetFramework>net10.0</TargetFramework>" in core
assert "OglImageCodecEngine.cs" not in core, "G11-02 codec accidentally included"
assert "OglOpenJpeg2CodecProvider.cs" not in core, "G11-02 codec accidentally included"

for path in (
    "OpenSim/Region/CoreModules/Agent/TextureSender/OglImageCodecEngine.cs",
    "OpenSim/Region/CoreModules/Agent/TextureSender/OglOpenJpeg2CodecProvider.cs",
    "addon-modules/OGLImageCodec",
    "tools/ci/OglImageCodecRegression",
):
    assert not (root / path).exists(), "Generation 1.1 optional codec must stay out of main: " + path

assert json.loads((root / "global.json").read_text())["sdk"]["version"].startswith("10.0.")
for name in ("OpenSim", "Robust"):
    obj = json.loads((root / "bin" / (name + ".runtimeconfig.json")).read_text())["runtimeOptions"]
    assert obj["tfm"] == "net10.0" and obj["framework"]["version"].startswith("10.")

for addon in ("NexTraffic", "NexTransit", "NexSmartLight", "OGLWeather", "OGLEnergy"):
    project = root / "addons" / addon / ("OpenGenesisLINK.Addons." + addon + ".csproj")
    assert "<TargetFramework>net10.0</TargetFramework>" in project.read_text(), addon

main_ci = (root / ".github/workflows/nexjast-ci.yml").read_text()
assert main_ci.count("dotnet-version: '10.0.x'") == 2
assert "dotnet-version: '8.0.x'" not in main_ci
assert "Release/net8.0/" not in main_ci
for workflow in ("gen11-net10-build.yml", "gen11-net10-arm64.yml"):
    data = (root / ".github/workflows" / workflow).read_text()
    assert "branches: [main]" in data or "- main" in data
    assert "dotnet-version: '10.0.x'" in data

for publisher in (root / ".github/workflows").glob("publish-*.yml"):
    data = publisher.read_text()
    assert "vars.OGL_GEN1_NET10_RELEASE_APPROVED == 'true'" in data, (
        "Automatic release cannot be enabled until integration tests are approved: " + publisher.name
    )
assert '"runtime": ".NET 10"' in (root / "tools/release/ogl_gen1_snapshot_zip.py").read_text()
print("PASS: main scoped to .NET 10 build/runtime; experimental codec excluded; release workflows held.")
