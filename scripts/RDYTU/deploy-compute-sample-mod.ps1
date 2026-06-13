param(
    [string]$GameRoot = "C:\Program Files (x86)\Steam\steamapps\common\Nuclear Option",
    [string]$Configuration = "RDYTU",
    [switch]$KeepOtherMods
)

$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "..\_env.ps1")
$RepoRoot = $NOLoaderRepoRoot

if (Get-Process -Name "NuclearOption" -ErrorAction SilentlyContinue) {
    Write-Error "Close Nuclear Option before deploy."
}

$project = Join-Path $RepoRoot "DEV.SDK\mods\NOLoader.ComputeSample\NOLoader.ComputeSample.csproj"
dotnet build $project -c $Configuration --verbosity minimal
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$dll = Join-Path $RepoRoot "DEV.SDK\mods\NOLoader.ComputeSample\bin\$Configuration\net48\NOLoader.ComputeSample.dll"
$modsRoot = Join-Path $GameRoot "NOLoader\mods"
$modRoot = Join-Path $modsRoot "ComputeSample"
New-Item -ItemType Directory -Path $modRoot -Force | Out-Null

if (-not $KeepOtherMods) {
    Get-ChildItem -Path $modsRoot -Directory | Where-Object { $_.Name -ne "ComputeSample" } | ForEach-Object {
        Remove-Item -Recurse -Force $_.FullName
        Write-Host "Removed mod folder: $($_.Name)"
    }
}

Copy-Item -Force $dll (Join-Path $modRoot "NOLoader.ComputeSample.dll")
Copy-Item -Force (Join-Path $RepoRoot "DEV.SDK\mods\NOLoader.ComputeSample\mod.json") (Join-Path $modRoot "mod.json")
& (Join-Path $RepoRoot "scripts\pack-mod-rdytu.ps1") -ModFolder $modRoot

Write-Host "ComputeSample deployed to $modRoot (enable core_balancer=1 in noloader_config.ini)"
