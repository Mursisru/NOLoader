param(
    [string]$GameRoot = "C:\Program Files (x86)\Steam\steamapps\common\Nuclear Option",
    [int]$SmokeSeconds = 40
)

$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "_env.ps1")
$RepoRoot = $NOLoaderRepoRoot
$FaultModSrc = Join-Path $RepoRoot "DEV.SDK\mods\NOLoader.FaultMission"
$FaultModDeploy = Join-Path $GameRoot "NOLoader\mods\FaultMission"
$RingLog = Join-Path $GameRoot "NOLoader\logs\noloader_ring.log"

Write-Host "=== Gate L4 FaultMission negative test ===" -ForegroundColor Cyan

if (-not (Test-Path $GameRoot)) {
    Write-Error "Game not installed at $GameRoot"
}

Write-Host "Building FaultMission..."
dotnet build (Join-Path $FaultModSrc "NOLoader.FaultMission.csproj") -c Debug --verbosity quiet
if ($LASTEXITCODE -ne 0) { exit 1 }

$bak = Join-Path $GameRoot "NuclearOption_Data\Managed\Assembly-CSharp.dll.noloader.bak"
$asm = Join-Path $GameRoot "NuclearOption_Data\Managed\Assembly-CSharp.dll"
$unityBak = Join-Path $GameRoot "NuclearOption_Data\Managed\UnityEngine.CoreModule.dll.noloader.bak"
$unity = Join-Path $GameRoot "NuclearOption_Data\Managed\UnityEngine.CoreModule.dll"
$physicsBak = Join-Path $GameRoot "NuclearOption_Data\Managed\UnityEngine.PhysicsModule.dll.noloader.bak"
$physics = Join-Path $GameRoot "NuclearOption_Data\Managed\UnityEngine.PhysicsModule.dll"

foreach ($pair in @(@($bak, $asm), @($unityBak, $unity), @($physicsBak, $physics))) {
    if (Test-Path $pair[0]) { Copy-Item -Force $pair[0] $pair[1] }
}

& (Join-Path $NOLoaderScriptsRoot "DEV.SDK\deploy-noloader.ps1") -GameRoot $GameRoot -SkipCecilPrePatch | Out-Null
if ($LASTEXITCODE -ne 0) { exit 1 }

New-Item -ItemType Directory -Force -Path $FaultModDeploy | Out-Null
Copy-Item -Force (Join-Path $RepoRoot "DEV.SDK\mods\NOLoader.FaultMission\mod.json") (Join-Path $FaultModDeploy "mod.json")
Copy-Item -Force (Join-Path $FaultModSrc "bin\Debug\net48\NOLoader.FaultMission.dll") (Join-Path $FaultModDeploy "NOLoader.FaultMission.dll")

Remove-Item $RingLog -ErrorAction SilentlyContinue

$proc = Get-Process -Name "NuclearOption" -ErrorAction SilentlyContinue
if ($proc) { Stop-Process -Name "NuclearOption" -Force -ErrorAction SilentlyContinue; Start-Sleep -Seconds 2 }

Write-Host "Cold-start smoke ($SmokeSeconds s) with FaultMission installed..."
$p = Start-Process -FilePath (Join-Path $GameRoot "NuclearOption.exe") -PassThru
Start-Sleep -Seconds $SmokeSeconds
if (-not $p.HasExited) { Stop-Process -Id $p.Id -Force }

if (-not (Test-Path $RingLog)) {
    Write-Error "Ring log missing: $RingLog"
}

$log = Get-Content $RingLog -Raw
$fail = 0

if ($log -match "\[GateL4\] Fault detail:.*Gate L4 negative test fault") {
    Write-Host "[ OK ] FaultMission exception logged" -ForegroundColor Green
} else {
    Write-Host "[FAIL] Expected [GateL4] Fault detail for FaultMission" -ForegroundColor Red
    $fail++
}

if ($log -match "\[GateL4\] Mod fault tracked: com\.at747\.faultmission") {
    Write-Host "[ OK ] Gate L4 mod fault tracked" -ForegroundColor Green
} else {
    Write-Host "[FAIL] Expected [GateL4] Mod fault tracked for FaultMission" -ForegroundColor Red
    $fail++
}

if ($log -match "\[GateL4\] Mission block flagged: com\.at747\.faultmission") {
    Write-Host "[ OK ] Gate L4 mission block flagged" -ForegroundColor Green
} else {
    Write-Host "[FAIL] Expected [GateL4] Mission block flagged" -ForegroundColor Red
    $fail++
}

if ($log -match "OnMainMenuReady|Core started|MainMenu hook fired") {
    Write-Host "[ OK ] Core bootstrap survived FaultMission L4 fault" -ForegroundColor Green
} else {
    Write-Host "[FAIL] Core bootstrap did not reach main menu" -ForegroundColor Red
    $fail++
}

Remove-Item $FaultModDeploy -Recurse -Force -ErrorAction SilentlyContinue

if ($fail -gt 0) { exit 1 }
Write-Host "Gate L4 FaultMission negative test PASSED" -ForegroundColor Green

