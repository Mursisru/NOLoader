param(
    [string]$RingLogPath = "C:\Program Files (x86)\Steam\steamapps\common\Nuclear Option\NOLoader\logs\noloader_ring.log",
    [switch]$RequireWorkers
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path $RingLogPath)) {
    Write-Host "[FAIL] Ring log not found: $RingLogPath" -ForegroundColor Red
    exit 1
}

$allLines = Get-Content $RingLogPath -ErrorAction Stop
$lines = @($allLines | Where-Object { $_ -match '\[CoreBalVerify\]' })
$coreLines = @($allLines | Where-Object { $_ -match '\[CoreBalancer\]' })
$gateLines = @($allLines | Where-Object { $_ -match 'GateL1.*corebalancerverify' })

$fail = 0
$pass = 0
function Step-Pass { param([string]$Msg) Write-Host "[ OK ] $Msg" -ForegroundColor Green; $script:pass++ }
function Step-Fail { param([string]$Msg) Write-Host "[FAIL] $Msg" -ForegroundColor Red; $script:fail++ }
function Step-Warn { param([string]$Msg) Write-Host "[WARN] $Msg" -ForegroundColor Yellow }

Write-Host ""
Write-Host "=== CoreBalancerVerify ring log analysis ===" -ForegroundColor Cyan
Write-Host "CoreBalVerify lines: $($lines.Count)"
Write-Host "CoreBalancer core lines: $($coreLines.Count)"
Write-Host ""

if ($gateLines.Count -gt 0) {
    $lastGate = $gateLines[-1]
    if ($lastGate -match 'Invalid guid format') {
        Step-Fail "mod blocked at GateL1 (invalid guid - redeploy fixed build)"
    }
}

$coreJoined = ($coreLines | Select-Object -Last 20) -join "`n"
if ($coreJoined -match '\[CoreBalancer\] topology') { Step-Pass "core topology logged" }
elseif ($coreJoined -match '\[CoreBalancer\] disabled') { Step-Warn "core_balancer=0 - worker pipeline not active" }
else { Step-Warn "no recent [CoreBalancer] topology/disabled lines" }

if ($coreJoined -match '\[CoreBalancer\] workers=') { Step-Pass "worker threads started" }
elseif ($RequireWorkers) { Step-Fail "workers not started (need core_balancer=1)" }

if ($lines.Count -eq 0) {
    Step-Fail "no [CoreBalVerify] lines - fly a mission after deploy, then re-run parser"
    Write-Host ""
    Write-Host "Pass: $pass  Fail: $fail" -ForegroundColor Cyan
    exit 1
}

$joined = $lines -join "`n"

$checks = @(
    @{ Name = "OnLoad"; Pattern = '\[CoreBalVerify\].*OnLoad' },
    @{ Name = "no-patch manifest"; Pattern = '\[CoreBalVerify\]\[PASS\] manifest:' },
    @{ Name = "run_compute apply"; Pattern = '\[CoreBalVerify\]\[PASS\] run_compute:' }
)

foreach ($c in $checks) {
    if ($joined -match $c.Pattern) { Step-Pass $c.Name } else { Step-Warn ("missing: " + $c.Name) }
}

if ($joined -match '\[CoreBalVerify\]\[PASS\] scheduler:') { Step-Pass "scheduler available" }
elseif ($joined -match '\[CoreBalVerify\]\[WARN\] scheduler:') { Step-Warn "scheduler offline (enable core_balancer=1)" }

if ($joined -match '\[CoreBalVerify\]\[PASS\] pipeline:') { Step-Pass "background pipeline" }
elseif ($RequireWorkers) { Step-Fail "background pipeline missing" }

if ($joined -match '\[CoreBalVerify\]\[PASS\] worker_math:') { Step-Pass "worker math apply" }

if ($joined -match '\[CoreBalVerify\]\[FAIL\] pipeline:') { Step-Fail "pipeline mismatch compute/apply" }
if ($joined -match '\[CoreBalVerify\]\[FAIL\] worker_math:') { Step-Fail "worker math failed" }
if ($joined -match '\[CoreBalVerify\]\[FAIL\] summary:') { Step-Fail "unload summary failed" }

if ($joined -match '\[CoreBalVerify\]\[PASS\] summary:') { Step-Pass "unload summary OK" }
elseif ($joined -match '\[CoreBalVerify\].*Summary') { Step-Warn "summary present but not PASS" }

Write-Host ""
Write-Host "--- recent CoreBalVerify lines ---" -ForegroundColor Cyan
$lines | Select-Object -Last 15 | ForEach-Object { Write-Host $_ }

Write-Host ""
Write-Host "Pass: $pass  Fail: $fail" -ForegroundColor Cyan
if ($fail -gt 0) { exit 1 }
