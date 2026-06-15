param(
    [string]$RepoRoot = "",
    [string]$OutDir = ""
)

$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "_env.ps1")
if ([string]::IsNullOrEmpty($RepoRoot)) { $RepoRoot = $NOLoaderRepoRoot }
if ([string]::IsNullOrEmpty($OutDir)) {
    $OutDir = Join-Path $RepoRoot "artifacts\release\0.1.0"
}

Set-Location $RepoRoot

& (Join-Path $NOLoaderScriptsRoot "build-proxy.ps1") -Configuration Release
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "Building RDYTU..."
dotnet build (Join-Path $RepoRoot "RDYTU\NOLoader.RDYTU.sln") -c RDYTU --verbosity minimal
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "Building DEV_SDK..."
dotnet build (Join-Path $RepoRoot "DEV.SDK\NOLoader.DEV_SDK.sln") -c DEV_SDK --verbosity minimal
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

function Get-BuiltDll($name, $config) {
    Get-ChildItem -Path $RepoRoot -Recurse -Filter $name |
        Where-Object { $_.DirectoryName -like "*\bin\$config\*" } |
        Sort-Object LastWriteTime -Descending |
        Select-Object -First 1
}

New-Item -ItemType Directory -Force -Path $OutDir | Out-Null

# --- RDYTU player zip ---
$rdyStage = Join-Path $env:TEMP "noloader-release-rdytu"
if (Test-Path $rdyStage) { Remove-Item -Recurse -Force $rdyStage }
$coreRdy = Join-Path $rdyStage "NOLoader\core"
$logsRdy = Join-Path $rdyStage "NOLoader\logs"
$modsRdy = Join-Path $rdyStage "NOLoader\mods"
New-Item -ItemType Directory -Force -Path $coreRdy, $logsRdy, $modsRdy | Out-Null
foreach ($dll in @("NOLoader.Core.dll","NOLoader.API.dll","NOLoader.Patcher.dll","NOLoader.Registry.dll","Mono.Cecil.dll")) {
    $src = Get-BuiltDll $dll "RDYTU"
    if (-not $src) { throw "Missing RDYTU $dll" }
    Copy-Item -Force $src.FullName (Join-Path $coreRdy $dll)
}
Copy-Item -Force (Join-Path $RepoRoot "deploy\noloader_config.ini") $rdyStage
Copy-Item -Force (Join-Path $RepoRoot "deploy\NOLoader\mods\README.txt") $modsRdy
Copy-Item -Force (Join-Path $RepoRoot "artifacts\proxy\winhttp.dll") $rdyStage
Copy-Item -Force (Join-Path $RepoRoot "docs\INSTALL.md") (Join-Path $rdyStage "INSTALL.txt")
@"
NOLoader 0.1.0 RDYTU вЂ” player install
Extract into Nuclear Option game root (game CLOSED).
See INSTALL.txt and https://github.com/Mursisru/NOLoader/blob/main/docs/RDYTU.md
Run PatchTool deploy or full deploy script for Cecil patches on Managed DLLs.
"@ | Set-Content (Join-Path $rdyStage "README-RELEASE.txt") -Encoding UTF8

$rdyZip = Join-Path $OutDir "NOLoader-0.1.0-RDYTU.zip"
if (Test-Path $rdyZip) { Remove-Item -Force $rdyZip }
Compress-Archive -Path (Join-Path $rdyStage "*") -DestinationPath $rdyZip -Force
Write-Host "Created $rdyZip"


# --- RDYTU.mini player zip (mod optimizer only) ---
$miniStage = Join-Path $env:TEMP "noloader-release-rdytu-mini"
if (Test-Path $miniStage) { Remove-Item -Recurse -Force $miniStage }
$coreMini = Join-Path $miniStage "NOLoader\core"
$logsMini = Join-Path $miniStage "NOLoader\logs"
$modsMini = Join-Path $miniStage "NOLoader\mods"
New-Item -ItemType Directory -Force -Path $coreMini, $logsMini, $modsMini | Out-Null
foreach ($dll in @("NOLoader.Core.dll","NOLoader.API.dll","NOLoader.Patcher.dll","NOLoader.Registry.dll","Mono.Cecil.dll")) {
    $src = Get-BuiltDll $dll "RDYTU"
    if (-not $src) { throw "Missing RDYTU $dll for mini zip" }
    Copy-Item -Force $src.FullName (Join-Path $coreMini $dll)
}
Copy-Item -Force (Join-Path $RepoRoot "deploy\noloader_config.mini.ini") (Join-Path $miniStage "noloader_config.ini")
Copy-Item -Force (Join-Path $RepoRoot "deploy\NOLoader\mods\README.txt") $modsMini
Copy-Item -Force (Join-Path $RepoRoot "artifacts\proxy\winhttp.dll") $miniStage
Copy-Item -Force (Join-Path $RepoRoot "docs\INSTALL.md") (Join-Path $miniStage "INSTALL.txt")
Copy-Item -Force (Join-Path $RepoRoot "docs\RDYTU.mini.md") (Join-Path $miniStage "RDYTU.mini.txt")
@"
NOLoader 0.1.0 RDYTU.mini - mod optimizer only
Extract into Nuclear Option game root (game CLOSED).
See RDYTU.mini.txt and INSTALL.txt.
https://github.com/Mursisru/NOLoader/blob/main/docs/RDYTU.mini.md
Run PatchTool deploy or .\scripts\RDYTU\deploy-noloader-mini.ps1 for Cecil patches.
"@ | Set-Content (Join-Path $miniStage "README-RELEASE.txt") -Encoding UTF8

$miniZip = Join-Path $OutDir "NOLoader-0.1.0-RDYTU.mini.zip"
if (Test-Path $miniZip) { Remove-Item -Force $miniZip }
Compress-Archive -Path (Join-Path $miniStage "*") -DestinationPath $miniZip -Force
Write-Host "Created $miniZip"
# --- DEV.SDK zip (core + telemetry + sample mod sources) ---
$devStage = Join-Path $env:TEMP "noloader-release-devsdk"
if (Test-Path $devStage) { Remove-Item -Recurse -Force $devStage }
$coreDev = Join-Path $devStage "NOLoader\core"
New-Item -ItemType Directory -Force -Path $coreDev | Out-Null
foreach ($dll in @("NOLoader.Core.dll","NOLoader.API.dll","NOLoader.Patcher.dll","NOLoader.Registry.dll","NOLoader.Telemetry.dll","Mono.Cecil.dll")) {
    $src = Get-BuiltDll $dll "DEV_SDK"
    if (-not $src) { throw "Missing DEV_SDK $dll" }
    Copy-Item -Force $src.FullName (Join-Path $coreDev $dll)
}
Copy-Item -Force (Join-Path $RepoRoot "deploy\noloader_config.ini") $devStage
Copy-Item -Force (Join-Path $RepoRoot "artifacts\proxy\winhttp.dll") $devStage
$samplesDest = Join-Path $devStage "sample-mods"
New-Item -ItemType Directory -Force -Path $samplesDest | Out-Null
Copy-Item -Recurse -Force (Join-Path $RepoRoot "DEV.SDK\mods\NOLoader.RegistrySample") (Join-Path $samplesDest "RegistrySample")
Copy-Item -Recurse -Force (Join-Path $RepoRoot "DEV.SDK\mods\NOLoader.WeaponNames") (Join-Path $samplesDest "WeaponNames")
Copy-Item -Recurse -Force (Join-Path $RepoRoot "DEV.SDK\mods\NOLoader.UniversalLoadout") (Join-Path $samplesDest "UniversalLoadout")
Copy-Item -Force (Join-Path $RepoRoot "docs\DEV_SDK.md") (Join-Path $devStage "DEV_SDK.txt")
@"
NOLoader 0.1.0 DEV.SDK вЂ” developer bundle
Core + Telemetry + proxy + sample mod sources.
Full repo: https://github.com/Mursisru/NOLoader
Deploy: .\scripts\deploy-noloader.ps1 -Configuration DEV_SDK
"@ | Set-Content (Join-Path $devStage "README-RELEASE.txt") -Encoding UTF8

$devZip = Join-Path $OutDir "NOLoader-0.1.0-DEV.SDK.zip"
if (Test-Path $devZip) { Remove-Item -Force $devZip }
Compress-Archive -Path (Join-Path $devStage "*") -DestinationPath $devZip -Force
Write-Host "Created $devZip"

Write-Host "Release zips ready in $OutDir"
