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

Get-ChildItem $ModsRoot -Directory -ErrorAction SilentlyContinue |
    Where-Object { $_.Name -ne "GpuRenderVerify" } |
    Remove-Item -Recurse -Force

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

function Restore-IniOverrides {
    param([string]$Path, [hashtable]$Overrides)
    if (-not (Test-Path $Path) -or $Overrides.Count -eq 0) { return }
    $content = Get-Content $Path -Raw
    foreach ($key in $Overrides.Keys) {
        $value = $Overrides[$key]
        if ($content -match "(?m)^$key\s*=") { $content = $content -replace "(?m)^$key\s*=.*", "$key=$value" }
        else { $content = $content.TrimEnd() + "`r`n$key=$value`r`n" }
    }
    Set-Content -Path $Path -Value $content -NoNewline
}

$iniPath = Join-Path $GameRoot "noloader_config.ini"
$preserve = @{}
if (Test-Path $iniPath) {
    foreach ($line in Get-Content $iniPath) {
        if ($line -match '^(gpu_render|gpu_hud_pass|gpu_fx_instancing|gfx_native_jobs|core_balancer|canvas_limiter)\s*=\s*1\s*$') {
            $k = ($line -split '=')[0].Trim()
            $preserve[$k] = '1'
        }
    }
}
Copy-Item -Force (Join-Path $RepoRoot "deploy\noloader_config.ini") $iniPath
Restore-IniOverrides -Path $iniPath -Overrides $preserve

function Ensure-BootConfigGfxJobs {
    param([string]$Root)
    $bootPath = Join-Path $Root "NuclearOption_Data\boot.config"
    $dataDir = Split-Path $bootPath -Parent
    if (-not (Test-Path $dataDir)) { return }
    $lines = @()
    if (Test-Path $bootPath) { $lines = @(Get-Content $bootPath) }
    $backup = "$bootPath.noloader.bak"
    if ((Test-Path $bootPath) -and -not (Test-Path $backup)) { Copy-Item $bootPath $backup }
    $keys = @('gfx-enable-gfx-jobs=1', 'gfx-enable-native-gfx-jobs=1')
    $changed = $false
    foreach ($k in $keys) {
        $name = ($k -split '=')[0]
        $found = $false
        foreach ($line in $lines) { if ($line.Trim().StartsWith("$name=")) { $found = $true; break } }
        if (-not $found) { $lines += $k; $changed = $true }
    }
    if ($changed) {
        Set-Content -Path $bootPath -Value $lines
        Write-Host "boot.config gfx-jobs merged (backup: .noloader.bak)"
    }
}

$noloaderIni = Join-Path $GameRoot "noloader_config.ini"
if (Test-Path $noloaderIni) {
    $iniText = Get-Content $noloaderIni -Raw
    if ($iniText -match '(?m)^gpu_render\s*=\s*1' -and $iniText -match '(?m)^gfx_native_jobs\s*=\s*1') {
        Ensure-BootConfigGfxJobs -Root $GameRoot
    }
}

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

