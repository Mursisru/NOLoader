**Developer:** Mursisru

# NOLoader

[![Nuclear Option](https://img.shields.io/badge/Game-Nuclear%20Option-blue)](https://store.steampowered.com/app/2168680/Nuclear_Option/)
[![NOLoader](https://img.shields.io/badge/Project-NOLoader%20Engine-purple)](https://github.com/Mursisru/NOLoader)
[![Version](https://img.shields.io/badge/Version-0.1.0-green)](https://github.com/Mursisru/NOLoader/releases/tag/v0.1.0)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow)](https://github.com/Mursisru/NOLoader/blob/main/LICENSE)

---

## Critical warnings

> [!IMPORTANT]
> **Close Nuclear Option** before installing or updating NOLoader. PatchTool edits `NuclearOption_Data\Managed\*.dll` - a running game causes Win32 IO errors.

> [!WARNING]
> **Remove conflicting loaders** - uninstall BepInEx `winhttp.dll` / Doorstop bootstrap before deploying NOLoader. Only one `winhttp.dll` proxy may be active.

> [!CAUTION]
> **Do not mix DEV.SDK and RDYTU runtime in one game session** - pick player (RDYTU) or mod-author (DEV.SDK) channel consistently.

> [!NOTE]
> **Two release zips:** `NOLoader-0.1.0-RDYTU.zip` (players) and `NOLoader-0.1.0-DEV.SDK.zip` (mod authors with F10/F11 overlay).

Standalone mod loader for **Nuclear Option** (Unity Mono). Replaces BepInEx for new mods: lightweight native bootstrap, declarative `mod.json`, Mono.Cecil IL patches, and layered validation gates — without Harmony at runtime.

| Channel | Audience | Version (current) |
|---------|----------|-------------------|
| **RDYTU** | Players, optimized runtime | `0.1.0 Build RDY1R6` |
| **DEV.SDK** | Mod authors, full tooling | `0.1.0 Build DEV1PM19` |

**GitHub release:** [`v0.1.0`](https://github.com/Mursisru/NOLoader/releases/tag/v0.1.0) — two zip assets: `NOLoader-0.1.0-RDYTU.zip` (player) and `NOLoader-0.1.0-DEV.SDK.zip` (mod authors).

## Table of contents

- [Critical warnings](#critical-warnings)
1. [Features](#features)
2. [Architecture overview](#architecture-overview)
3. [RDYTU vs DEV.SDK](#rdytu-vs-devsdk)
4. [Requirements](#requirements)
5. [Installation](#installation)
6. [Game folder layout](#game-folder-layout)
7. [Configuration](#configuration)
8. [Using mods](#using-mods)
9. [Building from source](#building-from-source)
10. [Scripts](#scripts)
11. [Documentation index](#documentation-index)
12. [Troubleshooting](#troubleshooting)
13. [Migration from BepInEx](#migration-from-bepinex)

## Features

- **Native `winhttp.dll` proxy** — hooks Mono init before game assemblies load (no Doorstop dependency).
- **Declarative mods** — each mod is a folder with `mod.json` + DLL; optional Cecil patches with signature hashes (Gate L2).
- **Load stages** — `PreMenu`, `MainMenu`, `Mission` with dependency ordering.
- **Mono.Cecil patching** — Prefix / Postfix / PrefixSkip; offline PatchTool + runtime mod patches.
- **Four validation gates** — manifest (L1), IL signatures (L2), registry SO (L3), mission block (L4).
- **NOModRegistry** — inject custom encyclopedia entries without new ScriptableObject files on disk.
- **Two build channels** — RDYTU (minimal overhead) and DEV.SDK (overlay, hot-reload, telemetry, diagnostics).
- **Hash-only RDYTU packs** — ship mods without plain text ids in player manifests.

---

## Architecture overview

```
Nuclear Option.exe
    └── winhttp.dll (NOLoader.Proxy)
            └── mono_jit_init_version hook
                    └── NOLoader.Core.Bootstrap.Initialize
                            ├── RuntimeConfig (INI)
                            ├── Gate L1 — read mod.json
                            ├── PatchTool markers / Cecil pre-patch
                            ├── MainMenu::Init → StartLoaderMainThread
                            ├── Load mods by stage
                            └── Gate L4 — mission protection
```

**Deep dive:** [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md)

**Game API for patches:** derive types and method signatures from your game install (`NuclearOption_Data/Managed/Assembly-CSharp.dll`) or a decompiler — do not invent signatures.

---

## RDYTU vs DEV.SDK

| | **RDYTU** | **DEV.SDK** |
|---|-----------|-------------|
| **Solution** | `RDYTU/NOLoader.RDYTU.sln` | `DEV.SDK/NOLoader.DEV_SDK.sln` |
| **Build config** | `RDYTU` | `DEV_SDK` |
| **Telemetry** | Not included | UDP NO2 ~30 Hz |
| **F10/F11 overlay** | No | Yes |
| **Hot-reload** | No | Yes |
| **Bundled player mods** | No (core only) | Sample mods on deploy |
| **Physics Rigidbody hooks** | Off by default | On by default |
| **Typical FPS overhead** | Low (core only) | Higher (diagnostics) |
| **Guide** | [docs/RDYTU.md](docs/RDYTU.md) | [docs/DEV_SDK.md](docs/DEV_SDK.md) |

---

## Requirements

**For player:**
- **Nuclear Option** (Steam), Windows x64
**For mod authors:**
- **.NET Framework 4.8** SDK (to build)
- **Visual Studio 2022** with C++ workload (native proxy)
**Note for users:**
- Remove **another mod loader** bootstrap from game root before install (if you has it)

Typical Steam install:

`<SteamLibrary>\steamapps\common\Nuclear Option\`

---

## Installation

> [!IMPORTANT]
> **Close Nuclear Option** before installing or updating NOLoader. PatchTool edits `NuclearOption_Data\Managed\*.dll` — a running game causes Win32 IO errors.

> [!WARNING]
> **Remove conflicting loaders** — uninstall BepInEx `winhttp.dll` / Doorstop bootstrap from the game root before deploying NOLoader. Only one `winhttp.dll` proxy may be active.

### Download GitHub Release (players)

1. Download **NOLoader-0.1.0-RDYTU.zip** from [Release v0.1.0](https://github.com/Mursisru/NOLoader/releases/tag/v0.1.0).
2. **Close the game.**
3. Extract into the game root (folder containing `Nuclear Option.exe`).
4. Apply Cecil patches if not already present (see [docs/INSTALL.md](docs/INSTALL.md)).
5. Launch — check `NOLoader/logs/proxy.log`.

### Option B — Build & deploy

From the repository root (game **closed**):

```powershell
.\scripts\build-proxy.ps1
.\scripts\deploy-noloader.ps1 -Configuration RDYTU
# or
.\scripts\deploy-noloader.ps1 -Configuration DEV_SDK
```

Optional `-GameRoot` if the game is not in the default Steam location.

**Full guide:** [docs/INSTALL.md](docs/INSTALL.md)

---

## Game folder layout

```
Nuclear Option/
  winhttp.dll
  noloader_config.ini
  NOLoader/
    core/          # Managed loader DLLs
    mods/          # One subfolder per mod
    logs/
  NuclearOption_Data/Managed/   # Patched game assemblies (+ .noloader.bak)
```

---

## Configuration

`noloader_config.ini` in game root — template: [deploy/noloader_config.ini](deploy/noloader_config.ini)

RDYTU section controls performance (ring log, physics hooks). DEV.SDK ignores `[RDYTU]`.

---

## Using mods

See [docs/MOD_AUTHOR.md](docs/MOD_AUTHOR.md). Drop folders under `NOLoader/mods/`.

For RDYTU hash-only packs: `.\scripts\pack-mod-rdytu.ps1`

---

## Building from source

```powershell
dotnet build RDYTU\NOLoader.RDYTU.sln -c RDYTU
dotnet build DEV.SDK\NOLoader.DEV_SDK.sln -c DEV_SDK
dotnet test tests\NOLoader.Core.Tests\NOLoader.Core.Tests.csproj -c DEV_SDK
```

---

## Scripts

PowerShell helpers in [`scripts/`](scripts/) — deploy, build proxy, verify, hash baking, release zips. See [scripts/README.md](scripts/README.md).

---

## Documentation index

| Document | Contents |
|----------|----------|
| [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) | Core, bootstrap, Cecil, lifecycle |
| [docs/RDYTU.md](docs/RDYTU.md) | Player channel |
| [docs/DEV_SDK.md](docs/DEV_SDK.md) | Developer channel |
| [docs/MOD_AUTHOR.md](docs/MOD_AUTHOR.md) | mod.json, patches |
| [docs/GATES.md](docs/GATES.md) | L1–L4 |
| [docs/INSTALL.md](docs/INSTALL.md) | Install steps |
| [docs/MIGRATION.md](docs/MIGRATION.md) | BepInEx migration |
| [CHANGELOG.md](CHANGELOG.md) | History |

---

## Troubleshooting

| Symptom | Action |
|---------|--------|
| No loader | Remove BepInEx winhttp; deploy proxy |
| Patch fail | Close game; restore `.noloader.bak`; redeploy |
| Gate L4 block | Fix mod error; check logs |
| Compare FPS vs vanilla | Restore managed DLLs from `.noloader.bak`, remove loader files |

---

## Migration from BepInEx

Greenfield loader — rewrite mods as `mod.json` + Cecil. See [docs/MIGRATION.md](docs/MIGRATION.md).

---

## Project structure

```
native/NOLoader.Proxy/
src/NOLoader.{API,Core,Patcher,PatchTool,Registry,Telemetry}/
DEV.SDK/   RDYTU/   deploy/   docs/   scripts/   tests/
```

Version: [src/NOLoader.Core/AppVersion.cs](src/NOLoader.Core/AppVersion.cs)

---

## Keywords

nuclear-option, noloader, mod-loader, csharp, unity, cecil
