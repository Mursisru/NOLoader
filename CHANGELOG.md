# Changelog

All notable changes to NOLoader. Version strings in code: `AppVersion.cs` (`DEV1PM*` / `RDY1R*`).

## GitHub Releases 0.1.0 (2026-06-09)

### Release assets (tag `v0.1.0`)

- **NOLoader-0.1.0-RDYTU.zip** — player core (optimized, no telemetry, no bundled mods)
- **NOLoader-0.1.0-DEV.SDK.zip** — developer core + telemetry + sample mod sources

### Documentation

- Full README, ARCHITECTURE, RDYTU, DEV_SDK, MOD_AUTHOR, GATES, INSTALL
- Operational scripts moved to `CH\_NOLoader_scripts_\`

### RDYTU `0.1.0 Build RDY1R6`

- Core-only deploy (no bundled player mods)
- ~1 FPS overhead vs vanilla (field test, core only)
- Zero Update polling; mission via `sceneLoaded` when needed
- Telemetry removed from RDYTU build
- Performance INI defaults: ring_log off, physics hooks off
- Hash-only mod pipeline (`pack-mod-rdytu.ps1`)

### DEV.SDK `0.1.0 Build DEV1PM19`

- Overlay F10/F11, hot-reload, UDP telemetry
- Sample mods: RegistrySample, WeaponNames, UniversalLoadout, BrokenMod, FaultMission
- Diag mods in `DEV.SDK/diag-mods/` (manual deploy)

---

## 0.1.0 development history (summary)

- Phase 1: C++ winhttp proxy, Bootstrap, mod.json, INOMod, load stages
- Phase 2: Mono.Cecil patcher, MurmurHash, WeaponNames prototype
- Phase 3: DEV_SDK / RDYTU configs, ring log, Gate L1/L2
- Phase 4: NOModRegistry, Physics Safety Catch, Gate L3
- Phase 5: DEV overlay, Hot-Reload, Gate L4, UDP telemetry (DEV only)
- Phase 6: RDYTU hardening, hash-only manifests, benchmark template, GitHub release

See [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) for technical detail.
