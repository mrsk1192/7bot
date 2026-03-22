[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$GameDir
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$modRoot = Join-Path $repoRoot "mod\mnetSevenDaysBridge"
$projectPath = Join-Path $modRoot "src\mnetSevenDaysBridge.csproj"
$managedDir = Join-Path $GameDir "7DaysToDie_Data\Managed"
$stageDir = Join-Path $modRoot "build\mnetSevenDaysBridge"
$dllOutput = Join-Path $modRoot "build\bin\net48\mnetSevenDaysBridge.dll"

if (-not (Test-Path $managedDir)) {
    throw "Managed DLL directory not found: $managedDir"
}

dotnet build $projectPath -c Release /p:SevenDaysManagedDir="$managedDir"
if ($LASTEXITCODE -ne 0) {
    throw "dotnet build failed with exit code $LASTEXITCODE"
}

New-Item -ItemType Directory -Force -Path $stageDir | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $stageDir "Config") | Out-Null

Copy-Item (Join-Path $modRoot "ModInfo.xml") (Join-Path $stageDir "ModInfo.xml") -Force
Copy-Item (Join-Path $modRoot "Config\bridge_config.json") (Join-Path $stageDir "Config\bridge_config.json") -Force
Copy-Item $dllOutput (Join-Path $stageDir "mnetSevenDaysBridge.dll") -Force

Write-Host "Build completed."
Write-Host "Staged mod path: $stageDir"
