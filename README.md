# NOLoader

Standalone mod loader for **Nuclear Option** (Unity Mono). Replaces BepInEx for new mods: lightweight native bootstrap, declarative `mod.json`, Mono.Cecil IL patches, and layered validation gates — without Harmony at runtime.

| Channel | Audience | Version (current) |
|---------|----------|-------------------|
| **RDYTU** | Players, optimized runtime | `0.1.0 Build RDY1R6` |
| **DEV.SDK** | Mod authors, full tooling | `0.1.0 Build DEV1PM19` |

**GitHub source mirror (pre-release):** `0.1.0 Build PR-R2PM1` — synced from Engine after release `0.1.0`; active Engine dev: `0.1.0 Build DEV2PM1`.

**Semver (GitHub Releases):** `0.1.0` — tag [`v0.1.0`](https://github.com/Mursisru/NOLoader/releases/tag/v0.1.0) includes **two zip assets:** `NOLoader-0.1.0-RDYTU.zip` (player) and `NOLoader-0.1.0-DEV.SDK.zip` (mod authors).

---

## Table of contents

1. [Features](#features)
2. [Architecture overview](#architecture-overview)
3. [RDYTU vs DEV.SDK](#rdytu-vs-devsdk)
4. [Requirements](#requirements)
5. [Installation](#installation)
6. [Game folder layout](#game-folder-layout)
7. [Configuration](#configuration)
8. [Using mods](#using-mods)
9. [Building from source](#building-from-source)
10. [Operational scripts](#operational-scripts)
11. [Documentation index](#documentation-index)
12. [Troubleshooting](#troubleshooting)
13. [Migration from BepInEx](#migration-from-bepinex)

---

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

**Game API for patches:** use only types from your local API dump (`CH\_Nuclear_Option_\Assembly-CSharp\`) — do not invent signatures.

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
| **Typical FPS overhead** | ~1 FPS (core, field test) | Higher (diagnostics) |
| **Guide** | [docs/RDYTU.md](docs/RDYTU.md) | [docs/DEV_SDK.md](docs/DEV_SDK.md) |

---

## Requirements

- **Nuclear Option** (Steam), Windows x64
- **.NET Framework 4.8** SDK (to build)
- **Visual Studio 2022** with C++ workload (native proxy)
- Remove **BepInEx** bootstrap from game root before install

Default game path: `C:\Program Files (x86)\Steam\steamapps\common\Nuclear Option\`

---

## Installation

### Option A — GitHub Release (players)

1. Download **NOLoader-0.1.0-RDYTU.zip** from [Release v0.1.0](https://github.com/Mursisru/NOLoader/releases/tag/v0.1.0) (player asset in the same release as DEV.SDK).
2. **Close the game.**
3. Extract into the game root.
4. Launch — check `NOLoader/logs/proxy.log`.

### Option B — Build & deploy

```powershell
# RDYTU
& "C:\Users\at747\Desktop\CH\_NOLoader_scripts_\RDYTU\deploy-noloader.ps1"

# DEV.SDK
& "C:\Users\at747\Desktop\CH\_NOLoader_scripts_\deploy-noloader.ps1" -Configuration DEV_SDK
```

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

See [docs/MOD_AUTHOR.md](docs/MOD_AUTHOR.md). Drop folders under `NOLoader/mods/`. RDYTU packs: `pack-mod-rdytu.ps1` in CH scripts.

---

## Building from source

```powershell
dotnet build RDYTU\NOLoader.RDYTU.sln -c RDYTU
dotnet build DEV.SDK\NOLoader.DEV_SDK.sln -c DEV_SDK
dotnet test tests\NOLoader.Core.Tests\NOLoader.Core.Tests.csproj -c DEV_SDK
```

---

## Operational scripts

Canonical location: **`C:\Users\at747\Desktop\CH\_NOLoader_scripts_\`**

Not stored in git — see [scripts/README.txt](scripts/README.txt).

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
| FPS test | `RDYTU/uninstall-for-fps-test.ps1` in CH scripts |

---

## Migration from BepInEx

Greenfield loader — rewrite mods as `mod.json` + Cecil. See [docs/MIGRATION.md](docs/MIGRATION.md).

---

## Project structure

```
native/NOLoader.Proxy/
src/NOLoader.{API,Core,Patcher,PatchTool,Registry,Telemetry}/
DEV.SDK/   RDYTU/   deploy/   docs/   tests/
```

Version: [src/NOLoader.Core/AppVersion.cs](src/NOLoader.Core/AppVersion.cs)
