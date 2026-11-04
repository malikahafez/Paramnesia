using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.AI;

namespace StealthGame.Editor
{
    /// <summary>
    /// Enemy types and their drops: melee zombies drop a Bone, the hazmat (toxic waste) zombie throws
    /// bones and drops a Slime flask, the Brute (tank) drops a Heart.
    /// </summary>
    public static class EnemyTypesSetup
    {
        const string Enemies = "Assets/Prefabs/Enemies";
        const string Collectibles = "Assets/Collectibles/Prefabs/";
        const string ProjectilePath = Enemies + "/Projectile_Bone.prefab";
        const string BrutePrefab = Enemies + "/Enemy_Zombie_Brute_01.prefab";
        const string BruteModel = "Assets/Prefabs/PolygonBossZombies/Prefabs/SM_Chr_ZombieBoss_Brute_01.prefab";
        const string CreaturePack = "Assets/Animations/Zombies/Creature Pack";
        const string BruteFolder = "Assets/Animations/Zombies/Brute";
        const string BruteController = BruteFolder + "/Brute.controller";
        const string TrailMaterial = Enemies + "/Projectile_Trail.mat";

        // Strike frames measured by sampling each clip's hand trajectory (seconds into the clip at speed 1).
        public const float ZombieAttackStart = 0.2f, ZombieAttackSpeed = 1.6f; // skips the idle lead-in, no longer crammed into 0.8 s
        const float ZombieClawContact = 1.0f, ZombieThrowRelease = 0.95f;      // right hand at peak forward speed / just before full reach
        const float PunchStart = 0.4f, PunchSpeed = 0.65f, PunchContact = 0.7f; // one-two clip: skip the jab, hit on the heavy second punch
        // Swipe: 0.3 s stand-still lead-in skipped; step back, then a ~1 m lunge 1.05-1.30 s. At 0.9x the lunge spans ~17 frames.
        const float SwipeStart = 0.25f, SwipeSpeed = 0.9f, SwipeContact = 1.28f;
        // Roar on spotting the player: skip the still lead-in, rear back, bellow, and cut into the charge as it sustains.
        const float RoarStart = 0.45f, RoarEnd = 3.0f, RoarSpeed = 1.1f;
        // Tank pace: ~2.2 m/s chase, under the zombies' 3.0 and the player's 4.0 walk (measured 3.1 at full pace).
        const float BruteRunPace = 0.7f;
        static float Windup(float contact, float start, float speed) => (contact - start) / speed;

        [MenuItem("Tools/Paramnesia/Set Up Enemy Types")]
        public static void Setup()
        {
            if (!AssetDatabase.IsValidFolder(BruteFolder)) AssetDatabase.CreateFolder("Assets/Animations/Zombies", "Brute");
            var zombie = AssetDatabase.LoadAssetAtPath<AnimatorController>("Assets/Animations/Zombies/ZombieIdle.controller");
            var zombieMachine = zombie.layers[0].stateMachine;
            Attack(zombieMachine, zombieMachine.defaultState,
                AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Animations/Zombies/Zombie Attack.anim"), ZombieAttackStart, ZombieAttackSpeed);
            EnableLookAt(zombie);
            var controller = BuildBruteController();
            BuildProjectile();
            // The Synty Brute prefab stands 3.7 m; 0.7 makes it ~2.6 m so it fits under house ceilings.
            ZombieEnemySetup.Build(AssetDatabase.LoadAssetAtPath<GameObject>(BruteModel), controller, "Zombie_Brute_01", 0.7f);
            ZombieAISetup.ConfigurePrefabs();
            DropEffectsSetup.Setup();
            AssetDatabase.SaveAssets();
            Check();
        }

        /// <summary>Called from ZombieAISetup.Configure so regenerating prefabs keeps their type.</summary>
        public static void ApplyType(string name, EnemyCombatTarget combat, NavMeshAgent agent)
        {
            combat.projectilePrefab = null;
            var ai = combat.GetComponent<ZombieAI>();
            ai.noticeState = "";
            if (name.Contains("Brute"))
            {
                ai.noticeState = "Brute Roar";
                ai.noticeOffset = RoarStart;
                ai.noticeDuration = (RoarEnd - RoarStart) / RoarSpeed;
                agent.angularSpeed = 120f; // heavier, wider turns
                combat.dropPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(Collectibles + "Collectible_Heart.prefab");
                combat.maxHealth = combat.currentHealth = 300;
                combat.attackDamage = 30;
                combat.attackReach = 2.6f;
                combat.attackCooldown = 3f;
                combat.attackWindups = new[] { Windup(PunchContact, PunchStart, PunchSpeed), Windup(SwipeContact, SwipeStart, SwipeSpeed) };
                agent.radius = 0.5f;
                agent.height = 2.6f;
                // Eyes sit ~2.6 m up, so a player in reach falls below the vision cone without a wider close-range bypass.
                combat.enemyDetection.proximityRange = 3f;
            }
            else if (name.Contains("BioHazardSuit"))
            {
                combat.dropPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(Collectibles + "Collectible_Slime.prefab");
                combat.projectilePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ProjectilePath);
                combat.projectileSpeed = 8.5f; // slow enough for a visible lob
                combat.attackReach = 7f;
                combat.attackWindups = new[] { Windup(ZombieThrowRelease, ZombieAttackStart, ZombieAttackSpeed) };
            }
            else
            {
                combat.dropPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(Collectibles + "Collectible_Bone.prefab");
                combat.attackWindups = new[] { Windup(ZombieClawContact, ZombieAttackStart, ZombieAttackSpeed) };
            }
        }

        /// <summary>
        /// Any State → attack on the Attack trigger (and AttackIndex when index ≥ 0), starting `start` seconds
        /// into the clip at `speed`, then back to locomotion. Rebuilds the state's transitions each run.
        /// </summary>
        public static AnimatorState Attack(AnimatorStateMachine sm, AnimatorState locomotion, AnimationClip clip, float start, float speed, int index = -1)
        {
            var state = sm.states.Select(s => s.state).FirstOrDefault(s => s.name == clip.name) ?? sm.AddState(clip.name);
            state.motion = clip;
            state.speed = speed;
            foreach (var old in sm.anyStateTransitions.Where(t => t.destinationState == state)) sm.RemoveAnyStateTransition(old);
            foreach (var old in state.transitions) state.RemoveTransition(old);
            // Blends long enough to carry the run's momentum into the wind-up and settle back out.
            var enter = sm.AddAnyStateTransition(state);
            enter.hasExitTime = false; enter.duration = 0.2f; enter.canTransitionToSelf = false;
            enter.offset = start / clip.length;
            enter.AddCondition(AnimatorConditionMode.If, 0f, "Attack");
            if (index >= 0) enter.AddCondition(AnimatorConditionMode.Equals, index, "AttackIndex");
            var exit = state.AddTransition(locomotion);
            exit.hasExitTime = true; exit.exitTime = 0.9f; exit.duration = 0.25f;
            return state;
        }

        // Look-at IK for head tracking (ZombieRootMotion.OnAnimatorIK).
        static void EnableLookAt(AnimatorController controller)
        {
            var layers = controller.layers;
            layers[0].iKPass = true;
            controller.layers = layers;
            EditorUtility.SetDirty(controller);
        }

        static AnimatorController BuildBruteController()
        {
            AnimationClip Clip(string source, string name, bool loop, bool moves = false) =>
                ZombieAISetup.Extract(source, name, loop, moves, CreaturePack, BruteFolder);
            var idle = Clip("mutant breathing idle", "Brute Idle", true);
            var walk = Clip("mutant walking", "Brute Walk", true, true);
            var run = Clip("mutant run", "Brute Run", true, true);
            var punch = Clip("mutant punch", "Brute Punch", false);
            var swipe = Clip("mutant swiping", "Brute Swipe", false);
            var roar = Clip("mutant roaring", "Brute Roar", false);
            var death = Clip("mutant dying", "Brute Death", false);

            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(BruteController);
            if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(BruteController);
            var sm = controller.layers[0].stateMachine;
            // Rebuild everything but the locomotion state so reruns stay idempotent.
            foreach (var transition in sm.anyStateTransitions) sm.RemoveAnyStateTransition(transition);
            foreach (var child in sm.states) if (child.state != sm.defaultState) sm.RemoveState(child.state);
            foreach (var (parameter, type) in new[] {
                ("Attack", AnimatorControllerParameterType.Trigger), ("AttackIndex", AnimatorControllerParameterType.Int),
                ("Death", AnimatorControllerParameterType.Trigger), ("IsDead", AnimatorControllerParameterType.Bool) })
                if (!controller.parameters.Any(p => p.name == parameter)) controller.AddParameter(parameter, type);
            var locomotion = ZombieAISetup.BuildLocomotion(controller, idle, walk, run, "Brute Locomotion", BruteRunPace);

            // Order matches attackWindups in ApplyType.
            Attack(sm, locomotion, punch, PunchStart, PunchSpeed, 0);
            Attack(sm, locomotion, swipe, SwipeStart, SwipeSpeed, 1);
            // ZombieAI cross-fades in at RoarStart; it hands back to locomotion on its own.
            var roarState = sm.AddState("Brute Roar");
            roarState.motion = roar;
            roarState.speed = RoarSpeed;
            var charge = roarState.AddTransition(locomotion);
            charge.hasExitTime = true; charge.exitTime = RoarEnd / roar.length; charge.duration = 0.35f;
            var deathState = sm.AddState("Brute Death");
            deathState.motion = death;
            var die = sm.AddAnyStateTransition(deathState);
            die.hasExitTime = false; die.duration = 0.1f; die.canTransitionToSelf = false;
            die.AddCondition(AnimatorConditionMode.If, 0f, "Death");
            EnableLookAt(controller);
            return controller;
        }

        static void BuildProjectile()
        {
            var root = new GameObject("Projectile_Bone");
            try
            {
                var visual = (GameObject)PrefabUtility.InstantiatePrefab(
                    AssetDatabase.LoadAssetAtPath<GameObject>(Collectibles + "Models/Bone.prefab"), root.transform);
                PrefabUtility.UnpackPrefabInstance(visual, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                foreach (var behaviour in visual.GetComponentsInChildren<MonoBehaviour>(true)) UnityEngine.Object.DestroyImmediate(behaviour);
                foreach (var collider in visual.GetComponentsInChildren<Collider>(true)) UnityEngine.Object.DestroyImmediate(collider);
                foreach (var particles in visual.GetComponentsInChildren<ParticleSystem>(true))
                    if (particles != null && particles.gameObject != visual) UnityEngine.Object.DestroyImmediate(particles.gameObject);
                var mesh = visual.GetComponentInChildren<MeshRenderer>();
                if (mesh == null) throw new InvalidOperationException("Bone model lost its mesh.");
                // Bone.prefab is saved at a world position; center the mesh on the projectile's hitbox and trail.
                visual.transform.localPosition -= mesh.bounds.center;
                var body = root.AddComponent<Rigidbody>();
                body.interpolation = RigidbodyInterpolation.Interpolate;
                body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
                var trigger = root.AddComponent<SphereCollider>();
                trigger.isTrigger = true;
                trigger.radius = 0.15f;

                // Toxic-green additive streak; a copy of the collectibles' particle material keeps the look independent.
                if (AssetDatabase.LoadAssetAtPath<Material>(TrailMaterial) == null)
                    AssetDatabase.CopyAsset("Assets/Collectibles/Particle_Sunray.mat", TrailMaterial);
                var trail = new GameObject("Trail").AddComponent<TrailRenderer>();
                trail.transform.SetParent(root.transform, false);
                trail.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(TrailMaterial);
                trail.time = 0.25f;
                trail.minVertexDistance = 0.04f;
                trail.widthMultiplier = 0.16f;
                trail.widthCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);
                trail.colorGradient = new Gradient
                {
                    colorKeys = new[] { new GradientColorKey(new Color(0.85f, 1f, 0.5f), 0f), new GradientColorKey(new Color(0.3f, 0.85f, 0.15f), 1f) },
                    alphaKeys = new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0f, 1f) }
                };
                trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                trail.receiveShadows = false;
                trail.emitting = false;
                root.AddComponent<EnemyProjectile>().trail = trail;
                PrefabUtility.SaveAsPrefabAsset(root, ProjectilePath);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        [MenuItem("Tools/Paramnesia/Check Enemy Types")]
        public static void Check()
        {
            int count = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { Enemies }))
            {
                var combat = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid)).GetComponent<EnemyCombatTarget>();
                if (combat == null) continue;
                string name = combat.name;
                bool ranged = name.Contains("BioHazardSuit");
                string drop = name.Contains("Brute") ? "Collectible_Heart" : ranged ? "Collectible_Slime" : "Collectible_Bone";
                if (combat.dropPrefab == null || combat.dropPrefab.name != drop) throw new InvalidOperationException(name + " should drop " + drop + ".");
                if ((combat.projectilePrefab != null) != ranged) throw new InvalidOperationException(name + ": only the hazmat zombie throws bones.");
                var projectile = ranged ? combat.projectilePrefab.GetComponent<EnemyProjectile>() : null;
                if (ranged && (projectile == null || projectile.trail == null)) throw new InvalidOperationException("Projectile_Bone needs EnemyProjectile with a trail.");
                if (ranged && projectile.GetComponentInChildren<MeshRenderer>().bounds.center.magnitude > 0.1f) throw new InvalidOperationException("Projectile_Bone mesh must sit on its hitbox.");
                if (combat.attackWindups.Length != (name.Contains("Brute") ? 2 : 1)) throw new InvalidOperationException(name + " needs one strike time per attack animation.");
                count++;
            }
            if (AssetDatabase.LoadAssetAtPath<GameObject>(BrutePrefab) == null) throw new InvalidOperationException("Missing " + BrutePrefab);
            var sm = AssetDatabase.LoadAssetAtPath<AnimatorController>(BruteController).layers[0].stateMachine;
            if (!(sm.defaultState.motion is BlendTree tree) || tree.children.Length != 17 ||
                sm.anyStateTransitions.Count(t => t.conditions.Any(c => c.parameter == "AttackIndex")) != 2)
                throw new InvalidOperationException("Brute controller needs its locomotion tree and 2 attack states.");
            Debug.Log("Enemy types: " + count + " enemies checked (Bone / Slime / Heart drops, hazmat projectile, Brute attacks).");
        }
    }
}
