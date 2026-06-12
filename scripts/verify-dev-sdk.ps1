param(
    [string]$GameRoot = "C:\Program Files (x86)\Steam\steamapps\common\Nuclear Option",
    [int]$SmokeSeconds = 50,
    [switch]$SkipGameLaunch,
    [switch]$SkipBrokenModTest,
    [switch]$SkipFaultMissionTest
)

$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "_env.ps1")
$RepoRoot = $NOLoaderRepoRoot
$DevSln = Join-Path $RepoRoot "DEV.SDK\NOLoader.DEV_SDK.sln"
Set-Location $RepoRoot

$fail = 0
$warn = 0
$pass = 0

function Step-Pass { param([string]$Msg) Write-Host "[ OK ] $Msg" -ForegroundColor Green; $script:pass++ }
function Step-Fail { param([string]$Msg) Write-Host "[FAIL] $Msg" -ForegroundColor Red; $script:fail++ }
function Step-Warn { param([string]$Msg) Write-Host "[WARN] $Msg" -ForegroundColor Yellow; $script:warn++ }

Write-Host "`n=== NOLoader DEV.SDK verification ===" -ForegroundColor Cyan

Write-Host "--- Baked core patch hashes ---" -ForegroundColor Cyan
$hashFile = Join-Path $RepoRoot "src\NOLoader.Core\Bootstrap\CoreBootstrapPatchHashes.generated.cs"
if (-not (Test-Path $hashFile)) { Step-Fail "Missing CoreBootstrapPatchHashes.generated.cs" }
else {
    $hashText = Get-Content $hashFile -Raw
    $hashCount = ([regex]::Matches($hashText, 'case "noloader\.')).Count
    if ($hashCount -ge 7) { Step-Pass "Baked core hashes present ($hashCount entries)" }
    else { Step-Fail "Expected >= 7 baked hashes, found $hashCount" }
    if ($hashText -match 'return "";') { Step-Warn 'Placeholder TryGet - run scripts\bake-core-patch-hashes.ps1' }
}

if (Test-Path $GameRoot) {
    Write-Host "--- Baked hashes vs vanilla game ---" -ForegroundColor Cyan
    & (Join-Path $NOLoaderScriptsRoot "verify-core-patch-hashes.ps1") -GameRoot $GameRoot
    if ($LASTEXITCODE -ne 0) { Step-Fail "verify-core-patch-hashes.ps1" } else { Step-Pass "Baked hashes match game" }
}

Write-Host "--- Build native proxy ---" -ForegroundColor Cyan
& (Join-Path $NOLoaderScriptsRoot "build-proxy.ps1") -Configuration Release
if ($LASTEXITCODE -ne 0) { Step-Fail "build-proxy.ps1"; exit 1 }
Step-Pass "build-proxy.ps1"

Write-Host "--- Build DEV_SDK ---" -ForegroundColor Cyan
dotnet build $DevSln -c DEV_SDK --verbosity quiet
if ($LASTEXITCODE -ne 0) { Step-Fail "DEV_SDK build"; exit 1 }
Step-Pass "DEV_SDK build"

Write-Host "--- Unit tests ---" -ForegroundColor Cyan
$env:NOLOADER_GAME_ROOT = $GameRoot
dotnet test $DevSln -c DEV_SDK --verbosity quiet
if ($LASTEXITCODE -ne 0) { Step-Fail "dotnet test"; exit 1 }
Step-Pass "dotnet test"

$coreDlls = @("NOLoader.Core.dll", "NOLoader.API.dll", "NOLoader.Patcher.dll", "NOLoader.Registry.dll", "NOLoader.Telemetry.dll")
Write-Host "--- Core artifacts ---" -ForegroundColor Cyan
foreach ($dll in $coreDlls) {
    $path = Get-ChildItem -Path $RepoRoot -Recurse -Filter $dll | Where-Object { $_.DirectoryName -like "*\bin\DEV_SDK\*" } | Select-Object -First 1
    if ($path) { Step-Pass "Artifact $dll" } else { Step-Fail "Missing $dll (DEV_SDK)" }
}

$proxy = Join-Path $RepoRoot "artifacts\proxy\winhttp.dll"
if (Test-Path $proxy) { Step-Pass "winhttp.dll proxy artifact" } else { Step-Fail "winhttp.dll proxy missing" }

if (Test-Path $GameRoot) {
    $gameProc = Get-Process -Name "NuclearOption" -ErrorAction SilentlyContinue
    if ($gameProc -and -not $SkipGameLaunch) {
        Step-Fail 'Game is running - close Nuclear Option before deploy/smoke/L2 test'
        Write-Host "`n=== DEV.SDK Summary ===" -ForegroundColor Cyan
        Write-Host "Pass: $pass  Warn: $warn  Fail: $fail"
        exit 1
    }

    Write-Host "--- DEV.SDK deploy ---" -ForegroundColor Cyan
    & (Join-Path $NOLoaderScriptsRoot "DEV.SDK\deploy-noloader.ps1") -GameRoot $GameRoot | Out-Null
    if ($LASTEXITCODE -ne 0) { Step-Fail "DEV.SDK deploy" } else { Step-Pass "DEV.SDK deploy" }

    foreach ($dll in $coreDlls) {
        if (Test-Path (Join-Path $GameRoot "NOLoader\core\$dll")) { Step-Pass "Deployed $dll" } else { Step-Fail "Not deployed: $dll" }
    }

    $weaponMod = Join-Path $GameRoot "NOLoader\mods\WeaponNames\mod.json"
    if (Test-Path $weaponMod) { Step-Pass "WeaponNames mod deployed" } else { Step-Fail "WeaponNames mod missing" }
} else {
    Step-Warn 'Game not installed - skipping deploy/smoke/L2 test'
    $SkipGameLaunch = $true
    $SkipBrokenModTest = $true
    $SkipFaultMissionTest = $true
}

if (-not $SkipBrokenModTest -and (Test-Path $GameRoot)) {
    Write-Host "--- Gate L2 BrokenMod negative ---" -ForegroundColor Cyan
    & (Join-Path $NOLoaderScriptsRoot "test-gate-l2-brokenmod.ps1") -GameRoot $GameRoot -SmokeSeconds 25
    if ($LASTEXITCODE -ne 0) { Step-Fail "test-gate-l2-brokenmod.ps1" } else { Step-Pass "Gate L2 BrokenMod negative test" }
    & (Join-Path $NOLoaderScriptsRoot "DEV.SDK\deploy-noloader.ps1") -GameRoot $GameRoot | Out-Null
}

if (-not $SkipFaultMissionTest -and (Test-Path $GameRoot)) {
    Write-Host "--- Gate L4 FaultMission negative ---" -ForegroundColor Cyan
    & (Join-Path $NOLoaderScriptsRoot "test-gate-l4-faultmission.ps1") -GameRoot $GameRoot -SmokeSeconds 40
    if ($LASTEXITCODE -ne 0) { Step-Fail "test-gate-l4-faultmission.ps1" } else { Step-Pass "Gate L4 FaultMission negative test" }
    & (Join-Path $NOLoaderScriptsRoot "DEV.SDK\deploy-noloader.ps1") -GameRoot $GameRoot | Out-Null
}

if (-not $SkipGameLaunch -and (Test-Path (Join-Path $GameRoot "NuclearOption.exe"))) {
    Write-Host ('--- Cold-start smoke ({0}s) ---' -f $SmokeSeconds) -ForegroundColor Cyan
    $bak = Join-Path $GameRoot "NuclearOption_Data\Managed\Assembly-CSharp.dll.noloader.bak"
    $asm = Join-Path $GameRoot "NuclearOption_Data\Managed\Assembly-CSharp.dll"
    if (Test-Path $bak) { Copy-Item -Force $bak $asm }
    & (Join-Path $NOLoaderScriptsRoot "DEV.SDK\deploy-noloader.ps1") -GameRoot $GameRoot | Out-Null
    $logs = Join-Path $GameRoot "NOLoader\logs"
    Remove-Item (Join-Path $logs "noloader_ring.log") -ErrorAction SilentlyContinue
    $p = Start-Process -FilePath (Join-Path $GameRoot "NuclearOption.exe") -PassThru
    Start-Sleep -Seconds $SmokeSeconds
    if (-not $p.HasExited) { Stop-Process -Id $p.Id -Force; Step-Pass ('Game smoke {0}s' -f $SmokeSeconds) }
    else { Step-Fail "Game exited early" }
    & (Join-Path $NOLoaderScriptsRoot "verify-noloader-logs.ps1") -GameRoot $GameRoot -DiagMode Menu
    if ($LASTEXITCODE -ne 0) { Step-Fail "verify-noloader-logs.ps1" } else { Step-Pass "verify-noloader-logs.ps1" }
}

Write-Host "`n=== DEV.SDK Summary ===" -ForegroundColor Cyan
Write-Host "Pass: $pass  Warn: $warn  Fail: $fail"
if ($fail -gt 0) { exit 1 }

