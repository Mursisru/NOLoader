param(
    [string]$RepoRoot = "C:\Users\at747\source\repos\NOLoader_Engine"
)

$ErrorActionPreference = "Stop"
$mods = Join-Path $RepoRoot "DEV.SDK\mods"
$shared = Join-Path $RepoRoot "DEV.SDK\shared"
$missileEtaSrc = "C:\Users\at747\source\repos\MissileETA_Engine\MissileETA_Engine"
$hudCommon = Join-Path $mods "NOLoader.HudCommon"

$hudFiles = @(
    "PrismPointerGraphic.cs",
    "FlightHudStyleReader.cs",
    "HudScreenScale.cs",
    "HudScreenPlacement.cs"
)
foreach ($f in $hudFiles) {
    Copy-Item -Force (Join-Path $missileEtaSrc $f) (Join-Path $hudCommon $f)
}

function Copy-Port {
    param(
        [string]$SourceDir,
        [string]$DestDir,
        [string]$OldNs,
        [string]$NewNs,
        [string[]]$Files,
        [string[]]$Exclude = @()
    )
    New-Item -ItemType Directory -Force -Path $DestDir | Out-Null
    foreach ($f in $Files) {
        if ($Exclude -contains $f) { continue }
        $src = Join-Path $SourceDir $f
        if (-not (Test-Path $src)) { Write-Warning "Missing $src"; continue }
        $text = Get-Content $src -Raw -Encoding UTF8
        $text = $text -replace "using HarmonyLib;\r?\n", ""
        $text = $text -replace "\[HarmonyPatch[^\]]*\]\r?\n", ""
        $text = $text -replace "using BepInEx[^\r\n]*\r?\n", ""
        $text = $text -replace "using BepInEx\.Configuration;\r?\n", ""
        $text = $text -replace "namespace $([regex]::Escape($OldNs))", "namespace $NewNs"
        $text = $text -replace "$([regex]::Escape($OldNs))\.", "$NewNs."
        [System.IO.File]::WriteAllText((Join-Path $DestDir $f), $text, [System.Text.UTF8Encoding]::new($false))
    }
}

$etaDest = Join-Path $mods "NOLoader.MissileEta"
$etaFiles = Get-ChildItem $missileEtaSrc -Filter "*.cs" | Where-Object { $_.Name -ne "MissileEtaPlugin.cs" -and $_.Name -ne "Properties" } | ForEach-Object Name
Copy-Port -SourceDir $missileEtaSrc -DestDir $etaDest -OldNs "MissileETA_Engine" -NewNs "NOLoader.MissileEta" -Files $etaFiles -Exclude $hudFiles

$arcSrc = "C:\Users\at747\source\repos\MissileLaunchArcHud_Engine\MissileLaunchArcHud_Engine"
$arcDest = Join-Path $mods "NOLoader.MissileLaunchArcHud"
$arcFiles = Get-ChildItem $arcSrc -Filter "*.cs" | Where-Object { $_.Name -ne "MissileLaunchArcHudPlugin.cs" } | ForEach-Object Name
Copy-Port -SourceDir $arcSrc -DestDir $arcDest -OldNs "MissileLaunchArcHud_Engine" -NewNs "NOLoader.MissileLaunchArcHud" -Files $arcFiles -Exclude $hudFiles

$vecSrc = "C:\Users\at747\source\repos\VectoringTargetHud_Engine"
$vecDest = Join-Path $mods "NOLoader.VectoringTargetHud"
$vecFiles = @("TargetHudLineController.cs", "PrismPointerGraphic.cs")
Copy-Port -SourceDir $vecSrc -DestDir $vecDest -OldNs "VectoringTargetHUD_Engine" -NewNs "NOLoader.VectoringTargetHud" -Files $vecFiles -Exclude @("PrismPointerGraphic.cs")

$holdSrc = "C:\Users\at747\source\repos\MissileHoldCam_Engine\MissileHoldCam_Engine"
$holdDest = Join-Path $mods "NOLoader.MissileHoldCam"
Copy-Port -SourceDir $holdSrc -DestDir $holdDest -OldNs "MissileHoldCam_Engine" -NewNs "NOLoader.MissileHoldCam" -Files @("MissileHoldCamController.cs", "MissileHoldCamConfigCache.cs")

$rwnSrc = "C:\Users\at747\source\repos\RealWeaponNames_Engine\RealWeaponNames_Engine"
$rwnDest = Join-Path $mods "NOLoader.RealWeaponNames"
New-Item -ItemType Directory -Force -Path (Join-Path $rwnDest "Services") | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $rwnDest "Data") | Out-Null
Copy-Port -SourceDir (Join-Path $rwnSrc "Services") -DestDir (Join-Path $rwnDest "Services") -OldNs "RealWeaponNames_Engine.Services" -NewNs "NOLoader.RealWeaponNames.Services" -Files @("WeaponDisplayNameResolver.cs", "WeaponSelectorUiHelper.cs")
Copy-Port -SourceDir (Join-Path $rwnSrc "Data") -DestDir (Join-Path $rwnDest "Data") -OldNs "RealWeaponNames_Engine.Data" -NewNs "NOLoader.RealWeaponNames.Data" -Files @("WeaponNameDictionary.cs")

Write-Host "Bootstrap copy complete."
