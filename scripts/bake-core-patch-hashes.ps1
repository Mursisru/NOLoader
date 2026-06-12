param(
    [string]$GameRoot = "C:\Program Files (x86)\Steam\steamapps\common\Nuclear Option"
)

$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "_env.ps1")
$RepoRoot = $NOLoaderRepoRoot
$Output = Join-Path $RepoRoot "src\NOLoader.Core\Bootstrap\CoreBootstrapPatchHashes.generated.cs"

Write-Host "Building PatchTool..."
dotnet build (Join-Path $RepoRoot "src\NOLoader.PatchTool\NOLoader.PatchTool.csproj") -c DEV_SDK --verbosity quiet
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "Baking core patch hashes from: $GameRoot"
dotnet run --project (Join-Path $RepoRoot "src\NOLoader.PatchTool\NOLoader.PatchTool.csproj") -c DEV_SDK --no-build -- bake-hashes $GameRoot $Output
exit $LASTEXITCODE

