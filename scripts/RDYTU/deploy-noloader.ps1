param(
    [string]$GameRoot = "C:\Program Files (x86)\Steam\steamapps\common\Nuclear Option",
    [switch]$IncludePlayerMods
)

$ErrorActionPreference = "Stop"
. (Join-Path (Split-Path $PSScriptRoot -Parent) "_env.ps1")
$RdyRoot = $NOLoaderRdyRoot
$RepoRoot = $NOLoaderRepoRoot
$Configuration = "RDYTU"
$Solution = Join-Path $RdyRoot "NOLoader.RDYTU.sln"

Set-Location $RepoRoot

Write-Host "Building NOLoader RDYTU ($Configuration)..."
dotnet build $Solution -c $Configuration --verbosity minimal
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

if ($IncludePlayerMods) {
    & (Join-Path $NOLoaderScriptsRoot "stage-player-mods.ps1") -Configuration $Configuration
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}

$DeployRoot = Join-Path $GameRoot "NOLoader"
$CoreDeploy = Join-Path $DeployRoot "core"
$ModsRoot = Join-Path $DeployRoot "mods"

New-Item -ItemType Directory -Force -Path $CoreDeploy | Out-Null
New-Item -ItemType Directory -Force -Path $ModsRoot | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $DeployRoot "logs") | Out-Null

Copy-Item -Force (Join-Path $RepoRoot "deploy\NOLoader\mods\README.txt") (Join-Path $ModsRoot "README.txt")

Get-ChildItem $ModsRoot -Directory -ErrorAction SilentlyContinue | Remove-Item -Recurse -Force

$coreDlls = @(
    "NOLoader.Core.dll",
    "NOLoader.API.dll",
    "NOLoader.Patcher.dll",
    "NOLoader.Registry.dll",
    "Mono.Cecil.dll"
)

$coreDllSources = @{
    "NOLoader.API.dll"       = Join-Path $RepoRoot "src\NOLoader.API\bin\$Configuration\netstandard2.0\NOLoader.API.dll"
    "NOLoader.Core.dll"      = Join-Path $RepoRoot "src\NOLoader.Core\bin\$Configuration\net48\NOLoader.Core.dll"
    "NOLoader.Patcher.dll"   = Join-Path $RepoRoot "src\NOLoader.Patcher\bin\$Configuration\net48\NOLoader.Patcher.dll"
    "NOLoader.Registry.dll"  = Join-Path $RepoRoot "src\NOLoader.Registry\bin\$Configuration\net48\NOLoader.Registry.dll"
    "Mono.Cecil.dll"         = Join-Path $RepoRoot "src\NOLoader.PatchTool\bin\$Configuration\net48\Mono.Cecil.dll"
}

foreach ($dll in $coreDlls) {
    $srcPath = $coreDllSources[$dll]
    if (-not (Test-Path $srcPath)) {
        $fallback = Get-ChildItem -Path $RepoRoot -Recurse -Filter $dll |
            Where-Object { $_.DirectoryName -like "*\bin\$Configuration\*" } |
            Sort-Object LastWriteTime -Descending |
            Select-Object -First 1
        if ($fallback) { $srcPath = $fallback.FullName }
    }
    if (-not (Test-Path $srcPath)) {
        Write-Error "Missing $dll for $Configuration (expected $($coreDllSources[$dll]))"
    }
    Copy-Item -Force $srcPath (Join-Path $CoreDeploy $dll)
}

$legacyTelemetry = Join-Path $CoreDeploy "NOLoader.Telemetry.dll"
if (Test-Path $legacyTelemetry) { Remove-Item -Force $legacyTelemetry }

Copy-Item -Force (Join-Path $RepoRoot "deploy\noloader_config.ini") (Join-Path $GameRoot "noloader_config.ini")

$proxyCandidates = @(
    (Join-Path $RepoRoot "artifacts\proxy\winhttp.dll"),
    (Join-Path $RepoRoot "native\NOLoader.Proxy\bin\x64\Release\proxy\winhttp.dll"),
    (Join-Path $RepoRoot "bin\x64\Release\proxy\winhttp.dll")
)
$proxyPath = $proxyCandidates | Where-Object { Test-Path $_ } | Select-Object -First 1
if (Test-Path $proxyPath) {
    Copy-Item -Force $proxyPath (Join-Path $GameRoot "winhttp.dll")
    Write-Host "Deployed winhttp.dll proxy"
} else {
    Write-Warning "Native proxy not built. Run MSBuild on native\NOLoader.Proxy (Release|x64)"
}

if ($IncludePlayerMods) {
    $stagedMods = Join-Path $RepoRoot "deploy\NOLoader\mods"
    $playerModNames = @(
        "HudCommon", "RepeatTakeoffMusic", "RealWeaponNames",
        "MissileHoldCam", "MissileEta"
    )
    foreach ($name in $playerModNames) {
        $src = Join-Path $stagedMods $name
        if (-not (Test-Path $src)) {
            Write-Warning "Player mod not staged: $name"
            continue
        }
        $dest = Join-Path $ModsRoot $name
        New-Item -ItemType Directory -Force -Path $dest | Out-Null
        Copy-Item -Force (Join-Path $src "*") $dest
        Write-Host "Deployed player mod: $name"
    }
}

Write-Host "Deployed NOLoader RDYTU core to $DeployRoot (channel: RDY, no dev overlay)"

. (Join-Path $NOLoaderScriptsRoot "_managed-restore.ps1")
Remove-InvalidVanillaSnapshots -GameRoot $GameRoot
if (Test-ManagedMirageMismatch -GameRoot $GameRoot) {
    Write-Error "Assembly-CSharp.dll is stale (Mirage version mismatch). Run scripts\uninstall-noloader.ps1 or Steam verify, then redeploy."
}

$gameProc = Get-Process -Name "NuclearOption" -ErrorAction SilentlyContinue
if ($gameProc) {
    Write-Warning "Game is running - Cecil pre-patch skipped. Close the game and re-run deploy."
} else {
    Write-Host "Pre-applying Cecil patches..."
    dotnet run --project (Join-Path $RepoRoot "src\NOLoader.PatchTool\NOLoader.PatchTool.csproj") -c $Configuration -- $GameRoot
    if ($LASTEXITCODE -ne 0) {
        Write-Warning "Cecil pre-patch failed (exit $LASTEXITCODE)"
    } else {
        Write-Host "Cecil pre-patch OK"
    }
}

Write-Host "Uninstall / vanilla restore: .\scripts\uninstall-noloader.ps1"

