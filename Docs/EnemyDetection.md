# Enemy detection guide

## Using the enemies

Open `Assets/Scenes/Level 1 Base.unity` to try the placed businessman zombie.
Drag any of the 50 `Assets/Prefabs/Enemies/Enemy_Zombie_*.prefab` assets into a
scene for another appearance. Rotate the enemy's blue (+Z) axis toward its view.
Give the player the `Player` tag, or assign its root Transform to **Player** on
the **Enemy Detection** component. The root should be at the player's feet.

The yellow **!** appears while the player is detected. It follows the animated
head, stays upright, and has no collider. It disappears immediately on losing
sight or disabling detection. Keep it for player feedback; logs and vision
overlays serve a separate development purpose.

## Developer tools

- Toggle **Tools > Paramnesia > Developer > Show Enemy Vision** to draw all enemy
  vision volumes, including unselected enemies. Toggle again to hide them. This
  is a local Editor preference, not a saved gameplay setting.
- Alternatively, enable **Show Vision** on an individual enemy's component.
  Turning off the global toggle leaves individually enabled displays visible.
- Enable **Gizmos** in the Scene view. The same overlay can be seen in the Game
  view when its Gizmos button is enabled. No debug vision geometry is in builds.
- Yellow means no detection; red means detected. The translucent horizontal
  sector shows direction/range; the wire outline shows the 3D cone's vertical
  extent and spherical range boundary. A red line points to the detected target.
  The full volume is shown through walls for inspection; walls still block sight.
- Enable **Log Detection Changes** on an enemy for Console messages such as
  `[Enemy_Zombie_Businessman_Male_01] Player detected` and `Player lost`.
  Messages occur on transitions, not every frame. Click a message to locate its
  enemy. Logging is compiled into the Editor and Development Builds only.

## Detection settings and behavior

| Setting | Meaning |
| --- | --- |
| Player | Optional explicit target; otherwise finds the active object tagged Player. |
| Eye Offset | Vision origin relative to the enemy root. |
| Target Offset | Aim point relative to the player root; defaults to 1 m up. |
| View Range | Maximum distance from eyes to target point; defaults to 10 m. |
| View Angle | Full cone angle, horizontal and vertical; defaults to 70 degrees. |
| Sight Layers | Layers whose solid colliders block sight. Keep walls included. |
| Detection Indicator / Anchor | Alert object and humanoid head bone; assigned in generated prefabs. |
| On Player Detected / Lost | UnityEvents for future gameplay responses. |

Detection checks distance, angle, then a physics ray to the target point every
frame. It ignores triggers, its own colliders, and target descendants. Walls need
colliders. This is one target point: a visible hand alone does not count if the
target point is hidden. `IsPlayerDetected` exposes the current state in code.
Disabling detection clears it and fires the lost event; transition logging is
performed during regular detection updates.

## Files and responsibilities

```text
Assets/
  PolygonZombies/                     Original imported pack, kept intact
  Animations/Zombies/
    Zombie Idle.anim                 Standalone looping humanoid animation
    ZombieIdle.controller            One state: Zombie Idle; Foot IK enabled
  Prefabs/Enemies/
    Enemy_Zombie_*.prefab             50 generated enemy variants
    PolygonZombie_*_URP.mat           URP/Lit copies of original materials
    DetectionAlert.mat               Yellow URP/Unlit material for the !
    README.md                        Quick setup notes
  Scripts/
    Systems/EnemyDetection.cs         Detection, events, alert, logs, editor gizmos
    Editor/ZombieEnemySetup.cs        Generation menu, vision toggle, self-checks
  Scenes/Level 1 Base.unity           One placed test enemy
Docs/EnemyDetection.md                This guide
```

Each enemy root contains EnemyDetection, a non-trigger CapsuleCollider, a
kinematic Rigidbody with gravity off, the original zombie prefab as a child,
and a Detection Alert child with two meshes. The model's Animator uses the pack's
valid humanoid avatar and shared controller. Apply Root Motion is off.

The animation was extracted after humanoid importing. Its clip and state names
are `Zombie Idle`, not `mixamo.com`. A `.anim` stores motion data, not another
instantiated skeleton; the zombie model still needs its own bones and avatar.
The FBX was previously moved to `Assets/Animations/Zombies/Source/Zombie Idle.fbx`,
but that source folder is currently absent. The standalone clip works without it.
The generator now uses that clip when the source is absent. If the source is
restored at that path, generation reimports and extracts it again.

The project's existing AI Navigation package was already installed. The Unity
Pipeline package was added to connect to and verify the running Editor; this
changed `Packages/manifest.json` and `Packages/packages-lock.json`. No new package
was needed for detection or the alert. Original pack shaders/materials were not
converted in place; the generated enemies use URP copies. Their copies omit the
pack's custom blood shader effect.

## Foot pose investigation

The original pack avatar and generated avatars report valid humanoid rigs.
Runtime samples at 0%, 25%, 50%, and 75% of the idle showed pointed feet and mesh
bottoms about 5–7 cm above the level floor. The placed root had previously been
raised approximately 9.8 cm using renderer bounds, which overestimated the
required floor offset. This placement was corrected to the floor surface.

Enabling the Animator state's built-in Foot IK steadied the mesh bottom to
within approximately 2 mm of the root over those four samples. It improves
retargeted foot contact but does not flatten the idle's foot rotation and is not
a terrain raycast foot-placement system. Without the original FBX, the authored
foot pose cannot be compared against the source rig, so an animation-versus-
retargeting diagnosis remains incomplete. A flat-footed humanoid idle or the
original FBX is the next useful input; no custom IK system was added.

## Rebuilding, checks, and limits

**Tools > Paramnesia > Create Zombie Enemies** rebuilds all 50 prefabs. It
overwrites generated prefab edits; tune scene instances instead, or update the
generator for changes that should reach every variant. It preserves existing
asset identities and shares one controller/clip. If both the source and the
standalone clip are missing, it reports an error rather than generating enemies
without an animation.

**Tools > Paramnesia > Check Enemy Detection** runs thirteen assertions in a
temporary additive scene: front, behind, side, range, wall blocking, ignored
self/target colliders, triggers, missing/inactive targets, alert show/hide/disable,
and transition-only logging. It removes its fixtures without saving the level.
Play mode was also used to verify advancing animation, alert following/visibility,
and foot placement. A standalone build has not been checked.

There is no movement, combat, suspicion timer, memory, or hearing yet. For the
next iteration, first settle the idle's foot pose, then add an in-place humanoid
walk and basic navigation. A short detection delay/last-seen timer would be a
useful later gameplay improvement if instant detection feels too abrupt. Keep
the current immediate detection while checking geometry and visibility.
