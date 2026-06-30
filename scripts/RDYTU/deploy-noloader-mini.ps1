param(
    [string]$GameRoot = "C:\Program Files (x86)\Steam\steamapps\common\Nuclear Option",
    [switch]$IncludePlayerMods
)

$ErrorActionPreference = "Stop"
& (Join-Path $PSScriptRoot "deploy-noloader.ps1") -GameRoot $GameRoot -RdytuMini -IncludePlayerMods:$IncludePlayerMods.IsPresent
