# Shared managed-DLL restore helpers for uninstall / repair scripts.

$script:ManagedModuleNames = @(
    "Assembly-CSharp.dll",
    "UnityEngine.CoreModule.dll",
    "UnityEngine.PhysicsModule.dll"
)

function Test-ManagedHasNOLoaderPatches {
    param([string]$Path)
    if (-not (Test-Path $Path)) { return $false }
    $text = [Text.Encoding]::ASCII.GetString([IO.File]::ReadAllBytes($Path))
    return $text.Contains("NOLoader.Core")
        -or $text.Contains("NOLoader.Registry")
        -or $text.Contains("OnMainMenuReady")
        -or $text.Contains("MissionGateHooks")
        -or $text.Contains("NOLoader.RealWeaponNames")
        -or $text.Contains("NOLoader.AutoFlare")
        -or $text.Contains("NOLoader.HudCommon")
        -or $text.Contains("NOLoader.RepeatTakeoffMusic")
}

function Get-ManagedFolder {
    param([string]$GameRoot)
    Join-Path $GameRoot "NuclearOption_Data\Managed"
}

function Remove-LegacyNoloaderBackups {
    param([string]$GameRoot)
    $managed = Get-ManagedFolder $GameRoot
    foreach ($mod in $script:ManagedModuleNames) {
        $legacy = Join-Path $managed ($mod + ".noloader.bak")
        if (Test-Path $legacy) {
            Remove-Item -Force $legacy
            Write-Host "Removed legacy backup: $($mod).noloader.bak"
        }
    }
}

function Test-SnapshotMirageMatchesGame {
    param(
        [string]$SnapshotPath,
        [string]$GameRoot
    )
    if ($SnapshotPath -notmatch 'Assembly-CSharp\.dll') { return $true }
    $managed = Get-ManagedFolder $GameRoot
    $mirageDll = Join-Path $managed "Mirage.dll"
    if (-not (Test-Path $mirageDll)) { return $true }
    $mirageVer = [System.Reflection.AssemblyName]::GetAssemblyName($mirageDll).Version
    $tmp = Join-Path $env:TEMP ("noloader_snap_" + [Guid]::NewGuid().ToString("N") + ".dll")
    try {
        Copy-Item -Force $SnapshotPath $tmp
        $snapRef = Get-AssemblyReferenceVersion -ModulePath $tmp -ReferenceName "Mirage"
        if (-not $snapRef) { return $false }
        return ($mirageVer.Major -eq $snapRef.Major) -and ($mirageVer.Minor -eq $snapRef.Minor) -and ($mirageVer.Build -eq $snapRef.Build)
    } finally {
        if (Test-Path $tmp) { Remove-Item -Force $tmp -ErrorAction SilentlyContinue }
    }
}

function Test-ValidVanillaSnapshot {
    param(
        [string]$Path,
        [string]$GameRoot = ""
    )
    if (-not (Test-Path $Path)) { return $false }
    if (Test-ManagedHasNOLoaderPatches $Path) { return $false }
    if ($GameRoot -and -not (Test-SnapshotMirageMatchesGame -SnapshotPath $Path -GameRoot $GameRoot)) { return $false }
    return $true
}

function Remove-InvalidVanillaSnapshots {
    param([string]$GameRoot)
    $managed = Get-ManagedFolder $GameRoot
    foreach ($mod in $script:ManagedModuleNames) {
        $vanilla = Join-Path $managed ($mod + ".noloader.vanilla.bak")
        if ((Test-Path $vanilla) -and -not (Test-ValidVanillaSnapshot $vanilla -GameRoot $GameRoot)) {
            Remove-Item -Force $vanilla
            Write-Host "Removed invalid vanilla snapshot: $($mod).noloader.vanilla.bak"
        }
    }
}

function Restore-ManagedFromVanillaSnapshot {
    param([string]$GameRoot)
    $managed = Get-ManagedFolder $GameRoot
    $restored = 0
    foreach ($mod in $script:ManagedModuleNames) {
        $vanilla = Join-Path $managed ($mod + ".noloader.vanilla.bak")
        $live = Join-Path $managed $mod
        if (-not (Test-ValidVanillaSnapshot $vanilla -GameRoot $GameRoot)) { continue }
        Copy-Item -Force $vanilla $live
        Write-Host "Restored from vanilla snapshot: $mod"
        $restored++
    }
    return $restored
}

function Get-AssemblyReferenceVersion {
    param(
        [string]$ModulePath,
        [string]$ReferenceName
    )
    if (-not (Test-Path $ModulePath)) { return $null }
    try {
        $asm = [System.Reflection.Assembly]::ReflectionOnlyLoadFrom($ModulePath)
        $ref = $asm.GetReferencedAssemblies() | Where-Object { $_.Name -eq $ReferenceName } | Select-Object -First 1
        if ($ref) { return $ref.Version }
    } catch { }
    return $null
}

function Test-ManagedMirageMismatch {
    param([string]$GameRoot)
    $managed = Get-ManagedFolder $GameRoot
    $csharp = Join-Path $managed "Assembly-CSharp.dll"
    $mirage = Join-Path $managed "Mirage.dll"
    if (-not ((Test-Path $csharp) -and (Test-Path $mirage))) { return $false }
    $mirageDll = [System.Reflection.AssemblyName]::GetAssemblyName($mirage).Version
    $csharpRef = Get-AssemblyReferenceVersion -ModulePath $csharp -ReferenceName "Mirage"
    if (-not $csharpRef) { return $false }
    return ($mirageDll.Major -ne $csharpRef.Major) -or ($mirageDll.Minor -ne $csharpRef.Minor) -or ($mirageDll.Build -ne $csharpRef.Build)
}

function Test-ManagedNeedsRestore {
    param([string]$GameRoot)
    if (Test-ManagedMirageMismatch -GameRoot $GameRoot) { return $true }
    $managed = Get-ManagedFolder $GameRoot
    foreach ($mod in $script:ManagedModuleNames) {
        $live = Join-Path $managed $mod
        if (Test-ManagedHasNOLoaderPatches $live) { return $true }
    }
    return $false
}

function Invoke-PatchToolRestoreVanilla {
    param(
        [string]$GameRoot,
        [string]$RepoRoot
    )
    $project = Join-Path $RepoRoot "src\NOLoader.PatchTool\NOLoader.PatchTool.csproj"
    dotnet run --project $project -c RDYTU -- restore-vanilla $GameRoot
    return $LASTEXITCODE
}

function Invoke-SteamVerifyManaged {
    param(
        [string]$GameRoot,
        [int]$SteamAppId = 2168680,
        [int]$WaitSeconds = 300
    )
    $managed = Get-ManagedFolder $GameRoot
    $asmLive = Join-Path $managed "Assembly-CSharp.dll"
    $coreLive = Join-Path $managed "UnityEngine.CoreModule.dll"

    $steam = Join-Path ${env:ProgramFiles(x86)} "Steam\Steam.exe"
    if (-not (Test-Path $steam)) {
        Write-Error "Steam not found. Verify Nuclear Option (app $SteamAppId) manually, then re-run."
    }

    Write-Host "Starting Steam integrity check (app $SteamAppId)..."
    Start-Process $steam "steam://validate/$SteamAppId"

    $deadline = (Get-Date).AddSeconds($WaitSeconds)
    while ((Get-Date) -lt $deadline) {
        Start-Sleep -Seconds 5
        if (-not (Test-Path $asmLive)) {
            Write-Host "Waiting for Steam to restore files..."
            continue
        }
        if (-not (Test-ManagedNeedsRestore -GameRoot $GameRoot)) {
            Write-Host "Steam verify restored vanilla managed DLLs."
            return
        }
    }

    if (-not (Test-Path $asmLive)) {
        Write-Error "Assembly-CSharp.dll still missing. Finish Steam verify, then re-run."
    }
    if (Test-ManagedNeedsRestore -GameRoot $GameRoot) {
        Write-Error "Managed DLLs still need repair after ${WaitSeconds}s (NOLoader patches or Mirage mismatch). Finish Steam verify, then re-run."
    }
}

function Restore-VanillaManagedModules {
    param(
        [string]$GameRoot,
        [string]$RepoRoot = "",
        [int]$SteamAppId = 2168680,
        [int]$WaitSeconds = 300
    )
    Remove-LegacyNoloaderBackups -GameRoot $GameRoot
    Remove-InvalidVanillaSnapshots -GameRoot $GameRoot

    if (-not (Test-ManagedNeedsRestore -GameRoot $GameRoot)) {
        Write-Host "Managed DLLs are already vanilla."
        return
    }

    $fromSnapshot = Restore-ManagedFromVanillaSnapshot -GameRoot $GameRoot
    if ($fromSnapshot -gt 0 -and -not (Test-ManagedNeedsRestore -GameRoot $GameRoot)) {
        Write-Host "Restored $fromSnapshot module(s) from .noloader.vanilla.bak"
        return
    }

    if ($RepoRoot -and (Test-Path (Join-Path $RepoRoot "src\NOLoader.PatchTool\NOLoader.PatchTool.csproj"))) {
        Write-Host "Trying PatchTool restore-vanilla..."
        $code = Invoke-PatchToolRestoreVanilla -GameRoot $GameRoot -RepoRoot $RepoRoot
        if ($code -eq 0 -and -not (Test-ManagedNeedsRestore -GameRoot $GameRoot)) {
            Write-Host "PatchTool restore-vanilla OK."
            return
        }
    }

    Invoke-SteamVerifyManaged -GameRoot $GameRoot -SteamAppId $SteamAppId -WaitSeconds $WaitSeconds
}

function Remove-NOLoaderInstallFiles {
    param([string]$GameRoot)
    foreach ($rel in @("winhttp.dll", "noloader_config.ini")) {
        $path = Join-Path $GameRoot $rel
        if (Test-Path $path) {
            Remove-Item -Force $path
            Write-Host "Removed: $rel"
        }
        $disabled = $path + ".noloader-disabled"
        if (Test-Path $disabled) {
            Remove-Item -Force $disabled
            Write-Host "Removed: $(Split-Path $disabled -Leaf)"
        }
    }

    $loader = Join-Path $GameRoot "NOLoader"
    if (Test-Path $loader) {
        Remove-Item -Recurse -Force $loader
        Write-Host "Removed: NOLoader\"
    }
    $disabledLoader = $loader + ".noloader-disabled"
    if (Test-Path $disabledLoader) {
        Remove-Item -Recurse -Force $disabledLoader
        Write-Host "Removed: NOLoader.noloader-disabled\"
    }
}
