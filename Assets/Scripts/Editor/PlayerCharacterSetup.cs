using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using Unity.Cinemachine;
using StarterAssets;
using GanzSe;
using StealthGame.Player;

namespace StealthGame.Editor
{
    [InitializeOnLoad]
    public static class PlayerCharacterSetup
    {
        public const int TargetSetupVersion = 7;

        private const string AnimFolder = "Assets/Animations/Player";
        private const string SourceAnimFolder = "Assets/Animations/Player/Source";
        private const string ControllerPath = AnimFolder + "/PlayerCharacter.controller";
        private const string SourceControllerPath = "Assets/Starter Assets/Runtime/ThirdPersonController/Character/Animations/StarterAssetsThirdPerson.controller";
        private const string ZombieControllerPath = "Assets/Animations/Zombies/ZombieIdle.controller";

        // GanzSe Modular Character Prefab & Fallback
        private const string GanzSePrefabPath = "Assets/URP GanzSe Free Modular Character Pack/Prefabs/Modular Character/GanzSe Free Modular Character Update 1_1.prefab";
        private const string GanzSeOriginalPrefabPath = "Assets/URP GanzSe Free Modular Character Pack/Prefabs/Modular Character/GanzSe Free Modular Character Original 1_0.prefab";
        private const string ArmatureModelPath = "Assets/Starter Assets/Runtime/ThirdPersonController/Character/Models/Armature.fbx";

        // Kevin Iglesias Motion-Captured Humanoid Animation Paths
        private const string KiCombatIdle1HPath = "Assets/Kevin Iglesias/Human Animations/Animations/Male/Combat/1H/HumanM@CombatIdle1H01.fbx";
        private const string KiIdle01Path = "Assets/Kevin Iglesias/Human Animations/Animations/Male/Idles/HumanM@Idle01.fbx";

        // 8-Direction Walk Clips
        private const string KiWalkForwardPath = "Assets/Kevin Iglesias/Human Animations/Animations/Male/Movement/Walk/HumanM@Walk01_Forward.fbx";
        private const string KiWalkBackwardPath = "Assets/Kevin Iglesias/Human Animations/Animations/Male/Movement/Walk/HumanM@Walk01_Backward.fbx";
        private const string KiWalkLeftPath = "Assets/Kevin Iglesias/Human Animations/Animations/Male/Movement/Walk/HumanM@Walk01_Left.fbx";
        private const string KiWalkRightPath = "Assets/Kevin Iglesias/Human Animations/Animations/Male/Movement/Walk/HumanM@Walk01_Right.fbx";
        private const string KiWalkForwardLeftPath = "Assets/Kevin Iglesias/Human Animations/Animations/Male/Movement/Walk/HumanM@Walk01_ForwardLeft.fbx";
        private const string KiWalkForwardRightPath = "Assets/Kevin Iglesias/Human Animations/Animations/Male/Movement/Walk/HumanM@Walk01_ForwardRight.fbx";
        private const string KiWalkBackwardLeftPath = "Assets/Kevin Iglesias/Human Animations/Animations/Male/Movement/Walk/HumanM@Walk01_BackwardLeft.fbx";
        private const string KiWalkBackwardRightPath = "Assets/Kevin Iglesias/Human Animations/Animations/Male/Movement/Walk/HumanM@Walk01_BackwardRight.fbx";

        // 8-Direction Run & Sprint Clips
        private const string KiRunForwardPath = "Assets/Kevin Iglesias/Human Animations/Animations/Male/Movement/Run/HumanM@Run01_Forward.fbx";
        private const string KiRunBackwardPath = "Assets/Kevin Iglesias/Human Animations/Animations/Male/Movement/Run/HumanM@Run01_Backward.fbx";
        private const string KiRunLeftPath = "Assets/Kevin Iglesias/Human Animations/Animations/Male/Movement/Run/HumanM@Run01_Left.fbx";
        private const string KiRunRightPath = "Assets/Kevin Iglesias/Human Animations/Animations/Male/Movement/Run/HumanM@Run01_Right.fbx";
        private const string KiRunForwardLeftPath = "Assets/Kevin Iglesias/Human Animations/Animations/Male/Movement/Run/HumanM@Run01_ForwardLeft.fbx";
        private const string KiRunForwardRightPath = "Assets/Kevin Iglesias/Human Animations/Animations/Male/Movement/Run/HumanM@Run01_ForwardRight.fbx";
        private const string KiRunBackwardLeftPath = "Assets/Kevin Iglesias/Human Animations/Animations/Male/Movement/Run/HumanM@Run01_BackwardLeft.fbx";
        private const string KiRunBackwardRightPath = "Assets/Kevin Iglesias/Human Animations/Animations/Male/Movement/Run/HumanM@Run01_BackwardRight.fbx";
        private const string KiSprintForwardPath = "Assets/Kevin Iglesias/Human Animations/Animations/Male/Movement/Sprint/HumanM@Sprint01_Forward.fbx";

        // Jump & Air Clips
        private const string KiJumpBeginPath = "Assets/Kevin Iglesias/Human Animations/Animations/Male/Movement/Jump/HumanM@Jump01 - Begin.fbx";
        private const string KiFallPath = "Assets/Kevin Iglesias/Human Animations/Animations/Male/Movement/Jump/HumanM@Fall01.fbx";
        private const string KiJumpLandPath = "Assets/Kevin Iglesias/Human Animations/Animations/Male/Movement/Jump/HumanM@Jump01 - Land.fbx";

        // Combat, Shield Parry, Hit & Death Clips
        private const string KiAttack1HRPath = "Assets/Kevin Iglesias/Human Animations/Animations/Male/Combat/1H/HumanM@Attack1H01_R.fbx";
        private const string KiAttack1HLPath = "Assets/Kevin Iglesias/Human Animations/Animations/Male/Combat/1H/HumanM@Attack1H01_L.fbx";
        private const string KiAttack2HPath = "Assets/Kevin Iglesias/Human Animations/Animations/Male/Combat/2H/HumanM@Attack2H01.fbx";
        private const string KiAttackPolearmPath = "Assets/Kevin Iglesias/Human Animations/Animations/Male/Combat/Polearm/HumanM@AttackPolearm01.fbx";
        private const string KiAttackShieldPath = "Assets/Kevin Iglesias/Human Animations/Animations/Male/Combat/Shield/HumanM@AttackShield01.fbx";
        private const string KiCombatDamagePath = "Assets/Kevin Iglesias/Human Animations/Animations/Male/Combat/HumanM@CombatDamage01.fbx";
        private const string KiDeathPath = "Assets/Kevin Iglesias/Human Animations/Animations/Male/Combat/HumanM@Death01.fbx";
        private const string KiUpperBodyMaskPath = "Assets/Kevin Iglesias/Human Animations/Models/Avatar Masks/Human Body Upper Mask.mask";

        // Weapons (1 = Sword, 2 = Spear/Polearm, 3 = Battle Axe / Warhammer) & Off-Hand Shield
        private static readonly string[] WeaponPrefabPaths = new string[]
        {
            "Assets/BrokenVector/LowPolyDungeon/Prefabs/Clutter/Sword.prefab",
            "Assets/BrokenVector/LowPolyDungeon/Prefabs/Clutter/Spear.prefab",
            "Assets/Dungon Pack/Prefabs/Axe.prefab"
        };

        private static readonly string[] FallbackWeaponPrefabPaths = new string[]
        {
            "Assets/Kevin Iglesias/Human Animations/Unity Demo Scenes/Human Melee Animations/Prefabs/Weapons/Human_Sword.prefab",
            "Assets/Kevin Iglesias/Human Animations/Unity Demo Scenes/Human Melee Animations/Prefabs/Weapons/Human_Polearm.prefab",
            "Assets/Kevin Iglesias/Human Animations/Unity Demo Scenes/Human Melee Animations/Prefabs/Weapons/Human_Warhammer.prefab"
        };

        private const string ShieldPrefabPath = "Assets/BrokenVector/LowPolyDungeon/Prefabs/Clutter/Shield.prefab";
        private const string FallbackShieldPrefabPath = "Assets/Kevin Iglesias/Human Animations/Unity Demo Scenes/Human Melee Animations/Prefabs/Weapons/Human_Shield.prefab";

        private static readonly string[] WeaponNames = new string[] { "Weapon_Sword", "Weapon_Spear", "Weapon_Axe" };

        static PlayerCharacterSetup()
        {
            EditorApplication.delayCall += AutoInitializeIfNeeded;
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.EnteredEditMode)
                {
                    EditorApplication.delayCall += AutoInitializeIfNeeded;
                }
            };
        }

        private static void AutoInitializeIfNeeded()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;

            bool controllerMissing = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath) == null;
            var activePlayer = GameObject.Find("PlayerCapsule");
            bool sceneNeedsSetup = false;

            if (activePlayer != null)
            {
                var pcc = activePlayer.GetComponent<PlayerCharacterController>();
                var receiver = activePlayer.GetComponentInChildren<PlayerAnimationEventReceiver>(true);
                sceneNeedsSetup = pcc == null || pcc.setupVersion < TargetSetupVersion || receiver == null;
            }

            if (controllerMissing || sceneNeedsSetup)
            {
                SetupPlayerCharacterAndAnimations();
            }
        }

        [MenuItem("Tools/Paramnesia/Setup Player Character & Animations")]
        public static void SetupPlayerCharacterAndAnimations()
        {
            Directory.CreateDirectory(AnimFolder);
            Directory.CreateDirectory(SourceAnimFolder);
            Directory.CreateDirectory("Assets/Materials/Characters");
            Directory.CreateDirectory("Assets/Prefabs/Player");

            ImportCustomHumanoidFbxFolder();

            AnimationClip crouchClip = CreateOrUpdateCrouchClip(AnimFolder + "/Player_Crouch_Idle.anim");
            AnimationClip slash1Clip = LoadClipAtPath(KiAttack1HRPath) ?? CreateOrUpdateAttackClip(AnimFolder + "/Player_Attack_Slash1.anim", 1);
            AnimationClip slash2Clip = LoadClipAtPath(KiAttack1HLPath) ?? CreateOrUpdateAttackClip(AnimFolder + "/Player_Attack_Slash2.anim", 2);
            AnimationClip heavy3Clip = LoadClipAtPath(KiAttack2HPath) ?? CreateOrUpdateAttackClip(AnimFolder + "/Player_Attack_Heavy3.anim", 3);
            AnimationClip spearClip = LoadClipAtPath(KiAttackPolearmPath) ?? slash2Clip;
            AnimationClip shieldParryClip = LoadClipAtPath(KiAttackShieldPath) ?? slash1Clip;
            AnimationClip hitClip = LoadClipAtPath(KiCombatDamagePath);
            AnimationClip deathClip = LoadClipAtPath(KiDeathPath);

            AnimatorController controller = CreateOrUpdateAnimatorController(
                crouchClip, slash1Clip, slash2Clip, heavy3Clip, spearClip, shieldParryClip, hitClip, deathClip);

            UpgradeZombieControllerAndEnemies(hitClip, deathClip);

            var playerObj = GameObject.Find("PlayerCapsule");
            if (playerObj != null)
            {
                ConfigurePlayerGameObject(playerObj, controller);
                EditorSceneManager.MarkSceneDirty(playerObj.scene);
                EditorSceneManager.SaveScene(playerObj.scene);
                Debug.Log($"[PlayerCharacterSetup] Upgraded PlayerCapsule & Enemies (v{TargetSetupVersion}) in '{playerObj.scene.name}' with 8-Direction 2D Locomotion BlendTree, Right-Click Shield Block/Parry, Weapon Swing Trails, and Enemy Hit/Death Reactions.");
            }
            else
            {
                Debug.Log("[PlayerCharacterSetup] Updated PlayerCharacter.controller & ZombieIdle.controller. Open 'Level 1 Base.unity' and run this tool to upgrade PlayerCapsule.");
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static AnimationClip LoadClipAtPath(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath)) return null;
            return AssetDatabase.LoadAllAssetsAtPath(assetPath)
                .OfType<AnimationClip>()
                .FirstOrDefault(c => !c.name.StartsWith("__preview__"));
        }

        [MenuItem("Tools/Paramnesia/Check Player Character Setup")]
        public static void CheckPlayerCharacterSetup()
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            var playerObj = GameObject.Find("PlayerCapsule");
            if (controller == null)
            {
                Debug.LogError("[Player Check] Missing PlayerCharacter.controller. Run Tools > Paramnesia > Setup Player Character & Animations.");
                return;
            }
            if (playerObj == null)
            {
                Debug.LogWarning("[Player Check] No PlayerCapsule found in active scene. Open 'Level 1 Base.unity' first.");
                return;
            }

            var pcc = playerObj.GetComponent<PlayerCharacterController>();
            bool hasAnimator = pcc != null && pcc.characterAnimator != null && pcc.characterAnimator.runtimeAnimatorController != null;
            bool hasWeapons = pcc != null && pcc.thirdPersonWeapons != null && pcc.thirdPersonWeapons.All(w => w != null && !w.isStatic);
            bool hasShield = pcc != null && pcc.leftHandShield != null && !pcc.leftHandShield.isStatic;
            bool hasModularHero = pcc != null && pcc.modularHeroController != null;
            int combatEnemies = Object.FindObjectsByType<EnemyCombatTarget>(FindObjectsSortMode.None).Length;

            Debug.Log($"[Player Check] Scene='{playerObj.scene.name}' | SetupVersion={pcc?.setupVersion} | HasGanzSeCharacter={hasModularHero} | Has8WayAnimator={hasAnimator} | All3WeaponsReady={hasWeapons} | HasShield={hasShield} | CombatReadyEnemies={combatEnemies}");
        }

        private static void UpgradeZombieControllerAndEnemies(AnimationClip hitClip, AnimationClip deathClip)
        {
            var zombieController = AssetDatabase.LoadAssetAtPath<AnimatorController>(ZombieControllerPath);
            if (zombieController != null && (hitClip != null || deathClip != null))
            {
                EnsureParameter(zombieController, "Hit", AnimatorControllerParameterType.Trigger);
                EnsureParameter(zombieController, "Death", AnimatorControllerParameterType.Trigger);
                EnsureParameter(zombieController, "IsDead", AnimatorControllerParameterType.Bool);

                var sm = zombieController.layers[0].stateMachine;
                if (hitClip != null)
                {
                    AddOrUpdateTriggerState(sm, "Zombie Hit", hitClip, "Hit", new Vector3(360f, 120f, 0f), true);
                }
                if (deathClip != null)
                {
                    AddOrUpdateTriggerState(sm, "Zombie Death", deathClip, "Death", new Vector3(360f, 210f, 0f), false);
                }
                EditorUtility.SetDirty(zombieController);
            }

            // Attach EnemyCombatTarget to all EnemyDetection objects in the active scene
            var sceneDetectors = Object.FindObjectsByType<EnemyDetection>(FindObjectsSortMode.None);
            foreach (var det in sceneDetectors)
            {
                if (det == null) continue;
                var combatTarget = det.GetComponent<EnemyCombatTarget>();
                if (combatTarget == null)
                {
                    combatTarget = det.gameObject.AddComponent<EnemyCombatTarget>();
                }
                combatTarget.enemyDetection = det;
                combatTarget.enemyAnimator = det.GetComponentInChildren<Animator>();
                combatTarget.enemyCollider = det.GetComponent<Collider>();
                EditorUtility.SetDirty(det.gameObject);
            }
        }

        private static AnimatorController CreateOrUpdateAnimatorController(
            AnimationClip crouchClip,
            AnimationClip slash1Clip,
            AnimationClip slash2Clip,
            AnimationClip heavy3Clip,
            AnimationClip spearClip,
            AnimationClip shieldParryClip,
            AnimationClip hitClip,
            AnimationClip deathClip)
        {
            if (!File.Exists(ControllerPath) && File.Exists(SourceControllerPath))
            {
                AssetDatabase.CopyAsset(SourceControllerPath, ControllerPath);
                AssetDatabase.ImportAsset(ControllerPath);
            }

            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null)
            {
                controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            }

            EnsureParameter(controller, "Speed", AnimatorControllerParameterType.Float);
            EnsureParameter(controller, "MoveX", AnimatorControllerParameterType.Float);
            EnsureParameter(controller, "MoveZ", AnimatorControllerParameterType.Float);
            EnsureParameter(controller, "MotionSpeed", AnimatorControllerParameterType.Float);
            EnsureParameter(controller, "Grounded", AnimatorControllerParameterType.Bool);
            EnsureParameter(controller, "Jump", AnimatorControllerParameterType.Bool);
            EnsureParameter(controller, "FreeFall", AnimatorControllerParameterType.Bool);
            EnsureParameter(controller, "IsCrouching", AnimatorControllerParameterType.Bool);
            EnsureParameter(controller, "IsBlocking", AnimatorControllerParameterType.Bool);
            EnsureParameter(controller, "ComboStep", AnimatorControllerParameterType.Int);
            EnsureParameter(controller, "WeaponType", AnimatorControllerParameterType.Int);
            EnsureParameter(controller, "Attack", AnimatorControllerParameterType.Trigger);
            EnsureParameter(controller, "Parry", AnimatorControllerParameterType.Trigger);
            EnsureParameter(controller, "Hit", AnimatorControllerParameterType.Trigger);
            EnsureParameter(controller, "Death", AnimatorControllerParameterType.Trigger);

            var baseSm = controller.layers[0].stateMachine;

            AnimationClip kiIdle = LoadClipAtPath(KiCombatIdle1HPath) ?? LoadClipAtPath(KiIdle01Path);

            // Load 8-Way Walk & Run Clips
            AnimationClip walkF = LoadClipAtPath(KiWalkForwardPath);
            AnimationClip walkB = LoadClipAtPath(KiWalkBackwardPath) ?? walkF;
            AnimationClip walkL = LoadClipAtPath(KiWalkLeftPath) ?? walkF;
            AnimationClip walkR = LoadClipAtPath(KiWalkRightPath) ?? walkF;
            AnimationClip walkFL = LoadClipAtPath(KiWalkForwardLeftPath) ?? walkF;
            AnimationClip walkFR = LoadClipAtPath(KiWalkForwardRightPath) ?? walkF;
            AnimationClip walkBL = LoadClipAtPath(KiWalkBackwardLeftPath) ?? walkB;
            AnimationClip walkBR = LoadClipAtPath(KiWalkBackwardRightPath) ?? walkB;

            AnimationClip runF = LoadClipAtPath(KiRunForwardPath);
            AnimationClip runB = LoadClipAtPath(KiRunBackwardPath) ?? runF;
            AnimationClip runL = LoadClipAtPath(KiRunLeftPath) ?? runF;
            AnimationClip runR = LoadClipAtPath(KiRunRightPath) ?? runF;
            AnimationClip runFL = LoadClipAtPath(KiRunForwardLeftPath) ?? runF;
            AnimationClip runFR = LoadClipAtPath(KiRunForwardRightPath) ?? runF;
            AnimationClip runBL = LoadClipAtPath(KiRunBackwardLeftPath) ?? runB;
            AnimationClip runBR = LoadClipAtPath(KiRunBackwardRightPath) ?? runB;

            AnimationClip kiSprint = LoadClipAtPath(KiSprintForwardPath) ?? runF;
            AnimationClip kiJumpStart = LoadClipAtPath(KiJumpBeginPath);
            AnimationClip kiFall = LoadClipAtPath(KiFallPath);
            AnimationClip kiJumpLand = LoadClipAtPath(KiJumpLandPath);

            // Configure 8-Direction 2D Freeform Directional BlendTree (MoveX, MoveZ)
            var locoState = baseSm.states.Select(s => s.state).FirstOrDefault(s => s.name == "Idle Walk Run Blend");
            if (locoState != null && locoState.motion is BlendTree blendTree && kiIdle != null && walkF != null && runF != null)
            {
                blendTree.blendType = BlendTreeType.FreeformDirectional2D;
                blendTree.blendParameter = "MoveX";
                blendTree.blendParameterY = "MoveZ";

                var children = new List<ChildMotion>
                {
                    new ChildMotion { motion = kiIdle, position = new Vector2(0f, 0f), timeScale = 1f },

                    // 8-Direction Walk Ring (radius = 2.0 m/s)
                    new ChildMotion { motion = walkF,  position = new Vector2(0f, 2.0f), timeScale = 1f },
                    new ChildMotion { motion = walkB,  position = new Vector2(0f, -2.0f), timeScale = 1f },
                    new ChildMotion { motion = walkL,  position = new Vector2(-2.0f, 0f), timeScale = 1f },
                    new ChildMotion { motion = walkR,  position = new Vector2(2.0f, 0f), timeScale = 1f },
                    new ChildMotion { motion = walkFL, position = new Vector2(-1.41f, 1.41f), timeScale = 1f },
                    new ChildMotion { motion = walkFR, position = new Vector2(1.41f, 1.41f), timeScale = 1f },
                    new ChildMotion { motion = walkBL, position = new Vector2(-1.41f, -1.41f), timeScale = 1f },
                    new ChildMotion { motion = walkBR, position = new Vector2(1.41f, -1.41f), timeScale = 1f },

                    // 8-Direction Run Ring (radius = 4.0 m/s)
                    new ChildMotion { motion = runF,  position = new Vector2(0f, 4.0f), timeScale = 1f },
                    new ChildMotion { motion = runB,  position = new Vector2(0f, -4.0f), timeScale = 1f },
                    new ChildMotion { motion = runL,  position = new Vector2(-4.0f, 0f), timeScale = 1f },
                    new ChildMotion { motion = runR,  position = new Vector2(4.0f, 0f), timeScale = 1f },
                    new ChildMotion { motion = runFL, position = new Vector2(-2.83f, 2.83f), timeScale = 1f },
                    new ChildMotion { motion = runFR, position = new Vector2(2.83f, 2.83f), timeScale = 1f },
                    new ChildMotion { motion = runBL, position = new Vector2(-2.83f, -2.83f), timeScale = 1f },
                    new ChildMotion { motion = runBR, position = new Vector2(2.83f, -2.83f), timeScale = 1f },

                    // Sprint Forward (6.5 m/s)
                    new ChildMotion { motion = kiSprint, position = new Vector2(0f, 6.5f), timeScale = 1f }
                };

                blendTree.children = children.ToArray();
                EditorUtility.SetDirty(blendTree);
            }

            ReplaceStateClipIfFound(baseSm, "JumpStart", kiJumpStart);
            ReplaceStateClipIfFound(baseSm, "InAir", kiFall);
            ReplaceStateClipIfFound(baseSm, "JumpLand", kiJumpLand);

            AddOrUpdateAttackState(baseSm, "Melee Slash 1", slash1Clip, 1, -1, new Vector3(520f, 320f, 0f), baseSm.defaultState);
            AddOrUpdateAttackState(baseSm, "Melee Slash 2", slash2Clip, 2, -1, new Vector3(520f, 390f, 0f), baseSm.defaultState);
            AddOrUpdateAttackState(baseSm, "Melee Heavy 3", heavy3Clip, 3, -1, new Vector3(520f, 460f, 0f), baseSm.defaultState);
            AddOrUpdateAttackState(baseSm, "Melee Spear Thrust", spearClip, 1, 1, new Vector3(520f, 530f, 0f), baseSm.defaultState);

            if (shieldParryClip != null) AddOrUpdateTriggerState(baseSm, "Shield Parry", shieldParryClip, "Parry", new Vector3(520f, 565f, 0f), true);
            if (hitClip != null) AddOrUpdateTriggerState(baseSm, "Combat Hit", hitClip, "Hit", new Vector3(520f, 615f, 0f), true);
            if (deathClip != null) AddOrUpdateTriggerState(baseSm, "Combat Death", deathClip, "Death", new Vector3(520f, 670f, 0f), false);

            var crouchState = baseSm.states.Select(s => s.state).FirstOrDefault(s => s.name == "Crouch Idle");
            if (crouchState == null)
            {
                crouchState = baseSm.AddState("Crouch Idle", new Vector3(-80f, 400f, 0f));
            }
            crouchState.motion = crouchClip;
            crouchState.iKOnFeet = true;

            AvatarMask upperMask = AssetDatabase.LoadAssetAtPath<AvatarMask>(KiUpperBodyMaskPath);
            if (upperMask != null)
            {
                var layers = controller.layers.ToList();
                int upperIdx = layers.FindIndex(l => l.name == "UpperBodyCombat");
                AnimatorStateMachine upperSm;
                if (upperIdx < 0)
                {
                    upperSm = new AnimatorStateMachine { name = "UpperBodyCombat", hideFlags = HideFlags.HideInHierarchy };
                    AssetDatabase.AddObjectToAsset(upperSm, controller);
                    var newLayer = new AnimatorControllerLayer
                    {
                        name = "UpperBodyCombat",
                        stateMachine = upperSm,
                        avatarMask = upperMask,
                        blendingMode = AnimatorLayerBlendingMode.Override,
                        defaultWeight = 1.0f
                    };
                    layers.Add(newLayer);
                    controller.layers = layers.ToArray();
                }
                else
                {
                    upperSm = layers[upperIdx].stateMachine;
                    layers[upperIdx].avatarMask = upperMask;
                    layers[upperIdx].defaultWeight = 1.0f;
                    controller.layers = layers.ToArray();
                }

                var emptyState = upperSm.states.Select(s => s.state).FirstOrDefault(s => s.name == "Upper Empty");
                if (emptyState == null)
                {
                    emptyState = upperSm.AddState("Upper Empty", new Vector3(200f, 120f, 0f));
                    emptyState.motion = null;
                    upperSm.defaultState = emptyState;
                }

                AddOrUpdateAttackState(upperSm, "Upper Slash 1", slash1Clip, 1, -1, new Vector3(500f, 80f, 0f), emptyState);
                AddOrUpdateAttackState(upperSm, "Upper Slash 2", slash2Clip, 2, -1, new Vector3(500f, 150f, 0f), emptyState);
                AddOrUpdateAttackState(upperSm, "Upper Heavy 3", heavy3Clip, 3, -1, new Vector3(500f, 220f, 0f), emptyState);
                AddOrUpdateAttackState(upperSm, "Upper Spear Thrust", spearClip, 1, 1, new Vector3(500f, 290f, 0f), emptyState);
                if (shieldParryClip != null)
                {
                    AddOrUpdateTriggerState(upperSm, "Upper Shield Parry", shieldParryClip, "Parry", new Vector3(500f, 360f, 0f), true);
                }
            }

            EditorUtility.SetDirty(controller);
            return controller;
        }

        private static void ReplaceStateClipIfFound(AnimatorStateMachine sm, string stateName, AnimationClip newClip)
        {
            if (newClip == null) return;
            var state = sm.states.Select(s => s.state).FirstOrDefault(s => s.name == stateName);
            if (state != null)
            {
                state.motion = newClip;
                EditorUtility.SetDirty(state);
            }
        }

        private static void EnsureParameter(AnimatorController controller, string paramName, AnimatorControllerParameterType type)
        {
            if (controller.parameters.Any(p => p.name == paramName)) return;
            controller.AddParameter(paramName, type);
        }

        private static void AddOrUpdateAttackState(
            AnimatorStateMachine sm,
            string stateName,
            AnimationClip clip,
            int comboStep,
            int requiredWeaponType,
            Vector3 position,
            AnimatorState returnState)
        {
            var state = sm.states.Select(s => s.state).FirstOrDefault(s => s.name == stateName);
            if (state == null)
            {
                state = sm.AddState(stateName, position);
            }
            state.motion = clip;

            foreach (var t in sm.anyStateTransitions.Where(t => t.destinationState == state).ToArray())
            {
                sm.RemoveAnyStateTransition(t);
            }

            var anyTransition = sm.AddAnyStateTransition(state);
            anyTransition.hasExitTime = false;
            anyTransition.duration = 0.08f;
            anyTransition.canTransitionToSelf = false;
            anyTransition.AddCondition(AnimatorConditionMode.If, 0f, "Attack");
            anyTransition.AddCondition(AnimatorConditionMode.Equals, comboStep, "ComboStep");
            if (requiredWeaponType >= 0)
            {
                anyTransition.AddCondition(AnimatorConditionMode.Equals, requiredWeaponType, "WeaponType");
            }
            else if (comboStep == 1)
            {
                anyTransition.AddCondition(AnimatorConditionMode.NotEqual, 1, "WeaponType");
            }

            if (returnState != null && !state.transitions.Any(t => t.destinationState == returnState))
            {
                var backTransition = state.AddTransition(returnState);
                backTransition.hasExitTime = true;
                backTransition.exitTime = 0.88f;
                backTransition.duration = 0.12f;
            }
        }

        private static void AddOrUpdateTriggerState(
            AnimatorStateMachine sm,
            string stateName,
            AnimationClip clip,
            string triggerName,
            Vector3 position,
            bool returnToDefault)
        {
            var state = sm.states.Select(s => s.state).FirstOrDefault(s => s.name == stateName);
            if (state == null)
            {
                state = sm.AddState(stateName, position);
                var anyTransition = sm.AddAnyStateTransition(state);
                anyTransition.hasExitTime = false;
                anyTransition.duration = 0.08f;
                anyTransition.canTransitionToSelf = false;
                anyTransition.AddCondition(AnimatorConditionMode.If, 0f, triggerName);

                if (returnToDefault && sm.defaultState != null)
                {
                    var backTransition = state.AddTransition(sm.defaultState);
                    backTransition.hasExitTime = true;
                    backTransition.exitTime = 0.9f;
                    backTransition.duration = 0.12f;
                }
            }
            state.motion = clip;
        }

        private static void ConfigurePlayerGameObject(GameObject playerObj, AnimatorController controller)
        {
            // 1. Hide placeholder grey Capsule mesh & disable duplicate CapsuleCollider
            Transform oldCapsule = playerObj.transform.Find("Capsule");
            if (oldCapsule != null)
            {
                var mr = oldCapsule.GetComponent<MeshRenderer>();
                if (mr != null) mr.enabled = false;
                var col = oldCapsule.GetComponent<CapsuleCollider>();
                if (col != null) col.enabled = false;
            }

            // 2. Instantiate GanzSe Modular Character as PlayerVisualModel
            Transform existingVisual = playerObj.transform.Find("PlayerVisualModel");
            if (existingVisual != null)
            {
                Object.DestroyImmediate(existingVisual.gameObject);
            }

            var charPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(GanzSePrefabPath)
                             ?? AssetDatabase.LoadAssetAtPath<GameObject>(GanzSeOriginalPrefabPath)
                             ?? AssetDatabase.LoadAssetAtPath<GameObject>(ArmatureModelPath);

            GameObject visualModelObj;
            if (charPrefab != null)
            {
                visualModelObj = (GameObject)PrefabUtility.InstantiatePrefab(charPrefab, playerObj.transform);
                if (PrefabUtility.IsPartOfPrefabInstance(visualModelObj))
                {
                    PrefabUtility.UnpackPrefabInstance(visualModelObj, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                }
                visualModelObj.name = "PlayerVisualModel";
            }
            else
            {
                visualModelObj = new GameObject("PlayerVisualModel");
                visualModelObj.transform.SetParent(playerObj.transform, false);
            }

            visualModelObj.transform.localPosition = Vector3.zero;
            visualModelObj.transform.localRotation = Quaternion.identity;
            visualModelObj.transform.localScale = Vector3.one;

            foreach (var t in visualModelObj.GetComponentsInChildren<Transform>(true))
            {
                GameObjectUtility.SetStaticEditorFlags(t.gameObject, 0);
                t.gameObject.isStatic = false;
            }

            // 3. Configure GanzSe ModularHeroController (1 armor piece per category, gauntlets active on ARMS)
            var modularController = ConfigureGanzSeModularParts(visualModelObj);

            foreach (var smr in visualModelObj.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var mats = smr.sharedMaterials;
                for (int i = 0; i < mats.Length; i++) mats[i] = EnsureUrpMaterial(mats[i]);
                smr.sharedMaterials = mats;
                smr.updateWhenOffscreen = true;
            }

            var animator = visualModelObj.GetComponent<Animator>();
            if (animator == null) animator = visualModelObj.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            var eventReceiver = visualModelObj.GetComponent<PlayerAnimationEventReceiver>();
            if (eventReceiver == null) eventReceiver = visualModelObj.AddComponent<PlayerAnimationEventReceiver>();

            // 4. Attach WeaponSocket_TP inside RightHand palm (used in BOTH 1st-Person and 3rd-Person)
            Transform rightHand = null;
            Transform leftHand = null;
            if (animator.isHuman)
            {
                rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
                leftHand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
            }
            if (rightHand == null)
            {
                rightHand = FindChildRecursive(visualModelObj.transform, "hand_r")
                            ?? FindChildRecursive(visualModelObj.transform, "Right_Hand");
            }
            if (leftHand == null)
            {
                leftHand = FindChildRecursive(visualModelObj.transform, "hand_l")
                           ?? FindChildRecursive(visualModelObj.transform, "Left_Hand");
            }

            Transform weaponSocket = null;
            GameObject[] tpWeapons = new GameObject[3];
            if (rightHand != null)
            {
                weaponSocket = rightHand.Find("WeaponSocket_TP");
                if (weaponSocket != null)
                {
                    Object.DestroyImmediate(weaponSocket.gameObject);
                }
                var socketObj = new GameObject("WeaponSocket_TP");
                weaponSocket = socketObj.transform;
                weaponSocket.SetParent(rightHand, false);

                weaponSocket.localPosition = new Vector3(-0.038f, 0.082f, 0.01f);
                weaponSocket.localRotation = Quaternion.LookRotation(Vector3.up, Vector3.forward);

                tpWeapons = InstantiateWeaponSet(weaponSocket, false);
            }

            // 5. Attach ShieldSocket_TP and Off-Hand Shield onto LeftHand
            Transform shieldSocket = null;
            GameObject shieldObj = null;
            if (leftHand != null)
            {
                shieldSocket = leftHand.Find("ShieldSocket_TP");
                if (shieldSocket != null)
                {
                    Object.DestroyImmediate(shieldSocket.gameObject);
                }
                var sObj = new GameObject("ShieldSocket_TP");
                shieldSocket = sObj.transform;
                shieldSocket.SetParent(leftHand, false);

                // In GanzSe hand_l local space, -X is the outside of the left forearm/wrist
                shieldSocket.localPosition = new Vector3(-0.065f, 0.04f, 0.0f);
                shieldSocket.localRotation = Quaternion.Euler(0f, -90f, 90f);

                shieldObj = InstantiateOffHandShield(shieldSocket);
            }

            // Clean up any old disembodied FirstPersonWeaponPivot
            var fpc = playerObj.GetComponent<FirstPersonController>();
            Transform cameraRoot = fpc != null && fpc.CinemachineCameraTarget != null
                ? fpc.CinemachineCameraTarget.transform
                : playerObj.transform.Find("PlayerCameraRoot");

            if (cameraRoot != null)
            {
                Transform oldFpPivot = cameraRoot.Find("FirstPersonWeaponPivot");
                if (oldFpPivot != null)
                {
                    Object.DestroyImmediate(oldFpPivot.gameObject);
                }
            }

            // 6. Wire up PlayerCharacterController component
            var pcc = playerObj.GetComponent<PlayerCharacterController>();
            if (pcc == null) pcc = playerObj.AddComponent<PlayerCharacterController>();

            eventReceiver.owner = pcc;
            pcc.setupVersion = TargetSetupVersion;
            pcc.firstPersonController = fpc;
            pcc.characterController = playerObj.GetComponent<CharacterController>();
            pcc.starterInputs = playerObj.GetComponent<StarterAssetsInputs>();
            pcc.characterAnimator = animator;
            pcc.characterModelRoot = visualModelObj.transform;
            pcc.modularHeroController = modularController;
            pcc.cameraRoot = cameraRoot;
            pcc.mainCamera = Camera.main;
            pcc.rightHandWeaponSocket = weaponSocket;
            pcc.leftHandShieldSocket = shieldSocket;
            pcc.leftHandShield = shieldObj;
            pcc.shieldEquipped = true;
            pcc.firstPersonWeaponPivot = null;
            pcc.thirdPersonWeapons = tpWeapons;
            pcc.firstPersonWeapons = new GameObject[0];
            pcc.thirdPersonCameraDistance = 2.6f;
            pcc.thirdPersonShoulderOffset = new Vector3(0.45f, 0.25f, 0f);

            var followCamObj = GameObject.Find("PlayerFollowCamera");
            if (followCamObj != null)
            {
                var followCam = followCamObj.GetComponent<CinemachineThirdPersonFollow>();
                pcc.followCameraComponent = followCam;
                if (followCam != null)
                {
                    followCam.CameraDistance = pcc.isThirdPersonView ? pcc.thirdPersonCameraDistance : 0f;
                    followCam.ShoulderOffset = pcc.isThirdPersonView ? pcc.thirdPersonShoulderOffset : Vector3.zero;
                    followCam.VerticalArmLength = pcc.isThirdPersonView ? 0.25f : 0f;
                    EditorUtility.SetDirty(followCam);
                }
            }

            List<AudioClip> footsteps = new List<AudioClip>();
            for (int i = 1; i <= 10; i++)
            {
                string clipPath = $"Assets/Starter Assets/Runtime/ThirdPersonController/Character/Sfx/Player_Footstep_{i:00}.wav";
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(clipPath);
                if (clip != null) footsteps.Add(clip);
            }
            pcc.footstepAudioClips = footsteps.ToArray();
            pcc.landingAudioClip = AssetDatabase.LoadAssetAtPath<AudioClip>(
                "Assets/Starter Assets/Runtime/ThirdPersonController/Character/Sfx/Player_Land.wav");

            pcc.ApplyCameraViewMode();

            EditorUtility.SetDirty(playerObj);
            EditorUtility.SetDirty(pcc);
        }

        private static GameObject InstantiateOffHandShield(Transform shieldSocket)
        {
            var shieldPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ShieldPrefabPath)
                               ?? AssetDatabase.LoadAssetAtPath<GameObject>(FallbackShieldPrefabPath);
            if (shieldPrefab == null) return null;

            GameObject holder = new GameObject("Shield_OffHand");
            holder.transform.SetParent(shieldSocket, false);
            holder.transform.localPosition = Vector3.zero;
            holder.transform.localRotation = Quaternion.identity;
            holder.transform.localScale = Vector3.one;

            GameObject meshObj = (GameObject)PrefabUtility.InstantiatePrefab(shieldPrefab, holder.transform);
            if (PrefabUtility.IsPartOfPrefabInstance(meshObj))
            {
                PrefabUtility.UnpackPrefabInstance(meshObj, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            }

            foreach (var t in holder.GetComponentsInChildren<Transform>(true))
            {
                GameObjectUtility.SetStaticEditorFlags(t.gameObject, 0);
                t.gameObject.isStatic = false;
                t.gameObject.layer = 0;
            }
            foreach (var col in holder.GetComponentsInChildren<Collider>(true))
            {
                Object.DestroyImmediate(col);
            }

            var mf = meshObj.GetComponentInChildren<MeshFilter>();
            if (mf != null && mf.sharedMesh != null)
            {
                Bounds b = mf.sharedMesh.bounds;
                float maxDim = Mathf.Max(b.size.x, b.size.y, b.size.z);
                float scale = maxDim > 0.001f ? (0.56f / maxDim) : 1f;
                meshObj.transform.localScale = Vector3.one * scale;
                meshObj.transform.localRotation = Quaternion.identity;
                meshObj.transform.localPosition = -b.center * scale;
            }

            Material ironMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/BrokenVector/LowPolyDungeon/Materials/Metal_Iron.mat");
            Material woodMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/BrokenVector/LowPolyDungeon/Materials/Wood_Polished.mat");

            foreach (var r in holder.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] == null || mats[i].shader == null || !mats[i].shader.name.StartsWith("Universal Render Pipeline"))
                    {
                        mats[i] = (i == 0 && woodMat != null) ? woodMat : (ironMat != null ? ironMat : EnsureUrpMaterial(mats[i]));
                    }
                }
                r.sharedMaterials = mats;
            }

            return holder;
        }

        private static ModularHeroController ConfigureGanzSeModularParts(GameObject visualModelObj)
        {
            Transform armorRoot = visualModelObj.transform.Find("ARMOR PARTS");
            Transform faceRoot = visualModelObj.transform.Find("FACE DETAILS PARTS")
                                 ?? (visualModelObj.transform.childCount >= 4 ? visualModelObj.transform.GetChild(3) : null);

            if (armorRoot == null && faceRoot == null) return null;

            var modularController = visualModelObj.GetComponent<ModularHeroController>();
            if (modularController == null)
            {
                modularController = visualModelObj.AddComponent<ModularHeroController>();
            }
            modularController.armorPartsRoot = armorRoot;
            modularController.facePartsRoot = faceRoot;
            modularController.showHelmet = false;

            if (armorRoot != null)
            {
                foreach (Transform category in armorRoot)
                {
                    SelectSingleChildVariation(category, 0);
                }
            }

            if (faceRoot != null)
            {
                foreach (Transform category in faceRoot)
                {
                    SelectSingleChildVariation(category, 0);
                }
            }

            modularController.ToggleHelmet();
            EditorUtility.SetDirty(modularController);
            return modularController;
        }

        private static void SelectSingleChildVariation(Transform category, int preferredIndex)
        {
            if (category == null || category.childCount == 0) return;
            int idx = Mathf.Clamp(preferredIndex, 0, category.childCount - 1);
            for (int i = 0; i < category.childCount; i++)
            {
                category.GetChild(i).gameObject.SetActive(i == idx);
            }
        }

        private static GameObject[] InstantiateWeaponSet(Transform parentSocket, bool isFirstPerson)
        {
            GameObject[] result = new GameObject[3];
            float[] targetWeaponLengths = new float[] { 0.88f, 1.25f, 0.78f }; // Sword, Spear, Axe length in meters
            float[] tipHeights = new float[] { 0.72f, 1.05f, 0.62f };

            Material ironMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/BrokenVector/LowPolyDungeon/Materials/Metal_Iron.mat");
            Material woodMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/BrokenVector/LowPolyDungeon/Materials/Wood_Polished.mat");

            for (int i = 0; i < 3; i++)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(WeaponPrefabPaths[i])
                             ?? AssetDatabase.LoadAssetAtPath<GameObject>(FallbackWeaponPrefabPaths[i]);

                GameObject holder = new GameObject(WeaponNames[i] + (isFirstPerson ? "_FP" : "_TP"));
                holder.transform.SetParent(parentSocket, false);
                holder.transform.localPosition = Vector3.zero;
                holder.transform.localRotation = Quaternion.identity;
                holder.transform.localScale = Vector3.one;

                GameObject meshObj;
                if (prefab != null)
                {
                    meshObj = (GameObject)PrefabUtility.InstantiatePrefab(prefab, holder.transform);
                    if (PrefabUtility.IsPartOfPrefabInstance(meshObj))
                    {
                        PrefabUtility.UnpackPrefabInstance(meshObj, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                    }
                }
                else
                {
                    meshObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    meshObj.transform.SetParent(holder.transform, false);
                }

                foreach (var t in holder.GetComponentsInChildren<Transform>(true))
                {
                    GameObjectUtility.SetStaticEditorFlags(t.gameObject, 0);
                    t.gameObject.isStatic = false;
                    t.gameObject.layer = 0;
                }

                foreach (var col in holder.GetComponentsInChildren<Collider>(true))
                {
                    Object.DestroyImmediate(col);
                }

                meshObj.transform.localPosition = Vector3.zero;
                meshObj.transform.localRotation = Quaternion.identity;
                meshObj.transform.localScale = Vector3.one;

                int bladeSubmeshIdx = 0;
                int handleSubmeshIdx = 1;

                var mf = meshObj.GetComponentInChildren<MeshFilter>();
                if (mf != null && mf.sharedMesh != null)
                {
                    Mesh m = mf.sharedMesh;
                    Bounds meshBounds = m.bounds;
                    Vector3 size = meshBounds.size;
                    float maxDim = Mathf.Max(size.x, size.y, size.z);

                    // 1. Align longest axis with holder +Y (blade/shaft axis)
                    Quaternion alignRot = Quaternion.identity;
                    if (size.z >= size.x && size.z >= size.y)
                    {
                        alignRot = Quaternion.Euler(90f, 0f, 0f); // -Z -> +Y (UP)
                    }
                    else if (size.x >= size.y && size.x >= size.z)
                    {
                        alignRot = Quaternion.Euler(0f, 0f, 90f);
                    }

                    if (i == 2) // Rotate Axe blade to face forward (+Z)
                    {
                        alignRot = Quaternion.Euler(0f, 90f, 0f) * alignRot;
                    }

                    // 2. Verify blade vs handle using submesh vertex positions so NO weapon can ever be upside down!
                    if (m.subMeshCount >= 2)
                    {
                        Vector3 avg0 = GetSubmeshVertexAverage(m, 0, alignRot, out float spanY0);
                        Vector3 avg1 = GetSubmeshVertexAverage(m, 1, alignRot, out float spanY1);

                        if (i == 0)
                        {
                            bladeSubmeshIdx = 0;
                            handleSubmeshIdx = 1;
                        }
                        else if (i == 1)
                        {
                            bladeSubmeshIdx = 1;
                            handleSubmeshIdx = 0;
                        }
                        else
                        {
                            bladeSubmeshIdx = (spanY0 <= spanY1) ? 0 : 1;
                            handleSubmeshIdx = 1 - bladeSubmeshIdx;
                        }

                        Vector3 bladeCenter = (bladeSubmeshIdx == 0) ? avg0 : avg1;
                        Vector3 handleCenter = (handleSubmeshIdx == 0) ? avg0 : avg1;

                        if (bladeCenter.y < handleCenter.y)
                        {
                            alignRot = Quaternion.Euler(180f, 0f, 0f) * alignRot;
                            avg0 = GetSubmeshVertexAverage(m, 0, alignRot, out spanY0);
                            avg1 = GetSubmeshVertexAverage(m, 1, alignRot, out spanY1);
                            bladeCenter = (bladeSubmeshIdx == 0) ? avg0 : avg1;
                            handleCenter = (handleSubmeshIdx == 0) ? avg0 : avg1;
                        }

                        float uniformScale = maxDim > 0.001f ? (targetWeaponLengths[i] / maxDim) : 1f;
                        meshObj.transform.localRotation = alignRot;
                        meshObj.transform.localScale = Vector3.one * uniformScale;

                        if (i == 0)
                        {
                            meshObj.transform.localPosition = -handleCenter * uniformScale;
                        }
                        else
                        {
                            Vector3 rotatedCenter = (alignRot * meshBounds.center) * uniformScale;
                            meshObj.transform.localPosition = -rotatedCenter + new Vector3(0f, targetWeaponLengths[i] * 0.22f, 0f);
                        }
                    }
                    else
                    {
                        float uniformScale = maxDim > 0.001f ? (targetWeaponLengths[i] / maxDim) : 1f;
                        meshObj.transform.localRotation = alignRot;
                        meshObj.transform.localScale = Vector3.one * uniformScale;
                        Vector3 rotatedCenter = (alignRot * meshBounds.center) * uniformScale;
                        meshObj.transform.localPosition = -rotatedCenter + new Vector3(0f, targetWeaponLengths[i] * 0.25f, 0f);
                    }
                }

                // Ensure valid URP materials (Iron on blade/head submesh, Wood on handle/shaft submesh)
                foreach (var renderer in holder.GetComponentsInChildren<Renderer>(true))
                {
                    var mats = renderer.sharedMaterials;
                    for (int mIdx = 0; mIdx < mats.Length; mIdx++)
                    {
                        if (mats[mIdx] == null || mats[mIdx].shader == null || !mats[mIdx].shader.name.StartsWith("Universal Render Pipeline"))
                        {
                            if (ironMat != null && woodMat != null)
                            {
                                mats[mIdx] = (mIdx == bladeSubmeshIdx) ? ironMat : woodMat;
                            }
                            else
                            {
                                mats[mIdx] = EnsureUrpMaterial(mats[mIdx]);
                            }
                        }
                    }
                    renderer.sharedMaterials = mats;
                }

                // Attach BladeTrail child for weapon swing trail rendering
                var trailObj = new GameObject("BladeTrail");
                trailObj.transform.SetParent(holder.transform, false);
                trailObj.transform.localPosition = new Vector3(0f, tipHeights[i], 0f);
                var trail = trailObj.AddComponent<TrailRenderer>();
                trail.time = 0.16f;
                trail.minVertexDistance = 0.02f;
                trail.startWidth = 0.14f;
                trail.endWidth = 0.0f;
                trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                trail.receiveShadows = false;
                trail.emitting = false;

                holder.SetActive(i == 0);
                result[i] = holder;
            }

            return result;
        }

        private static Vector3 GetSubmeshVertexAverage(Mesh m, int submeshIndex, Quaternion rot, out float spanY)
        {
            var subMesh = m.GetSubMesh(submeshIndex);
            if (subMesh.bounds.size.sqrMagnitude > 0.00001f)
            {
                Vector3 c = rot * subMesh.bounds.center;
                Vector3 ext = subMesh.bounds.extents;
                float minB = float.MaxValue;
                float maxB = float.MinValue;
                for (int sx = -1; sx <= 1; sx += 2)
                for (int sy = -1; sy <= 1; sy += 2)
                for (int sz = -1; sz <= 1; sz += 2)
                {
                    Vector3 corner = rot * (subMesh.bounds.center + new Vector3(ext.x * sx, ext.y * sy, ext.z * sz));
                    if (corner.y < minB) minB = corner.y;
                    if (corner.y > maxB) maxB = corner.y;
                }
                spanY = maxB - minB;
                return c;
            }

            if (!m.isReadable)
            {
                spanY = 0f;
                return rot * m.bounds.center;
            }

            Vector3[] verts = m.vertices;
            int[] tris = m.GetTriangles(submeshIndex);
            if (tris == null || tris.Length == 0 || verts == null || verts.Length == 0)
            {
                spanY = 0f;
                return rot * m.bounds.center;
            }

            Vector3 sum = Vector3.zero;
            float minY = float.MaxValue;
            float maxY = float.MinValue;
            int count = 0;

            for (int i = 0; i < tris.Length; i++)
            {
                Vector3 v = rot * verts[tris[i]];
                sum += v;
                if (v.y < minY) minY = v.y;
                if (v.y > maxY) maxY = v.y;
                count++;
            }

            spanY = maxY - minY;
            return sum / Mathf.Max(1, count);
        }

        private static Material EnsureUrpMaterial(Material mat)
        {
            if (mat == null) return null;
            if (mat.shader != null && mat.shader.name.StartsWith("Universal Render Pipeline")) return mat;

            string safeName = string.Join("_", mat.name.Split(Path.GetInvalidFileNameChars()));
            string urpMatPath = $"Assets/Materials/Characters/{safeName}_URP.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(urpMatPath);
            if (existing != null) return existing;

            var urpShader = Shader.Find("Universal Render Pipeline/Lit");
            if (urpShader == null) return mat;

            var newMat = new Material(urpShader) { name = safeName + "_URP" };
            Texture mainTex = mat.HasProperty("_BaseMap") ? mat.GetTexture("_BaseMap") :
                              (mat.HasProperty("_MainTex") ? mat.GetTexture("_MainTex") : null);
            Color baseCol = mat.HasProperty("_BaseColor") ? mat.GetColor("_BaseColor") :
                            (mat.HasProperty("_Color") ? mat.GetColor("_Color") : Color.white);

            if (mainTex != null) newMat.SetTexture("_BaseMap", mainTex);
            newMat.SetColor("_BaseColor", baseCol);
            newMat.SetFloat("_Smoothness", 0.3f);

            AssetDatabase.CreateAsset(newMat, urpMatPath);
            return newMat;
        }

        private static void ImportCustomHumanoidFbxFolder()
        {
            if (!Directory.Exists(SourceAnimFolder)) return;

            foreach (string fbxPath in Directory.GetFiles(SourceAnimFolder, "*.fbx"))
            {
                string assetPath = fbxPath.Replace("\\", "/");
                var importer = AssetImporter.GetAtPath(assetPath) as ModelImporter;
                if (importer == null) continue;

                bool needsReimport = false;
                if (importer.animationType != ModelImporterAnimationType.Human)
                {
                    importer.animationType = ModelImporterAnimationType.Human;
                    importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                    needsReimport = true;
                }

                if (needsReimport)
                {
                    importer.SaveAndReimport();
                }
            }
        }

        private static AnimationClip CreateOrUpdateCrouchClip(string path)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            bool isNew = false;
            if (clip == null)
            {
                clip = new AnimationClip { name = Path.GetFileNameWithoutExtension(path) };
                isNew = true;
            }
            clip.ClearCurves();

            AnimationCurve breatheCurve = new AnimationCurve(
                new Keyframe(0f, -0.38f),
                new Keyframe(1f, -0.36f),
                new Keyframe(2f, -0.38f));
            clip.SetCurve("", typeof(Transform), "localPosition.y", breatheCurve);

            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = true;
            AnimationUtility.SetAnimationClipSettings(clip, settings);

            if (isNew) AssetDatabase.CreateAsset(clip, path);
            else EditorUtility.SetDirty(clip);
            return clip;
        }

        private static AnimationClip CreateOrUpdateAttackClip(string path, int comboStep)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            bool isNew = false;
            if (clip == null)
            {
                clip = new AnimationClip { name = Path.GetFileNameWithoutExtension(path) };
                isNew = true;
            }
            clip.ClearCurves();

            float duration = comboStep == 3 ? 0.55f : 0.38f;
            var yawCurve = new AnimationCurve(
                new Keyframe(0f, 0f),
                new Keyframe(duration * 0.5f, -45f),
                new Keyframe(duration, 0f));
            clip.SetCurve("", typeof(Transform), "localEulerAngles.y", yawCurve);

            if (isNew) AssetDatabase.CreateAsset(clip, path);
            else EditorUtility.SetDirty(clip);
            return clip;
        }

        private static Transform FindChildRecursive(Transform parent, string targetName)
        {
            foreach (Transform child in parent)
            {
                if (child.name == targetName) return child;
                var found = FindChildRecursive(child, targetName);
                if (found != null) return found;
            }
            return null;
        }
    }
}
