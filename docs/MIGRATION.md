# Migration from BepInEx to NOLoader

1. Remove `BepInEx\` bootstrap (`winhttp.dll`, `doorstop_config.ini`).
2. Install NOLoader (`scripts/deploy-noloader.ps1`).
3. For each mod:
   - Add `mod.json` with `id`, `assembly`, `entryType`, `loadStage`.
   - Replace `BaseUnityPlugin` with `INOMod` (`OnLoad` / `OnUnload`).
   - Convert Harmony `[HarmonyPatch]` to `mod.json` `patches[]` entries targeting `Type::Method`.
   - Reference `NOLoader.API.dll` instead of `BepInEx.dll` / `0Harmony.dll`.
4. Test on **DEV_SDK** first; publish DLL-only to players on **RDYTU**.

## Load stage guide

| BepInEx pattern | NOLoader stage |
|-----------------|----------------|
| `Awake` + UI | `PreMenu` or `MainMenu` |
| After `MainMenu.WaitForLoaded` | `MainMenu` |
| Combat / camera / physics | `Mission` |

## RealWeaponNames (first migration target)

See `mods/NOLoader.WeaponNames` — Cecil postfix on `Encyclopedia::AfterLoad`, MurmurHash string table.
