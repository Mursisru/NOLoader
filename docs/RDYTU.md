# RDYTU — player / release channel

**Version:** `0.1.0 Build RDY1R6` (see `AppVersion.cs` when built with `RDYTU` configuration).

RDYTU is the **optimized** build for players and end-user mod packs. No developer overlay, no UDP telemetry, no hot-reload, minimal runtime overhead (~1 FPS vs vanilla in field testing, core only).

---

## Who should use RDYTU

- Players installing NOLoader in Nuclear Option
- Shipping hash-only mod packs (no plain ids in manifests)
- Production / benchmark runs

Use **DEV.SDK** if you are writing or debugging mods.

---

## Build

```powershell
cd C:\Users\at747\source\repos\NOLoader_Engine
dotnet build RDYTU\NOLoader.RDYTU.sln -c RDYTU
```

Output: `src/NOLoader.Core/bin/RDYTU/net48/` (+ API, Patcher, Registry). **No** `NOLoader.Telemetry.dll`.

---

## Install / deploy

**Game must be closed** (PatchTool modifies `NuclearOption_Data/Managed/*.dll`).

```powershell
& "C:\Users\at747\Desktop\CH\_NOLoader_scripts_\RDYTU\deploy-noloader.ps1"
```

This will:

1. Build RDYTU solution
2. Copy core DLLs to `NOLoader/core/`
3. Copy `winhttp.dll` proxy to game root
4. Copy `noloader_config.ini`
5. Clear `NOLoader/mods/*` — **all mod folders removed** (verify mods are not kept; use field-test scripts)
6. Run PatchTool (Cecil pre-patch)

Production deploy uses [`deploy/noloader_config.ini`](../deploy/noloader_config.ini) defaults (`mod_optimizer=0`, `gpu_render=0`, `frame_cache=0`). Field-test flags are **not** preserved across redeploy.

```powershell
# Field test base INI only (verify mod deployed separately):
& "...\RDYTU\deploy-noloader.ps1" -FieldTest
& "...\RDYTU\deploy-modoptimizer-verify-mod.ps1" -EnableModOptimizer
```

### FPS benchmark profiles

```powershell
& "...\RDYTU\benchmark-noloader-profile.ps1"           # status
& "...\RDYTU\benchmark-noloader-profile.ps1" -Profile B  # production minimal
& "...\RDYTU\benchmark-noloader-profile.ps1" -Profile C  # fieldtest INI base
```

| Profile | INI | mods/ |
|---------|-----|-------|
| A vanilla | NOLoader off | — |
| B minimal | production template | empty |
| C fieldtest | `-FieldTest` + explicit verify deploy | one *Verify |

**Verify mods** (`ModOptimizerVerify`, `GpuRenderVerify`, `CoreBalancerVerify`, `MechanicsVerify`) live in `DEV.SDK/mods/` only — **do not leave in `NOLoader/mods/` for flight**.

### Optional player mods

Add mod folders under `deploy/NOLoader/mods/`, then:

```powershell
& "C:\Users\at747\Desktop\CH\_NOLoader_scripts_\RDYTU\pack-player-mods.ps1"
& "C:\Users\at747\Desktop\CH\_NOLoader_scripts_\RDYTU\deploy-noloader.ps1" -IncludePlayerMods
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

### What affects FPS vs mod-only features

| Feature | Base-game FPS | Mod benefit |
|---------|---------------|-------------|
| Gate L4 only (`deploy -Minimal`) | ~1 FPS overhead | — |
| **maxopt (DEV9O11 production)** | ground cull + adaptive trees + HUD throttle | vanilla camera/TrackIR via `trackir_safe_mode` |
| `culling_ground_renderer=1` | GPU mesh off for skipped GV | horizon GPU 96% scenario |
| `fps_adaptive_detail=1` | runtime tree/grass throttle when FPS &lt;58 | grass `DetailRenderer` bottleneck |
| `culling_pilot_anim=1` | **opt-in** — field test only; thrust regression risk |
| `culling_optimizer=1` | **legacy** — enables both ground wheels + pilot anim |
| `gpu_hud_pass=1` | **experimental** — opt-in field test only | — |
| `canvas_limiter=1` | **experimental** — can break RawImage HUD textures | — |
| `string_cache=1` | opt-in only (not production) | HUD text mods |
| `world_snapshot_stride=4` | mod path only | mods using `ActivateWorld()` |
| `mod_optimizer=1` | Find IL overhead | mod scene locator |
| Verify mods in `mods/` | DDOL probes, ticks | field test only |

Ring log on boot: `[NOLoader] perf profile=minimal|maxopt|fieldtest`.

Deploy minimal rollback: `.\scripts\RDYTU\deploy-noloader.ps1 -Minimal`

Benchmark: `.\scripts\RDYTU\benchmark-noloader-profile.ps1 -Profile B` (maxopt) or `-Profile D` (minimal).

**Airport A/B (DEV9O7):** цель **≥60 FPS** оба прогона; TrackIR + extreme pan back — без тряски; `ring_log=1` → `.\scripts\RDYTU\parse-ground-cull-ringlog.ps1`.

---

## Verify install

```powershell
& "C:\Users\at747\Desktop\CH\_NOLoader_scripts_\verify-rdytu.ps1"
```

Manual checks:

- `NOLoader/core/NOLoader.Core.dll` — recent timestamp
- No `NOLoader.Telemetry.dll` in core
- `proxy.log` — bootstrap lines after launch
- Optional: FPS baseline script `RDYTU/uninstall-for-fps-test.ps1` in CH scripts

---

## Uninstall / restore vanilla

```powershell
& "C:\Users\at747\Desktop\CH\_NOLoader_scripts_\RDYTU\uninstall-for-fps-test.ps1"
& "C:\Users\at747\Desktop\CH\_NOLoader_scripts_\RDYTU\restore-after-fps-test.ps1"
```

Uninstall restores `Managed/*.dll` from `*.noloader.bak` and disables proxy.

---

## GitHub release asset

Download **NOLoader-0.1.0-RDYTU.zip** from [Release v0.1.0](https://github.com/Mursisru/NOLoader/releases/tag/v0.1.0). Contains pre-built core + proxy + INI + install README — no source required for players.

Source code: full repository; build with `RDYTU` configuration.

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
| Typical overhead | ~1 FPS (core) | Higher (diagnostics) |
