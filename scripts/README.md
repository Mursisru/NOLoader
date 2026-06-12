# NOLoader scripts

PowerShell helpers for building, deploying, and verifying NOLoader. Run from the **repository root** or pass `-GameRoot` / `$env:NOLOADER_REPO` when needed.

## Deploy (game must be closed)

```powershell
.\scripts\build-proxy.ps1
.\scripts\deploy-noloader.ps1 -Configuration RDYTU
.\scripts\deploy-noloader.ps1 -Configuration DEV_SDK
```

`-GameRoot` — path to Nuclear Option install (default: Steam `common\Nuclear Option`).

## Verify

```powershell
.\scripts\verify-rdytu.ps1
.\scripts\verify-dev-sdk.ps1
.\scripts\verify-noloader-all.ps1
```

## Hash baking (Gate L2 / core patches)

```powershell
.\scripts\bake-core-patch-hashes.ps1
.\scripts\bake-mod-patch-hashes.ps1
.\scripts\verify-core-patch-hashes.ps1
```

## RDYTU-specific

| Script | Purpose |
|--------|---------|
| `RDYTU/deploy-noloader.ps1` | Build + deploy RDYTU core |
| `RDYTU/pack-player-mods.ps1` | Pack mods for player deploy |
| `pack-mod-rdytu.ps1` | Hash-only mod pack for RDYTU |

## DEV.SDK-specific

| Script | Purpose |
|--------|---------|
| `DEV.SDK/deploy-noloader.ps1` | Build + deploy DEV core and sample mods |
| `deploy-diag-mods.ps1` | Build optional diag mods |
| `prepare-field-gate-dev.ps1` | Pre-flight before field verification |

## Release

```powershell
.\scripts\build-release-zips.ps1
```

Builds `artifacts/release/0.1.0/NOLoader-0.1.0-RDYTU.zip` and `NOLoader-0.1.0-DEV.SDK.zip`.

## Environment

| Variable | Purpose |
|----------|---------|
| `NOLOADER_REPO` | Override auto-detected repository root |
| `NOLOADER_GAME_ROOT` | Used by some verify scripts |
