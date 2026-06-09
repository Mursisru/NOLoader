param(
    [Parameter(Mandatory = $true)]
    [string]$SolutionPath,
    [Parameter(Mandatory = $true)]
    [ValidateSet("DEV_SDK", "RDYTU")]
    [string]$ConfigName
)

$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "_env.ps1")
$content = Get-Content -LiteralPath $SolutionPath -Raw

if ($content -notmatch '\{E1F2A3B4-C5D6-7890-4567-901234567890\}') {
    Write-Host "Proxy project not in solution: $SolutionPath"
    exit 0
}

if ($content -match "\{E1F2A3B4-C5D6-7890-4567-901234567890\}\.$ConfigName\|Any CPU\.ActiveCfg") {
    Write-Host "Proxy configs already patched: $SolutionPath"
    exit 0
}

$proxyBlock = @"
		{E1F2A3B4-C5D6-7890-4567-901234567890}.$ConfigName|Any CPU.ActiveCfg = Release|x64
		{E1F2A3B4-C5D6-7890-4567-901234567890}.$ConfigName|Any CPU.Build.0 = Release|x64
		{E1F2A3B4-C5D6-7890-4567-901234567890}.$ConfigName|x64.ActiveCfg = Release|x64
		{E1F2A3B4-C5D6-7890-4567-901234567890}.$ConfigName|x64.Build.0 = Release|x64
		{E1F2A3B4-C5D6-7890-4567-901234567890}.$ConfigName|x86.ActiveCfg = Release|x64
		{E1F2A3B4-C5D6-7890-4567-901234567890}.$ConfigName|x86.Build.0 = Release|x64
"@

$content = $content -replace "(\tEndGlobalSection\r?\n\tGlobalSection\(SolutionProperties\))", ($proxyBlock + "`r`n`tEndGlobalSection`r`n`tGlobalSection(SolutionProperties)")
Set-Content -LiteralPath $SolutionPath -Value $content -NoNewline
Write-Host "Patched proxy configs in $SolutionPath"

