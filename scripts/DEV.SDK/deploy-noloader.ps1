param(
    [string]$GameRoot = "C:\Program Files (x86)\Steam\steamapps\common\Nuclear Option",
    [switch]$SkipCecilPrePatch,
    [switch]$SkipHashVerify
)

$ErrorActionPreference = "Stop"
. (Join-Path (Split-Path $PSScriptRoot -Parent) "_env.ps1")
$SdkRoot = $NOLoaderSdkRoot
$RepoRoot = $NOLoaderRepoRoot
$Configuration = "DEV_SDK"
$Solution = Join-Path $SdkRoot "NOLoader.DEV_SDK.sln"
$ModsRootRel = "DEV.SDK\mods"

Set-Location $RepoRoot

& (Join-Path $NOLoaderScriptsRoot "build-proxy.ps1") -Configuration Release
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "Building NOLoader DEV.SDK ($Configuration)..."
dotnet build $Solution -c $Configuration --verbosity minimal
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$DeployRoot = Join-Path $GameRoot "NOLoader"
$CoreDeploy = Join-Path $DeployRoot "core"
$ModsRoot = Join-Path $DeployRoot "mods"

New-Item -ItemType Directory -Force -Path $CoreDeploy | Out-Null
New-Item -ItemType Directory -Force -Path $ModsRoot | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $DeployRoot "logs") | Out-Null

$legacyModFolders = @("TestMod", "LoaderLab", "MechanicsTest", "FaultMission", "LoaderDiagMenu", "LoaderDiag")
foreach ($folder in $legacyModFolders) {
    Remove-Item (Join-Path $ModsRoot $folder) -Recurse -Force -ErrorAction SilentlyContinue
}

Copy-Item -Force (Join-Path $RepoRoot "deploy\NOLoader\mods\README.txt") (Join-Path $ModsRoot "README.txt")

$coreDlls = @(
    "NOLoader.Core.dll",
    "NOLoader.API.dll",
    "NOLoader.Patcher.dll",
    "NOLoader.Registry.dll",
    "NOLoader.Telemetry.dll",
    "Mono.Cecil.dll"
)

foreach ($dll in $coreDlls) {
    $src = Get-ChildItem -Path $RepoRoot -Recurse -Filter $dll | Where-Object { $_.DirectoryName -like "*\bin\$Configuration\*" } | Select-Object -First 1
    if ($src) {
        Copy-Item -Force $src.FullName (Join-Path $CoreDeploy $dll)
    }
}

Copy-Item -Force (Join-Path $RepoRoot "deploy\noloader_config.ini") (Join-Path $GameRoot "noloader_config.ini")

$proxyCandidates = @(
    (Join-Path $RepoRoot "artifacts\proxy\winhttp.dll"),
    (Join-Path $RepoRoot "native\NOLoader.Proxy\bin\x64\Release\proxy\winhttp.dll"),
    (Join-Path $RepoRoot "bin\x64\Release\proxy\winhttp.dll")
)
$proxyPath = $proxyCandidates | Where-Object { Test-Path $_ } | Select-Object -First 1
if (Test-Path $proxyPath) {
    Copy-Item -Force $proxyPath (Join-Path $GameRoot "winhttp.dll")
    Write-Host "Deployed winhttp.dll proxy"
} else {
    Write-Warning "Native proxy not built. Run MSBuild on native\NOLoader.Proxy (Release|x64) or use Doorstop interim per docs/INSTALL.md"
}

Write-Host "Deployed NOLoader core to $DeployRoot"

Write-Host "Building RegistrySample mod..."
dotnet build (Join-Path $RepoRoot "$ModsRootRel\NOLoader.RegistrySample\NOLoader.RegistrySample.csproj") -c Debug --verbosity quiet
if ($LASTEXITCODE -eq 0) {
    $sampleDll = Join-Path $RepoRoot "$ModsRootRel\NOLoader.RegistrySample\bin\Debug\net48\NOLoader.RegistrySample.dll"
    $sampleDeploy = Join-Path $ModsRoot "RegistrySample"
    New-Item -ItemType Directory -Force -Path $sampleDeploy | Out-Null
    Copy-Item -Force (Join-Path $RepoRoot "DEV.SDK\mods\NOLoader.RegistrySample\mod.json") (Join-Path $sampleDeploy "mod.json")
    Copy-Item -Force $sampleDll (Join-Path $sampleDeploy "NOLoader.RegistrySample.dll")
    Write-Host "Deployed RegistrySample mod (loadStage: MainMenu)"
} else {
    Write-Warning "RegistrySample build failed"
}

Write-Host "Building WeaponNames mod..."
dotnet build (Join-Path $RepoRoot "$ModsRootRel\NOLoader.WeaponNames\NOLoader.WeaponNames.csproj") -c Debug --verbosity quiet
if ($LASTEXITCODE -ne 0) { Write-Error "WeaponNames build failed" }
$weaponDll = Join-Path $RepoRoot "$ModsRootRel\NOLoader.WeaponNames\bin\Debug\net48\NOLoader.WeaponNames.dll"
if (-not (Test-Path $weaponDll)) { Write-Error "WeaponNames DLL missing: $weaponDll" }
$weaponDeploy = Join-Path $ModsRoot "WeaponNames"
New-Item -ItemType Directory -Force -Path $weaponDeploy | Out-Null
Copy-Item -Force (Join-Path $RepoRoot "DEV.SDK\mods\NOLoader.WeaponNames\mod.json") (Join-Path $weaponDeploy "mod.json")
Copy-Item -Force $weaponDll (Join-Path $weaponDeploy "NOLoader.WeaponNames.dll")
Write-Host "Deployed WeaponNames mod (loadStage: MainMenu)"

Write-Host "Building UniversalLoadout mod..."
dotnet build (Join-Path $RepoRoot "$ModsRootRel\NOLoader.UniversalLoadout\NOLoader.UniversalLoadout.csproj") -c Debug --verbosity quiet
if ($LASTEXITCODE -ne 0) { Write-Error "UniversalLoadout build failed" }
$ulDll = Join-Path $RepoRoot "$ModsRootRel\NOLoader.UniversalLoadout\bin\Debug\net48\NOLoader.UniversalLoadout.dll"
if (-not (Test-Path $ulDll)) { Write-Error "UniversalLoadout DLL missing: $ulDll" }
$ulDeploy = Join-Path $ModsRoot "UniversalLoadout"
New-Item -ItemType Directory -Force -Path $ulDeploy | Out-Null
Copy-Item -Force (Join-Path $RepoRoot "DEV.SDK\mods\NOLoader.UniversalLoadout\mod.json") (Join-Path $ulDeploy "mod.json")
Copy-Item -Force $ulDll (Join-Path $ulDeploy "NOLoader.UniversalLoadout.dll")
Write-Host "Deployed UniversalLoadout mod (loadStage: MainMenu)"

Write-Host "Building BrokenMod (Gate L2 negative fixture)..."
dotnet build (Join-Path $RepoRoot "$ModsRootRel\NOLoader.BrokenMod\NOLoader.BrokenMod.csproj") -c Debug --verbosity quiet
if ($LASTEXITCODE -eq 0) {
    $brokenDll = Join-Path $RepoRoot "$ModsRootRel\NOLoader.BrokenMod\bin\Debug\net48\NOLoader.BrokenMod.dll"
    $brokenDeploy = Join-Path $ModsRoot "BrokenMod"
    New-Item -ItemType Directory -Force -Path $brokenDeploy | Out-Null
    Copy-Item -Force (Join-Path $RepoRoot "DEV.SDK\mods\NOLoader.BrokenMod\mod.json") (Join-Path $brokenDeploy "mod.json")
    Copy-Item -Force $brokenDll (Join-Path $brokenDeploy "NOLoader.BrokenMod.dll")
    Write-Host "Deployed BrokenMod (Gate L2 negative, filtered at L2, game continues)"
} else {
    Write-Warning "BrokenMod build failed"
}

if (-not $SkipHashVerify -and (Test-Path $GameRoot)) {
    Write-Host "Verifying baked core patch hashes..."
    & (Join-Path $NOLoaderScriptsRoot "verify-core-patch-hashes.ps1") -GameRoot $GameRoot
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Core patch hash mismatch - run scripts\bake-core-patch-hashes.ps1 after game update (or -SkipHashVerify)"
    }
}

$gameProc = Get-Process -Name "NuclearOption" -ErrorAction SilentlyContinue
if ($SkipCecilPrePatch) {
    Write-Host "Cecil pre-patch skipped (-SkipCecilPrePatch)"
} elseif ($gameProc) {
    Write-Warning "Game is running - Cecil pre-patch skipped. Close the game and re-run deploy for IL hooks."
} else {
    Write-Host "Pre-applying Cecil patches..."
    dotnet run --project (Join-Path $RepoRoot "src\NOLoader.PatchTool\NOLoader.PatchTool.csproj") -c $Configuration -- $GameRoot
    if ($LASTEXITCODE -ne 0) {
        Write-Warning "Cecil pre-patch failed (exit $LASTEXITCODE)"
    } else {
        Write-Host "Cecil pre-patch OK"
    }
}

