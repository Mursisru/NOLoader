# NOLoader 0.1.0 — RDYTU (player)

**Build:** `0.1.0 Build RDY1R6`  
**Target:** Nuclear Option players who want a lightweight mod loader with minimal FPS impact.

## What's included

- Core loader: `winhttp.dll` proxy + `NOLoader/core/*.dll`
- Default `noloader_config.ini` (performance-oriented)
- Empty `NOLoader/mods/` — add your own mods or hash-only packs
- `INSTALL.txt` — step-by-step install

## Highlights

- ~1 FPS overhead vs vanilla (field test, core only, no bundled mods)
- No telemetry, no F10 overlay, no hot-reload
- Telemetry and ring log off by default
- Mission stage via `SceneManager.sceneLoaded` (no per-frame polling)
- Hash-only mod pipeline for player packs (`pack-mod-rdytu.ps1` in CH scripts)

## Install

1. Close Nuclear Option completely.
2. Extract zip into game root (folder containing `Nuclear Option.exe`).
3. Run full deploy from source **or** PatchTool to apply Cecil markers on `Managed/*.dll` (see [INSTALL.md](https://github.com/Mursisru/NOLoader/blob/main/docs/INSTALL.md)).
4. Launch game; check `NOLoader/logs/noloader_ring.log` if needed.

## Documentation

- [README](https://github.com/Mursisru/NOLoader/blob/main/README.md)
- [RDYTU guide](https://github.com/Mursisru/NOLoader/blob/main/docs/RDYTU.md)
- [Architecture](https://github.com/Mursisru/NOLoader/blob/main/docs/ARCHITECTURE.md)

## Operational scripts (not in zip)

PowerShell deploy/build scripts live at `CH\_NOLoader_scripts_\` on the author's machine — see README **Operational scripts**.
