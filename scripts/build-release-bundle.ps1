param(
    [string]$RepoRoot = ""
)

$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "_env.ps1")
if ([string]::IsNullOrEmpty($RepoRoot)) { $RepoRoot = $NOLoaderRepoRoot }
Set-Location $RepoRoot

dotnet build (Join-Path $RepoRoot "RDYTU\NOLoader.RDYTU.sln") -c RDYTU --verbosity minimal
powershell -ExecutionPolicy Bypass -File (Join-Path $NOLoaderScriptsRoot "deploy-noloader.ps1") -Configuration RDYTU

$bundle = Join-Path $RepoRoot "artifacts\release\v0.1.0"
New-Item -ItemType Directory -Force -Path $bundle | Out-Null
$game = "C:\Program Files (x86)\Steam\steamapps\common\Nuclear Option"
Copy-Item -Recurse -Force (Join-Path $game "NOLoader") (Join-Path $bundle "NOLoader")
Copy-Item -Force (Join-Path $game "noloader_config.ini") $bundle
Copy-Item -Force (Join-Path $RepoRoot "docs\INSTALL.md") $bundle
Copy-Item -Force (Join-Path $RepoRoot "docs\MIGRATION.md") $bundle
Write-Host "Release bundle: $bundle"

