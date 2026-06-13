# NOGpuRender (NOLoader RDYTU)

Client-side GPU offload — **does not** change simulation, network, or saves.

## Version

`0.1.0 Build DEV2O13` (cycle **2**, letter **O**)

## INI (`noloader_config.ini`)

| Key | Default | Effect |
|-----|---------|--------|
| `gpu_render` | `0` | Master switch |
| `gfx_native_jobs` | `1` | Merge `gfx-enable-gfx-jobs=1` into `NuclearOption_Data/boot.config` |
| `gpu_metrics` | `1` | Log GPU device/threading every 30s |
| `canvas_limiter` | `0` | uGUI rebuild dedup (also when `gpu_render=1` without EngineTweaker) |
| `gpu_hud_pass` | `0` | Instanced overlay for CombatHUD markers |
| `gpu_fx_instancing` | `0` | GPU proxy draw for chaff (RadarChaff) |

## Layers

1. **boot.config** — native gfx jobs (CPU render thread split)
2. **canvas_limiter** — CPU uGUI rebuild skip
3. **GpuHudPass** — `Graphics.DrawMeshInstanced` for HUD markers
4. **FxInstancingRegistry** — batched chaff proxy meshes
5. **INOModGpuCompute** — mod compute dispatch hook (main thread)

## Mod API

```csharp
public class MyMod : INOMod, INOModGpuCompute {
    public void OnDispatchGpu(ref NOModContext ctx, object commandBuffer) {
        // commandBuffer is UnityEngine.Rendering.CommandBuffer
    }
}
```

Register via `NOModRuntime.Gpu` on mod load (automatic when implementing interface).

## Verify mod (single field test for DEV2O13)

`NOLoader.GpuRenderVerify` — GpuRender metrics/HUD probe, `INOModGpuCompute` dispatch, thrust/display_detail sanity.

Deploy: `scripts/RDYTU/deploy-gpurender-verify-mod.ps1`  
Parse: `scripts/RDYTU/parse-gpurender-verify-ringlog.ps1`

## Rollback

Set `gpu_render=0`, restore `boot.config` from `.noloader.bak`, redeploy.
