param(
    [Parameter(Mandatory = $true)]
    [string]$ModFolder,
    [string]$OutputFolder = ""
)

$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "_env.ps1")
$RepoRoot = $NOLoaderRepoRoot

function Get-ApiDll {
    $dll = Get-ChildItem -Path $RepoRoot -Recurse -Filter "NOLoader.API.dll" |
        Where-Object { $_.DirectoryName -like "*\bin\*" } |
        Sort-Object LastWriteTime -Descending |
        Select-Object -First 1
    if (-not $dll) {
        dotnet build (Join-Path $RepoRoot "src\NOLoader.API\NOLoader.API.csproj") -c Release --verbosity quiet | Out-Null
        $dll = Get-ChildItem -Path $RepoRoot -Recurse -Filter "NOLoader.API.dll" |
            Where-Object { $_.DirectoryName -like "*\bin\Release\*" } |
            Select-Object -First 1
    }
    if (-not $dll) { throw "NOLoader.API.dll not found - build src/NOLoader.API first" }
    return $dll.FullName
}

function Get-StringHash {
    param([string]$s)
    if ([string]::IsNullOrEmpty($s)) { return 0 }
    Add-Type -Path (Get-ApiDll) -ErrorAction SilentlyContinue | Out-Null
    return [NOLoader.API.StringHash]::Murmur32($s)
}

function Format-HashHex {
    param([int]$Value)
    return $Value.ToString('X8')
}

$modJsonPath = Join-Path $ModFolder "mod.json"
if (-not (Test-Path $modJsonPath)) { throw "mod.json not found in $ModFolder" }

$src = Get-Content $modJsonPath -Raw | ConvertFrom-Json
$idHash = if ($src.idHash) { [int]$src.idHash } else { Get-StringHash $src.id }

$depHashes = @()
foreach ($dep in @($src.dependencies)) {
    if ($dep -match '^[0-9A-Fa-f]{8}$') { $depHashes += $dep.ToUpperInvariant() }
    else { $depHashes += (Format-HashHex (Get-StringHash $dep)) }
}

$patchList = @()
$bakeList = @()
foreach ($p in @($src.patches)) {
    $targetHash = Format-HashHex (Get-StringHash $p.target)
    $injectHash = Format-HashHex (Get-StringHash $p.inject)
    $patchEntry = @{
        targetHash = $targetHash
        injectHash = $injectHash
        method     = $p.method
    }
    if ($p.expectedSignatureHash) {
        $patchEntry.expectedSignatureHash = $p.expectedSignatureHash
    }
    if ($p.throttleEveryN -and [int]$p.throttleEveryN -gt 1) {
        $patchEntry.throttleEveryN = [int]$p.throttleEveryN
    }
    $patchList += $patchEntry
    $bakeList += @{
        target                 = $p.target
        inject                 = $p.inject
        method                 = $p.method
        expectedSignatureHash  = $p.expectedSignatureHash
        throttleEveryN         = $p.throttleEveryN
        targetHash             = $targetHash
        injectHash             = $injectHash
    }
}

$out = [ordered]@{
    idHash       = $idHash
    version      = $src.version
    assembly     = $src.assembly
    entryType    = $src.entryType
    loadStage    = $src.loadStage
    dependencies = $depHashes
    patches      = $patchList
}

if ([string]::IsNullOrEmpty($OutputFolder)) {
    $OutputFolder = Join-Path $ModFolder "rdytu"
}
New-Item -ItemType Directory -Force -Path $OutputFolder | Out-Null

($out | ConvertTo-Json -Depth 6) | Set-Content -Path (Join-Path $OutputFolder "mod.json") -Encoding UTF8
($bakeList | ConvertTo-Json -Depth 6) | Set-Content -Path (Join-Path $OutputFolder "patch.bake.json") -Encoding UTF8

Get-ChildItem $ModFolder -File | Where-Object { $_.Extension -in '.dll', '.png', '.txt' } | ForEach-Object {
    Copy-Item -Force $_.FullName (Join-Path $OutputFolder $_.Name)
}

Write-Host "RDYTU hash-only manifest: $OutputFolder"
Write-Host "idHash=$idHash ($(Format-HashHex $idHash))"

