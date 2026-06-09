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
    & (Join-Path $NOLoaderScriptsRoot "RDYTU\pack-player-mods.ps1")
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

foreach ($dll in $coreDlls) {
    $src = Get-ChildItem -Path $RepoRoot -Recurse -Filter $dll | Where-Object { $_.DirectoryName -like "*\bin\$Configuration\*" } | Select-Object -First 1
    if ($src) {
        Copy-Item -Force $src.FullName (Join-Path $CoreDeploy $dll)
    }
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
    $packedMods = Join-Path $RdyRoot "mods\packed"
    if (Test-Path $packedMods) {
        Get-ChildItem $packedMods -Directory | ForEach-Object {
            $dest = Join-Path $ModsRoot $_.Name
            New-Item -ItemType Directory -Force -Path $dest | Out-Null
            Copy-Item -Force (Join-Path $_.FullName "*") $dest
            Write-Host "Deployed player mod: $($_.Name)"
        }
    }
}

Write-Host "Deployed NOLoader RDYTU core to $DeployRoot (channel: RDY, no dev overlay)"

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

