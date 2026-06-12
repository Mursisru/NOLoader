param(
    [string]$TranscriptRoot = "C:\Users\at747\.cursor\projects\c-Users-at747-source-repos-NOLoader-Engine\agent-transcripts\63820b73-8d0d-4225-843e-ee821a5fcbc1"
)

$ErrorActionPreference = "Stop"
$paths = @{}
$files = Get-ChildItem $TranscriptRoot -Recurse -Filter "*.jsonl"
foreach ($file in $files) {
    Get-Content $file.FullName -Encoding UTF8 | ForEach-Object {
        if ($_ -notmatch '"name":"Write"') { return }
        try {
            $j = $_ | ConvertFrom-Json
            foreach ($c in $j.message.content) {
                if ($c.name -ne "Write") { continue }
                $p = [string]$c.input.path
                if ($p -notmatch "NOLoader_Engine") { continue }
                if ($p -match "NOLoader\.(ModConfig|HudCommon|RealWeapon|RepeatTakeoff|AutoFlare|MissileHold|Vectoring|MissileEta|MissileLaunch)|stage-player-mods|convert-bepinex") {
                    $paths[$p] = [string]$c.input.contents
                }
            }
        }
        catch { }
    }
}

Write-Host "Found $($paths.Count) files"
foreach ($p in ($paths.Keys | Sort-Object)) {
    Write-Host $p
    $dest = $p
    if ($dest -match "DEV\.SDK\\mods\\NOLoader\.ModConfig") {
        $dest = $dest -replace "DEV\.SDK\\mods\\NOLoader\.ModConfig", "DEV.SDK\shared\NOLoader.ModConfig"
    }
    $dir = Split-Path -Parent $dest
    if ($dir -and -not (Test-Path $dir)) {
        New-Item -ItemType Directory -Force -Path $dir | Out-Null
    }
    [System.IO.File]::WriteAllText($dest, $paths[$p], [System.Text.UTF8Encoding]::new($false))
}
