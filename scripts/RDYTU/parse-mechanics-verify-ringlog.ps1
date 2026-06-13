param(
    [string]$RingLogPath = "C:\Program Files (x86)\Steam\steamapps\common\Nuclear Option\NOLoader\logs\noloader_ring.log"
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path $RingLogPath)) {
    Write-Host "[FAIL] Ring log not found: $RingLogPath" -ForegroundColor Red
    exit 1
}

$lines = Get-Content $RingLogPath -ErrorAction Stop | Where-Object { $_ -match '\[MechVerify\]' }
if ($lines.Count -eq 0) {
    Write-Host "[FAIL] No [MechVerify] lines — deploy MechanicsVerify and fly a mission." -ForegroundColor Red
    exit 1
}

$fail = 0
$pass = 0
function Step-Pass { param([string]$Msg) Write-Host "[ OK ] $Msg" -ForegroundColor Green; $script:pass++ }
function Step-Fail { param([string]$Msg) Write-Host "[FAIL] $Msg" -ForegroundColor Red; $script:fail++ }
function Step-Warn { param([string]$Msg) Write-Host "[WARN] $Msg" -ForegroundColor Yellow }

$joined = $lines -join "`n"

Write-Host "`n=== MechanicsVerify ring log analysis ===" -ForegroundColor Cyan
Write-Host "MechVerify lines: $($lines.Count)`n"

$checks = @(
    @{ Name = "OnLoad"; Pattern = '\[MechVerify\].*OnLoad' },
    @{ Name = "no-patch manifest"; Pattern = '\[MechVerify\]\[PASS\] manifest:' },
    @{ Name = "display_detail pass"; Pattern = '\[MechVerify\]\[PASS\] display_detail:' },
    @{ Name = "thrust_sim pass"; Pattern = '\[MechVerify\]\[PASS\] thrust_sim:' }
)

foreach ($c in $checks) {
    if ($joined -match $c.Pattern) { Step-Pass $c.Name } else { Step-Warn ("missing: " + $c.Name) }
}

if ($joined -match '\[MechVerify\]\[FAIL\] display_detail:') { Step-Fail "display_detail failed (EngineTweaker/culling regression)" }
if ($joined -match '\[MechVerify\]\[FAIL\] thrust_sim:') { Step-Fail "thrust_sim failed under throttle" }
if ($joined -match '\[MechVerify\]\[PASS\] summary:') { Step-Pass "unload summary OK" }
elseif ($joined -match '\[MechVerify\].*Summary') { Step-Warn "summary present but not PASS" }

Write-Host "`n--- Last 12 MechVerify lines ---" -ForegroundColor Cyan
$lines | Select-Object -Last 12 | ForEach-Object { Write-Host $_ }

Write-Host "`nPass: $pass  Fail: $fail" -ForegroundColor Cyan
if ($fail -gt 0) { exit 1 }
