# Player Character Model, Mixamo Animations & Melee Weapons Setup

## Overview
This document describes the **Main Player Character Model, Mixamo Action Animations, Weapon-Specific Melee Combat System (with Attack Spam Buffer), and 1st/3rd-Person Camera System** implemented for **Paramnesia** (DMET 909).

---

## 1. 3D Character Model (`PlayerVisualModel`)
- **Primary Model**: [`Assets/PolygonApocalypse/Prefabs/Characters/SM_Chr_Cool_Female_01.prefab`](../Assets/PolygonApocalypse/Prefabs/Characters/SM_Chr_Cool_Female_01.prefab) (Polygon Apocalypse Pack Female Survivor with humanoid rig [`Assets/PolygonApocalypse/Models/Characters.fbx`](../Assets/PolygonApocalypse/Models/Characters.fbx) and hair attachment `SM_Chr_Attach_Cool_Female_Hair_01`), automatically instantiated as `PlayerVisualModel` inside `PlayerCapsule` with all materials converted to `Universal Render Pipeline/Lit`.
- **Skin Cycling Removed**: Pressing `M` no longer cycles character skins.

---

## 2. Mixamo Humanoid Action Animations (`PlayerCharacter.controller`)
- **Source Folder**: [`Assets/Animations/Player/Source/`](../Assets/Animations/Player/Source/)
  - Automatically imported as `Humanoid` with Root Motion locked (`lockRootRotation`, `lockRootHeightY`, `lockRootPositionXZ`) by [`PlayerCharacterSetup.cs`](../Assets/Scripts/Editor/PlayerCharacterSetup.cs):
  - **Idle**: `Breathing_Idle.fbx`
  - **Walk (`4.0 m/s`)**: `Walking.fbx`
  - **Run (`6.0 m/s`)**: `Running.fbx`
  - **Jump**: `Jumping.fbx`
  - **Crouch Idle & Crouch Walk (`2.2 m/s`)**: `Crouch_Idle.fbx` + `Crouched_Walking.fbx` (blended via `Crouch Walk Blend` tree when `IsCrouching == true`)
  - **Weapon-Specific Melee Attacks (`WeaponType` parameter)**:
    - **`0 = Sword`**: `Sword_And_Shield_Slash.fbx` (`Melee Sword Attack` & `Upper Sword Attack`)
    - **`1 = Spear`**: `Standing_Melee_Attack_Horizontal.fbx` (`Melee Spear Attack` & `Upper Spear Attack`)
    - **`2 = Axe`**: `Standing_Melee_Attack_Downward.fbx` (`Melee Axe Attack` & `Upper Axe Attack`)

---

## 3. Melee Weapons, Placement & Attack Spam Buffer
- **Right-Hand Weapons (`WeaponSocket_TP` on `RightHand`)**:
  - `PlayerCharacterController.ConfigureWeaponSocketForHandBone()` automatically aligns the weapon socket with the hand bone's wrist-to-knuckles and palm axes so the weapon handle sits flush inside the closed right hand in both 3rd-person and 1st-person views:
  1. **Sword (`[1]` key)**: `Assets/BrokenVector/LowPolyDungeon/Prefabs/Clutter/Sword.prefab` (`25` damage, `2.6m` range, diagonal slash)
  2. **Spear (`[2]` key)**: `Assets/BrokenVector/LowPolyDungeon/Prefabs/Clutter/Spear.prefab` (`32` damage, `3.5m` extended reach, horizontal strike)
  3. **Battle Axe (`[3]` key)**: `Assets/Dungon Pack/Prefabs/Axe.prefab` (`45` damage, `2.7m` range, downward cleave)
- **Attack Spam Buffer**:
  - Enforces a `0.35s` recovery cooldown (`attackCooldownBuffer`) after each attack completes before the next swing can start, with a single-input buffer window (`0.22s`) so spamming `LMB` never breaks or machine-guns animations.
- **Removed Systems**:
  - Shield, Block/Parry, Combat Dash, Stamina Bar, and `M` skin cycling have all been removed per the checklist.

---

## 4. Camera Pan/Tilt Limits & 1st/3rd-Person Positioning
- **Tilt (Pitch) Clamping**:
  - **1st-Person View**: Clamped to `[-65°, +70°]`, eye height `1.50m` (`0.90m` when crouching).
  - **3rd-Person View**: Clamped to `[-35°, +60°]`, camera distance `2.8m`, shoulder offset `(0.40, 0.32, 0.0)`.
- **Pan & Tilt Speed Limits**:
  - [`FirstPersonController.cs`](../Assets/Prefabs/Starter%20Assets/Runtime/FirstPersonController/Scripts/FirstPersonController.cs) clamps per-frame horizontal pan (`MaxPanDeltaPerFrame = 14°`) and vertical tilt (`MaxTiltDeltaPerFrame = 10°`) to prevent sudden camera snaps.

---

## 5. Player Controls Summary
| Input | Action |
| :--- | :--- |
| **`W / A / S / D`** | Walk (`4.0 m/s` — Mixamo `Walking.fbx`) |
| **`Left Shift`** | Run (`6.0 m/s` — Mixamo `Running.fbx`) |
| **`Space`** | Jump (Mixamo `Jumping.fbx`) |
| **`C` or `Left Ctrl`** | Toggle Crouch (`2.2 m/s` — Mixamo `Crouch_Idle.fbx` & `Crouched_Walking.fbx`) |
| **`Left Mouse Button` or `F`** | Attack (Weapon-specific animation + `0.35s` anti-spam buffer) |
| **`1 / 2 / 3` or `Scroll Wheel`** | Switch Weapon (`1` = Sword, `2` = Spear, `3` = Axe) |
| **`V`** | Toggle Camera View (**1st-Person** <-> **3rd-Person**) |
