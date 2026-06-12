param(
    [string]$GameRoot = "C:\Program Files (x86)\Steam\steamapps\common\Nuclear Option",
    [string]$RingLogPath = ""
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($RingLogPath)) {
    $RingLogPath = Join-Path $GameRoot "NOLoader\logs\noloader_ring.log"
}

if (-not (Test-Path $RingLogPath)) {
    Write-Error "Ring log not found: $RingLogPath"
}

$lines = Get-Content $RingLogPath -ErrorAction Stop | Where-Object { $_ -match '\[PerfTest\]' }
if ($lines.Count -eq 0) {
    Write-Host "[FAIL] No [PerfTest] lines in ring log - deploy mod and fly a mission." -ForeColor Red
    exit 1
}

$pass = 0
$warn = 0
$fail = 0

function Step-Pass { param([string]$Msg) Write-Host "[ OK ] $Msg" -ForeColor Green; $script:pass++ }
function Step-Warn { param([string]$Msg) Write-Host "[WARN] $Msg" -ForeColor Yellow; $script:warn++ }
function Step-Fail { param([string]$Msg) Write-Host "[FAIL] $Msg" -ForeColor Red; $script:fail++ }

Write-Host "`n=== PerfTest ring log analysis ===" -ForeColor Cyan
Write-Host "Log: $RingLogPath"
Write-Host "PerfTest lines: $($lines.Count)`n"

$checks = @(
    @{ Name = "OnLoad"; Pattern = '\[PerfTest\].*OnLoad' },
    @{ Name = "tick_fast"; Pattern = '\[PerfTest\]\[PASS\] tick_fast:' },
    @{ Name = "tick_normal"; Pattern = '\[PerfTest\]\[PASS\] tick_normal:' },
    @{ Name = "tick_slow"; Pattern = '\[PerfTest\]\[PASS\] tick_slow:' },
    @{ Name = "world snapshot"; Pattern = '\[PerfTest\].*world units=' },
    @{ Name = "arraypool bound"; Pattern = '\[PerfTest\]\[PASS\] arraypool: services\.Pool bound' },
    @{ Name = "budget bound"; Pattern = '\[PerfTest\]\[PASS\] budget: services\.Budget bound' }
)

$joined = $lines -join "`n"
foreach ($c in $checks) {
    if ($joined -match $c.Pattern) { Step-Pass $c.Name } else { Step-Fail $c.Name }
}

if ($joined -match '\[PerfTest\]\[PASS\] cecil_throttle:') { Step-Pass "cecil throttle patch" }
elseif ($joined -match '\[PerfTest\]\[WARN\] cecil_throttle:') { Step-Warn "cecil throttle patch (no hits yet)" }
else { Step-Fail "cecil throttle patch" }

if ($joined -match '\[PerfTest\]\[PASS\] summary: tick scheduler exercised') { Step-Pass "unload summary ticks" }
elseif ($joined -match '\[PerfTest\].*Summary') { Step-Warn "summary present but ticks incomplete" }
else { Step-Warn "no unload summary (exit mission to flush)" }

if ($joined -match '\[Perf\] demote mod=') { Step-Pass "core budget demote logged" }
else { Step-Warn "no [Perf] demote (set HeavyWork=2 in mod.ini to test)" }

Write-Host "`n--- Last 15 PerfTest lines ---" -ForeColor Cyan
$lines | Select-Object -Last 15 | ForEach-Object { Write-Host $_ }

Write-Host ("`n=== Result: pass={0} warn={1} fail={2} ===" -f $pass, $warn, $fail) -ForeColor Cyan
if ($fail -gt 0) { exit 1 }
exit 0
