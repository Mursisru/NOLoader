param(
    [string]$GameRoot = "C:\Program Files (x86)\Steam\steamapps\common\Nuclear Option",
    [string]$Configuration = "DEV_SDK",
    [switch]$SkipPatchTool
)

$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "..\_env.ps1")
$RepoRoot = $NOLoaderRepoRoot

if (Get-Process -Name "NuclearOption" -ErrorAction SilentlyContinue) {
    Write-Error "Close Nuclear Option before deploy (PatchTool needs Managed DLLs unlocked)."
}

$project = Join-Path $RepoRoot "DEV.SDK\mods\NOLoader.NVGConfig\NOLoader.NVGConfig.csproj"
dotnet build $project -c $Configuration --verbosity minimal
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$dll = Join-Path $RepoRoot "DEV.SDK\mods\NOLoader.NVGConfig\bin\$Configuration\net48\NOLoader.NVGConfig.dll"
$modConfigProject = Join-Path $RepoRoot "DEV.SDK\shared\NOLoader.ModConfig\NOLoader.ModConfig.csproj"
$modConfigDll = Join-Path $RepoRoot "DEV.SDK\shared\NOLoader.ModConfig\bin\$Configuration\net48\NOLoader.ModConfig.dll"
if (-not (Test-Path $modConfigDll)) {
    dotnet build $modConfigProject -c $Configuration --verbosity minimal
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}

$modRoot = Join-Path $GameRoot "NOLoader\mods\NVGConfig"
New-Item -ItemType Directory -Path $modRoot -Force | Out-Null

Copy-Item -Force $dll (Join-Path $modRoot "NOLoader.NVGConfig.dll")
Copy-Item -Force $modConfigDll (Join-Path $modRoot "NOLoader.ModConfig.dll")
Copy-Item -Force (Join-Path $RepoRoot "DEV.SDK\mods\NOLoader.NVGConfig\mod.json") (Join-Path $modRoot "mod.json")

$iniSrc = Join-Path $RepoRoot "DEV.SDK\mods\NOLoader.NVGConfig\mod_config.ini"
$iniDst = Join-Path $modRoot "mod_config.ini"
if (-not (Test-Path $iniDst)) {
    Copy-Item -Force $iniSrc $iniDst
    Write-Host "Copied default mod_config.ini (edit FilterMode in $iniDst)"
}

if (-not $SkipPatchTool) {
    Write-Host "Applying mod IL patches via PatchTool..."
    dotnet run --project (Join-Path $RepoRoot "src\NOLoader.PatchTool\NOLoader.PatchTool.csproj") -c $Configuration -- $GameRoot
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}

Write-Host "NVGConfig deployed to $modRoot"
