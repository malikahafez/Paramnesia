using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using Unity.AI.Navigation;
using Weapon = StealthGame.Player.PlayerCharacterController.WeaponType;

namespace StealthGame.Editor
{
    /// <summary>
    /// The final boss prototype: its animator, its prefab (the Synty Brute at full size) and a throwaway arena scene,
    /// Boss_Dev, to tune it in until the real boss arena exists.
    /// </summary>
    public static class BossSetup
    {
        const string Folder = "Assets/Animations/Zombies/Boss";
        const string ControllerPath = Folder + "/BossBrute.controller";
        const string BruteClips = "Assets/Animations/Zombies/Brute/";
        const string CreaturePack = "Assets/Animations/Zombies/Creature Pack";
        const string BruteModel = "Assets/Prefabs/PolygonBossZombies/Prefabs/SM_Chr_ZombieBoss_Brute_01.prefab";
        const string Output = "Assets/Prefabs/Boss";
        public const string PrefabPath = Output + "/Enemy_Boss_Brute.prefab";
        const string Built = "Assets/Prefabs/Enemies/Enemy_Boss_Brute.prefab"; // where ZombieEnemySetup.Build saves it
        const string TelegraphPath = Output + "/BossTelegraph.mat";
        const string DustPath = Output + "/BossDust.mat", RingPath = Output + "/BossRing.mat";
        const string ScenePath = "Assets/Scenes/Boss_Dev.unity";
        const string NavigationPath = "Assets/Scenes/BossDevNavigation.asset"; // its own, so Level 1's ZombieNavigation.asset is never rebaked
        const float Scale = 1.1f; // the Synty Brute stands 3.7 m; ~4.1 m makes it the biggest thing in the game
        // Playback speeds. The clip moments the AI reacts to (release, contact, landing...) are constants on BossBruteAI.
        // The run plays at its natural pace: slowed it floats, sped up it scrabbles. Stun creeps through the leap's fists-down hold.
        const float LeapSpeed = 1.2f, RoarSpeed = 1.1f, StunSpeed = 0.12f, BossRunPace = 1f;
        static readonly string[] Adds = { "Enemy_Zombie_Businessman_Male_01", "Enemy_Zombie_Punk_Female_01" };
        // Level 1 roots the dev scene keeps: the player rig, cameras, light, input and pause menu.
        static readonly string[] Keep = { "PlayerCapsule", "PlayerFollowCamera", "MainCamera", "Directional Light", "UI_EventSystem", "PauseMenuManager", "PauseMenuCanvas", "GameManager" };

        [MenuItem("Tools/Paramnesia/Set Up Boss")]
        public static void Setup()
        {
            var controller = BuildController();
            ZombieEnemySetup.Build(AssetDatabase.LoadAssetAtPath<GameObject>(BruteModel), controller, "Boss_Brute", Scale);
            if (!AssetDatabase.IsValidFolder(Output)) AssetDatabase.CreateFolder("Assets/Prefabs", "Boss");
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            var root = PrefabUtility.LoadPrefabContents(Built);
            try
            {
                // Build gave it a ZombieAI and the tank's stats (its name has "Brute"); swap the AI and keep the shared roam/notice tuning.
                var old = root.GetComponent<ZombieBrain>();
                var boss = root.AddComponent<BossBruteAI>();
                JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(old), boss);
                UnityEngine.Object.DestroyImmediate(old);
                // Rerunning keeps the boss tuning done in the inspector; the fields below belong to this setup.
                float walk = old.roamSpeed, run = old.runSpeed; // gait speeds Build just read from the locomotion tree
                if (existing != null && existing.GetComponent<BossBruteAI>() is BossBruteAI tuned) JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(tuned), boss);
                boss.roamSpeed = walk;
                boss.runSpeed = run;
                boss.telegraphMaterial = TelegraphMaterial();
                boss.dustMaterial = EffectMaterial(DustPath, true);
                boss.ringMaterial = EffectMaterial(RingPath, false);
                boss.glowMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Collectibles/Particle_Sunray.mat"); // additive soft dot
                boss.noticeState = BossBruteAI.BellowState; // the intro roar on first sight, played out in full before it moves
                boss.noticeOffset = BossBruteAI.RoarFrom;
                boss.noticeDuration = (BossBruteAI.RoarEnd - BossBruteAI.RoarFrom) / RoarSpeed;
                boss.addPrefabs = Adds.Select(a => AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemies/" + a + ".prefab")).ToArray();

                var combat = root.GetComponent<EnemyCombatTarget>();
                combat.isBoss = true;
                combat.ragdollOnDeath = false; // plays the Brute's death instead of crumpling
                combat.maxHealth = combat.currentHealth = 1500;
                combat.enableCounterAttack = false; // the boss AI runs its own punch and swipe
                combat.attackReach = 3.4f;
                var capsule = root.GetComponent<CapsuleCollider>();
                capsule.radius = 0.8f; // Build caps it at 0.35 m, far thinner than this body
                var agent = root.GetComponent<NavMeshAgent>();
                agent.radius = 0.8f;
                agent.height = capsule.height;
                var detection = root.GetComponent<EnemyDetection>();
                detection.viewRange = 20f; // leap and charge work from across the arena
                detection.viewAngle = 120f;
                detection.proximityRange = 5f;
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            // Out of Prefabs/Enemies so the enemy-type checks and the zombie re-config loop never touch it.
            AssetDatabase.DeleteAsset(Built);
            AssetDatabase.SaveAssets();
            Check();
        }

        static AnimatorController BuildController()
        {
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Animations/Zombies", "Boss");
            AnimationClip Brute(string name) => AssetDatabase.LoadAssetAtPath<AnimationClip>(BruteClips + name + ".anim")
                ?? throw new InvalidOperationException("Missing " + name + ": run Tools/Paramnesia/Set Up Enemy Types first.");
            // Leap keeps its root travel out of the pose: the AI flies the body to the landing spot itself.
            var leap = ZombieAISetup.Extract("mutant jump attack", BossBruteAI.LeapClip, false, true, CreaturePack, Folder);
            var flex = ZombieAISetup.Extract("mutant flexing muscles", BossBruteAI.FlexClip, false, false, CreaturePack, Folder);
            var roar = Brute(BossBruteAI.RoarClip);

            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath) ?? AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            var sm = controller.layers[0].stateMachine;
            // Rebuild everything but the locomotion state so reruns stay idempotent.
            foreach (var transition in sm.anyStateTransitions) sm.RemoveAnyStateTransition(transition);
            foreach (var child in sm.states) if (child.state != sm.defaultState) sm.RemoveState(child.state);
            foreach (var (parameter, type) in new[] {
                ("Attack", AnimatorControllerParameterType.Trigger), ("AttackIndex", AnimatorControllerParameterType.Int),
                ("Death", AnimatorControllerParameterType.Trigger), ("IsDead", AnimatorControllerParameterType.Bool) })
                if (!controller.parameters.Any(p => p.name == parameter)) controller.AddParameter(parameter, type);
            if (!controller.parameters.Any(p => p.name == BossBruteAI.Pace))
                controller.AddParameter(new AnimatorControllerParameter { name = BossBruteAI.Pace, type = AnimatorControllerParameterType.Float, defaultFloat = 1f });
            var locomotion = ZombieAISetup.BuildLocomotion(controller, Brute("Brute Idle"), Brute("Brute Walk"), Brute("Brute Run"), BossBruteAI.Locomotion, BossRunPace);

            // The AI cross-fades into each state part-way through its clip, and each plays out to `end` seconds before easing
            // back to locomotion. speed 0 = driven by the Pace parameter, which the AI slows for wind-ups.
            AnimatorState State(string name, AnimationClip clip, float speed, float end)
            {
                var state = sm.AddState(name);
                state.motion = clip;
                if (speed > 0f) state.speed = speed;
                else { state.speedParameter = BossBruteAI.Pace; state.speedParameterActive = true; }
                if (end <= 0f) return state; // the AI cross-fades out of it
                var back = state.AddTransition(locomotion);
                back.hasExitTime = true; back.exitTime = end / clip.length; back.duration = 0.35f;
                return state;
            }
            State(BossBruteAI.PunchState, Brute(BossBruteAI.PunchClip), 0f, BossBruteAI.PunchEnd);
            State(BossBruteAI.SwipeState, Brute(BossBruteAI.SwipeClip), 0f, BossBruteAI.SwipeEnd);
            State(BossBruteAI.LeapState, leap, LeapSpeed, leap.length * 0.95f);
            State(BossBruteAI.StunState, leap, StunSpeed, 0f);
            State(BossBruteAI.RecoverState, leap, LeapSpeed, leap.length * 0.95f);
            State(BossBruteAI.FlexState, flex, 1f, BossBruteAI.FlexEnd);
            State(BossBruteAI.BellowState, roar, RoarSpeed, BossBruteAI.RoarEnd);
            State(BossBruteAI.ChargeState, Brute(BossBruteAI.RunClip), 0f, 0f);
            var die = sm.AddAnyStateTransition(State(BossBruteAI.DeathState, Brute("Brute Death"), 1f, 0f));
            die.hasExitTime = false; die.duration = 0.1f; die.canTransitionToSelf = false;
            die.AddCondition(AnimatorConditionMode.If, 0f, "Death");
            var layers = controller.layers;
            layers[0].iKPass = true; // head look-at (ZombieRootMotion.OnAnimatorIK)
            controller.layers = layers;
            EditorUtility.SetDirty(controller);
            return controller;
        }

        // Transparent red for the leap ring, charge line and tremor ring; an asset so the transparent shader variant ships.
        static Material TelegraphMaterial()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(TelegraphPath);
            if (material != null) { material.SetFloat("_Cull", 0f); EditorUtility.SetDirty(material); return material; }
            material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            material.SetFloat("_Cull", 0f); // the floor shapes show from either side
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)RenderQueue.Transparent;
            material.SetColor("_BaseColor", new Color(1f, 0.08f, 0.04f, 0.35f));
            AssetDatabase.CreateAsset(material, TelegraphPath);
            return material;
        }

        // The collectibles' soft-dot particle material switched from additive glow to plain alpha: with its dot it is a dust puff,
        // without it a flat see-through colour for the shockwave rings.
        static Material EffectMaterial(string path, bool softDot)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            if (!AssetDatabase.CopyAsset("Assets/Collectibles/Particle_Sunray.mat", path)) throw new InvalidOperationException("Could not copy the particle material to " + path);
            material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!softDot) material.SetTexture("_BaseMap", null);
            material.SetFloat("_Blend", 0f);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            material.SetColor("_BaseColor", Color.white);
            EditorUtility.SetDirty(material);
            return material;
        }

        [MenuItem("Tools/Paramnesia/Create Boss Dev Scene")]
        public static void CreateDevScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null) Setup();
            // A copy of Level 1 keeps the fully set-up player rig; everything else is rebuilt on each run.
            if (!File.Exists(ScenePath) && !AssetDatabase.CopyAsset("Assets/Scenes/Level 1 Base.unity", ScenePath))
                throw new InvalidOperationException("Could not copy Level 1 Base to " + ScenePath);
            var scene = EditorSceneManager.OpenScene(ScenePath);
            foreach (var go in scene.GetRootGameObjects())
                if (!Keep.Contains(go.name)) UnityEngine.Object.DestroyImmediate(go);
            var player = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "PlayerCapsule")
                ?? throw new InvalidOperationException("Level 1 Base has no PlayerCapsule to copy.");
            player.transform.position = Vector3.up * 200f; // out of the bake volume so it doesn't carve a hole in the floor

            // A 36 m walled square with four pillars to lure the charge into.
            var arena = new GameObject("Boss Arena");
            void Block(string name, Vector3 position, Vector3 size)
            {
                var block = GameObject.CreatePrimitive(PrimitiveType.Cube);
                block.name = name;
                block.transform.SetParent(arena.transform, false);
                block.transform.localPosition = position;
                block.transform.localScale = size;
                block.isStatic = true;
            }
            Block("Floor", new Vector3(0f, -0.5f, 0f), new Vector3(38f, 1f, 38f));
            foreach (int side in new[] { -1, 1 })
            {
                Block("Wall", new Vector3(0f, 2f, side * 18.5f), new Vector3(38f, 4f, 1f));
                Block("Wall", new Vector3(side * 18.5f, 2f, 0f), new Vector3(1f, 4f, 38f));
                foreach (int other in new[] { -1, 1 }) Block("Pillar", new Vector3(side * 7f, 2.5f, other * 7f), new Vector3(1.6f, 5f, 1.6f));
            }
            var spawns = new[] { new Vector3(-14f, 0f, 14f), new Vector3(14f, 0f, 14f) }.Select((p, i) =>
            {
                var point = new GameObject("Add Spawn " + (i + 1)).transform;
                point.SetParent(arena.transform, false);
                point.SetPositionAndRotation(p, Quaternion.Euler(0f, 180f, 0f));
                return point;
            }).ToArray();

            var surface = new GameObject("Boss Navigation").AddComponent<NavMeshSurface>();
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.collectObjects = CollectObjects.Volume;
            surface.center = new Vector3(0f, 2f, 0f);
            surface.size = new Vector3(40f, 12f, 40f);
            surface.BuildNavMesh();
            if (surface.navMeshData == null) throw new InvalidOperationException("Boss arena navigation bake failed.");
            AssetDatabase.DeleteAsset(NavigationPath);
            AssetDatabase.CreateAsset(surface.navMeshData, NavigationPath);

            var bossObject = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath), scene);
            bossObject.transform.SetPositionAndRotation(new Vector3(0f, 0f, 6f), Quaternion.Euler(0f, 180f, 0f));
            var boss = bossObject.GetComponent<BossBruteAI>();
            boss.addSpawnPoints = spawns;
            PrefabUtility.RecordPrefabInstancePropertyModifications(boss);
            player.transform.SetPositionAndRotation(new Vector3(0f, 0.05f, -14f), Quaternion.identity);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("Boss_Dev ready: walk forward to start the fight. Rerun this menu to rebuild the arena; tune the boss on its prefab.");
        }

        [MenuItem("Tools/Paramnesia/Check Boss")]
        public static void Check()
        {
            void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Require(prefab != null, "Missing " + PrefabPath + ": run Tools/Paramnesia/Set Up Boss.");
            Require(AssetDatabase.LoadAssetAtPath<GameObject>(Built) == null, "The boss must live in Prefabs/Boss, not Prefabs/Enemies.");
            var brains = prefab.GetComponents<ZombieBrain>();
            Require(brains.Length == 1 && brains[0] is BossBruteAI, "The boss needs BossBruteAI as its only AI.");
            var ai = (BossBruteAI)brains[0];
            var combat = prefab.GetComponent<EnemyCombatTarget>();
            Require(combat.isBoss && !combat.ragdollOnDeath && !combat.enableCounterAttack, "The boss needs super armor, its death clip, and the built-in melee off (its AI runs punch and swipe).");
            Require(ai.telegraphMaterial != null && ai.dustMaterial != null && ai.ringMaterial != null && ai.glowMaterial != null && ai.addPrefabs.Length > 0 && ai.addPrefabs.All(p => p != null), "The boss is missing its telegraph, dust or ring material, or its add prefabs.");
            Require(ai.runSpeed > ai.roamSpeed && ai.roamSpeed > 0f, "The boss needs its walk and run gait speeds.");
            var states = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath).layers[0].stateMachine.states.Select(s => s.state.name).ToArray();
            foreach (var state in new[] { BossBruteAI.Locomotion, BossBruteAI.PunchState, BossBruteAI.SwipeState, BossBruteAI.LeapState, BossBruteAI.StunState,
                BossBruteAI.RecoverState, BossBruteAI.FlexState, BossBruteAI.BellowState, BossBruteAI.ChargeState, BossBruteAI.DeathState })
                Require(states.Contains(state), "Boss controller is missing " + state);

            // Weapon rules, on a scratch boss with this prefab's tuning.
            var probe = new GameObject("Boss rules check");
            try
            {
                var rules = probe.AddComponent<BossBruteAI>();
                JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(ai), rules);
                int HitsToBreak(Weapon weapon, int damage)
                {
                    rules.armor = rules.maxArmor;
                    int hits = 0;
                    while (rules.armor > 0 && hits < 100) { rules.Resolve(damage, weapon, false); hits++; }
                    return hits;
                }
                int axe = HitsToBreak(Weapon.Axe, 45), sword = HitsToBreak(Weapon.Sword, 25);
                Require(axe <= 4, "The axe should break the armor in 4 hits or fewer, took " + axe + ".");
                Require(sword >= 3 * axe, "The sword should take far longer to break the armor (" + sword + " hits vs the axe's " + axe + ").");
                rules.armor = rules.maxArmor;
                Require(rules.Resolve(45, Weapon.Axe, false) < 45, "Armor must soak part of every hit.");
                rules.armor = 0;
                Require(rules.Resolve(25, Weapon.Sword, true) > rules.Resolve(45, Weapon.Axe, true), "On exposed flesh a sword hit must beat an axe hit.");
                Require(rules.Resolve(25, Weapon.Sword, false) == 25, "Unarmored, unexposed hits land unchanged.");
            }
            finally { UnityEngine.Object.DestroyImmediate(probe); }
            Debug.Log("Boss checks passed: prefab, controller, and the axe/sword armor rules.");
        }
    }
}
