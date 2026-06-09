# Install NOLoader

Complete installation guide for **Nuclear Option** on Windows x64.

For channel-specific usage after install, see:

- Players: [RDYTU.md](RDYTU.md)
- Mod authors: [DEV_SDK.md](DEV_SDK.md)

---

## Prerequisites

1. **Nuclear Option** installed (Steam).
2. **.NET Framework 4.8 SDK** — to build from source.
3. **Visual Studio 2022** with **Desktop development with C++** — to build `NOLoader.Proxy` (winhttp.dll).
4. **Remove BepInEx bootstrap** from game root if present:
   - Delete or rename BepInEx `winhttp.dll`
   - Remove `doorstop_config.ini` (BepInEx)
   - NOLoader uses its own proxy and `noloader_config.ini`

Typical game folder:

`<SteamLibrary>\steamapps\common\Nuclear Option\`

---

## Option A — GitHub Release (recommended for players)

1. Download **NOLoader-0.1.0-RDYTU.zip** from [GitHub Release v0.1.0](https://github.com/Mursisru/NOLoader/releases/tag/v0.1.0).
2. **Exit Nuclear Option completely.**
3. Extract archive contents into the game root folder.
4. If this is a fresh install, run PatchTool once to apply Cecil markers (see Option B step 4, or use deploy script from a clone).
5. Launch the game.
6. Verify `NOLoader/logs/proxy.log` contains bootstrap lines.

No build tools required for the zip contents themselves.

---

## Option B — Build from source

### 1. Clone

```powershell
git clone https://github.com/Mursisru/NOLoader.git
cd NOLoader
```

### 2. Build native proxy

```powershell
.\scripts\build-proxy.ps1
```

Or manually:

```powershell
MSBuild native\NOLoader.Proxy\NOLoader.Proxy.vcxproj /p:Configuration=Release /p:Platform=x64
```

Output: `artifacts/proxy/winhttp.dll`

### 3. Build managed core

**RDYTU (players):**

```powershell
dotnet build RDYTU\NOLoader.RDYTU.sln -c RDYTU
```

**DEV.SDK (developers):**

```powershell
dotnet build DEV.SDK\NOLoader.DEV_SDK.sln -c DEV_SDK
```

### 4. Deploy (game must be closed)

**RDYTU:**

```powershell
.\scripts\deploy-noloader.ps1 -Configuration RDYTU
```

**DEV.SDK:**

```powershell
.\scripts\deploy-noloader.ps1 -Configuration DEV_SDK
```

If the game is not in the default Steam path:

```powershell
.\scripts\deploy-noloader.ps1 -Configuration RDYTU -GameRoot "D:\Games\Nuclear Option"
```

Deploy copies DLLs, proxy, INI, and runs **PatchTool** (Cecil) when the game is not running.

### 5. Verify

```powershell
.\scripts\verify-rdytu.ps1
# or
.\scripts\verify-dev-sdk.ps1
```

---

## What gets patched on disk

PatchTool modifies (with backups `*.noloader.bak`):

| File | Purpose |
|------|---------|
| `Assembly-CSharp.dll` | MainMenu hook, encyclopedia bridge, Gate L4 CanLoad, optional motor hook |
| `UnityEngine.CoreModule.dll` | Scene load Gate L4 |
| `UnityEngine.PhysicsModule.dll` | Optional Rigidbody hooks (DEV); restored in RDYTU |

**Always close the game before deploy/patch.**

---

## Uninstall

1. Restore vanilla managed DLLs from backups in `NuclearOption_Data/Managed/` (rename `*.noloader.bak` back over patched files, or redeploy from a clean game backup).
2. Remove `winhttp.dll` (NOLoader proxy), `noloader_config.ini`, and the `NOLoader/` folder if desired.
3. Reinstall BepInEx separately if needed.

---

## Next steps

- Add mods: [MOD_AUTHOR.md](MOD_AUTHOR.md)
- Understand gates: [GATES.md](GATES.md)
- Architecture: [ARCHITECTURE.md](ARCHITECTURE.md)
