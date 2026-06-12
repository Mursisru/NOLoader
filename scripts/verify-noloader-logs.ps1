param(
    [string]$GameRoot = "C:\Program Files (x86)\Steam\steamapps\common\Nuclear Option",
    [string]$PlayerLog = "$env:USERPROFILE\AppData\LocalLow\Shockfront\NuclearOption\Player.log",
    [ValidateSet("Menu", "Battle")]
    [string]$DiagMode = "Battle"
)

$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "_env.ps1")
$LoaderRoot = Join-Path $GameRoot "NOLoader"
$fail = 0

function Test-LogLine {
    param(
        [string]$Label,
        [string]$Path,
        [string]$Pattern,
        [switch]$Required
    )

    if (-not (Test-Path $Path)) {
        if ($Required) {
            Write-Host "[FAIL] $Label - file missing: $Path"
            $script:fail++
        } else {
            Write-Host "[WARN] $Label - file missing: $Path"
        }
        return
    }

    $hit = Select-String -Path $Path -Pattern $Pattern | Select-Object -Last 1
    if ($hit) {
        Write-Host "[ OK ] $Label"
        Write-Host "       $($hit.Line.Trim())"
    } elseif ($Required) {
        Write-Host "[FAIL] $Label - pattern not found: $Pattern"
        $script:fail++
    } else {
        Write-Host "[WARN] $Label - pattern not found: $Pattern"
    }
}

Write-Host "NOLoader core smoke verification (no test mods)"
Write-Host "Game: $GameRoot"
Write-Host ""

Test-LogLine -Label "Native proxy attach" -Path (Join-Path $LoaderRoot "logs\proxy.log") -Pattern "GetProcAddress IAT hook installed" -Required
Test-LogLine -Label "Bootstrap.Initialize" -Path (Join-Path $LoaderRoot "logs\proxy.log") -Pattern "Bootstrap.Initialize completed" -Required
Test-LogLine -Label "MainMenu hook" -Path (Join-Path $LoaderRoot "logs\noloader_ring.log") -Pattern "MainMenu hook fired" -Required
$ringLog = Join-Path $LoaderRoot "logs\noloader_ring.log"
if (Test-Path $ringLog) {
    $patchHit = Select-String -Path $ringLog -Pattern "Patched Assembly-CSharp written to disk|Assembly-CSharp already patched|Assembly-CSharp core pre-patched|Mod patches written to Assembly-CSharp" | Select-Object -Last 1
    if ($patchHit) {
        Write-Host "[ OK ] Bootstrap patch"
        Write-Host "       $($patchHit.Line.Trim())"
    } else {
        Write-Host "[FAIL] Bootstrap patch - no patch log line"
        $fail++
    }
} else {
    Write-Host "[FAIL] Bootstrap patch - ring log missing"
    $fail++
}
Test-LogLine -Label "Core started" -Path $PlayerLog -Pattern "\[NOLoader\] Core started" -Required
if ($DiagMode -eq "Menu") {
    Test-LogLine -Label "LoaderDiagMenu start" -Path $ringLog -Pattern "LoaderDiagMenu START \(menu sphere only\)"
    Test-LogLine -Label "LoaderDiagMenu audit" -Path $ringLog -Pattern "LoaderDiagMenu MENU AUDIT COMPLETE"
} else {
    Test-LogLine -Label "LoaderDiag start" -Path $ringLog -Pattern "LoaderDiag START \(battle sphere only\)"
    Test-LogLine -Label "LoaderDiag battle audit" -Path $ringLog -Pattern "LoaderDiag BATTLE AUDIT COMPLETE" -Required
}
Test-LogLine -Label "No crash stack" -Path $PlayerLog -Pattern "END OF STACKTRACE"

$fatal = Join-Path $LoaderRoot "logs\bootstrap_fatal.txt"
if (Test-Path $fatal) {
    Write-Host "[FAIL] bootstrap_fatal.txt present"
    Get-Content $fatal -Tail 5 | ForEach-Object { Write-Host "       $_" }
    $fail++
}

Write-Host ""
if ($fail -eq 0) {
    Write-Host "All required checks passed."
    exit 0
}

Write-Host "$fail required check(s) failed."
exit 1

