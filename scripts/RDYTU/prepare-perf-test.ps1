param(
    [switch]$NoMods,
    [switch]$PerfTestOnly,
    [string]$GameRoot = "C:\Program Files (x86)\Steam\steamapps\common\Nuclear Option"
)

$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "..\_env.ps1")

$modsRoot = Join-Path $GameRoot "NOLoader\mods"
$stashRoot = Join-Path $GameRoot "NOLoader\mods._perf_stash"

if (-not (Test-Path $modsRoot)) {
    New-Item -ItemType Directory -Path $modsRoot -Force | Out-Null
}

if ($NoMods) {
    if (Test-Path $stashRoot) {
        Remove-Item -Recurse -Force $stashRoot
    }
    New-Item -ItemType Directory -Path $stashRoot -Force | Out-Null
    Get-ChildItem -Path $modsRoot -Directory | ForEach-Object {
        Move-Item -Path $_.FullName -Destination (Join-Path $stashRoot $_.Name) -Force
    }
    Write-Host "Perf test stage A: mods folder cleared."
}
elseif ($PerfTestOnly) {
    if (Test-Path $stashRoot) {
        Get-ChildItem -Path $stashRoot -Directory | ForEach-Object {
            $dest = Join-Path $modsRoot $_.Name
            if (Test-Path $dest) {
                Remove-Item -Recurse -Force $dest
            }
            Move-Item -Path $_.FullName -Destination $dest -Force
        }
        Remove-Item -Recurse -Force $stashRoot
    }

    Get-ChildItem -Path $modsRoot -Directory | Where-Object { $_.Name -ne "PerfTest" } | ForEach-Object {
        if (-not (Test-Path $stashRoot)) {
            New-Item -ItemType Directory -Path $stashRoot -Force | Out-Null
        }
        Move-Item -Path $_.FullName -Destination (Join-Path $stashRoot $_.Name) -Force
    }
    Write-Host "Perf test stage B: only PerfTest/ should remain."
}
else {
    Write-Host "Use -NoMods or -PerfTestOnly"
    exit 1
}

Write-Host "Remaining mod folders:"
Get-ChildItem -Path $modsRoot -Directory | ForEach-Object { Write-Host "  - $($_.Name)" }
