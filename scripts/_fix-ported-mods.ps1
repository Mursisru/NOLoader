param(
    [string]$RepoRoot = "C:\Users\at747\source\repos\NOLoader_Engine"
)

$ErrorActionPreference = "Stop"
$modsRoot = Join-Path $RepoRoot "DEV.SDK\mods"

function Add-UsingIfMissing {
    param([string]$FilePath, [string]$Using)
    $text = Get-Content $FilePath -Raw -Encoding UTF8
    if ($text -notmatch [regex]::Escape($Using)) {
        $text = $text -replace "(^using [^\r\n]+;\r?\n)+", "`$0$Using`r`n"
        if ($text -notmatch [regex]::Escape($Using)) {
            $text = "$Using`r`n" + $text
        }
        [System.IO.File]::WriteAllText($FilePath, $text, [System.Text.UTF8Encoding]::new($false))
    }
}

function Fix-Namespace {
    param([string]$Dir, [string]$OldNs, [string]$NewNs)
    Get-ChildItem $Dir -Filter *.cs -Recurse | ForEach-Object {
        $text = Get-Content $_.FullName -Raw -Encoding UTF8
        $text = $text -replace "namespace $([regex]::Escape($OldNs))", "namespace $NewNs"
        [System.IO.File]::WriteAllText($_.FullName, $text, [System.Text.UTF8Encoding]::new($false))
    }
}

Fix-Namespace (Join-Path $modsRoot "NOLoader.HudCommon") "MissileETA_Engine" "NOLoader.HudCommon"

$etaDir = Join-Path $modsRoot "NOLoader.MissileEta"
Get-ChildItem $etaDir -Filter *.cs | ForEach-Object {
    Add-UsingIfMissing $_.FullName "using NOLoader.HudCommon;"
    $text = Get-Content $_.FullName -Raw -Encoding UTF8
    $text = $text -replace "MissileEtaPlugin\.(\w+)\.Value", 'MissileEtaConfigCache.$1'
    [System.IO.File]::WriteAllText($_.FullName, $text, [System.Text.UTF8Encoding]::new($false))
}
Remove-Item (Join-Path $etaDir "AppVersion.cs") -Force -ErrorAction SilentlyContinue

$arcDir = Join-Path $modsRoot "NOLoader.MissileLaunchArcHud"
Get-ChildItem $arcDir -Filter *.cs | ForEach-Object {
    Add-UsingIfMissing $_.FullName "using NOLoader.HudCommon;"
    $text = Get-Content $_.FullName -Raw -Encoding UTF8
    $text = $text -replace "MissileLaunchArcHudPlugin\.(\w+)\.Value", 'MissileLaunchArcHudConfigCache.$1'
    [System.IO.File]::WriteAllText($_.FullName, $text, [System.Text.UTF8Encoding]::new($false))
}
Remove-Item (Join-Path $arcDir "AppVersion.cs") -Force -ErrorAction SilentlyContinue

$vecDir = Join-Path $modsRoot "NOLoader.VectoringTargetHud"
Get-ChildItem $vecDir -Filter *.cs | ForEach-Object {
    Add-UsingIfMissing $_.FullName "using NOLoader.HudCommon;"
    $text = Get-Content $_.FullName -Raw -Encoding UTF8
    $text = $text -replace "GetValue\(VectoringTargetHUDPlugin\.(\w+),", 'VectoringTargetHudConfigCache.$1 ?? '
    $text = $text -replace "GetString\(VectoringTargetHUDPlugin\.(\w+),", 'VectoringTargetHudConfigCache.$1 ?? '
    [System.IO.File]::WriteAllText($_.FullName, $text, [System.Text.UTF8Encoding]::new($false))
}

$holdDir = Join-Path $modsRoot "NOLoader.MissileHoldCam"
Get-ChildItem $holdDir -Filter *.cs | ForEach-Object {
    $text = Get-Content $_.FullName -Raw -Encoding UTF8
    $text = $text -replace "MissileHoldCamPlugin\.(\w+)\.Value", 'MissileHoldCamConfigCache.$1'
    $text = $text -replace "MissileHoldCamPlugin\.(\w+) != null && MissileHoldCamPlugin\.\1\.Value", 'MissileHoldCamConfigCache.$1'
    [System.IO.File]::WriteAllText($_.FullName, $text, [System.Text.UTF8Encoding]::new($false))
}

Write-Host "Fix script complete."
