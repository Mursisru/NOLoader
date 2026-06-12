param(
    [string]$GameRoot = "C:\Program Files (x86)\Steam\steamapps\common\Nuclear Option",
    [int]$SmokeSeconds = 50,
    [switch]$SkipGameLaunch,
    [switch]$SkipBrokenModTest
)

$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "_env.ps1")
$RepoRoot = $NOLoaderRepoRoot

Write-Host "=== NOLoader full verification (DEV.SDK + RDYTU, separate solutions) ===" -ForegroundColor Cyan

& (Join-Path $NOLoaderScriptsRoot "verify-dev-sdk.ps1") -GameRoot $GameRoot -SmokeSeconds $SmokeSeconds -SkipGameLaunch:$SkipGameLaunch -SkipBrokenModTest:$SkipBrokenModTest
if ($LASTEXITCODE -ne 0) { exit 1 }

& (Join-Path $NOLoaderScriptsRoot "verify-rdytu.ps1") -GameRoot $GameRoot
exit $LASTEXITCODE

