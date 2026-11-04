# PowerShell 5+ equivalent of gen11_retarget_generated_csproj.py for Windows.
# The bundled Prebuild generator only knows framework IDs through net9_0.
param([switch] $Check)

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$tagPattern = '(<TargetFrameworks?>)([^<]*)(</TargetFrameworks?>)'
$oldTfmPattern = '(?<![A-Za-z0-9])net8\.0(?=\s*(?:;|$)|-)'
$scanned = 0
$converted = 0
$stale = @()

Get-ChildItem -LiteralPath $root -Filter '*.csproj' -File -Recurse | ForEach-Object {
    $file = $_
    $relative = $file.FullName.Substring($root.Length).TrimStart([char]'\', [char]'/')
    $parts = $relative -split '[\\/]'
    if ($parts | Where-Object { $_ -in @('obj', 'bin', '.git', 'Prebuild') }) {
        return
    }
    $scanned++
    $original = [System.IO.File]::ReadAllText($file.FullName)
    $updated = [System.Text.RegularExpressions.Regex]::Replace(
        $original, $tagPattern,
        [System.Text.RegularExpressions.MatchEvaluator] {
            param($match)
            $tfms = [System.Text.RegularExpressions.Regex]::Replace(
                $match.Groups[2].Value, $oldTfmPattern, 'net10.0')
            return $match.Groups[1].Value + $tfms + $match.Groups[3].Value
        })
    if ($updated -ne $original) {
        if ($Check) {
            $stale += $relative
        } else {
            [System.IO.File]::WriteAllText(
                $file.FullName, $updated, [System.Text.UTF8Encoding]::new($false))
            $converted++
        }
    }
}
if ($stale.Count) {
    $stale | ForEach-Object { Write-Error "Still targets net8.0: $_" }
    exit 1
}
Write-Host "Generation 1.1 .NET 10 TFM gate: $scanned projects inspected, $converted converted"
