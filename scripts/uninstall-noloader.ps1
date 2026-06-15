param(
    [string]$GameRoot = "C:\Program Files (x86)\Steam\steamapps\common\Nuclear Option",
    [int]$SteamAppId = 2168680,
    [int]$WaitSeconds = 300
)

$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "_env.ps1")
. (Join-Path $PSScriptRoot "_managed-restore.ps1")

if (Get-Process -Name "NuclearOption" -ErrorAction SilentlyContinue) {
    Write-Error "Close Nuclear Option before uninstall."
}

Write-Host "=== NOLoader full uninstall ==="
Restore-VanillaManagedModules -GameRoot $GameRoot -RepoRoot $NOLoaderRepoRoot -SteamAppId $SteamAppId -WaitSeconds $WaitSeconds
Remove-NOLoaderInstallFiles -GameRoot $GameRoot

$marker = Join-Path $GameRoot "NOLoader_UNINSTALLED.txt"
@"
NOLoader removed at: $(Get-Date -Format o)

Managed DLLs restored to vanilla (no IL patches).
winhttp.dll removed — if you deleted NOLoader manually earlier, use NOLoaderRestore.exe in game root next time.
Re-install:
  .\scripts\deploy-noloader.ps1 -Configuration RDYTU
"@ | Set-Content -Encoding UTF8 $marker

Write-Host "`nUninstall complete. Launch the game - main menu should load normally."
Write-Host "Marker: $marker"
