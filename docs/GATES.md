# Validation gates (L1–L4)

NOLoader validates mods and patches in layers. Failures at L1/L2 prevent bad manifests or IL from breaking the game. L3 protects registry data. L4 protects mission start after a mod fault.

---

## Gate L1 — Manifest

**When:** bootstrap, before any mod DLL load.

**Checks:**

- `mod.json` parseable JSON
- Required fields: assembly, entryType, loadStage
- `id` or `idHash` present (RDYTU hash-only uses `idHash` only)
- Duplicate mod ids / hashes
- Dependency graph acyclic (DFS)

**On failure:** mod marked invalid; errors in ring log (if enabled) and `GateReportStore` (DEV overlay).

---

## Gate L2 — IL patch signatures

**When:** PatchTool (offline) or Bootstrap / `ModPatchScheduler` (runtime mod patches).

**Rules:**

- Every mod patch must include `expectedSignatureHash` (16 hex chars).
- Hash = first 16 hex of SHA256 over canonical method signature string.
- On mismatch: **rollback** entire patch batch for that assembly; **no partial write** to live DLL (DEV1P6+).

**Tools:**

```powershell
.\scripts\bake-mod-patch-hashes.ps1
.\scripts\bake-core-patch-hashes.ps1
.\scripts\verify-core-patch-hashes.ps1
```

**BrokenMod** (DEV.SDK only): intentional bad patch for L2 testing — not deployed by default.

---

## Gate L3 — ScriptableObject / registry

**When:** mod registers encyclopedia entries via `NOModRegistry`.

**Checks:**

- Entry types are structs (mod author contract)
- Asset references valid
- Physics-related fields sane before inject

Inject runs in `RegistryGameBridge.OnEncyclopediaAfterLoad` (once per encyclopedia load).

---

## Gate L4 — Mission block

**When:** after a loaded mod throws (load failure or runtime exception attributed to mod assembly).

**Behavior:**

- `ModLifecycleManager.FlagModForMissionBlock`
- PrefixSkip on `MapLoader.CanLoad` and `SceneManager.LoadSceneAsync` returns block when flag set
- DEV: fullscreen red banner with stack trace
- RDYTU: log only (if ring log / exception subscription enabled)

**RDYTU default:** `exception_tracking_subscribe=0` — no `Application.logMessageReceived` hook unless enabled in INI.

**FaultMission** mod (DEV.SDK): intentional throw for L4 testing.

---

## Field verification (DEV.SDK)

Diagnostic mods (`LoaderDiagMenu`, `LoaderDiag`) run structured probes and show **DONE** overlay. Not included in RDYTU deploy.

```powershell
.\scripts\verify-noloader-logs.ps1
```

Expected cold start: menu **DONE** → battle **DONE**; ring log contains `Core started`, `MainMenu hook fired`.
