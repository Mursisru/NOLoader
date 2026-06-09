# NOLoader diagnostic mods (off-game archive)

LoaderDiag / LoaderDiagMenu are **not** deployed with normal `deploy-noloader.ps1`.

| What | Path |
|------|------|
| Source | `DEV.SDK/mods/NOLoader.LoaderDiag*` + `NOLoader.DiagCommon` |
| Manifests | `DEV.SDK/diag-mods/LoaderDiag*/mod.json` |
| Built drop (canonical) | `C:\Users\at747\Desktop\CH\_NOLoader_diag_\` |
| Build + pack | `scripts/deploy-diag-mods.ps1` |
| Install into game (field gate only) | `scripts/deploy-diag-mods.ps1 -InstallToGame` |

Copy `_NOLoader_diag_\LoaderDiagMenu` and `_NOLoader_diag_\LoaderDiag` into  
`...\Nuclear Option\NOLoader\mods\` only when running menu/battle audit sessions.
