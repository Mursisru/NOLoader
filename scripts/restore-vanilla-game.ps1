param(
    [string]$GameRoot = "C:\Program Files (x86)\Steam\steamapps\common\Nuclear Option",
    [int]$SteamAppId = 2168680,
    [int]$WaitSeconds = 300,
    [switch]$KeepLoaderFiles
)

$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "_env.ps1")
. (Join-Path $PSScriptRoot "_managed-restore.ps1")

if (Get-Process -Name "NuclearOption" -ErrorAction SilentlyContinue) {
    Write-Error "Close Nuclear Option before restore."
}

Restore-VanillaManagedModules -GameRoot $GameRoot -RepoRoot $NOLoaderRepoRoot -SteamAppId $SteamAppId -WaitSeconds $WaitSeconds

if (-not $KeepLoaderFiles) {
    Remove-NOLoaderInstallFiles -GameRoot $GameRoot
}

Write-Host "Vanilla restore complete. Game runs without NOLoader IL patches."
