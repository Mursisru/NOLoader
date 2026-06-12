param(
    [string]$RepoRoot = "",
    [string]$Configuration = "DEV_SDK",
    [string]$BepInExConfigRoot = "C:\Users\at747\Desktop\.dll\BepInEx\config"
)

$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "_env.ps1")
if ([string]::IsNullOrEmpty($RepoRoot)) { $RepoRoot = $NOLoaderRepoRoot }

$ModsRootRel = "DEV.SDK\mods"
$StagingRoot = Join-Path $RepoRoot "deploy\NOLoader\mods"
$ModConfigProject = Join-Path $RepoRoot "DEV.SDK\shared\NOLoader.ModConfig\NOLoader.ModConfig.csproj"
$ModConfigDllName = "NOLoader.ModConfig.dll"

Get-ChildItem $StagingRoot -Directory -ErrorAction SilentlyContinue | Remove-Item -Recurse -Force

function Get-ModBinDll {
    param([string]$ProjectName)
    $proj = Join-Path $RepoRoot "$ModsRootRel\$ProjectName\$ProjectName.csproj"
    dotnet build $proj -c $Configuration --verbosity quiet
    if ($LASTEXITCODE -ne 0) { throw "Build failed: $ProjectName" }
    $dll = Get-ChildItem -Path (Join-Path $RepoRoot "$ModsRootRel\$ProjectName\bin\$Configuration") -Recurse -Filter "$ProjectName.dll" -ErrorAction SilentlyContinue |
        Sort-Object LastWriteTime -Descending | Select-Object -First 1
    if (-not $dll) {
        $dll = Get-ChildItem -Path (Join-Path $RepoRoot "$ModsRootRel\$ProjectName\bin") -Recurse -Filter "$ProjectName.dll" -ErrorAction SilentlyContinue |
            Sort-Object LastWriteTime -Descending | Select-Object -First 1
    }
    if (-not $dll) { throw "DLL not found after build: $ProjectName" }
    return $dll
}

function Get-ModConfigDll {
    $dll = Get-ChildItem -Path (Join-Path $RepoRoot "DEV.SDK\shared\NOLoader.ModConfig\bin") -Recurse -Filter $ModConfigDllName -ErrorAction SilentlyContinue |
        Sort-Object LastWriteTime -Descending | Select-Object -First 1
    if (-not $dll) {
        dotnet build $ModConfigProject -c $Configuration --verbosity quiet
        if ($LASTEXITCODE -ne 0) { throw "Build failed: NOLoader.ModConfig" }
        $dll = Get-ChildItem -Path (Join-Path $RepoRoot "DEV.SDK\shared\NOLoader.ModConfig\bin") -Recurse -Filter $ModConfigDllName |
            Sort-Object LastWriteTime -Descending | Select-Object -First 1
    }
    return $dll
}

function Deploy-PlayerMod {
    param(
        [string]$ProjectName,
        [string]$DeployFolderName,
        [switch]$IncludeModConfig,
        [string]$CfgSource = "",
        [switch]$UseProjectModIni
    )

    $srcDir = Join-Path $RepoRoot "$ModsRootRel\$ProjectName"
    $dest = Join-Path $StagingRoot $DeployFolderName
    New-Item -ItemType Directory -Force -Path $dest | Out-Null

    Copy-Item -Force (Join-Path $srcDir "mod.json") (Join-Path $dest "mod.json")

    $mainDll = Get-ModBinDll -ProjectName $ProjectName
    Copy-Item -Force $mainDll.FullName (Join-Path $dest "$ProjectName.dll")

    if ($IncludeModConfig) {
        $cfgDll = Get-ModConfigDll
        Copy-Item -Force $cfgDll.FullName (Join-Path $dest $ModConfigDllName)
    }

    $destIni = Join-Path $dest "mod_config.ini"
    if ($UseProjectModIni) {
        $projectIni = Join-Path $srcDir "mod_config.ini"
        if (-not (Test-Path $projectIni)) { throw "Missing mod_config.ini: $projectIni" }
        Copy-Item -Force $projectIni $destIni
    }
    elseif ($CfgSource -and (Test-Path $CfgSource)) {
        & (Join-Path $NOLoaderScriptsRoot "convert-bepinex-cfg-to-mod-ini.ps1") -SourceCfg $CfgSource -OutputIni $destIni
    }

    Write-Host "Staged: $DeployFolderName"
}

$playerModFolders = @(
    "HudCommon", "RepeatTakeoffMusic", "RealWeaponNames",
    "MissileHoldCam", "MissileEta"
)
foreach ($name in $playerModFolders) {
    Remove-Item (Join-Path $StagingRoot $name) -Recurse -Force -ErrorAction SilentlyContinue
}

$cfg = $BepInExConfigRoot

Deploy-PlayerMod -ProjectName "NOLoader.HudCommon" -DeployFolderName "HudCommon"
Deploy-PlayerMod -ProjectName "NOLoader.RepeatTakeoffMusic" -DeployFolderName "RepeatTakeoffMusic" -IncludeModConfig -CfgSource (Join-Path $cfg "com.at747.repeattakeoffmusic.cfg")
Deploy-PlayerMod -ProjectName "NOLoader.RealWeaponNames" -DeployFolderName "RealWeaponNames" -IncludeModConfig -CfgSource (Join-Path $cfg "com.at747.realweaponnames.cfg")
Deploy-PlayerMod -ProjectName "NOLoader.MissileHoldCam" -DeployFolderName "MissileHoldCam" -IncludeModConfig -CfgSource (Join-Path $cfg "com.at747.missileholdcam.cfg")
Deploy-PlayerMod -ProjectName "NOLoader.MissileEta" -DeployFolderName "MissileEta" -IncludeModConfig -CfgSource (Join-Path $cfg "com.at747.missileeta.cfg")

Write-Host "Player mods staged -> $StagingRoot"
