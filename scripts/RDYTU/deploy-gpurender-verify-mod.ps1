param(
    [string]$GameRoot = "C:\Program Files (x86)\Steam\steamapps\common\Nuclear Option",
    [string]$Configuration = "RDYTU",
    [switch]$KeepOtherMods,
    [switch]$EnableGpuRender
)

$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "..\_env.ps1")
$RepoRoot = $NOLoaderRepoRoot

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
    if ($changed) { Set-Content -Path $bootPath -Value $lines; Write-Host "boot.config gfx-jobs updated" }
}

if (Get-Process -Name "NuclearOption" -ErrorAction SilentlyContinue) {
    Write-Error "Close Nuclear Option before deploy."
}

$project = Join-Path $RepoRoot "DEV.SDK\mods\NOLoader.GpuRenderVerify\NOLoader.GpuRenderVerify.csproj"
dotnet build $project -c $Configuration --verbosity minimal
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$dll = Join-Path $RepoRoot "DEV.SDK\mods\NOLoader.GpuRenderVerify\bin\$Configuration\net48\NOLoader.GpuRenderVerify.dll"
$modConfigDll = Join-Path $RepoRoot "DEV.SDK\shared\NOLoader.ModConfig\bin\$Configuration\net48\NOLoader.ModConfig.dll"

$modsRoot = Join-Path $GameRoot "NOLoader\mods"
$modRoot = Join-Path $modsRoot "GpuRenderVerify"
New-Item -ItemType Directory -Path $modRoot -Force | Out-Null

if (-not $KeepOtherMods) {
    Get-ChildItem -Path $modsRoot -Directory | Where-Object { $_.Name -ne "GpuRenderVerify" } | ForEach-Object {
        Remove-Item -Recurse -Force $_.FullName
    }
}

Copy-Item -Force $dll (Join-Path $modRoot "NOLoader.GpuRenderVerify.dll")
Copy-Item -Force $modConfigDll (Join-Path $modRoot "NOLoader.ModConfig.dll")
Copy-Item -Force (Join-Path $RepoRoot "DEV.SDK\mods\NOLoader.GpuRenderVerify\mod.json") (Join-Path $modRoot "mod.json")

$iniSrc = Join-Path $RepoRoot "DEV.SDK\mods\NOLoader.GpuRenderVerify\mod.ini"
$iniDst = Join-Path $modRoot "mod.ini"
if (-not (Test-Path $iniDst)) { Copy-Item -Force $iniSrc $iniDst }

& (Join-Path $RepoRoot "scripts\pack-mod-rdytu.ps1") -ModFolder $modRoot

$noloaderIni = Join-Path $GameRoot "noloader_config.ini"
if ($EnableGpuRender -and (Test-Path $noloaderIni)) {
    $content = Get-Content $noloaderIni -Raw
    foreach ($pair in @('gpu_render=1', 'gpu_metrics=1')) {
        $key = ($pair -split '=')[0]
        if ($content -match "(?m)^$key\s*=") { $content = $content -replace "(?m)^$key\s*=.*", $pair }
        else { $content = $content.TrimEnd() + "`r`n$pair`r`n" }
    }
    Set-Content -Path $noloaderIni -Value $content -NoNewline
}

if ($EnableGpuRender) { Ensure-BootConfigGfxJobs -Root $GameRoot }

Write-Host "Field verify mod deployed to $modRoot (single DEV2O13 test mod)"
