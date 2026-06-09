# RDYTU — player / release channel

**Version:** `0.1.0 Build RDY1R6` (see `AppVersion.cs` when built with `RDYTU` configuration).

RDYTU is the **optimized** build for players and end-user mod packs. No developer overlay, no UDP telemetry, no hot-reload, minimal runtime overhead.

---

## Who should use RDYTU

- Players installing NOLoader in Nuclear Option
- Shipping hash-only mod packs (no plain ids in manifests)
- Production / benchmark runs

Use **DEV.SDK** if you are writing or debugging mods.

---

## Build

```powershell
dotnet build RDYTU\NOLoader.RDYTU.sln -c RDYTU
```

Output: `src/NOLoader.Core/bin/RDYTU/net48/` (+ API, Patcher, Registry). **No** `NOLoader.Telemetry.dll`.

---

## Install / deploy

**Game must be closed** (PatchTool modifies `NuclearOption_Data/Managed/*.dll`).

```powershell
.\scripts\deploy-noloader.ps1 -Configuration RDYTU
```

This will:

1. Build RDYTU solution
2. Copy core DLLs to `NOLoader/core/`
3. Copy `winhttp.dll` proxy to game root
4. Copy `noloader_config.ini`
5. Clear `NOLoader/mods/*` (except README) — **no bundled mods**
6. Run PatchTool (Cecil pre-patch)

### Optional player mods

Add mod folders under `deploy/NOLoader/mods/`, then:

```powershell
.\scripts\RDYTU\pack-player-mods.ps1
.\scripts\deploy-noloader.ps1 -Configuration RDYTU -IncludePlayerMods
```

---

## Game layout after install

```
Nuclear Option/
  winhttp.dll                 # NOLoader proxy
  noloader_config.ini
  NOLoader/
    core/
      NOLoader.Core.dll       # RDY channel build
      NOLoader.API.dll
      NOLoader.Patcher.dll
      NOLoader.Registry.dll
      Mono.Cecil.dll
    mods/
      README.txt              # empty by default
    logs/
      proxy.log
      bootstrap_fatal.txt     # if bootstrap fails
    patch_state.txt           # deploy markers (PatchTool)
  NuclearOption_Data/
    Managed/
      Assembly-CSharp.dll     # patched (with .noloader.bak)
      UnityEngine.CoreModule.dll
```

---

## Performance profile (`noloader_config.ini`)

Section `[RDYTU]` — **DEV.SDK ignores this section**.

| Key | Default | Effect |
|-----|---------|--------|
| `ring_log=0` | off | No lock on log writes during gameplay |
| `physics_catch_unity=0` | off | No global `Rigidbody.AddForce` hooks |
| `physics_catch_motor=0` | off | No `Motor::Thrust` per-frame hook |
| `exception_tracking=1` | on | Track mod faults at load |
| `exception_tracking_subscribe=0` | off | No Unity log hook unless enabled |
| `stage_poll_seconds=1.0` | legacy | Ignored — mission uses `sceneLoaded` events |
| `ring_flush_ms=8000` | 8s | Background flush when `ring_log=1` |

---

## Verify install

```powershell
.\scripts\verify-rdytu.ps1
```

Manual checks:

- `NOLoader/core/NOLoader.Core.dll` — recent timestamp
- No `NOLoader.Telemetry.dll` in core
- `proxy.log` — bootstrap lines after launch

---

## Uninstall / restore vanilla

Restore `NuclearOption_Data/Managed/*.dll` from `*.noloader.bak`, remove NOLoader proxy and folder. See [INSTALL.md](INSTALL.md).

---

## GitHub release asset

Download **NOLoader-0.1.0-RDYTU.zip** from [Release v0.1.0](https://github.com/Mursisru/NOLoader/releases/tag/v0.1.0). Contains pre-built core + proxy + INI + install README.

---

## RDYTU vs DEV.SDK

| Feature | RDYTU | DEV.SDK |
|---------|-------|---------|
| F10/F11 overlay | No | Yes |
| Hot-reload | No | Yes |
| UDP telemetry | No | Yes |
| Hash-only mod manifests | Yes | Optional |
| Gate L4 fullscreen banner | No | Yes |
| Sample mods in deploy | No | Yes (RegistrySample, WeaponNames, …) |
| Default physics hooks | Off | On (Rigidbody) |
| Typical overhead | Low (core) | Higher (diagnostics) |
