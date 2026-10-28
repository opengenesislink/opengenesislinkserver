#!/bin/sh
set -e

case "$1" in

 'clean')
    dotnet bin/prebuild.dll /file prebuild.xml /clean

  ;;


  'autoclean')

    echo y|dotnet bin/prebuild.dll /file prebuild.xml /clean

  ;;



  *)

    cp bin/System.Drawing.Common.dll.linux bin/System.Drawing.Common.dll
    dotnet bin/prebuild.dll /target vs2022 /targetframework net8_0 /excludedir = "obj | bin" /file prebuild.xml
    # Prebuild regenerates OpenSim.sln from scratch. Keep SDK-style city
    # projects in the actual solution, not as separate manual build commands.
    # This also makes a plain 'dotnet build OpenSim.sln' compile their DLLs.
    dotnet sln OpenSim.sln add --in-root \
        addons/NexTraffic/OpenGenesisLINK.Addons.NexTraffic.csproj \
        addons/NexTransit/OpenGenesisLINK.Addons.NexTransit.csproj \
        addons/NexSmartLight/OpenGenesisLINK.Addons.NexSmartLight.csproj
    printf '%s\n' \
        '#!/bin/sh' 'set -eu' \
        'dotnet build -c Release OpenSim.sln' \
        'for addon in NexTraffic NexTransit NexSmartLight; do' \
        '    test -s "bin/OpenGenesisLINK.Addons.${addon}.dll" || { echo "Missing city addon DLL: ${addon}" >&2; exit 1; }' \
        'done' \
        'bash addon-modules/OGLAssetService/build.sh Release' \
        'bash addon-modules/OGLWeather/build.sh Release' > compile.sh
    chmod +x compile.sh

  ;;

esac
