# Triad Ablative Magazine Protection System (TAMPS)

Experimental BattleTech Advanced Universe equipment mod by **Night Sentinels / Wolfbreaker**.

TAMPS protects up to two ammunition bins installed in the same side torso. The first protected ammunition explosion is fully contained. The damaged core then has a 75% chance to remain operational; if it survives, the second protected bin receives one final 50% containment attempt. The unit is destroyed after that second attempt regardless of the result. Left- and right-torso installations operate independently.

## Repository contents

- `Source/` – complete C# source
- `Build-And-Install.ps1` – local .NET Framework build script
- `mod.json` – ModTek manifest and runtime settings
- `upgrades/Gear_TAMPS.json` – equipment definition


## Portable path handling

The game root is resolved dynamically by walking upward from the directory supplied to `TAMPS.Mod.Init` until the enclosing `Mods` directory is found. No drive letter, Steam folder, or storefront-specific installation path is hard-coded.

`AmmoBoxAllowList.json` is disabled by default. When explicitly enabled with `WriteAllowList: true`, diagnostic entries contain repository-independent relative paths such as `Mods/SomeMod/ammobox/file.json`, not a user's absolute Windows path.

## Building

Place the repository folder under `BATTLETECH/Mods/TAMPS`, then run:

```powershell
powershell -ExecutionPolicy Bypass -File .\Build-And-Install.ps1
```

The script resolves the BattleTech root from its own location unless a root path is supplied explicitly. Required game and ModTek assemblies are referenced locally and are not redistributed in this repository.

## Status

Experimental BTAU test build. This source package contains the v1.0.7 path fix and equipment data. Rebuild `TAMPS.dll` locally before distributing a binary claimed to correspond exactly to this source revision.
