# NOLoader RDYTU

Release (player) workspace. Open **`NOLoader.RDYTU.sln`**.

**Version:** `0.1.0 Build RDY1R6` (core only — no bundled player mods).

## Build

```powershell
dotnet build NOLoader.RDYTU.sln -c RDYTU
```

Configuration **`RDYTU`**: optimized core, no overlay, no hot-reload, **no UDP telemetry** (telemetry is DEV.SDK only).

## Deploy

Scripts live in **`C:\Users\at747\Desktop\CH\_NOLoader_scripts_\`** (not in repo).

Core only:

```powershell
& "C:\Users\at747\Desktop\CH\_NOLoader_scripts_\RDYTU\deploy-noloader.ps1"
```

Optional player mods (after adding to `deploy\NOLoader\mods`):

```powershell
& "C:\Users\at747\Desktop\CH\_NOLoader_scripts_\RDYTU\pack-player-mods.ps1"
& "C:\Users\at747\Desktop\CH\_NOLoader_scripts_\RDYTU\deploy-noloader.ps1" -IncludePlayerMods
```

## Verify

From CH scripts folder:

```powershell
& "C:\Users\at747\Desktop\CH\_NOLoader_scripts_\verify-rdytu.ps1"
```

## RDYTU vs DEV.SDK

| Feature | RDYTU | DEV.SDK |
|---------|-------|---------|
| Overlay / hot-reload | No | Yes |
| UDP Sim-Connect telemetry | **No** | Yes |
| FileWatcher | No | Yes |
| Hash-only mod manifests | Yes | Plain strings + optional dev keys |
| Gate L4 UI banner | Ring log only | Fullscreen stack trace |
| Physics Rigidbody hooks | Off (default) | On |

## Performance (`noloader_config.ini` [RDYTU])

| Key | Default | Effect |
|-----|---------|--------|
| `ring_log=0` | off | No lock on log writes during gameplay |
| `physics_catch_unity=0` | off | No global `Rigidbody.AddForce` hooks |
| `physics_catch_motor=0` | off | No `Motor::Thrust` per-frame hook |
| `stage_poll_seconds=1.0` | legacy | Ignored — mission stage uses `sceneLoaded` events |
| `exception_tracking_subscribe=0` | off | No `logMessageReceived` hook unless enabled |
| `ring_flush_ms=8000` | 8s | Background log flush when ring_log=1 |

## Scope

| Included | Excluded |
|----------|----------|
| API, Core, Patcher, Registry | Telemetry, DEV overlay, hot-reload |
| PatchTool (offline Cecil) | Diag mods, LoaderDiag |

## Release bundle

```powershell
& "C:\Users\at747\Desktop\CH\_NOLoader_scripts_\build-release-bundle.ps1"
```
