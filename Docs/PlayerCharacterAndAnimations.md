# Player Character Model, Animations & Melee Weapons Setup

## Overview
This document describes the **Main Player Character Model, 8-Direction Motion-Captured Animations, Melee Combat & Shield Block/Parry System, Combat Dash, Stealth Backstab, and Enemy Combat AI** implemented for **Paramnesia** (DMET 909).

---

## 1. 3D Character Model & Modular Armor (`PlayerVisualModel`)
- **Asset Package**: **URP GanzSe Free Modular Character Pack** (`Assets/URP GanzSe Free Modular Character Pack/Prefabs/Modular Character/GanzSe Free Modular Character Update 1_1.prefab`)
- **Rig**: Humanoid Avatar (`GanzSe Free Modular Character 1_1Avatar`), retargeting-ready with full finger, spine, and limb hierarchy.
- **Modular Armor & Face Customization**:
  - Uses [`ModularHeroController`](../Assets/URP%20GanzSe%20Free%20Modular%20Character%20Pack/Demo/Scripts/ModularHeroController.cs) on `PlayerVisualModel` to activate 1 clean armor piece per category (`ARMOR PARTS`) and 1 facial feature per category (`FACE DETAILS PARTS`).
  - Press **`M`** at runtime (or click **Randomize Armor Parts** / **Randomize Face Parts** in the Unity Inspector) to cycle through modular medieval armor sets and facial features.

---

## 2. 8-Direction Motion-Captured Humanoid Animations (`PlayerCharacter.controller`)
- **Asset Packages**:
  - **Kevin Iglesias — Human Basic Motions FREE** (`Assets/Kevin Iglesias/Human Animations/`)
  - **Kevin Iglesias — Human Melee Animations FREE** (`Assets/Kevin Iglesias/Human Animations/`)
- **Animator Controller**: [`Assets/Animations/Player/PlayerCharacter.controller`](../Assets/Animations/Player/PlayerCharacter.controller)
  - **Layer 0 — Base Layer (8-Way 2D Directional Locomotion, Jump, Crouch, Shield Parry & Combat)**:
    - `Idle Walk Run Blend` (**2D Freeform Directional BlendTree** driven by `MoveX` and `MoveZ`):
      - `(0, 0)`: `HumanM@CombatIdle1H01.fbx`
      - **8-Direction Walk Ring (`2.0 m/s`)**: `Walk01_Forward`, `Walk01_Backward`, `Walk01_Left`, `Walk01_Right`, `Walk01_ForwardLeft`, `Walk01_ForwardRight`, `Walk01_BackwardLeft`, `Walk01_BackwardRight`
      - **8-Direction Run Ring (`4.0 m/s`)**: `Run01_Forward`, `Run01_Backward`, `Run01_Left`, `Run01_Right`, `Run01_ForwardLeft`, `Run01_ForwardRight`, `Run01_BackwardLeft`, `Run01_BackwardRight`
      - **Sprint (`(0, 6.5)`)**: `HumanM@Sprint01_Forward.fbx`
    - `JumpStart`: `HumanM@Jump01 - Begin.fbx`
    - `InAir`: `HumanM@Fall01.fbx`
    - `JumpLand`: `HumanM@Jump01 - Land.fbx`
    - `Crouch Idle`: `Player_Crouch_Idle.anim` + procedural stealth knee/spine crouch pose
    - `Melee Slash 1` (`ComboStep == 1`, `WeaponType != 1`): `HumanM@Attack1H01_R.fbx`
    - `Melee Slash 2` (`ComboStep == 2`): `HumanM@Attack1H01_L.fbx`
    - `Melee Heavy 3` (`ComboStep == 3`): `HumanM@Attack2H01.fbx`
    - `Melee Spear Thrust` (`ComboStep == 1`, `WeaponType == 1`): `HumanM@AttackPolearm01.fbx`
    - `Shield Parry` (`Parry` Trigger): `HumanM@AttackShield01.fbx`
    - `Combat Hit` (`Hit` Trigger): `HumanM@CombatDamage01.fbx`
    - `Combat Death` (`Death` Trigger): `HumanM@Death01.fbx`
  - **Layer 1 — UpperBodyCombat Layer (`Human Body Upper Mask.mask`)**:
    - Uses `Assets/Kevin Iglesias/Human Animations/Models/Avatar Masks/Human Body Upper Mask.mask` so the player can swing their Sword, Spear, or Battle Axe or perform a Shield Parry while walking, strafing, or sprinting without interrupting leg locomotion.

---

## 3. Melee Weapons, Swing Trails, Hit-Stop & Off-Hand Shield
- **Right-Hand Weapons (`WeaponSocket_TP` on `RightHand`)**:
  1. **Sword (`[1]` key)**: `Assets/BrokenVector/LowPolyDungeon/Prefabs/Clutter/Sword.prefab` (Fast slashes, 25–50 combo damage)
  2. **Spear (`[2]` key)**: `Assets/BrokenVector/LowPolyDungeon/Prefabs/Clutter/Spear.prefab` (+35% extended reach, polearm thrust animation)
  3. **Battle Axe (`[3]` key)**: `Assets/Dungon Pack/Prefabs/Axe.prefab` (+35% heavy cleave damage)
  - Each weapon includes a glowing **`BladeTrail` (`TrailRenderer`)** at its tip that activates during melee swings, spawns impact sparks (`MeleeHitSparkBurst`), and triggers **Hit-Stop (`32ms–55ms` impact freeze-frame)** + **Camera Impulse Shake** on heavy blows.
- **Off-Hand Shield (`ShieldSocket_TP` on `LeftHand`)**:
  - Uses `Assets/BrokenVector/LowPolyDungeon/Prefabs/Clutter/Shield.prefab` strapped to the character's left forearm (`hand_l`).
  - Toggle visibility with **`G`**, or hold **Right Mouse Button (`RMB`) / `Q`** to raise the shield & weapon into a defensive **Block / Parry** stance:
    - **Standard Block (Hold `RMB`)**: Blocks **80% of incoming enemy damage**.
    - **Timed Perfect Parry (Press `RMB` within `0.35s` of an enemy strike)**: Blocks **100% of damage**, restores `20` Stamina, and **staggers the enemy for `1.8s`** (`1.75x` Riposte bonus damage on your next hit).

---

## 4. Combat Dodge / Sidestep Dash, Stealth Backstab & Enemy Combat (`EnemyCombatTarget.cs`)
- **Combat Dodge / Sidestep Dash (`Left Alt` or `E`)**:
  - Performs a quick `11.5 m/s` directional dodge or backstep (`25` Stamina cost) with **invulnerability frames (`0.24s`)** and subtle camera tilt.
- **Stealth Backstab Finisher**:
  - Sneaking behind an undetected enemy shows **`>> STEALTH BACKSTAB READY! <<`** and deals **`3x` Critical Backstab Damage** (instant takedown).
- **Enemy Pursuit, Counter-Attack, Health Bar & Death (`EnemyCombatTarget.cs`)**:
  - Detected enemies pursue the player, telegraph melee strikes (`[!] STRIKING!`), flash crimson when hit, display a floating health bar, stagger when parried, and play `HumanM@Death01.fbx` on defeat.

---

## 5. Controls Summary
| Input | Action |
| :--- | :--- |
| **`W / A / S / D`** | 8-Direction Walk / Strafe / Backpedal (`4.0 m/s`) |
| **`Left Shift`** | Sprint (`6.5 m/s`) |
| **`Left Alt` or `E`** | Combat Dodge / Sidestep Dash (Invulnerability Frames) |
| **`Space`** | Jump |
| **`C` or `Left Ctrl`** | Toggle Stealth Crouch (`1.2m` collider height + lowers `EnemyDetection.targetOffset`) |
| **`Left Mouse Button` or `F`** | 3-Hit Melee Attack Combo / Stealth Backstab Finisher |
| **`Right Mouse Button` or `Q`** | Hold to Shield Block (80% reduction) / Timed Press for Perfect Parry (`0` DMG + Stagger) |
| **`G`** | Toggle Off-Hand Shield On / Off |
| **`1 / 2 / 3` or `Scroll Wheel`** | Switch Weapon (`1` = Sword, `2` = Spear, `3` = Axe) |
| **`V`** | Toggle Camera View (**1st-Person** <-> **3rd-Person**) |
| **`M`** | Randomize Modular Character Armor & Face |
