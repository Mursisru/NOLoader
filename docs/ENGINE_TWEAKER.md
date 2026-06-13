# NOEngineTweaker (RDYTU)

Global Cecil patches in `NOLoader.Core` that optimize Nuclear Option gameplay IL — not per-mod fixes.

**Version:** `0.1.0 Build RDY1R108+`  
**Game:** signature hashes verified against Steam build **23670157** (~2026-06-12).

## INI (`noloader_config.ini` in game root)

| Key | Default (RDYTU deploy) | Effect |
|-----|------------------------|--------|
| `engine_tweaker` | `1` | Master switch for all tweaker patches |
| `string_cache` | `1` | Redirect `UnitConverter::*Reading` to cached strings |
| `hud_refresh_skip` | `1` | Skip HUD/MFD `Refresh()` when speed/alt unchanged |
| `culling_optimizer` | `1` | Distance-based visual throttling via `displayDetail` |
| `frame_cache` | `1` | Cache camera/aircraft position per frame (`INOModFrameCache`) |
| `canvas_limiter` | `0` | Dedupe uGUI graphic rebuild requests (opt-in) |
| `cull_distance_m` | `5000` | Force `displayDetail=0` beyond this distance |
| `display_detail_min` | `1.0` | Threshold for skipping wheel/animator work |
| `string_cache_max` | `2000` | Pre-warm table size for speed strings |

## Patched modules

- `Assembly-CSharp.dll` — UnitConverter, HUDAppManager, MFDAppManager, CameraStateManager, GroundVehicle, Pilot*
- `UnityEngine.UI.dll` — `CanvasUpdateRegistry` (only when `canvas_limiter=1`)

## Rollback

```powershell
# Game closed
dotnet run --project src\NOLoader.PatchTool\NOLoader.PatchTool.csproj -c RDYTU -- restore-vanilla "C:\Program Files (x86)\Steam\steamapps\common\Nuclear Option"
.\scripts\RDYTU\deploy-noloader.ps1 -Configuration RDYTU
```

## After Steam update

1. `restore-vanilla` + redeploy  
2. `PatchTool verify-hashes`  
3. Re-run `hash-patch` for targets in `EngineTweakerPatches.cs` if game changed  

Ring log: `[NOLoader] NOEngineTweaker initialized …` at main menu; `[EngineTweaker] string_cache hits=…` via periodic stats hook.
