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

$project = Join-Path $RepoRoot "DEV.SDK\mods\NOLoader.MechanicsVerify\NOLoader.MechanicsVerify.csproj"
dotnet build $project -c $Configuration --verbosity minimal
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$dll = Join-Path $RepoRoot "DEV.SDK\mods\NOLoader.MechanicsVerify\bin\$Configuration\net48\NOLoader.MechanicsVerify.dll"
$modConfigProject = Join-Path $RepoRoot "DEV.SDK\shared\NOLoader.ModConfig\NOLoader.ModConfig.csproj"
$modConfigDll = Join-Path $RepoRoot "DEV.SDK\shared\NOLoader.ModConfig\bin\$Configuration\net48\NOLoader.ModConfig.dll"
if (-not (Test-Path $modConfigDll)) {
    dotnet build $modConfigProject -c $Configuration --verbosity minimal
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}

$modsRoot = Join-Path $GameRoot "NOLoader\mods"
$modRoot = Join-Path $modsRoot "MechanicsVerify"
New-Item -ItemType Directory -Path $modRoot -Force | Out-Null

if (-not $KeepOtherMods) {
    Get-ChildItem -Path $modsRoot -Directory | Where-Object { $_.Name -ne "MechanicsVerify" } | ForEach-Object {
        Remove-Item -Recurse -Force $_.FullName
        Write-Host "Removed mod folder: $($_.Name)"
    }
}

Copy-Item -Force $dll (Join-Path $modRoot "NOLoader.MechanicsVerify.dll")
Copy-Item -Force $modConfigDll (Join-Path $modRoot "NOLoader.ModConfig.dll")
Copy-Item -Force (Join-Path $RepoRoot "DEV.SDK\mods\NOLoader.MechanicsVerify\mod.json") (Join-Path $modRoot "mod.json")

$iniSrc = Join-Path $RepoRoot "DEV.SDK\mods\NOLoader.MechanicsVerify\mod.ini"
$iniDst = Join-Path $modRoot "mod.ini"
if (-not (Test-Path $iniDst)) {
    Copy-Item -Force $iniSrc $iniDst
}

& (Join-Path $RepoRoot "scripts\pack-mod-rdytu.ps1") -ModFolder $modRoot

Write-Host "MechanicsVerify deployed to $modRoot (no Cecil patches, PatchTool not required)"
Write-Host "Next: fly mission with throttle, then: scripts\RDYTU\parse-mechanics-verify-ringlog.ps1"
