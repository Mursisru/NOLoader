param(
    [string]$GameRoot = "C:\Program Files (x86)\Steam\steamapps\common\Nuclear Option",
    [string]$Configuration = "RDYTU",
    [switch]$KeepOtherMods,
    [switch]$EnableCollisionLayers,
    [switch]$FullProbe
)

$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "..\_env.ps1")
$RepoRoot = $NOLoaderRepoRoot

if (Get-Process -Name "NuclearOption" -ErrorAction SilentlyContinue) {
    Write-Error "Close Nuclear Option before deploy."
}

$project = Join-Path $RepoRoot "DEV.SDK\mods\NOLoader.ModOptimizerVerify\NOLoader.ModOptimizerVerify.csproj"
dotnet build $project -c $Configuration --verbosity minimal
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$dll = Join-Path $RepoRoot "DEV.SDK\mods\NOLoader.ModOptimizerVerify\bin\$Configuration\net48\NOLoader.ModOptimizerVerify.dll"

$modsRoot = Join-Path $GameRoot "NOLoader\mods"
$modRoot = Join-Path $modsRoot "ModOptimizerVerify"
New-Item -ItemType Directory -Path $modRoot -Force | Out-Null

if (-not $KeepOtherMods) {
    Get-ChildItem -Path $modsRoot -Directory | Where-Object { $_.Name -ne "ModOptimizerVerify" } | ForEach-Object {
        Remove-Item -Recurse -Force $_.FullName
    }
}

Copy-Item -Force $dll (Join-Path $modRoot "NOLoader.ModOptimizerVerify.dll")
Copy-Item -Force (Join-Path $RepoRoot "DEV.SDK\mods\NOLoader.ModOptimizerVerify\mod.json") (Join-Path $modRoot "mod.json")

$iniSrc = Join-Path $RepoRoot "DEV.SDK\mods\NOLoader.ModOptimizerVerify\mod.ini"
$iniDst = Join-Path $modRoot "mod.ini"
Copy-Item -Force $iniSrc $iniDst
if ($FullProbe) {
    $iniText = Get-Content $iniDst -Raw
    if ($iniText -match '(?m)^spawn_count\s*=') {
        $iniText = $iniText -replace '(?m)^spawn_count\s*=.*', 'spawn_count=30'
    } else {
        $iniText = $iniText.TrimEnd() + "`r`nspawn_count=30`r`n"
    }
    Set-Content -Path $iniDst -Value $iniText -NoNewline
}

& (Join-Path $RepoRoot "scripts\pack-mod-rdytu.ps1") -ModFolder $modRoot

$noloaderIni = Join-Path $GameRoot "noloader_config.ini"
if (Test-Path $noloaderIni) {
    $content = Get-Content $noloaderIni -Raw
    foreach ($pair in @('mod_optimizer=1', 'mod_tick_analyzer=1', 'mod_reflection_cache=1', 'mod_scene_locator=1', 'mod_shader_warmup=1')) {
        $key = ($pair -split '=')[0]
        if ($content -match "(?m)^$key\s*=") { $content = $content -replace "(?m)^$key\s*=.*", $pair }
        else { $content = $content.TrimEnd() + "`r`n$pair`r`n" }
    }
    if ($EnableCollisionLayers) {
        $pair = 'mod_collision_layers=1'
        $key = 'mod_collision_layers'
        if ($content -match "(?m)^$key\s*=") { $content = $content -replace "(?m)^$key\s*=.*", $pair }
        else { $content = $content.TrimEnd() + "`r`n$pair`r`n" }
    }
    Set-Content -Path $noloaderIni -Value $content -NoNewline
}

Write-Host "ModOptimizer verify mod deployed to $modRoot (DEV4O2 lite=$([int](-not $FullProbe)))"
