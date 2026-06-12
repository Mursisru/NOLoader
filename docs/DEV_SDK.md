# DEV.SDK — developer channel

**Version:** `0.1.0 Build DEV1PM19` / GitHub mirror `PR-R2PM1` after sync.

DEV.SDK is the **full-featured** workspace for mod authors: overlay, hot-reload, telemetry, diagnostic mods, plain-text manifests, and sample mods.

---

## Who should use DEV.SDK

- Writing new NOLoader mods
- Debugging Gate L1–L4
- Running integration tests and field audits
- Prototyping Cecil patches before RDYTU hash-only pack

---

## Open solution

```
DEV.SDK/NOLoader.DEV_SDK.sln
```

Includes: API, Core (+ DEV overlay), Patcher, Registry, **Telemetry**, native proxy project, tests, sample mods.

Configuration: **`DEV_SDK`** → defines `NOLoader_DEV`.

---

## Build & test

```powershell
cd C:\Users\at747\source\repos\NOLoader_Engine
dotnet build DEV.SDK\NOLoader.DEV_SDK.sln -c DEV_SDK
dotnet test tests\NOLoader.Core.Tests\NOLoader.Core.Tests.csproj -c DEV_SDK
dotnet test tests\NOLoader.Patcher.Tests\NOLoader.Patcher.Tests.csproj -c DEV_SDK
```

Native proxy (once per machine or after C++ changes):

```powershell
& "C:\Users\at747\Desktop\CH\_NOLoader_scripts_\build-proxy.ps1"
```

---

## Deploy to game

**Close Nuclear Option first.**

```powershell
& "C:\Users\at747\Desktop\CH\_NOLoader_scripts_\deploy-noloader.ps1" -Configuration DEV_SDK
```

Deploys:

- Core + **Telemetry** DLL
- Sample mods: RegistrySample, WeaponNames, UniversalLoadout, BrokenMod (L2 test)
- Cecil pre-patch when game closed
- Optional hash verify (`-SkipHashVerify` to skip)

Diagnostic mods (`LoaderDiag`, `LoaderDiagMenu`) — **not** auto-deployed:

```powershell
& "C:\Users\at747\Desktop\CH\_NOLoader_scripts_\deploy-diag-mods.ps1"
```

Archived copies: `C:\Users\at747\Desktop\CH\_NOLoader_diag_\`

---

## Developer UI

| Key | Action |
|-----|--------|
| **F10** | Toggle log / gate panel |
| **F11** | Hot-reload mods (DEV only) |

Ring log always on in DEV (not controlled by RDYTU INI section).

---

## Telemetry (NO2)

- UDP port **49000**, ~30 Hz when enabled
- `TelemetryHost` captures on main thread (DEV only)
- **Not present** in RDYTU build

---

## Sample mods (`DEV.SDK/mods/`)

| Mod | loadStage | Purpose |
|-----|-----------|---------|
| RegistrySample | MainMenu | NOModRegistry demo |
| WeaponNames | MainMenu | Encyclopedia postfix prototype |
| UniversalLoadout | MainMenu | WeaponChecker IL patches |
| BrokenMod | PreMenu | Gate L2 negative test |
| FaultMission | MainMenu | Gate L4 negative test |

Each mod has `mod.json` beside source in its project folder.

---

## Full verification

```powershell
& "C:\Users\at747\Desktop\CH\_NOLoader_scripts_\verify-dev-sdk.ps1"
& "C:\Users\at747\Desktop\CH\_NOLoader_scripts_\verify-noloader-all.ps1"
```

Gate-specific tests:

```powershell
& "C:\Users\at747\Desktop\CH\_NOLoader_scripts_\test-gate-l2-brokenmod.ps1"
& "C:\Users\at747\Desktop\CH\_NOLoader_scripts_\test-gate-l4-faultmission.ps1"
```

---

## After game update

Re-bake core patch hashes (game DLLs changed):

```powershell
& "C:\Users\at747\Desktop\CH\_NOLoader_scripts_\bake-core-patch-hashes.ps1"
& "C:\Users\at747\Desktop\CH\_NOLoader_scripts_\verify-core-patch-hashes.ps1"
```

Restore vanilla managed DLLs from `.noloader.bak` if needed, then redeploy.

---

## GitHub release asset

Download **NOLoader-0.1.0-DEV.SDK.zip** from [Release v0.1.0](https://github.com/Mursisru/NOLoader/releases/tag/v0.1.0). Contains DEV-built core + telemetry + proxy + sample mod sources/build outputs for mod authors.

Full source repository recommended for development.

---

## Switching to RDYTU for players

1. Build/deploy RDYTU channel (see [RDYTU.md](RDYTU.md))
2. Remove DEV-only mods from `NOLoader/mods/`
3. Pack mods with `pack-mod-rdytu.ps1` for hash-only manifests

See [MIGRATION.md](MIGRATION.md) for BepInEx migration notes.
