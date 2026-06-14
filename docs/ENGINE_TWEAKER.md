# NOEngineTweaker (RDYTU)

Global Cecil patches in `NOLoader.Core` that optimize Nuclear Option gameplay IL — not per-mod fixes.

**Version:** `0.1.0 Build DEV9O11+`  
**Game:** signature hashes verified against Steam build **23670157** (~2026-06-12).

## INI (`noloader_config.ini` in game root)

| Key | Default (production deploy) | Effect |
|-----|------------------------|--------|
| `trackir_safe_mode` | `1` | In cockpit + TrackIR (or extreme look back/down): bypass tweaker hooks for stable camera |
| `engine_tweaker` | `1` | Master switch for tweaker Cecil patches |
| `hud_marker_throttle` | `1` | Round-robin budget for `HUDUnitMarker::UpdatePosition` |
| `hud_markers_per_frame` | `12` | Per-frame marker W2S cap (`0` = auto) |
| `culling_ground_wheels` | `1` | CPU skip: `GroundVehicle::Update` + `AnimateWheels` |
| `culling_ground_renderer` | `1` | GPU skip: `Renderer.enabled=false` on compound-culled GV |
| `culling_offscreen_only` | `1` | Compound gate: off-screen **or** distance/detail caps |
| `culling_on_screen_max_m` | `400` | Skip wheels/audio/meshes farther than N m on-screen |
| `fps_adaptive_detail` | `1` | When FPS &lt;57 for 1.5s: lower tree range to 0.55× (no frame skip) |
| `culling_pilot_anim` | `0` | **Off** — thrust regression risk |
| `gpu_render` | `0` | **Off** in production — no GpuRenderHost overhead |
| `cull_distance_m` | `5000` | Hard distance cap |
| `display_detail_min` | `1.0` | Early-exit before frustum when below threshold |

### Stable 60+ FPS profile (DEV9O11)

**Camera / TrackIR:** `CameraStateManager` not patched. `trackir_safe_mode=1` — in cockpit + TrackIR (or extreme pan/tilt) bypasses tweaker hooks (GV cull, HUD throttle, GPU cull).

1. **CPU** — compound ground skip (wheels + audio), early `displayDetail` exit
2. **GPU** — `culling_ground_renderer` disables distant/off-screen `GroundVehicle` meshes
3. **Adaptive** — `fps_adaptive_detail` lowers tree range only while FPS &lt;57 (restores at ≥63)

Ring log: `ground_renderer_skip`, `adaptive_detail=on|off`, `[TrackIrSafe] protect=on`.

**Field test:** `ring_log=1` → `.\scripts\RDYTU\parse-ground-cull-ringlog.ps1` — cluster vs horizon 60 s.

## Patched modules

- `Assembly-CSharp.dll` — `GroundVehicle::Update`, `AnimateWheels`, `CombatHUD` markers, optional `Pilot::*` (no `CameraStateManager`)

## Rollback

```powershell
# Game closed
.\scripts\uninstall-noloader.ps1
# or redeploy minimal:
.\scripts\RDYTU\deploy-noloader.ps1 -Minimal
```
