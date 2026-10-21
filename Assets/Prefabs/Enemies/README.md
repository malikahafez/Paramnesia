# Zombie enemies

Drag any `Enemy_Zombie_*` prefab into a scene. All 50 variants have a solid capsule,
a kinematic rigidbody, a humanoid Animator, and the supplied looping zombie idle.

The player must be tagged `Player`, or assigned explicitly in Enemy Detection.
Default vision is a 10 metre, 70 degree cone, measured from the enemy's eyes along
its blue (+Z) axis. Solid colliders block sight; triggers do not. Select an enemy
with Scene view Gizmos enabled and Show Vision checked to see its yellow cone,
which turns red on detection. Toggle all enemies through
`Tools > Paramnesia > Developer > Show Enemy Vision`.
Use `IsPlayerDetected`, `On Player Detected`, and `On Player Lost` for later gameplay.
The yellow 3D exclamation mark above each enemy appears only while it sees the player.
Detection and loss happen immediately, without combat or chasing.

Adjust View Range, View Angle, Eye Offset, Target Offset, and Sight Layers in the
Inspector. Target Offset defaults to one metre above the player's root. Keep wall
layers included in Sight Layers. A collider is required on geometry that blocks sight.

`Tools > Paramnesia > Create Zombie Enemies` rebuilds the generated prefabs from
the pack and overwrites prefab edits. Tune scene instances for now.
`Tools > Paramnesia > Check Enemy Detection` runs thirteen checks without saving the level.
Enable Log Detection Changes for transition-only Console messages during development.

The controller plays the standalone humanoid `Assets/Animations/Zombies/Zombie Idle.anim`
clip, named `Zombie Idle`. The optional source FBX path is
`Assets/Animations/Zombies/Source/Zombie Idle.fbx`; it is currently absent.
Generation works from the standalone clip without the FBX.
Enemies retarget the clip using their own valid humanoid avatars.

Enemy materials use URP/Lit copies of the pack textures; original pack materials
are preserved. The pack's custom blood shader effect is not part of these copies.
For movement later, supply an in-place humanoid zombie walk clip; a run clip is
optional. No attack, hit, or death clips are needed for this stage.

Full change history, file map, developer controls, foot-pose findings, and limits:
[Enemy detection guide](../../../Docs/EnemyDetection.md).
