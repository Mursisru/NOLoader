param(
    [string]$RepoRoot = ""
)

$ErrorActionPreference = "Stop"
. (Join-Path (Split-Path $PSScriptRoot -Parent) "_env.ps1")
if ([string]::IsNullOrEmpty($RepoRoot)) {
    $RepoRoot = $NOLoaderRepoRoot
}
$RdyRoot = Join-Path $RepoRoot "RDYTU"
$PackedRoot = Join-Path $RdyRoot "mods\packed"

if (Test-Path $PackedRoot) {
    Remove-Item -Recurse -Force $PackedRoot
}

$playerModRoots = Get-ChildItem (Join-Path $RepoRoot "deploy\NOLoader\mods") -Directory -ErrorAction SilentlyContinue
if (-not $playerModRoots -or $playerModRoots.Count -eq 0) {
    Write-Host "No player mods under deploy\NOLoader\mods вЂ” packed folder cleared."
    exit 0
}

New-Item -ItemType Directory -Force -Path $PackedRoot | Out-Null
foreach ($modDir in $playerModRoots) {
    $out = Join-Path $PackedRoot $modDir.Name
    & (Join-Path $NOLoaderScriptsRoot "pack-mod-rdytu.ps1") -ModFolder $modDir.FullName -OutputFolder $out
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    Write-Host "Packed: $($modDir.Name)"
}

Write-Host "Packed RDYTU player mods -> $PackedRoot"

