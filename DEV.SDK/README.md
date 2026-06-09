# NOLoader DEV.SDK

Development workspace for NOLoader. Open **`NOLoader.DEV_SDK.sln`** in Visual Studio or Rider.

**Scripts:** `C:\Users\at747\Desktop\CH\_NOLoader_scripts_\` (see `README.md` there).

## Build

```powershell
dotnet build NOLoader.DEV_SDK.sln -c DEV_SDK
dotnet test NOLoader.DEV_SDK.sln -c DEV_SDK
```

Hash / verify (from CH scripts):

```powershell
& "C:\Users\at747\Desktop\CH\_NOLoader_scripts_\bake-core-patch-hashes.ps1"
& "C:\Users\at747\Desktop\CH\_NOLoader_scripts_\verify-dev-sdk.ps1"
```

Native proxy:

```powershell
& "C:\Users\at747\Desktop\CH\_NOLoader_scripts_\build-proxy.ps1"
```

## Deploy to game

```powershell
& "C:\Users\at747\Desktop\CH\_NOLoader_scripts_\deploy-noloader.ps1" -Configuration DEV_SDK
```

Deploys core (`DEV_SDK` build), gameplay mods (`RegistrySample`, `WeaponNames`, `UniversalLoadout`), and runs Cecil pre-patch when the game is closed.

Diagnostic mods (`LoaderDiagMenu`, `LoaderDiag`) are **not** deployed — see `DEV.SDK/diag-mods/` and `deploy-diag-mods.ps1` in CH scripts.

## Contents

| Path | Description |
|------|-------------|
| `mods/` | Dev and sample mods |
| `../src/` | Shared core libraries |
| `../tests/` | Unit and patcher integration tests |
| `../native/` | C++ winhttp proxy |

## Field verification

Cold-start → menu **2× DONE** → battle **2× DONE** → check `NOLoader/logs/noloader_ring.log`.

Or: `prepare-field-gate-dev.ps1` then `verify-noloader-logs.ps1` in CH scripts folder.
