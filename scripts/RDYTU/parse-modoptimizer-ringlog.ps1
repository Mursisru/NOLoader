param(
    [string]$RingLogPath = "C:\Program Files (x86)\Steam\steamapps\common\Nuclear Option\NOLoader\logs\noloader_ring.log"
)

$ErrorActionPreference = "Stop"
if (-not (Test-Path $RingLogPath)) { Write-Host "[FAIL] Ring log not found"; exit 1 }

$lines = @(Get-Content $RingLogPath | Where-Object { $_ -match '\[ModOpt\]|\[ModOptVerify\]' })
Write-Host "=== NOLoader ModOptimizer Verify (DEV5O1) ===" -ForegroundColor Cyan
Write-Host "Lines: $($lines.Count)"
$joined = $lines -join "`n"

if ($joined -match '\[ModOpt\] enabled') { Write-Host "[ OK ] ModOptimizer enabled" -ForegroundColor Green }
elseif ($joined -match '\[ModOpt\] disabled') { Write-Host "[WARN] mod_optimizer=0" -ForegroundColor Yellow }

foreach ($layer in @('manifest', 'spawn', 'reflection_cache', 'scene_locator', 'find_redirect', 'collision_layers', 'mod_optimizer')) {
    if ($joined -match "\[ModOptVerify\]\[PASS\] $layer") { Write-Host "[ OK ] $layer" -ForegroundColor Green }
}

if ($joined -match '\[ModOpt\]\[PASS\] tick_clean') { Write-Host "[ OK ] analyzer tick_clean" -ForegroundColor Green }
if ($joined -match '\[ModOpt\]\[WARN\]') { Write-Host "[WARN] analyzer warnings present" -ForegroundColor Yellow }
if ($joined -match 'warmup shaders=') { Write-Host "[ OK ] shader warmup logged" -ForegroundColor Green }
if ($joined -match 'collision registered=') { Write-Host "[ OK ] collision registry active" -ForegroundColor Green }

if ($joined -match '\[ModOptVerify\]\[FAIL\]') { Write-Host "[FAIL] see [ModOptVerify][FAIL] above" -ForegroundColor Red }

$lines | Select-Object -Last 20 | ForEach-Object { Write-Host $_ }
