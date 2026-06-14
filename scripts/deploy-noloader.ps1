param(
    [ValidateSet("DEV_SDK", "RDYTU", "Release")]
    [string]$Configuration = "DEV_SDK",
    [string]$GameRoot = "C:\Program Files (x86)\Steam\steamapps\common\Nuclear Option",
    [switch]$SkipHashVerify,
    [switch]$IncludePlayerMods,
    [switch]$FieldTest
)

$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "_env.ps1")
$RepoRoot = $NOLoaderRepoRoot

if ($Configuration -eq "RDYTU") {
    & (Join-Path $NOLoaderScriptsRoot "RDYTU\deploy-noloader.ps1") @PSBoundParameters
} else {
    if ($Configuration -ne "DEV_SDK") {
        Write-Warning "Configuration '$Configuration' mapped to DEV_SDK deploy."
    }
    & (Join-Path $NOLoaderScriptsRoot "DEV.SDK\deploy-noloader.ps1") -GameRoot $GameRoot @PSBoundParameters
}

