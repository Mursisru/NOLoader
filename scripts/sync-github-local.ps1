param(
    [string]$Dest = ""
)

$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "_env.ps1")

if ([string]::IsNullOrWhiteSpace($Dest)) {
    Write-Error "Pass -Dest <path-to-github-mirror>"
}

$Source = $NOLoaderRepoRoot
New-Item -ItemType Directory -Force -Path $Dest | Out-Null
robocopy $Source $Dest /MIR /XD bin obj .vs .git artifacts /XF *.user *.suo
if ($LASTEXITCODE -ge 8) { exit $LASTEXITCODE }
Write-Host "Synced $Source -> $Dest"
