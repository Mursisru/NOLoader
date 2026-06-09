# Adds DEV_SDK or RDYTU solution configurations to a generated .sln file.
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

if ($content -match "${ConfigName}\|Any CPU") {
    Write-Host "Already patched: $SolutionPath"
    exit 0
}

$modProjectNames = @(
    "NOLoader.DiagCommon",
    "NOLoader.LoaderDiag",
    "NOLoader.LoaderDiagMenu",
    "NOLoader.RegistrySample",
    "NOLoader.WeaponNames",
    "NOLoader.TestMod",
    "NOLoader.LoaderLab",
    "NOLoader.MechanicsTest",
    "NOLoader.BrokenMod"
)

$projectGuids = @{}
foreach ($line in ($content -split "`r?`n")) {
    if ($line -match 'Project\("\{FAE04EC0[^"]+"\) = "([^"]+)", "[^"]+", "(\{[0-9A-F-]+\})"') {
        $projectGuids[$Matches[2].ToUpper()] = $Matches[1]
    }
}

$solutionConfigBlock = @"
		${ConfigName}|Any CPU = ${ConfigName}|Any CPU
		${ConfigName}|x64 = ${ConfigName}|x64
		${ConfigName}|x86 = ${ConfigName}|x86
"@

$content = $content -replace "(\tEndGlobalSection\r?\n\tGlobalSection\(ProjectConfigurationPlatforms\))", ($solutionConfigBlock + "`r`n`tEndGlobalSection`r`n`tGlobalSection(ProjectConfigurationPlatforms)")

$projectConfigLines = New-Object System.Collections.Generic.List[string]
foreach ($guid in $projectGuids.Keys) {
    $name = $projectGuids[$guid]
    $isMod = $modProjectNames -contains $name
    if ($ConfigName -eq "DEV_SDK" -and $isMod) {
        $active = "Debug|Any CPU"
    } else {
        $active = "${ConfigName}|Any CPU"
    }

    $projectConfigLines.Add("`t`t$guid.${ConfigName}|Any CPU.ActiveCfg = $active")
    $projectConfigLines.Add("`t`t$guid.${ConfigName}|Any CPU.Build.0 = $active")
    $projectConfigLines.Add("`t`t$guid.${ConfigName}|x64.ActiveCfg = $active")
    $projectConfigLines.Add("`t`t$guid.${ConfigName}|x64.Build.0 = $active")
    $projectConfigLines.Add("`t`t$guid.${ConfigName}|x86.ActiveCfg = $active")
    $projectConfigLines.Add("`t`t$guid.${ConfigName}|x86.Build.0 = $active")
}

$insert = ($projectConfigLines -join "`r`n") + "`r`n"
$content = $content -replace "(\tEndGlobalSection\r?\n\tGlobalSection\(SolutionProperties\))", ($insert + "`tEndGlobalSection`r`n`tGlobalSection(SolutionProperties)")

Set-Content -LiteralPath $SolutionPath -Value $content -NoNewline
Write-Host "Patched $ConfigName configs in $SolutionPath"

