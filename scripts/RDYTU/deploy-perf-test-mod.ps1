param(
    [string]$GameRoot = "C:\Program Files (x86)\Steam\steamapps\common\Nuclear Option",
    [string]$Configuration = "RDYTU",
    [switch]$SkipPatchTool
)

$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "..\_env.ps1")
$RepoRoot = $NOLoaderRepoRoot

if (Get-Process -Name "NuclearOption" -ErrorAction SilentlyContinue) {
    Write-Error "Close Nuclear Option before deploy (PatchTool needs Managed DLLs unlocked)."
}

$project = Join-Path $RepoRoot "DEV.SDK\mods\NOLoader.PerfTest\NOLoader.PerfTest.csproj"
dotnet build $project -c $Configuration --verbosity minimal
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$dll = Join-Path $RepoRoot "DEV.SDK\mods\NOLoader.PerfTest\bin\$Configuration\net48\NOLoader.PerfTest.dll"
$modConfigProject = Join-Path $RepoRoot "DEV.SDK\shared\NOLoader.ModConfig\NOLoader.ModConfig.csproj"
$modConfigDll = Join-Path $RepoRoot "DEV.SDK\shared\NOLoader.ModConfig\bin\$Configuration\net48\NOLoader.ModConfig.dll"
if (-not (Test-Path $modConfigDll)) {
    dotnet build $modConfigProject -c $Configuration --verbosity minimal
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}
$modRoot = Join-Path $GameRoot "NOLoader\mods\PerfTest"
New-Item -ItemType Directory -Path $modRoot -Force | Out-Null

Copy-Item -Force $dll (Join-Path $modRoot "NOLoader.PerfTest.dll")
Copy-Item -Force $modConfigDll (Join-Path $modRoot "NOLoader.ModConfig.dll")
Copy-Item -Force (Join-Path $RepoRoot "DEV.SDK\mods\NOLoader.PerfTest\mod.json") (Join-Path $modRoot "mod.json")

$iniSrc = Join-Path $RepoRoot "DEV.SDK\mods\NOLoader.PerfTest\mod.ini"
$iniDst = Join-Path $modRoot "mod.ini"
if (-not (Test-Path $iniDst)) {
    Copy-Item -Force $iniSrc $iniDst
    Write-Host "Copied default mod.ini (edit HeavyWork in $iniDst if needed)"
}

& (Join-Path $RepoRoot "scripts\pack-mod-rdytu.ps1") -ModFolder $modRoot

if (-not $SkipPatchTool) {
    Write-Host "Applying mod IL patches via PatchTool..."
    dotnet run --project (Join-Path $RepoRoot "src\NOLoader.PatchTool\NOLoader.PatchTool.csproj") -c $Configuration -- $GameRoot
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}

Write-Host "PerfTest deployed to $modRoot"
Write-Host "Next: prepare-perf-test.ps1 -PerfTestOnly, fly a mission, then parse-perftest-ringlog.ps1"
