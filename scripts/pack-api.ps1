param(
    [ValidateSet("DEV_SDK", "RDYTU")]
    [string]$Configuration = "DEV_SDK"
)

$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "_env.ps1")
$RepoRoot = $NOLoaderRepoRoot
Set-Location $RepoRoot

New-Item -ItemType Directory -Force -Path (Join-Path $RepoRoot "artifacts\NuGet") | Out-Null
dotnet pack (Join-Path $RepoRoot "src\NOLoader.API\NOLoader.API.csproj") -c $Configuration -o (Join-Path $RepoRoot "artifacts\NuGet") --verbosity minimal
Write-Host "API package written to artifacts\NuGet"

