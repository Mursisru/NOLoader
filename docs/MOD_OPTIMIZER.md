# NOModOptimizer (NOLoader RDYTU)

Surgical optimizations for **mod DLLs only** — does not change VSync, target FPS, display detail, EngineTweaker culling, or game IL unless a mod opts in via manifest.

**Does not increase base-game FPS.** For rendering wins use EngineTweaker HUD/culling (not in RDYTU Cecil plan yet) or NOGpuRender sub-flags — see [Production profile](#production-profile).

## Version

`0.1.0 Build DEV4O2` (cycle **4**, letter **O**)

Phase 1 + Phase 2 (analyzer, reflection cache, shader warmup, Find redirect, scene locator, collision registry).

## INI (`noloader_config.ini`)

| Key | Default | Effect |
|-----|---------|--------|
| `mod_optimizer` | `0` | Master switch (sub-keys ignored when `0`) |
| `mod_tick_analyzer` | `1` | Cecil scan mod DLL at load (Update/Find/reflection) |
| `mod_reflection_cache` | `1` | Opt-in delegate bake API |
| `mod_scene_locator` | `1` | Name→GO registry + `GameObject.Find` redirect for mod callers |
| `mod_collision_layers` | `0` | Opt-in projectile layer matrix (validate layer slot) |
| `mod_shader_warmup` | `1` | Mission-load shader warmup from manifest |
| `mod_layer_projectile` | `27` | Reserved physics layer for mod projectiles |
| `mod_shader_warmup_budget_ms` | `50` | Max warmup time per mission load |

## Production profile

| Setting | Production | Field test |
|---------|------------|------------|
| `ring_log` | `0` | `1` (lock per line) |
| `mod_optimizer` | `0` unless mods need it | `1` with verify mod |
| `gpu_render` | `0` or full stack (`gpu_hud_pass=1`, …) | metrics only OK |
| Verify mod | **remove** from `NOLoader/mods/` | `deploy-modoptimizer-verify-mod.ps1` (lite, 3 proxies) |

**EngineTweaker RDYTU:** only `string_cache` + `frame_cache` postfix are patched. `hud_refresh_skip` / `culling_optimizer` are **not** in the RDYTU Cecil plan (known throttle/gameplay regressions).

**NOGpuRender:** with `gpu_render=1` but `gpu_hud_pass=0` and no `INOModGpuCompute` mods, DEV4O2 skips per-camera `CommandBuffer` work (no idle GPU dispatch overhead).

## 1. Update culling (API-first)

RDYTU already schedules mod work via `INOModTickFast` / `Normal` / `Slow`. ModOptimizer **does not** IL-rewrite `Update` in mod DLLs.

At load, `ModLoadAnalyzer` scans the mod assembly:

- `MonoBehaviour` types with non-empty `Update` / `LateUpdate` / `FixedUpdate`
- `GameObject.Find` call sites
- `Type.GetMethod` + `Invoke` heuristics

Ring log:

- `[ModOpt][PASS] tick_clean mod=…`
- `[ModOpt][WARN] mod=… magic_update=N find_calls=N reflection_invoke=N`

If a mod implements tick interfaces **and** has magic `Update`, tick tier is demoted one level (`ModExecutionBudget`).

## 2. Reflection baking

```csharp
// manifest optional block
"reflectionBake": [
  { "type": "MyMod.MyType", "method": "StaticWorker" }
]

// runtime
NOModRuntime.Reflection.TryGetDelegate<Action>(asm, typeName, methodName, out var del);
NOModRuntime.Reflection.Bake(asm, modIdHash, typeName, methodName);
```

Main thread only (same rule as GPU compute). No global `System.Reflection` intercept in v1.

## 3. Collision matrix (opt-in)

```csharp
NOModRuntime.Collision.RegisterProjectile(go, ModCollisionProfile.Projectile);
NOModRuntime.Collision.Unregister(go);
```

Assigns GO to `mod_layer_projectile` (default **27** — confirm vs game layer map before enabling `mod_collision_layers=1`).

## 4. GameObject.Find liquidator

Cecil **Redirect** on `UnityEngine.GameObject::Find(string)` in `UnityEngine.CoreModule.dll`.

`ModOptimizerHooks.FindRedirect`:

- **Game/core callers:** native `Find` via vanilla CoreModule backup (`ModNativeGameObjectFind`) — DEV4O2
- **Mod callers:** `ModSceneLocator.TryGet` → hierarchy fallback → cache hit

Mods register spawned objects:

```csharp
NOModRuntime.Scene.Register("MyFxRoot", go);
NOModRuntime.Scene.Unregister("MyFxRoot");
```

## 5. Shader pre-warming

```json
"warmup": {
  "shaders": ["Custom/ModExplosion"],
  "materials": ["effects/trail.mat"],
  "prefabs": []
}
```

Runs on `ModLifecycleManager.NotifyMissionReady()`. Shaders via `Shader.Find` + `Material.SetPass`. Budget capped by `mod_shader_warmup_budget_ms`.

Log: `[ModOpt] warmup materials=N shaders=N ms=…`

## Verify mod

`NOLoader.ModOptimizerVerify` — **lite default (3 proxy GOs)**; reflection, scene locator, Find, collision (when `mod_collision_layers=1`).

Deploy: `scripts/RDYTU/deploy-modoptimizer-verify-mod.ps1`  
Full stress (30 GO): `-FullProbe -EnableCollisionLayers`  
Parse: `scripts/RDYTU/parse-modoptimizer-ringlog.ps1`

**Do not leave verify mod installed for normal play** — it adds DontDestroyOnLoad proxies and tick demote noise.

## Rollback

Set `mod_optimizer=0`. If `mod_scene_locator` was enabled, restore `UnityEngine.CoreModule.dll` from `.noloader.vanilla.bak` and redeploy.

## Intentionally not done

- IL-rewrite `Update` → custom hook in mod DLL
- Global reflection `Invoke` intercept
- Gameplay/network/physics patches
- Refresh rate or visual LOD changes
- EngineTweaker HUD/culling in RDYTU build (regression hold)
