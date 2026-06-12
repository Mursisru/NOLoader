param(
    [string]$GameRoot = "C:\Program Files (x86)\Steam\steamapps\common\Nuclear Option",
    [int]$MenuSmokeSeconds = 35,
    [switch]$SkipGameLaunch,
    [switch]$WithPerfTestMission
)

$ErrorActionPreference = "Stop"
. (Join-Path (Split-Path $PSScriptRoot -Parent) "_env.ps1")

$RepoRoot = $NOLoaderRepoRoot
$RdySln = Join-Path $NOLoaderRdyRoot "NOLoader.RDYTU.sln"
Set-Location $RepoRoot

$fail = 0
$warn = 0
$pass = 0

function Step-Pass { param([string]$Msg) Write-Host "[ OK ] $Msg" -ForegroundColor Green; $script:pass++ }
function Step-Fail { param([string]$Msg) Write-Host "[FAIL] $Msg" -ForeColor Red; $script:fail++ }
function Step-Warn { param([string]$Msg) Write-Host "[WARN] $Msg" -ForeColor Yellow; $script:warn++ }

function Test-DllHasString {
    param([string]$DllPath, [string]$Needle)
    if (-not (Test-Path $DllPath)) { return $false }
    $text = [Text.Encoding]::ASCII.GetString([IO.File]::ReadAllBytes($DllPath))
    return $text.Contains($Needle)
}

Write-Host "`n=== RDYTU perf TZ verification ===" -ForegoundColor Cyan

Write-Host "--- Build RDYTU ---" -ForegoundColor Cyan
dotnet build $RdySln -c RDYTU --verbosity quiet
if ($LASTEXITCODE -ne 0) { Step-Fail "RDYTU build"; exit 1 }
Step-Pass "RDYTU build"

Write-Host "--- Unit tests (RDYTU) ---" -ForegoundColor Cyan
$env:NOLOADER_GAME_ROOT = $GameRoot
dotnet test $RdySln -c RDYTU --verbosity quiet
if ($LASTEXITCODE -ne 0) { Step-Fail "dotnet test RDYTU" } else { Step-Pass "dotnet test RDYTU" }

Write-Host "--- Patcher unit tests (throttle IL) ---" -ForegoundColor Cyan
dotnet test (Join-Path $RepoRoot "tests\NOLoader.Patcher.Tests\NOLoader.Patcher.Tests.csproj") -c RDYTU --filter ThrottleIl --verbosity quiet
if ($LASTEXITCODE -ne 0) { Step-Fail "ThrottleIl unit test" } else { Step-Pass "ThrottleIl unit test" }

$rdyCore = Join-Path $RepoRoot "src\NOLoader.Core\bin\RDYTU\net48\NOLoader.Core.dll"
$devCore = Join-Path $RepoRoot "src\NOLoader.Core\bin\DEV_SDK\net48\NOLoader.Core.dll"
$rdyApi = Join-Path $RepoRoot "src\NOLoader.API\bin\RDYTU\netstandard2.0\NOLoader.API.dll"

Write-Host "--- Build DEV_SDK (isolation baseline) ---" -ForegoundColor Cyan
$DevSln = Join-Path $RepoRoot "DEV.SDK\NOLoader.DEV_SDK.sln"
dotnet build $DevSln -c DEV_SDK --verbosity quiet
if ($LASTEXITCODE -ne 0) { Step-Fail "DEV_SDK build" } else { Step-Pass "DEV_SDK build" }

Write-Host "--- Layer isolation (RDYTU vs DEV_SDK) ---" -ForegoundColor Cyan
if (Test-DllHasString $rdyCore "NOLoader.Core.Runtime.Perf") { Step-Pass "RDYTU includes Runtime/Perf" } else { Step-Fail "RDYTU missing Runtime/Perf" }
if (Test-Path $devCore) {
    if (Test-DllHasString $devCore "NOLoader.Core.Runtime.Perf") { Step-Fail "DEV_SDK must not include Runtime/Perf" }
    else { Step-Pass "DEV_SDK excludes Runtime/Perf" }
}

Write-Host "--- API contract (5 layers surface) ---" -ForegoundColor Cyan
$apiTypes = @(
    "INOModTickFast",
    "INOModTickNormal",
    "INOModTickSlow",
    "NOModServices",
    "INOModArrayPool",
    "INOModWorldReader",
    "IModExecutionBudgetView",
    "PatchThrottleGate"
)
foreach ($t in $apiTypes) {
    if (Test-DllHasString $rdyApi $t) { Step-Pass "API $t" } else { Step-Fail "API missing $t" }
}

$patchDesc = [System.Reflection.Assembly]::LoadFrom($rdyApi).GetType("NOLoader.API.Manifest.PatchDescriptor")
if ($patchDesc.GetField("ThrottleEveryN")) { Step-Pass "PatchDescriptor.ThrottleEveryN" } else { Step-Fail "PatchDescriptor.ThrottleEveryN" }

Write-Host "--- Migrated mods compile (RDYTU) ---" -ForegoundColor Cyan
$modProjects = @(
    (Get-ChildItem (Join-Path $RepoRoot "DEV.SDK\mods\NOLoader.Missile*") -Directory |
        Where-Object { $_.Name -notmatch 'HoldCam|LaunchArc' } |
        Select-Object -First 1 -ExpandProperty FullName),
    (Join-Path $RepoRoot "DEV.SDK\mods\NOLoader.MissileLaunchArcHud\NOLoader.MissileLaunchArcHud.csproj"),
    (Join-Path $RepoRoot "DEV.SDK\mods\NOLoader.VectoringTargetHud\NOLoader.VectoringTargetHud.csproj"),
    (Join-Path $RepoRoot "DEV.SDK\mods\NOLoader.PerfTest\NOLoader.PerfTest.csproj")
)
foreach ($proj in $modProjects) {
    if (-not $proj) { Step-Fail "Missile tracking mod folder not found"; continue }
    $csproj = if ($proj -like '*.csproj') { $proj } else { Join-Path $proj ((Split-Path $proj -Leaf) + ".csproj") }
    dotnet build $csproj -c RDYTU --verbosity quiet
    if ($LASTEXITCODE -ne 0) { Step-Fail "Build $csproj -c RDYTU" } else { Step-Pass "Build $(Split-Path $csproj -Leaf) RDYTU" }
}

if (Test-Path $GameRoot) {
    $ManagedRestoreName = [string][char]0x5F + [char]0x6D + [char]0x61 + [char]0x6E + [char]0x61 + [char]0x67 + [char]0x65 + [char]0x64 + "-restore.ps1"
    . (Join-Path (Split-Path $PSScriptRoot -Parent) $ManagedRestoreName)
    if (Test-ManagedMirageMismatch -GameRoot $GameRoot) { Step-Fail "Assembly-CSharp Mirage mismatch" }
    else { Step-Pass "Assembly-CSharp Mirage match" }

    $deployCore = Join-Path $GameRoot "NOLoader\core\NOLoader.Core.dll"
    if (Test-Path $deployCore) {
        $ver = [System.Reflection.Assembly]::LoadFrom($deployCore).GetType("NOLoader.Core.AppVersion")
        $sub = $ver.GetField("SubNumber").GetValue($null)
        Step-Pass "Deployed Core build R$sub"
    } else {
        Step-Warn "NOLoader not deployed - run deploy-noloader.ps1"
    }
}

if (-not $SkipGameLaunch -and (Test-Path (Join-Path $GameRoot "NuclearOption.exe"))) {
    if (Get-Process -Name "NuclearOption" -ErrorAction SilentlyContinue) {
        Step-Fail "Close Nuclear Option before smoke"
    } else {
        Write-Host "--- Stage A: empty mods menu smoke ($MenuSmokeSeconds s) ---" -ForegoundColor Cyan
        & (Join-Path $PSScriptRoot "prepare-perf-test.ps1") -NoMods -GameRoot $GameRoot
        $ring = Join-Path $GameRoot "NOLoader\logs\noloader_ring.log"
        $player = Join-Path $env:USERPROFILE "AppData\LocalLow\Shockfront\NuclearOption\Player.log"

        $p = Start-Process -FilePath (Join-Path $GameRoot "NuclearOption.exe") -PassThru
        Start-Sleep -Seconds $MenuSmokeSeconds
        if ($p.HasExited) { Step-Fail "Game exited during menu smoke" }
        else { Stop-Process -Id $p.Id -Force; Step-Pass "Menu smoke ${MenuSmokeSeconds}s" }

        Start-Sleep -Seconds 2
        if (Test-Path $ring) {
            $tail = Get-Content $ring -Tail 40 -ErrorAction SilentlyContinue
            if ($tail -match "Core started") { Step-Pass "Ring: Core started" } else { Step-Fail "Ring: Core started missing" }
            if ($tail -match "FATAL") { Step-Fail "Ring: FATAL during menu smoke" } else { Step-Pass "Ring: no FATAL" }
            if ($tail -match "\[Perf\] demote") { Step-Warn "Perf demote on empty mods (unexpected)" } else { Step-Pass "Ring: no demote (zero-mod overhead)" }
        } else { Step-Fail "Ring log missing after smoke" }

        if (Test-Path $player) {
            $newTail = Get-Content $player -Tail 80 -ErrorAction SilentlyContinue
            if ($newTail -match "Crash!!!") { Step-Fail "Player.log crash during menu smoke" }
            else { Step-Pass "Player.log: no crash" }
        }
    }
}

Write-Host "`n=== TZ layer status (automated vs manual) ===" -ForegoundColor Cyan
Write-Host "  [auto] Tick scheduler API + RDYTU Core types"
Write-Host "  [auto] World snapshot API + service type in RDYTU"
Write-Host "  [auto] Cecil throttle IL (unit test)"
Write-Host "  [auto] ArrayPool + frame budget types"
Write-Host "  [auto] Zero-mod menu smoke (no perf demote)"
Write-Host "  [manual] WorldSnapshot in mission with PerfTest"
Write-Host "  [manual] Budget demote with HeavyWork=2"
Write-Host "  [manual] Player mod RDYTU packs not yet deployed (R100)"

Write-Host "`n=== Verification summary ===" -ForegoundColor Cyan
Write-Host "Pass: $pass  Warn: $warn  Fail: $fail"
if ($fail -gt 0) { exit 1 }
exit 0
