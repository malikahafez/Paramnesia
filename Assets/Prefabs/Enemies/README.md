# Zombie enemies

Drag any Enemy_Zombie_* prefab into a scene with a baked NavMesh. All 50 variants
have humanoid animation, vision, combat and navigation. Tag the player Player,
or assign it on Enemy Detection.

Zombie AI controls a 4 m roaming radius around spawn (or an assigned Roam Center),
walking and running at the speed authored in the clips (about 0.33 and 3.04 m/s). Losing sight stops the
zombie for 3 seconds, then it resumes roaming. Adjust these values on Zombie AI.
The yellow ! flares on detection and follows the animated head.

Five standalone zombie animation clips are shared by the controller. The 2D
locomotion tree blends idle/walk/run using local velocity. Root motion supplies movement; navigation only steers and keeps it on walkable ground. The source pack only
provides forward gaits; navigation turns the body toward travel. Authored strafe
and backward steps require additional source clips. Source FBXs were removed;
models retain their own required skinning skeleton and humanoid avatar.

To hide placed zombies from gameplay, clear the checkbox beside their name at the
top of the Inspector, then save the scene. The Hierarchy eye only hides Scene view.

Full settings, navigation and test tools: [Zombie AI guide](../../../Docs/EnemyDetection.md).
