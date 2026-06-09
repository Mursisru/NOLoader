param(
    [string]$GameRoot = "C:\Program Files (x86)\Steam\steamapps\common\Nuclear Option"
)

$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "_env.ps1")
$RepoRoot = $NOLoaderRepoRoot
$PatchTool = Join-Path $RepoRoot "src\NOLoader.PatchTool\NOLoader.PatchTool.csproj"

dotnet build $PatchTool -c DEV_SDK --verbosity quiet
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

function Get-Hash {
    param([string]$Target, [string]$Inject, [string]$Method)
    $out = dotnet run --project $PatchTool -c DEV_SDK --no-build -- hash-patch $GameRoot "Assembly-CSharp.dll" $Target $Inject $Method
    if ($LASTEXITCODE -ne 0) { throw "hash-patch failed for $Target" }
    return ($out | Select-Object -Last 1).Trim()
}

Write-Host "Encyclopedia::AfterLoad -> $(Get-Hash 'Encyclopedia::AfterLoad' 'NOLoader.WeaponNames.Patches::AfterLoadPostfix' 'Postfix')"
Write-Host "WeaponMount::Initialize -> $(Get-Hash 'WeaponMount::Initialize' 'NOLoader.WeaponNames.Patches::InitializePrefix' 'Prefix')"

