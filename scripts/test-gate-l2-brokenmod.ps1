param(
    [string]$GameRoot = "C:\Program Files (x86)\Steam\steamapps\common\Nuclear Option",
    [int]$SmokeSeconds = 25
)

$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "_env.ps1")
$RepoRoot = $NOLoaderRepoRoot
$BrokenModSrc = Join-Path $RepoRoot "DEV.SDK\mods\NOLoader.BrokenMod"
$BrokenModDeploy = Join-Path $GameRoot "NOLoader\mods\BrokenMod"
$RingLog = Join-Path $GameRoot "NOLoader\logs\noloader_ring.log"

Write-Host "=== Gate L2 BrokenMod negative test ===" -ForegroundColor Cyan

if (-not (Test-Path $GameRoot)) {
    Write-Error "Game not installed at $GameRoot"
}

Write-Host "Building BrokenMod..."
dotnet build (Join-Path $BrokenModSrc "NOLoader.BrokenMod.csproj") -c Debug --verbosity quiet
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

New-Item -ItemType Directory -Force -Path $BrokenModDeploy | Out-Null
Copy-Item -Force (Join-Path $RepoRoot "DEV.SDK\mods\NOLoader.BrokenMod\mod.json") (Join-Path $BrokenModDeploy "mod.json")
Copy-Item -Force (Join-Path $BrokenModSrc "bin\Debug\net48\NOLoader.BrokenMod.dll") (Join-Path $BrokenModDeploy "NOLoader.BrokenMod.dll")

Remove-Item $RingLog -ErrorAction SilentlyContinue

$proc = Get-Process -Name "NuclearOption" -ErrorAction SilentlyContinue
if ($proc) { Stop-Process -Name "NuclearOption" -Force -ErrorAction SilentlyContinue; Start-Sleep -Seconds 2 }

Write-Host "Cold-start smoke ($SmokeSeconds s) with BrokenMod installed..."
$p = Start-Process -FilePath (Join-Path $GameRoot "NuclearOption.exe") -PassThru
Start-Sleep -Seconds $SmokeSeconds
if (-not $p.HasExited) { Stop-Process -Id $p.Id -Force }

if (-not (Test-Path $RingLog)) {
    Write-Error "Ring log missing: $RingLog"
}

$log = Get-Content $RingLog -Raw
$fail = 0

if ($log -match "Signature mismatch for Encyclopedia::AfterLoad|com\.at747\.brokenmod rejected") {
    Write-Host "[ OK ] Gate L2 signature mismatch logged" -ForegroundColor Green
} else {
    Write-Host "[FAIL] Expected Gate L2 rejection for BrokenMod patch" -ForegroundColor Red
    $fail++
}

if ($log -match "Patch failed for mod com\.at747\.brokenmod|com\.at747\.brokenmod rejected") {
    Write-Host "[ OK ] BrokenMod isolated (L2)" -ForegroundColor Green
} else {
    Write-Host "[FAIL] Expected BrokenMod L2 isolation message" -ForegroundColor Red
    $fail++
}

if ($log -match "OnMainMenuReady|Core started|MainMenu hook fired") {
    Write-Host "[ OK ] Core bootstrap survived BrokenMod L2 failure" -ForegroundColor Green
} else {
    Write-Host "[FAIL] Core bootstrap did not reach main menu" -ForegroundColor Red
    $fail++
}

Remove-Item $BrokenModDeploy -Recurse -Force -ErrorAction SilentlyContinue

if ($fail -gt 0) { exit 1 }
Write-Host "Gate L2 BrokenMod negative test PASSED" -ForegroundColor Green

