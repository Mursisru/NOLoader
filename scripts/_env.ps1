# Shared paths for NOLoader scripts (repo-relative)
function Get-NOLoaderRepoRoot {
    if ($env:NOLOADER_REPO) {
        return (Resolve-Path $env:NOLOADER_REPO -ErrorAction Stop).Path
    }
    # _env.ps1 lives in <repo>/scripts/
    return (Resolve-Path (Join-Path $PSScriptRoot "..") -ErrorAction Stop).Path
}

$script:NOLoaderScriptsRoot = $PSScriptRoot
$script:NOLoaderRepoRoot = Get-NOLoaderRepoRoot
$script:NOLoaderRdyRoot = Join-Path $NOLoaderRepoRoot "RDYTU"
$script:NOLoaderSdkRoot = Join-Path $NOLoaderRepoRoot "DEV.SDK"
