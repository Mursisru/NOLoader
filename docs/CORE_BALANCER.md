# CoreBalancer (NOLoader RDYTU)

Multi-core orchestration for **mod compute** — not a Unity/PhysX thread rewriter.

## What it does

- **NOMulticoreScheduler** — dedicated worker threads (not `ThreadPool`) for opt-in mod math.
- **Double-buffer** — immutable `NOWorldUnit[]` snapshot for worker reads (`NOModRuntime.StableWorld`).
- **Topology log** — logical/physical/hybrid P-E detection at bootstrap (`[CoreBalancer] topology …`).
- **Main-thread metrics** — avg/p95 mod tick cost logged every 30s.

## What it does NOT do

- Split Unity main thread.
- Pin Render/PhysX/process affinity (production).
- Auto-offload existing mods without `INOModBackgroundWork`.

## INI (`noloader_config.ini`)

| Key | Default | Meaning |
|-----|---------|---------|
| `core_balancer` | `0` | Master switch |
| `mod_worker_count` | `0` | Workers (`0` = auto `logical/4`, max 8) |
| `mod_affinity_mask` | `auto` | Worker CPU mask |
| `main_thread_affinity` | `0` | `0` / `1` / `auto` / hex mask — **experimental** |
| `double_buffer` | `1` | Publish immutable world snapshots |
| `mod_compute_budget_ms` | `2.0` | Reserved for L4 budget integration |

## Mod API

```csharp
// Simple path
NOModRuntime.Scheduler.RunCompute(
    () => { /* pure math, no Unity API */ },
    () => { /* apply on main thread */ });

// Full pipeline (1-frame latency)
public class MyMod : INOModBackgroundWork {
    void OnCaptureInputs(ref NOModContext ctx, ref ModWorkInput input) { }
    void OnCompute(in ModWorkInput input, ref ModWorkOutput output) { }
    void OnApplyResults(ref NOModContext ctx, in ModWorkOutput output) { }
}
```

**Worker threads:** no `UnityEngine`, no game reflection, no `Destroy()`.

## Sample mod

`DEV.SDK/mods/NOLoader.ComputeSample/` — enable with `core_balancer=1`.

## Rollback

Set `core_balancer=0` and redeploy — no Cecil changes required.
