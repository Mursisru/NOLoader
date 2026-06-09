param(
    [string]$GameRoot = "C:\Program Files (x86)\Steam\steamapps\common\Nuclear Option"
)

$ErrorActionPreference = "Stop"
. (Join-Path (Split-Path $PSScriptRoot -Parent) "_env.ps1")
$Marker = Join-Path $GameRoot "NOLoader_FPS_TEST_DISABLED.txt"
$Managed = Join-Path $GameRoot "NuclearOption_Data\Managed"

if (Get-Process -Name "NuclearOption" -ErrorAction SilentlyContinue) {
    Write-Error "Close Nuclear Option before uninstall."
    exit 1
}

function Disable-ItemRename {
    param([string]$Path, [string]$Suffix = ".noloader-disabled")
    if (-not (Test-Path $Path)) { return }
    $dest = $Path + $Suffix
    if (Test-Path $dest) { Remove-Item -Force $dest }
    Rename-Item -Path $Path -NewName (Split-Path $dest -Leaf)
    Write-Host "Disabled: $Path"
}

function Restore-Module {
    param([string]$ModuleFile)
    $live = Join-Path $Managed $ModuleFile
    $bak = $live + ".noloader.bak"
    if (-not (Test-Path $bak)) {
        Write-Warning "No backup: $bak"
        return
    }
    Copy-Item -Force $bak $live
    Write-Host "Restored vanilla: $ModuleFile"
}

$modules = @(
    "Assembly-CSharp.dll",
    "UnityEngine.CoreModule.dll",
    "UnityEngine.PhysicsModule.dll"
)
foreach ($m in $modules) { Restore-Module $m }

Disable-ItemRename (Join-Path $GameRoot "winhttp.dll")
Disable-ItemRename (Join-Path $GameRoot "noloader_config.ini")
Disable-ItemRename (Join-Path $GameRoot "NOLoader")

$patchState = Join-Path $GameRoot "NOLoader.noloader-disabled\patch_state.txt"
if (Test-Path $patchState) { Remove-Item -Force $patchState -ErrorAction SilentlyContinue }

@"
NOLoader disabled for FPS baseline test.
Disabled at: $(Get-Date -Format o)

Restore:
  .\scripts\RDYTU\restore-after-fps-test.ps1

Or full redeploy:
  .\scripts\deploy-noloader.ps1 -Configuration RDYTU
"@ | Set-Content -Encoding UTF8 $Marker

Write-Host "`nFPS test mode: game runs without NOLoader (vanilla managed DLLs, no proxy)."
Write-Host "Marker: $Marker"

