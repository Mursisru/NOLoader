# NOLoader 0.1.0

First public release for **Nuclear Option**. Two install bundles in one release — pick the channel that fits your role.

## Download assets

| File | Channel | Build | For |
|------|---------|-------|-----|
| **NOLoader-0.1.0-RDYTU.zip** | RDYTU (player) | `0.1.0 Build RDY1R6` | Players — minimal overhead, core only |
| **NOLoader-0.1.0-DEV.SDK.zip** | DEV.SDK | `0.1.0 Build DEV1PM19` | Mod authors — telemetry, overlay, sample mods |

Full source and documentation: this repository.

---

## RDYTU — player install

**Target:** players who want a lightweight mod loader with minimal FPS impact.

**Included:** `winhttp.dll` proxy, `NOLoader/core/*.dll`, performance-oriented `noloader_config.ini`, empty `NOLoader/mods/`, `INSTALL.txt`.

**Highlights:**
- Core-only bundle (add your own mods)
- No telemetry, no F10 overlay, no hot-reload
- Ring log and physics hooks off by default
- Hash-only mod pipeline for player packs

**Install:** close the game → extract zip into game root → apply Cecil patches (see [INSTALL.md](docs/INSTALL.md)) → launch.

**Guide:** [docs/RDYTU.md](docs/RDYTU.md)

---

## DEV.SDK — mod author bundle

**Target:** mod authors and loader developers.

**Included:** DEV core + `NOLoader.Telemetry.dll`, proxy, dev `noloader_config.ini`, sample mod **sources** (RegistrySample, WeaponNames, UniversalLoadout), `DEV_SDK.txt`.

**Highlights:**
- F10 overlay / F11 hot-reload
- UDP NO2 telemetry (~30 Hz, port 49000)
- Gate L1–L4 diagnostics
- Full solution: `DEV.SDK/NOLoader.DEV_SDK.sln`

**Guide:** [docs/DEV_SDK.md](docs/DEV_SDK.md) · [docs/MOD_AUTHOR.md](docs/MOD_AUTHOR.md)

---

## Documentation

- [README.md](README.md) — overview, architecture, configuration
- [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) — bootstrap, Cecil, lifecycle
- [docs/GATES.md](docs/GATES.md) — validation gates L1–L4
- [CHANGELOG.md](CHANGELOG.md) — full change history
