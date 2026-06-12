# Diagnostic mods (DEV.SDK only)

Optional audit mods for menu and battle sessions. **Not** deployed by default with `deploy-noloader.ps1`.

| Mod | Stage | Host |
|-----|-------|------|
| LoaderDiagMenu | PreMenu | Main menu probes |
| LoaderDiag | Mission | Battle map probes |

## Build & install

```powershell
.\scripts\deploy-diag-mods.ps1
# optional: copy built folders into game mods manually
```

Manifests: `DEV.SDK/diag-mods/LoaderDiagMenu/mod.json`, `LoaderDiag/mod.json`

Copy built `LoaderDiagMenu/` and `LoaderDiag/` folders into `NOLoader/mods/` only when running audit sessions.
