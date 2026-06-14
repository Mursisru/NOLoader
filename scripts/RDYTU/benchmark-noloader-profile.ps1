param(
    [ValidateSet("A", "B", "C", "D", "status")]
    [string]$Profile = "status",
    [string]$GameRoot = "C:\Program Files (x86)\Steam\steamapps\common\Nuclear Option"
)

$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "..\_env.ps1")
$RepoRoot = $NOLoaderRepoRoot

$iniPath = Join-Path $GameRoot "noloader_config.ini"
$modsRoot = Join-Path $GameRoot "NOLoader\mods"
$ringLog = Join-Path $GameRoot "NOLoader\logs\noloader_ring.log"
$deployScript = Join-Path $PSScriptRoot "deploy-noloader.ps1"

$verifyModNames = @(
    "ModOptimizerVerify",
    "GpuRenderVerify",
    "CoreBalancerVerify",
    "MechanicsVerify"
)

function Get-IniFlags {
    param([string]$Path)
    $flags = @{}
    if (-not (Test-Path $Path)) { return $flags }
    foreach ($line in Get-Content $Path) {
        if ($line -match '^\s*([^#;][^=]+)\s*=\s*(.+)\s*$') {
            $flags[$Matches[1].Trim()] = $Matches[2].Trim()
        }
    }
    return $flags
}

function Show-Status {
    Write-Host ""
    Write-Host "=== NOLoader FPS benchmark status ===" -ForegroundColor Cyan
    Write-Host "GameRoot: $GameRoot"
    Write-Host "NOLoader proxy: $(if (Test-Path (Join-Path $GameRoot 'winhttp.dll')) { 'installed' } else { 'missing' })"
    Write-Host ""
    Write-Host "mods/:"
    $dirs = @(Get-ChildItem $modsRoot -Directory -ErrorAction SilentlyContinue)
    if ($dirs.Count -eq 0) {
        Write-Host "  (empty - production OK)"
    } else {
        foreach ($d in $dirs) {
            $tag = if ($verifyModNames -contains $d.Name) { " [VERIFY - remove for flight]" } else { "" }
            Write-Host "  - $($d.Name)$tag"
        }
    }
    Write-Host ""
    Write-Host "noloader_config.ini (perf keys):"
    $ini = Get-IniFlags $iniPath
    foreach ($key in @(
        'engine_tweaker', 'culling_optimizer', 'culling_ground_wheels', 'culling_pilot_anim', 'culling_offscreen_only', 'culling_on_screen_max_m',
        'culling_ground_renderer', 'fps_adaptive_detail',
        'hud_marker_throttle', 'hud_markers_per_frame',
        'ring_log', 'mod_optimizer', 'gpu_render', 'gpu_hud_pass', 'world_snapshot_stride', 'frame_cache'
    )) {
        $val = if ($ini.ContainsKey($key)) { $ini[$key] } else { '(missing)' }
        Write-Host "  $key=$val"
    }
    Write-Host ""
    if (Test-Path $ringLog) {
        Write-Host "ring log tail (last 8 lines):"
        Get-Content $ringLog -Tail 8 | ForEach-Object { Write-Host "  $_" }
    } else {
        Write-Host "ring log: (not found - launch game once)"
    }
    Write-Host ""
    Write-Host "Profiles:"
    Write-Host "  A vanilla   - uninstall NOLoader or rename winhttp.dll"
    Write-Host "  B maxopt    - production deploy (default noloader_config.ini)"
    Write-Host "  C fieldtest - deploy -FieldTest + verify mods"
    Write-Host "  D minimal   - deploy -Minimal (Gate L4 only)"
}

function Apply-ProfileB {
    if (Get-Process -Name "NuclearOption" -ErrorAction SilentlyContinue) {
        Write-Error "Close Nuclear Option before profile B deploy."
    }
    & $deployScript -GameRoot $GameRoot
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    Write-Host ""
    Write-Host "Profile B ready: max-opt production INI (HUD throttle + off-screen ground wheel cull)." -ForegroundColor Green
}

function Apply-ProfileC {
    if (Get-Process -Name "NuclearOption" -ErrorAction SilentlyContinue) {
        Write-Error "Close Nuclear Option before profile C deploy."
    }
    & $deployScript -GameRoot $GameRoot -FieldTest
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    Write-Host ""
    Write-Host "Profile C base INI applied (-FieldTest). Deploy a verify mod explicitly, e.g.:"
    Write-Host "  .\deploy-modoptimizer-verify-mod.ps1 -EnableModOptimizer"
    Write-Host ""
}

function Apply-ProfileD {
    if (Get-Process -Name "NuclearOption" -ErrorAction SilentlyContinue) {
        Write-Error "Close Nuclear Option before profile D deploy."
    }
    & $deployScript -GameRoot $GameRoot -Minimal
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    Write-Host ""
    Write-Host "Profile D ready: minimal Gate L4 only (no perf hooks)." -ForegroundColor Green
}

function Show-Checklist {
    Write-Host ""
    Write-Host "=== Field checklist (dense units / 30+ AI) ===" -ForegroundColor Cyan
    Write-Host '[ ] Same map / weather / aircraft'
    Write-Host '[ ] Fly through cluster of units — record avg FPS 2-3 min'
    Write-Host '[ ] ring log: ground_offscreen_skip, ground_audio_skip, cull_skip'
    Write-Host '[ ] Compare A (vanilla) vs B (maxopt) vs D (minimal)'
    Write-Host '[ ] Profile C only when testing ModOptimizer/GpuRender verify mods'
    Write-Host ""
}

switch ($Profile) {
    "status" { Show-Status; Show-Checklist }
    "A" {
        Write-Host "Profile A (vanilla): uninstall NOLoader or restore winhttp.dll manually."
        Write-Host "  .\uninstall-for-fps-test.ps1"
        Show-Checklist
    }
    "B" { Apply-ProfileB; Show-Status; Show-Checklist }
    "C" { Apply-ProfileC; Show-Status; Show-Checklist }
    "D" { Apply-ProfileD; Show-Status; Show-Checklist }
}
