param(
    [string]$RingLogPath = "C:\Program Files (x86)\Steam\steamapps\common\Nuclear Option\NOLoader\logs\noloader_ring.log"
)

$ErrorActionPreference = "Stop"
if (-not (Test-Path $RingLogPath)) { Write-Host "[FAIL] Ring log not found"; exit 1 }

$lines = @(Get-Content $RingLogPath | Where-Object { $_ -match '\[GpuVerify\]|\[GpuRender\]' })
Write-Host "=== NOLoader Field Verify (DEV2O13) ===" -ForegroundColor Cyan
Write-Host "Lines: $($lines.Count)"
$joined = $lines -join "`n"

foreach ($layer in @('manifest', 'hud_markers', 'gpu_compute', 'display_detail', 'thrust_sim', 'ring_log')) {
    if ($joined -match "\[GpuVerify\]\[PASS\] $layer") { Write-Host "[ OK ] $layer" -ForegroundColor Green }
}

if ($joined -match '\[GpuVerify\]\[FAIL\]') { Write-Host "[FAIL] see [GpuVerify][FAIL] above" -ForegroundColor Red }
if ($joined -match '\[GpuRender\] enabled') { Write-Host "[ OK ] GpuRender enabled" -ForegroundColor Green }
elseif ($joined -match '\[GpuRender\] disabled') { Write-Host "[WARN] gpu_render=0" -ForegroundColor Yellow }
if ($joined -match 'threadingMode=') { Write-Host "[ OK ] GPU metrics in ring log" -ForegroundColor Green }

$lines | Select-Object -Last 15 | ForEach-Object { Write-Host $_ }
