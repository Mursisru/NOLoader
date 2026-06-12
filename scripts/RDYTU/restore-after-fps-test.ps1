param(
    [string]$GameRoot = "C:\Program Files (x86)\Steam\steamapps\common\Nuclear Option",
    [switch]$IncludePlayerMods
)

$ErrorActionPreference = "Stop"
. (Join-Path (Split-Path $PSScriptRoot -Parent) "_env.ps1")
$RdyRoot = $NOLoaderRdyRoot
$Marker = Join-Path $GameRoot "NOLoader_FPS_TEST_DISABLED.txt"

if (Get-Process -Name "NuclearOption" -ErrorAction SilentlyContinue) {
    Write-Error "Close Nuclear Option before restore."
    exit 1
}

function Enable-ItemRename {
    param([string]$DisabledPath)
    if (-not (Test-Path $DisabledPath)) { return }
    $live = $DisabledPath -replace '\.noloader-disabled$', ''
    if (Test-Path $live) { Remove-Item -Force $live }
    Rename-Item -Path $DisabledPath -NewName (Split-Path $live -Leaf)
    Write-Host "Restored: $live"
}

Enable-ItemRename (Join-Path $GameRoot "NOLoader.noloader-disabled")
Enable-ItemRename (Join-Path $GameRoot "noloader_config.ini.noloader-disabled")
Enable-ItemRename (Join-Path $GameRoot "winhttp.dll.noloader-disabled")

if (Test-Path $Marker) { Remove-Item -Force $Marker }

$include = if ($IncludePlayerMods) { @("-IncludePlayerMods") } else { @() }
& (Join-Path $NOLoaderScriptsRoot "RDYTU\deploy-noloader.ps1") -GameRoot $GameRoot @include
exit $LASTEXITCODE

