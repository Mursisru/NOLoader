param(
    [string]$GameRoot = "C:\Program Files (x86)\Steam\steamapps\common\Nuclear Option"
)

$ErrorActionPreference = "Stop"
. (Join-Path (Split-Path $PSScriptRoot -Parent) "_env.ps1")
. (Join-Path (Split-Path $PSScriptRoot -Parent) "_managed-restore.ps1")
$Marker = Join-Path $GameRoot "NOLoader_FPS_TEST_DISABLED.txt"

if (Get-Process -Name "NuclearOption" -ErrorAction SilentlyContinue) {
    Write-Error "Close Nuclear Option before uninstall."
}

Write-Host "=== NOLoader FPS test uninstall ==="
Restore-VanillaManagedModules -GameRoot $GameRoot -RepoRoot $NOLoaderRepoRoot
Remove-NOLoaderInstallFiles -GameRoot $GameRoot

@"
NOLoader disabled for FPS baseline test.
Disabled at: $(Get-Date -Format o)

Restore managed DLLs (if needed):
  .\scripts\restore-vanilla-game.ps1 -KeepLoaderFiles

Re-enable NOLoader:
  .\scripts\RDYTU\restore-after-fps-test.ps1

Or full redeploy:
  .\scripts\deploy-noloader.ps1 -Configuration RDYTU
"@ | Set-Content -Encoding UTF8 $Marker

Write-Host "`nFPS test mode: vanilla managed DLLs, NOLoader removed."
Write-Host "Marker: $Marker"
