# OpenGenesisLINK G1.1 native OpenJPEG2 shim on Windows x64.
# Requires MSVC/CMake and vcpkg; uses a static OpenJPEG library so the
# resulting optional shim DLL needs no separate libopenjp2.dll deployment.
param(
    [string] $BuildDirectory = "",
    [switch] $SkipVcpkgInstall
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$root = (Resolve-Path (Join-Path $PSScriptRoot "../..")).Path
if (-not [Environment]::Is64BitProcess -or -not $IsWindows) {
    throw "G1.1 codec native Windows build requires Windows x64 PowerShell 7"
}

$vcpkgRoot = $env:VCPKG_INSTALLATION_ROOT
if ([string]::IsNullOrWhiteSpace($vcpkgRoot)) {
    $vcpkgRoot = $env:VCPKG_ROOT
}
if ([string]::IsNullOrWhiteSpace($vcpkgRoot)) {
    $vcpkgRoot = "C:\vcpkg"
}
$vcpkgExe = Join-Path $vcpkgRoot "vcpkg.exe"
$toolchain = Join-Path $vcpkgRoot "scripts/buildsystems/vcpkg.cmake"
if (-not (Test-Path $vcpkgExe) -or -not (Test-Path $toolchain)) {
    throw "A vcpkg installation is required at $vcpkgRoot"
}

if (-not $SkipVcpkgInstall) {
    & $vcpkgExe install "openjpeg:x64-windows-static"
    if ($LASTEXITCODE -ne 0) { throw "vcpkg install openjpeg:x64-windows-static failed" }
}

if ([string]::IsNullOrWhiteSpace($BuildDirectory)) {
    $BuildDirectory = Join-Path $root "build/ogl-imagecodec-windows"
}
$source = Join-Path $root "addon-modules/OGLImageCodec/native"
& cmake -S $source -B $BuildDirectory -A x64 "-DCMAKE_TOOLCHAIN_FILE=$toolchain" "-DVCPKG_TARGET_TRIPLET=x64-windows-static"
if ($LASTEXITCODE -ne 0) { throw "Native OpenJPEG2 CMake configure failed" }

& cmake --build $BuildDirectory --config Release
if ($LASTEXITCODE -ne 0) { throw "Native OpenJPEG2 CMake compilation failed" }

$dll = Get-ChildItem -LiteralPath $BuildDirectory -Recurse -File -Filter "ogl-openjpeg2-x86_64.dll" |
    Where-Object { $_.Directory.Name -eq "Release" } | Select-Object -First 1
if ($null -eq $dll -or $dll.Length -le 0) {
    throw "Native OpenJPEG2 x64 DLL missing from release build"
}
$destination = Join-Path $root "bin/lib64"
New-Item -ItemType Directory -Force -Path $destination | Out-Null
Copy-Item -LiteralPath $dll.FullName -Destination (Join-Path $destination $dll.Name) -Force
Write-Host "G1.1 native OpenJPEG2 Windows x64: $($dll.Name) ($($dll.Length) bytes)"
