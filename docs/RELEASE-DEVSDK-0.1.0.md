# NOLoader 0.1.0 — DEV.SDK (mod authors)

**Build:** `0.1.0 Build DEV1PM19`  
**Target:** Mod authors and loader developers.

## What's included

- DEV core + `NOLoader.Telemetry.dll`
- `winhttp.dll` proxy + `noloader_config.ini` (dev defaults)
- Sample mod **sources**: RegistrySample, WeaponNames, UniversalLoadout
- `DEV_SDK.txt` — developer channel guide

## Highlights

- F10 overlay / F11 hot-reload
- UDP NO2 telemetry (~30 Hz, port 49000)
- Gate L1–L4 diagnostics in overlay
- Sample mods for registry, Cecil patches, loadout mechanics
- Full solution in GitHub repo: `DEV.SDK/NOLoader.DEV_SDK.sln`

## Install / develop

1. Clone https://github.com/Mursisru/NOLoader
2. Build: `dotnet build DEV.SDK\NOLoader.DEV_SDK.sln -c DEV_SDK`
3. Deploy: `CH\_NOLoader_scripts_\deploy-noloader.ps1 -Configuration DEV_SDK` (game closed)
4. Optional diag mods: `DEV.SDK/diag-mods/` + `deploy-diag-mods.ps1`

## Documentation

- [DEV.SDK guide](https://github.com/Mursisru/NOLoader/blob/main/docs/DEV_SDK.md)
- [Mod author guide](https://github.com/Mursisru/NOLoader/blob/main/docs/MOD_AUTHOR.md)
- [Gates L1–L4](https://github.com/Mursisru/NOLoader/blob/main/docs/GATES.md)
