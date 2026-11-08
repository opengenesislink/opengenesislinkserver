#!/usr/bin/env python3
"""Keep legacy OpenSim-compatible GridInfo routes branded OpenGenesisLINK."""
from pathlib import Path
import re

source = Path("OpenSim/Server/Handlers/Grid/GridInfoHandlers.cs").read_text(encoding="utf-8")
connector = Path("OpenSim/Server/Handlers/Grid/GridInfoServerInConnector.cs").read_text(encoding="utf-8")
mark = '_info["platform"] = "OpenGenesisLINK";'
assert source.count(mark) == 1, "GridInfo platform must be set to OpenGenesisLINK once"
assert '_info["platform"] = "OpenSim";' not in source, "Legacy OpenSim brand reappeared"
assert source.index(mark) > source.index('IConfig gridCfg = configSource.Configs["GridInfoService"];'), (
    "Operator GridInfoService settings may not override the OGL identity"
)
for method in ("XmlRpcGridInfoMethod", "RestGetGridInfoMethod", "JsonGetGridInfoMethod"):
    assert method in source, method
assert source.count("foreach (KeyValuePair<string, string> k in _info)") >= 2
assert re.search(r"foreach \(KeyValuePair<string, string>\s+k in _info\)", source)
assert '"/get_grid_info"' in connector and '"/json_grid_info"' in connector
assert 'clock = BuildViewerClock()' in source, "Do not disturb OGL viewer clock"
assert 'org.opengenesislink.viewer.grid-info.v1' in source, "Do not disturb OGL native discovery"
print("OGL GridInfo platform identity and compatibility guard: PASS")
