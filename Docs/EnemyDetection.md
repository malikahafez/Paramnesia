# Zombie AI

The two zombies in `Assets/Scenes/Level 1 Base.unity` are enabled for testing.
To hide them from gameplay again, select `Enemy_Zombie_Businessman_Male_01`
and `Enemy_Zombie_Punk_Female_01` in the Hierarchy, clear the checkbox beside
their name at the top of the Inspector, and save the scene. The Hierarchy eye
only hides objects in the Scene view; it does not disable them during play.

## Behavior and tuning

Each enemy has **Zombie AI**, **NavMesh Agent**, **Enemy Detection**, and
**Enemy Combat Target**. Existing combat, health, stagger and death are preserved.
Zombie AI owns movement; combat no longer directly moves these enemies through walls.

- Roam Center: optional Transform; empty means the initial spawn position.
- Roam Radius: 4 m by default, with reachable destinations inside that sphere.
- Gait speeds come from the clips: walk approximately 0.33 m/s, run 3.04 m/s.
- Idle Pause: 2 seconds between roaming destinations.
- Lost Sight Wait: 3 seconds. Zombies stop where they lost sight, then roam again.
- Seeing the player interrupts roaming/waiting immediately. The yellow ! pops
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
