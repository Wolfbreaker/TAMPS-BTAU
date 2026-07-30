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



**What the Triad Ablative Magazine Protection System (TAMPS) Is**

A TAMPS unit is installed in either the **left torso** or the **right torso** and protects up to **two ammunition bins located in that same side torso**.

A separate TAMPS unit may be installed in the opposite side torso. When two units are installed, the left and right systems operate independently.

Each TAMPS installation forms a three-part system—the “Triad”—consisting of two protected ammunition bins and one shared ablative containment core.


               ONE TAMPS UNIT
               LEFT OR RIGHT TORSO
             SHARED ABLATIVE CORE
                 │
        ┌────────┴────────┐
        │                 │
   AMMO BIN A    		    	AMMO BIN B
    Protected         			   Protected

  2 protected ammo bins
        	    +
  1 shared ablative core
            =
      TRIAD-SYSTEM


With two TAMPS units installed:


LEFT TORSO TAMPS             RIGHT TORSO TAMPS

  Shared Core               Shared Core
      /   \                     /   \
  Ammo A   Ammo B          Ammo C   Ammo D

 Independent system     Independent system
```

## Technical implementation note

I was unable to create a completely new custom ammunition-pocket system without significantly altering the original gameplay concept of the Battletech, so the current implementation uses **Dynamic Slots** to emulate the two protected magazine pockets.

In other words, the pockets are simulated by assigning up to two ammunition bins in the same side torso to the installed TAMPS unit, rather than by creating a separate physical ammunition-container type.

This is one of the main areas I would like tested, particularly whether the Dynamic Slot assignments display correctly, remain stable after saving and loading, and consistently protect the intended ammunition bins during combat.

## How it works

The first explosion from either protected ammunition bin is fully suppressed by the shared ablative core. The ammunition is lost, but the explosion causes no damage to the internal structure, weapons, equipment, or vulnerable engine components in that torso.

After the first successful containment, the core has a **75% chance** to remain operational.

FIRST PROTECTED AMMO EXPLOSION
               │
               ▼
     EXPLOSION FULLY SUPPRESSED
               │
    No internal structure,
   weapon, equipment, or
 engine damage
       │
┌──────┴──────┐
│            	│
75% chance    25% chance
core survives core is exhausted
```

If the core survives, the second protected ammunition bin remains covered. Should that bin also detonate, the damaged core has a **50% chance** to suppress the second explosion.

After this second attempt, the TAMPS unit is always exhausted, regardless of whether the suppression succeeds.

SECOND PROTECTED AMMO EXPLOSION
               │
        ┌──────┴──────┐
        │             │
   50% chance      50% chance
   explosion       suppression
   suppressed        fails
        │             |
        └──────┬──────┘
    		       ▼
       	  TAMPS EXHAUSTED
```

TAMPS only protects the two ammunition bins assigned to its own side torso. It does not protect ammunition stored in the opposite torso, center torso, arms, or legs.

Its limited coverage, critical-slot requirement, finite endurance, and chance-based second layer of protection are intended to keep the system useful without making CASE, CASE II, or SHIELD obsolete.
