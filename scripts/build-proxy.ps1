param(
    [ValidateSet("Release", "Debug")]
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "_env.ps1")
$RepoRoot = $NOLoaderRepoRoot
$vcxproj = Join-Path $RepoRoot "native\NOLoader.Proxy\NOLoader.Proxy.vcxproj"
$vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
$msbuild = Join-Path (& $vswhere -latest -products * -requires Microsoft.Component.MSBuild -property installationPath) "MSBuild\Current\Bin\MSBuild.exe"

Write-Host "Building NOLoader.Proxy ($Configuration|x64)..."
& $msbuild $vcxproj /p:Configuration=$Configuration /p:Platform=x64 /verbosity:minimal
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$built = Join-Path $RepoRoot "native\NOLoader.Proxy\bin\x64\$Configuration\proxy\winhttp.dll"
$artifacts = Join-Path $RepoRoot "artifacts\proxy"
New-Item -ItemType Directory -Force -Path $artifacts | Out-Null
Copy-Item -Force $built (Join-Path $artifacts "winhttp.dll")
Write-Host "Built: $built"
Write-Host "Copied to: $(Join-Path $artifacts 'winhttp.dll')"

