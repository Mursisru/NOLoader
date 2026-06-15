# RDYTU.mini

Minimal player loader branch: **mod load optimizer only** — no EngineTweaker, GpuRender, CoreBalancer, or HUD throttle IL.

**Version:** `0.1.0 RDYTU.mini`

## Purpose

1. Load all mods from `NOLoader/mods/` once via **NOModOptimizer** (reflection cache, scene locator, tick analyzer, shader warmup).
2. Write **one** startup line to `NOLoader/logs/noloader_boot.log` (no ring log spam).

## INI (`noloader_config.ini`)

| Key | Value |
|-----|-------|
| `rdytu_mini` | `1` — master switch; forces all perf hooks off |
| `mod_optimizer` | `1` |
| `mod_reflection_cache` | `1` |
| `mod_scene_locator` | `1` |
| `mod_tick_analyzer` | `1` |
| `ring_log` | `0` |

Template: `deploy/noloader_config.mini.ini`

## Deploy

```powershell
# Game closed
.\scripts\RDYTU\deploy-noloader-mini.ps1
# or
.\scripts\RDYTU\deploy-noloader.ps1 -RdytuMini
```

## Boot log example

```
[NOLoader] 0.1.0 RDYTU.mini mods=4 loaded=4 failed=0 optimizer=1 reflection=1 scene=1
```

## What stays

- Gate L4 (mission load safety)
- Mod lifecycle + tick scheduler
- Mod IL patch pipeline + preload
- NOModRegistry (Encyclopedia hook)
- `GameObject.Find` redirect when `mod_scene_locator=1`

## What is removed

- `engine_tweaker` (ground cull, TrackIR guard, adaptive trees)
- `gpu_render`, `core_balancer`, `hud_marker_throttle`
- Ring log noise (`ring_log=0`)

## Branch

```text
git checkout RDYTU.mini
```

Player zip label: **NOLoader-0.1.0-RDYTU.mini** (when packed from this branch).
