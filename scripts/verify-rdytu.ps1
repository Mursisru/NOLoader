param(

    [string]$GameRoot = "C:\Program Files (x86)\Steam\steamapps\common\Nuclear Option"

)



$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "_env.ps1")

$RepoRoot = $NOLoaderRepoRoot

$RdySln = Join-Path $RepoRoot "RDYTU\NOLoader.RDYTU.sln"

Set-Location $RepoRoot



$fail = 0

$pass = 0



function Step-Pass { param([string]$Msg) Write-Host "[ OK ] $Msg" -ForegroundColor Green; $script:pass++ }

function Step-Fail { param([string]$Msg) Write-Host "[FAIL] $Msg" -ForegroundColor Red; $script:fail++ }



Write-Host "`n=== NOLoader RDYTU verification ===" -ForegroundColor Cyan



Write-Host "--- Unit tests ---" -ForegroundColor Cyan

dotnet test (Join-Path $RepoRoot "tests\NOLoader.Core.Tests\NOLoader.Core.Tests.csproj") -c DEV_SDK --verbosity quiet --no-restore 2>$null

if ($LASTEXITCODE -ne 0) {

    dotnet test (Join-Path $RepoRoot "tests\NOLoader.Core.Tests\NOLoader.Core.Tests.csproj") -c DEV_SDK --verbosity minimal

    if ($LASTEXITCODE -ne 0) { Step-Fail "unit tests"; exit 1 }

}

Step-Pass "unit tests"



Write-Host "--- Build native proxy ---" -ForegroundColor Cyan

& (Join-Path $NOLoaderScriptsRoot "build-proxy.ps1") -Configuration Release

if ($LASTEXITCODE -ne 0) { Step-Fail "build-proxy.ps1"; exit 1 }

Step-Pass "build-proxy.ps1"



Write-Host "--- Build RDYTU ---" -ForegroundColor Cyan

dotnet build $RdySln -c RDYTU --verbosity quiet

if ($LASTEXITCODE -ne 0) { Step-Fail "RDYTU build"; exit 1 }

Step-Pass "RDYTU build"



$coreDlls = @("NOLoader.Core.dll", "NOLoader.API.dll", "NOLoader.Patcher.dll", "NOLoader.Registry.dll")

Write-Host "--- RDYTU artifacts ---" -ForegroundColor Cyan

foreach ($dll in $coreDlls) {

    $path = Get-ChildItem -Path $RepoRoot -Recurse -Filter $dll | Where-Object { $_.DirectoryName -like "*\bin\RDYTU\*" } | Select-Object -First 1

    if ($path) { Step-Pass "Artifact $dll" } else { Step-Fail "Missing $dll (RDYTU)" }

}



$coreDll = Get-ChildItem -Path $RepoRoot -Recurse -Filter "NOLoader.Core.dll" | Where-Object { $_.DirectoryName -like "*\bin\RDYTU\*" } | Select-Object -First 1

if ($coreDll) {

    $bytes = [System.IO.File]::ReadAllBytes($coreDll.FullName)

    $text = [System.Text.Encoding]::ASCII.GetString($bytes)

    if ($text -match 'DevOverlay|HotReloadService|GateL1Panel|TelemetryHost|TelemetryService') {

        Step-Fail "RDYTU Core.dll contains DEV-only symbols"

    } else {

        Step-Pass "RDYTU Core.dll stripped of DEV overlay symbols"

    }

}



Write-Host "--- pack-mod-rdytu ---" -ForegroundColor Cyan

$ulProj = Join-Path $RepoRoot "DEV.SDK\mods\NOLoader.UniversalLoadout\NOLoader.UniversalLoadout.csproj"
dotnet build $ulProj -c Release --verbosity quiet | Out-Null
$ulDll = Join-Path $RepoRoot "DEV.SDK\mods\NOLoader.UniversalLoadout\bin\Release\net48\NOLoader.UniversalLoadout.dll"
$ulMod = Join-Path $env:TEMP "noloader-rdytu-verify-pack-src"
$packOut = Join-Path $env:TEMP "noloader-rdytu-verify-pack"
if (Test-Path $ulMod) { Remove-Item -Recurse -Force $ulMod }
if (Test-Path $packOut) { Remove-Item -Recurse -Force $packOut }
New-Item -ItemType Directory -Force -Path $ulMod | Out-Null
@'
{
  "idHash": "67ACB843",
  "version": "1.0.1",
  "assembly": "NOLoader.UniversalLoadout.dll",
  "entryType": "NOLoader.UniversalLoadout.UniversalLoadoutMod",
  "loadStage": "MainMenu",
  "dependencies": [],
  "patches": []
}
'@ | Set-Content (Join-Path $ulMod "mod.json") -Encoding UTF8
Copy-Item -Force $ulDll (Join-Path $ulMod "NOLoader.UniversalLoadout.dll")

& (Join-Path $NOLoaderScriptsRoot "pack-mod-rdytu.ps1") -ModFolder $ulMod -OutputFolder $packOut

if ($LASTEXITCODE -ne 0) { Step-Fail "pack-mod-rdytu.ps1" } else { Step-Pass "pack-mod-rdytu.ps1" }

if (-not (Test-Path (Join-Path $packOut "patch.bake.json"))) { Step-Fail "patch.bake.json missing" } else { Step-Pass "patch.bake.json generated" }

$hashMod = Get-Content (Join-Path $packOut "mod.json") -Raw

if ($hashMod -match '"id"\s*:') { Step-Fail "hash-only mod.json must not contain plain id" } else { Step-Pass "hash-only mod.json (no plain id)" }



Write-Host "--- pack-player-mods ---" -ForegroundColor Cyan

& (Join-Path $NOLoaderScriptsRoot "RDYTU\pack-player-mods.ps1")

if ($LASTEXITCODE -ne 0) { Step-Fail "pack-player-mods.ps1" } else { Step-Pass "pack-player-mods.ps1" }



if (Test-Path $GameRoot) {

    Write-Host "--- RDYTU deploy (core only) ---" -ForegroundColor Cyan

    & (Join-Path $NOLoaderScriptsRoot "RDYTU\deploy-noloader.ps1") -GameRoot $GameRoot | Out-Null

    if ($LASTEXITCODE -ne 0) { Step-Fail "RDYTU deploy" } else { Step-Pass "RDYTU deploy" }

    if (Test-Path (Join-Path $GameRoot "NOLoader\mods\LoaderDiag\NOLoader.LoaderDiag.dll")) {

        Step-Fail "RDYTU deploy must not include LoaderDiag"

    } else {

        Step-Pass "No diag mods in RDYTU deploy"

    }

}



Write-Host "`n=== RDYTU Summary ===" -ForegroundColor Cyan

Write-Host "Pass: $pass  Fail: $fail"

if ($fail -gt 0) { exit 1 }


