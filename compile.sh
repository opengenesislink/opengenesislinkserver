#!/bin/sh
set -eu

# Normal Generation-1 server build includes OGLWeather and NexCity modules
# via OpenSim.sln. No additional manual addon builds necessary.
dotnet build -c Release OpenSim.sln
for addon in OGLWeather NexTraffic NexTransit NexSmartLight; do
    test -s "bin/OpenGenesisLINK.Addons.${addon}.dll" || {
        echo "Missing built addon: ${addon}" >&2
        exit 1
    }
done
