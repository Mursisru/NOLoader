param(
    [string]$GameRoot = "C:\Program Files (x86)\Steam\steamapps\common\Nuclear Option",
    [switch]$IncludePlayerMods,
    [switch]$FieldTest,
    [switch]$Minimal,
    [switch]$RdytuMini
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

# Production deploy clears all mod folders (verify + player). -IncludePlayerMods re-stages player mods below.
Get-ChildItem $ModsRoot -Directory -ErrorAction SilentlyContinue |
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

function Set-IniKey {
    param([string]$Path, [string]$Key, [string]$Value)
    $content = Get-Content $Path -Raw
    if ($content -match "(?m)^$Key\s*=") { $content = $content -replace "(?m)^$Key\s*=.*", "$Key=$Value" }
    else { $content = $content.TrimEnd() + "`r`n$Key=$Value`r`n" }
    Set-Content -Path $Path -Value $content -NoNewline
}

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
        # Preserve only safe user overrides — not field-test perf flags.
        if ($line -match '^(ring_log|exception_tracking|exception_tracking_subscribe|stage_poll_seconds|ring_flush_ms|cull_distance_m|display_detail_min|string_cache_max|core_balancer|mod_worker_count|mod_affinity_mask|main_thread_affinity|double_buffer|mod_compute_budget_ms|mod_tick_analyzer|mod_reflection_cache|mod_shader_warmup|mod_layer_projectile|mod_shader_warmup_budget_ms|physics_catch_unity|physics_catch_motor)\s*=\s*(\S+)\s*$') {
            $k = ($line -split '=')[0].Trim()
            $preserve[$k] = ($line -split '=', 2)[1].Trim()
        }
    }
}
Copy-Item -Force (Join-Path $RepoRoot "deploy\noloader_config.ini") $iniPath
if ($RdytuMini) {
    Copy-Item -Force (Join-Path $RepoRoot "deploy\noloader_config.mini.ini") $iniPath
    Write-Host "RDYTU.mini INI: mod_optimizer only (rdytu_mini=1)"
}
Restore-IniOverrides -Path $iniPath -Overrides $preserve

if ($Minimal) {
    foreach ($pair in @{
        'engine_tweaker' = '0'; 'string_cache' = '0'; 'culling_optimizer' = '0'
        'culling_ground_wheels' = '0'; 'culling_pilot_anim' = '0'; 'culling_offscreen_only' = '0'; 'culling_on_screen_max_m' = '0'
        'culling_ground_renderer' = '0'; 'fps_adaptive_detail' = '0'
        'hud_marker_throttle' = '0'; 'hud_markers_per_frame' = '0'
        'frame_cache' = '0'; 'canvas_limiter' = '0'
        'gpu_render' = '0'; 'gpu_hud_pass' = '0'; 'gpu_fx_instancing' = '0'
        'mod_optimizer' = '0'
    }.GetEnumerator()) {
        Set-IniKey -Path $iniPath -Key $pair.Key -Value $pair.Value
    }
    Write-Host "Minimal INI: Gate L4 only (no perf hooks)"
}

if ($FieldTest) {
    Set-IniKey -Path $iniPath -Key "mod_optimizer" -Value "1"
    Set-IniKey -Path $iniPath -Key "ring_log" -Value "1"
    Write-Host "FieldTest INI: mod_optimizer=1 ring_log=1 (deploy verify mod separately)"
}

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

function Restore-BootConfigGfxJobsIfIdle {
    param([string]$Root)
    $bootPath = Join-Path $Root "NuclearOption_Data\boot.config"
    if (-not (Test-Path $bootPath)) { return }
    $stripKeys = @('gfx-enable-gfx-jobs', 'gfx-enable-native-gfx-jobs')
    $lines = @(Get-Content $bootPath)
    $filtered = @($lines | Where-Object {
        $t = $_.Trim()
        $drop = $false
        foreach ($k in $stripKeys) { if ($t.StartsWith("$k=")) { $drop = $true; break } }
        -not $drop
    })
    if ($filtered.Count -ne $lines.Count) {
        Set-Content -Path $bootPath -Value $filtered
        Write-Host "boot.config gfx-jobs lines removed (gpu hud/fx off)"
    }
}

$noloaderIni = Join-Path $GameRoot "noloader_config.ini"
if (Test-Path $noloaderIni) {
    $iniText = Get-Content $noloaderIni -Raw
    $needsGfxJobs = ($iniText -match '(?m)^gpu_hud_pass\s*=\s*1') -or ($iniText -match '(?m)^gpu_fx_instancing\s*=\s*1')
    if ($needsGfxJobs -and ($iniText -match '(?m)^gfx_native_jobs\s*=\s*1')) {
        Ensure-BootConfigGfxJobs -Root $GameRoot
    } else {
        Restore-BootConfigGfxJobsIfIdle -Root $GameRoot
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

Write-Host "Deployed NOLoader RDYTU core to $DeployRoot (channel: $(if ($RdytuMini) { 'RDYTU.mini' } else { 'RDY' }), mods cleared)"

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
