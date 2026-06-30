# NVG Config (NOLoader)

Night-vision colour presets for Nuclear Option. Port of Domiyaa's BepInEx NVG Config mod to NOLoader.

## Presets

| FilterMode | Description |
|------------|-------------|
| `GreenPhosphor` | Vanilla NVG look (default) |
| `WhitePhosphor` | Desaturated cool white phosphor |
| `Monochrome` | Full desaturation, white filter |
| `FullColor` | Boosted saturation and contrast |
| `AlienTechnology` | Purple sci-fi tint |
| `Custom` | Use `[Custom Filter]` sliders |

## Configuration

Edit `mod_config.ini` in this folder. Changes are picked up automatically while in a mission (file mtime watch).

## Install

1. Build: `dotnet build NOLoader.NVGConfig.csproj -c DEV_SDK` (or RDYTU).
2. Copy this folder to `<Game>/NOLoader/mods/NVGConfig/` (DLL + `mod.json` + optional `mod_config.ini`).
3. Run PatchTool once so `NightVision` IL patches are baked into `Assembly-CSharp.dll`.

Or use `scripts/DEV.SDK/deploy-nvgconfig-mod.ps1`.

## Requirements

- NOLoader with Mission-stage mod support
- No BepInEx or Harmony at runtime
