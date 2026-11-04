@echo OFF

copy bin\System.Drawing.Common.dll.win bin\System.Drawing.Common.dll

dotnet bin\prebuild.dll /target vs2022 /targetframework net8_0 /excludedir = "obj | bin" /file prebuild.xml
if errorlevel 1 exit /b 1
rem The bundled generator cannot emit net10_0 yet: retarget generated projects.
powershell -NoProfile -ExecutionPolicy Bypass -File tools\ci\gen11_retarget_generated_csproj.ps1
if errorlevel 1 exit /b 1
powershell -NoProfile -ExecutionPolicy Bypass -File tools\ci\gen11_retarget_generated_csproj.ps1 -Check
if errorlevel 1 exit /b 1
rem Restore the independent city addon projects after solution regeneration.
dotnet sln OpenSim.sln add --in-root addons/NexTraffic/OpenGenesisLINK.Addons.NexTraffic.csproj addons/NexTransit/OpenGenesisLINK.Addons.NexTransit.csproj addons/NexSmartLight/OpenGenesisLINK.Addons.NexSmartLight.csproj addons/OGLWeather/OpenGenesisLINK.Addons.OGLWeather.csproj addons/OGLEnergy/OpenGenesisLINK.Addons.OGLEnergy.csproj
if errorlevel 1 exit /b 1

    @echo Creating compile.bat
rem To compile in release mode
    @echo dotnet build --configuration Release OpenSim.sln > compile.bat
rem To compile in debug mode comment line (add rem to start) above and uncomment next (remove rem)
rem    @echo dotnet build --configuration Debug OpenSim.sln > compile.bat
:done


if exist "bin\addin-db-002" (
	del /F/Q/S bin\addin-db-002 > NUL
	rmdir /Q/S bin\addin-db-002
	)
if exist "bin\addin-db-004" (
	del /F/Q/S bin\addin-db-004 > NUL
	rmdir /Q/S bin\addin-db-004
	)	