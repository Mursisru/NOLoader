param(
    [string]$GameRoot = ""
)

$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "_env.ps1")
$RepoRoot = $NOLoaderRepoRoot
$DevSln = Join-Path $RepoRoot "DEV.SDK\NOLoader.DEV_SDK.sln"
Set-Location $RepoRoot

if ([string]::IsNullOrWhiteSpace($GameRoot)) {
    $GameRoot = Join-Path ${env:ProgramFiles(x86)} "Steam\steamapps\common\Nuclear Option"
}

function Test-GameManagedPresent {
    param([string]$Root)
    Test-Path (Join-Path $Root "NuclearOption_Data\Managed\UnityEngine.CoreModule.dll")
}

$hasGame = Test-GameManagedPresent $GameRoot
$fail = 0

Write-Host "`n=== NOLoader DEV.SDK CI verification ===" -ForegroundColor Cyan
if ($hasGame) {
    Write-Host "Game Managed DLLs found — full build + tests" -ForegroundColor Green
} else {
    Write-Host "Game not installed — lite CI (API + Patcher + proxy; no Unity mods)" -ForegroundColor Yellow
}

Write-Host "--- Build proxy ---" -ForegroundColor Cyan
& (Join-Path $NOLoaderScriptsRoot "build-proxy.ps1") -Configuration Release
if ($LASTEXITCODE -ne 0) { exit 1 }

if ($hasGame) {
    Write-Host "--- Build DEV_SDK (full) ---" -ForegroundColor Cyan
    dotnet build $DevSln -c DEV_SDK --verbosity quiet
    if ($LASTEXITCODE -ne 0) { exit 1 }

    Write-Host "--- Unit tests (full) ---" -ForegroundColor Cyan
    $env:NOLOADER_GAME_ROOT = $GameRoot
    dotnet test $DevSln -c DEV_SDK --verbosity quiet
    if ($LASTEXITCODE -ne 0) { exit 1 }
} else {
    Write-Host "--- Build API + Patcher ---" -ForegroundColor Cyan
    dotnet build (Join-Path $RepoRoot "src\NOLoader.API\NOLoader.API.csproj") --verbosity quiet
    if ($LASTEXITCODE -ne 0) { exit 1 }
    dotnet build (Join-Path $RepoRoot "src\NOLoader.Patcher\NOLoader.Patcher.csproj") -c DEV_SDK --verbosity quiet
    if ($LASTEXITCODE -ne 0) { exit 1 }

    Write-Host "--- Patcher unit tests ---" -ForegroundColor Cyan
    dotnet test (Join-Path $RepoRoot "tests\NOLoader.Patcher.Tests\NOLoader.Patcher.Tests.csproj") -c DEV_SDK --verbosity quiet
    if ($LASTEXITCODE -ne 0) { exit 1 }
}

Write-Host "--- Baked hash file ---" -ForegroundColor Cyan
$hashFile = Join-Path $RepoRoot "src\NOLoader.Core\Bootstrap\CoreBootstrapPatchHashes.generated.cs"
if (-not (Test-Path $hashFile)) {
    Write-Host "[FAIL] Missing baked hashes"
    exit 1
}

if ($hasGame) {
    Write-Host "--- Hash verify vs game ---" -ForegroundColor Cyan
    & (Join-Path $NOLoaderScriptsRoot "verify-core-patch-hashes.ps1") -GameRoot $GameRoot
    if ($LASTEXITCODE -ne 0) { $fail++ }
}

Write-Host "--- Deploy manifest guids ---" -ForegroundColor Cyan
$modManifests = Get-ChildItem (Join-Path $RepoRoot "deploy\NOLoader\mods") -Recurse -Filter mod.json -ErrorAction SilentlyContinue
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
