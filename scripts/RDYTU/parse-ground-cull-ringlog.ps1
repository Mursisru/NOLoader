param(
    [string]$GameRoot = "C:\Program Files (x86)\Steam\steamapps\common\Nuclear Option",
    [int]$TailLines = 200
)

$ErrorActionPreference = "Stop"
$ringLog = Join-Path $GameRoot "NOLoader\logs\noloader_ring.log"
if (-not (Test-Path $ringLog)) {
    Write-Error "Ring log not found: $ringLog (launch game with ring_log=1)"
}

$lines = @(Get-Content $ringLog -Tail $TailLines)
$tweaker = @($lines | Where-Object { $_ -match '\[EngineTweaker\]' })
$init = @($lines | Where-Object { $_ -match 'NOEngineTweaker initialized' })

Write-Host ""
Write-Host "=== Ground cull ring log summary ===" -ForegroundColor Cyan
Write-Host "Log: $ringLog"
Write-Host "Tail: $TailLines lines | EngineTweaker entries: $($tweaker.Count)"
Write-Host ""

if ($init.Count -gt 0) {
    Write-Host "Boot:"
    $init | Select-Object -Last 1 | ForEach-Object { Write-Host "  $_" }
    Write-Host ""
}

function Get-StatValue {
    param([string]$Line, [string]$Key)
    if ($Line -match ($Key + '=([0-9]+)')) { return [long]$Matches[1] }
    return $null
}

$rows = @()
for ($i = 0; $i -lt $tweaker.Count; $i++) {
    $line = $tweaker[$i]
    $rows += [pscustomobject]@{
        Index = $i + 1
        CullSkip = Get-StatValue $line 'cull_skip'
        OffscreenSkip = Get-StatValue $line 'ground_offscreen_skip'
        AudioSkip = Get-StatValue $line 'ground_audio_skip'
        RendererSkip = Get-StatValue $line 'ground_renderer_skip'
        AdaptiveDetail = if ($line -match 'adaptive_detail=on') { 'on' } elseif ($line -match 'adaptive_detail=off') { 'off' } else { $null }
        HudMarkerSkip = Get-StatValue $line 'hud_marker_skip'
        Raw = $line
    }
}

if ($rows.Count -eq 0) {
    Write-Host "No [EngineTweaker] lines in tail. Enable ring_log=1 and fly near dense ground units."
    exit 0
}

$last = $rows[-1]
Write-Host "Latest stats:"
Write-Host "  cull_skip=$($last.CullSkip)"
Write-Host "  ground_offscreen_skip=$($last.OffscreenSkip)"
Write-Host "  ground_audio_skip=$($last.AudioSkip)"
Write-Host "  ground_renderer_skip=$($last.RendererSkip)"
Write-Host "  adaptive_detail=$($last.AdaptiveDetail)"
Write-Host "  hud_marker_skip=$($last.HudMarkerSkip)"
Write-Host ""

if ($rows.Count -ge 2) {
    $first = $rows[0]
    Write-Host "Delta (first -> last in tail):"
    Write-Host "  cull_skip +$($last.CullSkip - $first.CullSkip)"
    Write-Host "  ground_offscreen_skip +$($last.OffscreenSkip - $first.OffscreenSkip)"
    Write-Host "  ground_audio_skip +$($last.AudioSkip - $first.AudioSkip)"
    Write-Host ""
}

Write-Host "Field A/B (60 s each, same route):"
Write-Host "  Run 1: look at airport cluster"
Write-Host "  Run 2: look at horizon / away from cluster"
Write-Host "  Compare ground_offscreen_skip growth and ground_audio_skip vs FPS"
Write-Host ""
Write-Host "Interpretation:"
Write-Host "  Low offscreen_skip + low FPS on Run 2 => false-visible on-screen units (fixed by DEV9O5 bounds + on_screen_max_m)"
Write-Host "  High offscreen_skip + low FPS on Run 2 => audio path dominated (ground_audio_skip should rise on DEV9O5)"
