# NOModOptimizer (NOLoader RDYTU)

Surgical optimizations for **mod DLLs only** — does not change VSync, target FPS, display detail, EngineTweaker culling, or game IL unless a mod opts in via manifest.

## Version

`0.1.0 Build DEV4O1` (cycle **4**, letter **O**)

Phase 1 + Phase 2 (analyzer, reflection cache, shader warmup, Find redirect, scene locator, collision registry) — single build **DEV4O1**.

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

- Caller assembly filter — game/core unchanged path uses hierarchy search (avoids redirect recursion)
- Mod callers: `ModSceneLocator.TryGet` → hierarchy fallback → cache hit

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

`NOLoader.ModOptimizerVerify` — spawns 30 proxy GOs, scene locator, reflection bake, collision (when `mod_collision_layers=1`).

Deploy: `scripts/RDYTU/deploy-modoptimizer-verify-mod.ps1 -EnableCollisionLayers`  
Parse: `scripts/RDYTU/parse-modoptimizer-ringlog.ps1`

## Rollback

Set `mod_optimizer=0`. If `mod_scene_locator` was enabled, restore `UnityEngine.CoreModule.dll` from `.noloader.bak` and redeploy.

## Intentionally not done

- IL-rewrite `Update` → custom hook in mod DLL
- Global reflection `Invoke` intercept
- Gameplay/network/physics patches
- Refresh rate or visual LOD changes
