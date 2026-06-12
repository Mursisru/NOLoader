# NOLoader DEV.SDK

Development workspace for NOLoader. Open **`NOLoader.DEV_SDK.sln`** in Visual Studio or Rider.

Scripts: [`scripts/`](../scripts/README.md)

## Build

```powershell
dotnet build NOLoader.DEV_SDK.sln -c DEV_SDK
dotnet test NOLoader.DEV_SDK.sln -c DEV_SDK
```

Hash / verify:

```powershell
.\scripts\bake-core-patch-hashes.ps1
.\scripts\verify-dev-sdk.ps1
```

Native proxy:

```powershell
.\scripts\build-proxy.ps1
```

## Deploy to game

```powershell
.\scripts\deploy-noloader.ps1 -Configuration DEV_SDK
```

Deploys core (`DEV_SDK` build), gameplay mods (`RegistrySample`, `WeaponNames`, `UniversalLoadout`), and runs Cecil pre-patch when the game is closed.

Diagnostic mods (`LoaderDiagMenu`, `LoaderDiag`) are **not** deployed automatically — see `diag-mods/` and `.\scripts\deploy-diag-mods.ps1`.

## Contents

| Path | Description |
|------|-------------|
| `mods/` | Dev and sample mods |
| `../src/` | Shared core libraries |
| `../tests/` | Unit and patcher integration tests |
| `../native/` | C++ winhttp proxy |

## Field verification

Cold-start → menu **2× DONE** → battle **2× DONE** → check `NOLoader/logs/noloader_ring.log`.

Or: `.\scripts\prepare-field-gate-dev.ps1` then `.\scripts\verify-noloader-logs.ps1`

See also [docs/DEV_SDK.md](../docs/DEV_SDK.md).
