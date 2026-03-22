[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$GameDir
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$buildRoot = Join-Path $repoRoot "mod\mnetSevenDaysBridge\build\mnetSevenDaysBridge"
$targetRoot = Join-Path $GameDir "Mods\mnetSevenDaysBridge"

if (-not (Test-Path $buildRoot)) {
    throw "Staged mod directory not found. Run tools/build_mod.ps1 first: $buildRoot"
}

Remove-Item $targetRoot -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force -Path $targetRoot | Out-Null
Copy-Item (Join-Path $buildRoot "*") $targetRoot -Recurse -Force

Write-Host "Copied staged mod into game Mods directory."
Write-Host "Target path: $targetRoot"
