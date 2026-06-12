param(
    [string]$DropRoot = "",
    [string]$GameRoot = "C:\Program Files (x86)\Steam\steamapps\common\Nuclear Option",
    [switch]$InstallToGame
)

$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "_env.ps1")
$RepoRoot = $NOLoaderRepoRoot
$ModsRootRel = "DEV.SDK\mods"
$ManifestRoot = Join-Path $RepoRoot "DEV.SDK\diag-mods"
if ([string]::IsNullOrWhiteSpace($DropRoot)) {
    $DropRoot = Join-Path $RepoRoot "artifacts\diag-mods"
}

function Deploy-DiagPack {
    param(
        [string]$ProjectRelPath,
        [string]$ModFolder,
        [string]$ModDllName,
        [string]$TargetRoot
    )

    Write-Host "Building $ModFolder..."
    dotnet build (Join-Path $RepoRoot $ProjectRelPath) -c Debug --verbosity quiet
    if ($LASTEXITCODE -ne 0) { throw "$ModFolder build failed" }

    $outDir = Join-Path $RepoRoot (Join-Path (Split-Path $ProjectRelPath -Parent) "bin\Debug\net48")
    $deployDir = Join-Path $TargetRoot $ModFolder
    New-Item -ItemType Directory -Force -Path $deployDir | Out-Null
    Copy-Item -Force (Join-Path $ManifestRoot "$ModFolder\mod.json") (Join-Path $deployDir "mod.json")
    Copy-Item -Force (Join-Path $outDir $ModDllName) (Join-Path $deployDir $ModDllName)
    $commonDll = Join-Path $outDir "NOLoader.DiagCommon.dll"
    if (Test-Path $commonDll) {
        Copy-Item -Force $commonDll (Join-Path $deployDir "NOLoader.DiagCommon.dll")
    }
    Write-Host "Packed $ModFolder -> $deployDir"
}

Write-Host "=== NOLoader diag mods pack ===" -ForegroundColor Cyan
New-Item -ItemType Directory -Force -Path $DropRoot | Out-Null

Deploy-DiagPack -ProjectRelPath "$ModsRootRel\NOLoader.LoaderDiagMenu\NOLoader.LoaderDiagMenu.csproj" `
    -ModFolder "LoaderDiagMenu" -ModDllName "NOLoader.LoaderDiagMenu.dll" -TargetRoot $DropRoot
Deploy-DiagPack -ProjectRelPath "$ModsRootRel\NOLoader.LoaderDiag\NOLoader.LoaderDiag.csproj" `
    -ModFolder "LoaderDiag" -ModDllName "NOLoader.LoaderDiag.dll" -TargetRoot $DropRoot

Copy-Item -Force (Join-Path $ManifestRoot "README.md") (Join-Path $DropRoot "README.md")

if ($InstallToGame) {
    $gameMods = Join-Path $GameRoot "NOLoader\mods"
    foreach ($folder in @("LoaderDiagMenu", "LoaderDiag")) {
        $src = Join-Path $DropRoot $folder
        $dst = Join-Path $gameMods $folder
        New-Item -ItemType Directory -Force -Path $dst | Out-Null
        Copy-Item -Force (Join-Path $src "*") $dst
        Write-Host "Installed $folder -> $dst" -ForegroundColor Yellow
    }
}

Write-Host "Diag drop ready: $DropRoot" -ForegroundColor Green

