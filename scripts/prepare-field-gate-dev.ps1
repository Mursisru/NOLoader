param(
    [switch]$SkipSmokeTest,
    [switch]$SkipGateL2Negative,
    [string]$GameRoot = "C:\Program Files (x86)\Steam\steamapps\common\Nuclear Option"
)

$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "_env.ps1")
$RepoRoot = $NOLoaderRepoRoot

Write-Host "=== NOLoader DEV.SDK field gate prep ===" -ForegroundColor Cyan

$gameProc = Get-Process -Name "NuclearOption" -ErrorAction SilentlyContinue
if ($gameProc) {
    Write-Error "Close Nuclear Option before field gate prep (Cecil + hash verify require exclusive file access)."
}

$bak = Join-Path $GameRoot "NuclearOption_Data\Managed\Assembly-CSharp.dll.noloader.bak"
$gameAsm = Join-Path $GameRoot "NuclearOption_Data\Managed\Assembly-CSharp.dll"
$unityBak = Join-Path $GameRoot "NuclearOption_Data\Managed\UnityEngine.CoreModule.dll.noloader.bak"
$unityAsm = Join-Path $GameRoot "NuclearOption_Data\Managed\UnityEngine.CoreModule.dll"
$physicsBak = Join-Path $GameRoot "NuclearOption_Data\Managed\UnityEngine.PhysicsModule.dll.noloader.bak"
$physicsAsm = Join-Path $GameRoot "NuclearOption_Data\Managed\UnityEngine.PhysicsModule.dll"
foreach ($pair in @(@($bak, $gameAsm), @($unityBak, $unityAsm), @($physicsBak, $physicsAsm))) {
    if (Test-Path $pair[0]) { Copy-Item -Force $pair[0] $pair[1] }
}

if (-not $SkipGateL2Negative) {
    Write-Host "`n--- Gate L2 BrokenMod negative (automated) ---" -ForegroundColor Cyan
    & (Join-Path $NOLoaderScriptsRoot "test-gate-l2-brokenmod.ps1") -GameRoot $GameRoot -SmokeSeconds 25
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}

& (Join-Path $NOLoaderScriptsRoot "DEV.SDK\deploy-noloader.ps1") -GameRoot $GameRoot
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$logs = Join-Path $GameRoot "NOLoader\logs"
Remove-Item (Join-Path $logs "proxy.log"), (Join-Path $logs "noloader_ring.log") -ErrorAction SilentlyContinue
Write-Host "Cleared NOLoader logs for clean 2x DONE session"

if (-not $SkipSmokeTest) {
    Write-Host "`nRunning 45s cold-start smoke test..." -ForegroundColor Cyan
    $exe = Join-Path $GameRoot "NuclearOption.exe"
    $p = Start-Process -FilePath $exe -PassThru
    Start-Sleep -Seconds 45
    if (-not $p.HasExited) { Stop-Process -Id $p.Id -Force }
    & (Join-Path $NOLoaderScriptsRoot "verify-noloader-logs.ps1") -GameRoot $GameRoot
}

Write-Host "`n=== Field checklist (2x DONE) ===" -ForegroundColor Cyan
Write-Host "0. Pack diag mods: scripts\deploy-diag-mods.ps1 -InstallToGame (optional, off-game by default)"
Write-Host "1. Cold-start -> menu 2x DONE (LoaderDiagMenu 1.4.0)"
Write-Host "2. Battle map -> 2x DONE (LoaderDiag)"
Write-Host "3. WeaponNames: mainmenu.weaponnames_patch PASS in ring log"
Write-Host "4. Gate L2 negative already verified above (unless -SkipGateL2Negative)"
Write-Host "`nRing log: $(Join-Path $GameRoot 'NOLoader\logs\noloader_ring.log')"

