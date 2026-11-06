# Zombie AI

The four zombies in `Assets/Scenes/Level 1 Base.unity` are enabled for testing:
two melee zombies in the middle, the ranged hazmat in the south-west corner and the
Brute on the east side. To hide them from gameplay again, select them in the Hierarchy, clear the checkbox beside
their name at the top of the Inspector, and save the scene. The Hierarchy eye
only hides objects in the Scene view; it does not disable them during play.

## Behavior and tuning

Each enemy has **Zombie AI**, **NavMesh Agent**, **Enemy Detection**, and
**Enemy Combat Target**. Existing combat, health, stagger and death are preserved.
Zombie AI owns movement; combat no longer directly moves these enemies through walls.

- Roam Center: optional Transform; empty means the initial spawn position.
- Roam Radius: 4 m by default, with reachable destinations inside that sphere.
- Gait speeds come from the clips: walk approximately 0.33 m/s, run 3.04 m/s.
- Idle Pause: 2 seconds on average between roaming destinations (each pause 1-3 s).
- Reaction Time: on spotting the player a zombie takes a 0.35-0.7 s beat, its head
  snapping round first and the body turning after, before it charges. The Brute
  instead roars (Notice State) when the player is more than 4 m away, at most every 20 s.
- Lost Sight Wait: 3 seconds. Losing the player, a zombie jogs to where they were heading
  (last sighting plus their direction of travel), looks around for this long, then roams again.
- Being hit, even unseen, turns a zombie on the attacker: it hunts their position and
  charges as soon as it sees them. No roar mid-fight.
- Heads track whatever has their attention (look-at IK): the player while noticing and
  chasing, the route ahead while roaming, random glances while idle or searching.
- Root motion keeps driving through stops and into attacks, so zombies decelerate out of a
  run instead of freezing; they also ease off speed into sharp turns (200 deg/s, Brute 120).
- Seeing the player interrupts roaming/searching immediately. The yellow ! pops
  larger for 0.4 seconds on detection, follows the humanoid head, and hides on sight loss.
- Vision checks run at 10 Hz, use a reusable raycast buffer, and ignore triggers,
  self and target colliders. A full buffer conservatively blocks sight.
- Proximity Range on Enemy Detection defaults to 1.25 m from eyes to target.
  Inside it, players in the front hemisphere bypass the vision angle, including
  crouched players below the normal cone. Walls and maximum view range still
  apply. Targets behind or directly beside the zombie get no proximity detection.
  Set it to zero to disable. Vision gizmos show its forward semicircle.
- Chase paths refresh at most five times per second. Roam paths must be complete.

Select an enemy to see its cyan roaming radius. The home area restricts roaming;
pursuit can leave it. Returning enemies choose destinations back inside it.

## Animation assets

Only five standalone humanoid `.anim` files are retained from Scary Zombie Pack:
Zombie Idle, Zombie Walk, Zombie Run, Zombie Attack, and Zombie Death.
The imported FBX folder was removed after extraction and validation. No animation
source skeletons or duplicate character models are instantiated. The original
PolygonZombies model skeleton and valid ZombiesAvatar remain necessary for skinning.

The shared ZombieIdle controller contains a 2D Cartesian locomotion blend tree
using local velocity, with damped parameters and idle/walk/run samples in eight
directions. The pack supplies only forward gaits: the NavMeshAgent turns the
character toward movement. This smooths steering and speed changes; it does not
create authored sideways or backward stepping. Add directional clips if enemies
later need to strafe while facing the player. Animation root motion drives translation once through ZombieRootMotion on the model Animator. The NavMeshAgent has Update Position off: it steers and plans paths, while animation displacement is constrained to the NavMesh. Foot IK is on. Fixed chase speed and procedural leg/attack overlays are disabled for these enemies.
Existing hit reactions are kept; attack and death use the zombie pack clips.

## Enemy types and drops

Scripts live in `Assets/Scripts/Enemies`. Each enemy drops its collectible on death
(Enemy Combat Target > Drop Prefab); walking over it adds it to PlayerInventory.

| Enemy | Prefab | Attack | Drop |
|---|---|---|---|
| Melee zombie | every other `Enemy_Zombie_*` | zombie claw | Bone |
| Toxic waste (ranged) | `Enemy_Zombie_BioHazardSuit_Male_01` | throws bones from 7 m (zombie attack swing) | Slime flask |
| Tank (Brute) | `Enemy_Zombie_Brute_01` | punch or swipe, 30 dmg, 300 HP | Heart |

Thrown bones (`Projectile_Bone`, EnemyProjectile) ride in the hazmat's right hand through
the wind-up, leave it on the swing's release frame, tumble end over end on a lob trailing a
toxic-green streak, and land as a normal enemy strike: dash dodges, blocking reduces, a
perfect parry staggers the thrower. An interrupted wind-up discards the bone in hand.

Hits are timed to the animation: Enemy Combat Target > Attack Windups holds the seconds
from each attack's start to its strike frame, measured from the clips' hand paths
(constants at the top of `EnemyTypesSetup`). Zombie Attack plays at 1.6x from 0.2 s in
(claw lands at 0.5 s, bone leaves the hand at 0.47 s). Brute Punch skips the jab of the
one-two and lands the heavy second punch at 0.46 s; Brute Swipe skips its stand-still lead-in
and plays at 0.9x so the step back and ~1 m lunge read clearly, landing at 1.14 s.

The Brute runs at 0.7x pace (`BruteRunPace`), so it chases at about 2.4 m/s: slower than
regular zombies (3.0 m/s) and the player's walk (4.0 m/s), a lumbering stride rather than a sprint. The Brute uses the PolygonBossZombies Brute model with humanoid clips extracted
from `Animations/Zombies/Creature Pack` into `Animations/Zombies/Brute` (Brute.controller).
Types are applied by name in `EnemyTypesSetup.ApplyType`, so regenerating prefabs keeps them.

Drops glow with a real point light (warm ivory, toxic green, blood red) that pulses, a
soft halo, and their own particles: drifting bone dust; rising bubbles and drips from the
flask; embers and a lub-dub heartbeat swell on the heart. Picking one up throws a burst
of sparks and a flash. Tools > Paramnesia > Set Up Drop Effects rebuilds them
(`DropEffectsSetup`, runtime `DropGlow`); the old sunray billboards are switched off.

Dead enemies ragdoll (Enemy Combat Target > Ragdoll On Death; untick for the death
animation). The ragdoll is built from the humanoid skeleton only at death, thrown along
the killing blow, and frozen with its colliders off once it settles (1.5-6 s), so living
enemies cost nothing extra and corpses never snag the player. Later corpses can overlap
settled ones.

- Tools > Paramnesia > Set Up Enemy Types: extract Brute clips, build Brute.controller,
  Projectile_Bone and Enemy_Zombie_Brute_01, then assign drops and types to every enemy prefab.
- Tools > Paramnesia > Check Enemy Types: verify drops, the ranged projectile and Brute attacks.

## Navigation and tools

Zombie Navigation contains a baked humanoid NavMeshSurface using physics colliders
inside a 60 x 12 x 60 m volume centered at (0, 2, 0). No obstacles or links were needed
for the current test area. Re-bake its surface after moving level collision geometry.
The NavMeshAgents use a 0.3 m avoidance radius, 1.8 m height, kinematic Rigidbodies,
and the default humanoid agent type.

- Tools > Paramnesia > Set Up Zombie Roaming AI: update all 50 generated enemy
  prefabs and current scene enemies, bake navigation, enable placed enemies and save.
  Reuses extracted clips when the source folder is absent.
- Tools > Paramnesia > Check Zombie AI Setup: validate humanoid avatars, navigation
  coverage at placed enemies, and locomotion tree setup.
- Tools > Paramnesia > Check Zombie AI In Play Mode: temporary target verifies
  detection/alert, chase state, waiting while sight is lost, and return to roaming.
- Tools > Paramnesia > Check Enemy Detection: 22 geometry/event/proximity assertions.
- Tools > Paramnesia > Developer > Show Enemy Vision: toggle editor vision gizmos.

The original Create Zombie Enemies tool preserves the locomotion tree and adds AI
when it is present. Player setup also preserves the zombie controller's clips.

## Verification

The 22 detection assertions and live detect/alert/chase/wait/roam checks passed.
Root-motion sampling measured 1.031906 m of actual movement against 1.031906 m
of animation displacement, with the model local position remaining zero.
Standalone builds and authored sideways/backward gaits were not tested.
