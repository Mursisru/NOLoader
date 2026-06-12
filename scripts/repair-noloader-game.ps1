param(
    [string]$GameRoot = "C:\Program Files (x86)\Steam\steamapps\common\Nuclear Option",
    [int]$SteamAppId = 2168680,
    [int]$WaitSeconds = 180
)

$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "_env.ps1")
. (Join-Path $PSScriptRoot "_managed-restore.ps1")
$RepoRoot = $NOLoaderRepoRoot

if (Get-Process -Name "NuclearOption" -ErrorAction SilentlyContinue) {
    Write-Error "Close Nuclear Option before repair."
}

Remove-LegacyNoloaderBackups -GameRoot $GameRoot

$managed = Get-ManagedFolder $GameRoot
$asmLive = Join-Path $managed "Assembly-CSharp.dll"

function Test-AsmHasModPatches {
    param([string]$Path)
    if (-not (Test-Path $Path)) { return $false }
    $text = [Text.Encoding]::ASCII.GetString([IO.File]::ReadAllBytes($Path))
    return $text.Contains("NOLoader.RealWeaponNames")
        -or $text.Contains("NOLoader.AutoFlare")
        -or $text.Contains("NOLoader.HudCommon")
        -or $text.Contains("NOLoader.RepeatTakeoffMusic")
}

if ((Test-AsmHasModPatches $asmLive) -or (Test-ManagedNeedsRestore -GameRoot $GameRoot)) {
    Restore-VanillaManagedModules -GameRoot $GameRoot -RepoRoot $RepoRoot -SteamAppId $SteamAppId -WaitSeconds $WaitSeconds
}

Write-Host "Building PatchTool (RDYTU)..."
dotnet build (Join-Path $RepoRoot "RDYTU\NOLoader.RDYTU.sln") -c RDYTU --verbosity minimal
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "Offline Cecil pre-patch (core only)..."
dotnet run --project (Join-Path $RepoRoot "src\NOLoader.PatchTool\NOLoader.PatchTool.csproj") -c RDYTU -- $GameRoot
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "Repair OK. Next: deploy-noloader.ps1 -Configuration RDYTU"
