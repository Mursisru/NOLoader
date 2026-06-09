param(
    [string]$GameRoot = "C:\Program Files (x86)\Steam\steamapps\common\Nuclear Option"
)

$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "_env.ps1")
$RepoRoot = $NOLoaderRepoRoot
$DevSln = Join-Path $RepoRoot "DEV.SDK\NOLoader.DEV_SDK.sln"
Set-Location $RepoRoot

$fail = 0
Write-Host "`n=== NOLoader DEV.SDK CI verification (no game required) ===" -ForegroundColor Cyan

Write-Host "--- Build proxy ---" -ForegroundColor Cyan
& (Join-Path $NOLoaderScriptsRoot "build-proxy.ps1") -Configuration Release
if ($LASTEXITCODE -ne 0) { exit 1 }

Write-Host "--- Build DEV_SDK ---" -ForegroundColor Cyan
dotnet build $DevSln -c DEV_SDK --verbosity quiet
if ($LASTEXITCODE -ne 0) { exit 1 }

Write-Host "--- Unit tests ---" -ForegroundColor Cyan
$env:NOLOADER_GAME_ROOT = $GameRoot
dotnet test $DevSln -c DEV_SDK --verbosity quiet
if ($LASTEXITCODE -ne 0) { exit 1 }

Write-Host "--- Baked hash file ---" -ForegroundColor Cyan
$hashFile = Join-Path $RepoRoot "src\NOLoader.Core\Bootstrap\CoreBootstrapPatchHashes.generated.cs"
if (-not (Test-Path $hashFile)) { Write-Host "[FAIL] Missing baked hashes"; exit 1 }

if (Test-Path $GameRoot) {
    Write-Host "--- Hash verify vs game ---" -ForegroundColor Cyan
    & (Join-Path $NOLoaderScriptsRoot "verify-core-patch-hashes.ps1") -GameRoot $GameRoot
    if ($LASTEXITCODE -ne 0) { $fail++ }
}

Write-Host "--- Deploy manifest guids ---" -ForegroundColor Cyan
$modManifests = Get-ChildItem (Join-Path $RepoRoot "deploy\NOLoader\mods") -Recurse -Filter mod.json
foreach ($mf in $modManifests) {
    $text = Get-Content $mf.FullName -Raw
    if ($text -match '"guid"\s*:') {
        Write-Host "[ OK ] $($mf.Directory.Name) has guid"
    } else {
        Write-Host "[FAIL] $($mf.Directory.Name) missing guid"
        $fail++
    }
}

Write-Host "`n=== CI Summary ===" -ForegroundColor Cyan
if ($fail -gt 0) { exit 1 }
Write-Host "All CI checks passed" -ForegroundColor Green

