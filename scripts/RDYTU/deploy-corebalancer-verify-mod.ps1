param(
    [string]$GameRoot = "C:\Program Files (x86)\Steam\steamapps\common\Nuclear Option",
    [string]$Configuration = "RDYTU",
    [switch]$KeepOtherMods,
    [switch]$EnableCoreBalancer
)

$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "..\_env.ps1")
$RepoRoot = $NOLoaderRepoRoot

if (Get-Process -Name "NuclearOption" -ErrorAction SilentlyContinue) {
    Write-Error "Close Nuclear Option before deploy."
}

$project = Join-Path $RepoRoot "DEV.SDK\mods\NOLoader.CoreBalancerVerify\NOLoader.CoreBalancerVerify.csproj"
dotnet build $project -c $Configuration --verbosity minimal
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$dll = Join-Path $RepoRoot "DEV.SDK\mods\NOLoader.CoreBalancerVerify\bin\$Configuration\net48\NOLoader.CoreBalancerVerify.dll"
$modConfigProject = Join-Path $RepoRoot "DEV.SDK\shared\NOLoader.ModConfig\NOLoader.ModConfig.csproj"
$modConfigDll = Join-Path $RepoRoot "DEV.SDK\shared\NOLoader.ModConfig\bin\$Configuration\net48\NOLoader.ModConfig.dll"
if (-not (Test-Path $modConfigDll)) {
    dotnet build $modConfigProject -c $Configuration --verbosity minimal
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}

$modsRoot = Join-Path $GameRoot "NOLoader\mods"
$modRoot = Join-Path $modsRoot "CoreBalancerVerify"
New-Item -ItemType Directory -Path $modRoot -Force | Out-Null

if (-not $KeepOtherMods) {
    Get-ChildItem -Path $modsRoot -Directory | Where-Object { $_.Name -ne "CoreBalancerVerify" } | ForEach-Object {
        Remove-Item -Recurse -Force $_.FullName
        Write-Host "Removed mod folder: $($_.Name)"
    }
}

Copy-Item -Force $dll (Join-Path $modRoot "NOLoader.CoreBalancerVerify.dll")
Copy-Item -Force $modConfigDll (Join-Path $modRoot "NOLoader.ModConfig.dll")
Copy-Item -Force (Join-Path $RepoRoot "DEV.SDK\mods\NOLoader.CoreBalancerVerify\mod.json") (Join-Path $modRoot "mod.json")

$iniSrc = Join-Path $RepoRoot "DEV.SDK\mods\NOLoader.CoreBalancerVerify\mod.ini"
$iniDst = Join-Path $modRoot "mod.ini"
if (-not (Test-Path $iniDst)) {
    Copy-Item -Force $iniSrc $iniDst
}

& (Join-Path $RepoRoot "scripts\pack-mod-rdytu.ps1") -ModFolder $modRoot

$noloaderIni = Join-Path $GameRoot "noloader_config.ini"
if (-not (Test-Path $noloaderIni)) {
    $template = Join-Path $RepoRoot "deploy\noloader_config.ini"
    if (Test-Path $template) {
        Copy-Item -Force $template $noloaderIni
        Write-Host "Copied template noloader_config.ini to game root"
    }
}

if ($EnableCoreBalancer -and (Test-Path $noloaderIni)) {
    $content = Get-Content $noloaderIni -Raw
    if ($content -match '(?m)^core_balancer\s*=') {
        $content = $content -replace '(?m)^core_balancer\s*=.*', 'core_balancer=1'
    } else {
        $content = $content.TrimEnd() + "`r`ncore_balancer=1`r`n"
    }
    Set-Content -Path $noloaderIni -Value $content -NoNewline
    Write-Host "Set core_balancer=1 in noloader_config.ini"
}

Write-Host "CoreBalancerVerify deployed to $modRoot (no Cecil patches)"
Write-Host "Full test: core_balancer=1 in game-root noloader_config.ini, fly mission 30s+, then:"
Write-Host "  scripts\RDYTU\parse-corebalancer-verify-ringlog.ps1"
