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
        public const int TargetSetupVersion = 13;

        private const string AnimFolder = "Assets/Animations/Player";
        private const string SourceAnimFolder = "Assets/Animations/Player/Source";
        private const string ControllerPath = AnimFolder + "/PlayerCharacter.controller";
        private const string UpperBodyMaskPath = AnimFolder + "/PlayerUpperBody.mask";
        private const string FpRightArmMeshAssetPath = "Assets/Prefabs/Player/PlayerFPRightArmMesh.asset";
        private const string SourceControllerPath = "Assets/Starter Assets/Runtime/ThirdPersonController/Character/Animations/StarterAssetsThirdPerson.controller";
        private const string ZombieControllerPath = "Assets/Animations/Zombies/ZombieIdle.controller";

        // Polygon Apocalypse Female Character Model & Rig
        private const string PolygonCharactersModelPath = "Assets/PolygonApocalypse/Models/Characters.fbx";
        private const string PolygonCoolFemalePrefabPath = "Assets/PolygonApocalypse/Prefabs/Characters/SM_Chr_Cool_Female_01.prefab";
        private const string PolygonSoldierFemalePrefabPath = "Assets/PolygonApocalypse/Prefabs/Characters/SM_Chr_Soldier_Female_01.prefab";
        private const string PolygonScoutFemalePrefabPath = "Assets/PolygonApocalypse/Prefabs/Characters/SM_Chr_Scout_Female_01.prefab";

        // Mixamo Action Animation Paths in Assets/Animations/Player/Source/
        private const string MixamoIdlePath = SourceAnimFolder + "/Breathing_Idle.fbx";
        private const string MixamoWalkPath = SourceAnimFolder + "/Walking.fbx";
        private const string MixamoRunPath = SourceAnimFolder + "/Running.fbx";
        private const string MixamoJumpPath = SourceAnimFolder + "/Jumping.fbx";
        private const string MixamoCrouchIdlePath = SourceAnimFolder + "/Crouch_Idle.fbx";
        private const string MixamoCrouchWalkPath = SourceAnimFolder + "/Crouched_Walking.fbx";
        private const string MixamoSwordAttackPath = SourceAnimFolder + "/Sword_And_Shield_Slash.fbx";
        private const string MixamoSpearAttackPath = SourceAnimFolder + "/Standing_Melee_Attack_Horizontal.fbx";
        private const string MixamoAxeAttackPath = SourceAnimFolder + "/Standing_Melee_Attack_Downward.fbx";

        // Fallback Character Prefabs (Used until Polygon Apocalypse Woman is imported)
        private const string GanzSePrefabPath = "Assets/URP GanzSe Free Modular Character Pack/Prefabs/Modular Character/GanzSe Free Modular Character Update 1_1.prefab";
        private const string GanzSeOriginalPrefabPath = "Assets/URP GanzSe Free Modular Character Pack/Prefabs/Modular Character/GanzSe Free Modular Character Original 1_0.prefab";
        private const string ArmatureModelPath = "Assets/Starter Assets/Runtime/ThirdPersonController/Character/Models/Armature.fbx";

        // Fallback Kevin Iglesias Female Humanoid Clips
        private const string KiFemaleIdlePath = "Assets/Kevin Iglesias/Human Animations/Animations/Female/Idles/HumanF@Idle01.fbx";
        private const string KiFemaleWalkPath = "Assets/Kevin Iglesias/Human Animations/Animations/Female/Movement/Walk/HumanF@Walk01_Forward.fbx";
        private const string KiFemaleRunPath = "Assets/Kevin Iglesias/Human Animations/Animations/Female/Movement/Run/HumanF@Run01_Forward.fbx";
        private const string KiFemaleJumpBeginPath = "Assets/Kevin Iglesias/Human Animations/Animations/Female/Movement/Jump/HumanF@Jump01 - Begin.fbx";
        private const string KiFemaleFallPath = "Assets/Kevin Iglesias/Human Animations/Animations/Female/Movement/Jump/HumanF@Fall01.fbx";
        private const string KiFemaleJumpLandPath = "Assets/Kevin Iglesias/Human Animations/Animations/Female/Movement/Jump/HumanF@Jump01 - Land.fbx";
        private const string KiFemaleAttack1HPath = "Assets/Kevin Iglesias/Human Animations/Animations/Female/Combat/1H/HumanF@Attack1H01_R.fbx";
        private const string KiFemaleAttackPolearmPath = "Assets/Kevin Iglesias/Human Animations/Animations/Female/Combat/Polearm/HumanF@AttackPolearm01.fbx";
        private const string KiFemaleAttack2HPath = "Assets/Kevin Iglesias/Human Animations/Animations/Female/Combat/2H/HumanF@Attack2H01.fbx";
        private const string KiCombatDamagePath = "Assets/Kevin Iglesias/Human Animations/Animations/Female/Combat/HumanF@CombatDamage01.fbx";
        private const string KiDeathPath = "Assets/Kevin Iglesias/Human Animations/Animations/Female/Combat/HumanF@Death01.fbx";
        private const string KiUpperBodyMaskPath = "Assets/Kevin Iglesias/Human Animations/Models/Avatar Masks/Human Body Upper Mask.mask";

        // Weapons (1 = Sword, 2 = Spear/Polearm, 3 = Battle Axe)
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

        private static readonly string[] WeaponNames = new string[] { "Weapon_Sword", "Weapon_Spear", "Weapon_Axe" };

        static PlayerCharacterSetup()
        {
            EditorApplication.delayCall += AutoInitializeIfNeeded;
            EditorApplication.projectChanged += () =>
            {
                EditorApplication.delayCall += AutoInitializeIfNeeded;
            };
            EditorSceneManager.sceneOpened += (scene, mode) =>
            {
                EditorApplication.delayCall += AutoInitializeIfNeeded;
            };
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

            GameObject polygonWomanPrefab = FindPolygonApocalypseWomanPrefab();

            if (activePlayer != null)
            {
                var pcc = activePlayer.GetComponent<PlayerCharacterController>();
                var receiver = activePlayer.GetComponentInChildren<PlayerAnimationEventReceiver>(true);
                bool needsPolygonSwap = polygonWomanPrefab != null &&
                                        pcc != null &&
                                        pcc.modularHeroController != null; // Still using GanzSe instead of Polygon Woman
                sceneNeedsSetup = pcc == null || pcc.setupVersion < TargetSetupVersion || receiver == null || needsPolygonSwap;
            }

            if (controllerMissing || sceneNeedsSetup)
            {
                SetupPlayerCharacterAndAnimations();
            }
        }

        private static GameObject FindPolygonApocalypseWomanPrefab()
        {
            string[] directPaths = new[]
            {
                PolygonCoolFemalePrefabPath,
                PolygonSoldierFemalePrefabPath,
                PolygonScoutFemalePrefabPath
            };

            foreach (string directPath in directPaths)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(directPath);
                if (prefab != null) return prefab;
            }

            // Preferred Polygon Apocalypse female character names
            string[] preferredNames = new[]
            {
                "SM_Chr_Cool_Female_01",
                "SM_Chr_Soldier_Female_01",
                "SM_Chr_Scout_Female_01",
                "SM_Chr_Teen_Female_01",
                "SM_Chr_Mechanic_Female_01",
                "SM_Chr_Eastern_Female_01",
                "SM_Chr_Punk_Female_01",
                "Character_Female_Survivor_01"
            };

            string[] guids = AssetDatabase.FindAssets("Female t:Prefab");
            List<string> candidatePaths = new List<string>();
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(path)) continue;
                if (path.Contains("PolygonZombies") || path.Contains("Prefabs/Enemies") || path.Contains("Dummy") || path.Contains("Chr_Attach") || path.Contains("Zombie"))
                    continue;
                if (path.Contains("PolygonApocalypse") || path.Contains("Character_Female") || path.Contains("Apocalypse"))
                {
                    candidatePaths.Add(path);
                }
            }

            foreach (string pref in preferredNames)
            {
                string match = candidatePaths.FirstOrDefault(p => p.Contains(pref));
                if (!string.IsNullOrEmpty(match))
                {
                    return AssetDatabase.LoadAssetAtPath<GameObject>(match);
                }
            }

            if (candidatePaths.Count > 0)
            {
                return AssetDatabase.LoadAssetAtPath<GameObject>(candidatePaths[0]);
            }

            return null;
        }

        private static void EnsurePolygonCharacterHumanoidRig()
        {
            if (File.Exists(PolygonCharactersModelPath))
            {
                var importer = AssetImporter.GetAtPath(PolygonCharactersModelPath) as ModelImporter;
                if (importer != null)
                {
                    bool needsReimport = false;
                    if (importer.animationType != ModelImporterAnimationType.Human)
                    {
                        importer.animationType = ModelImporterAnimationType.Human;
                        importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                        needsReimport = true;
                    }
                    if (!importer.isReadable)
                    {
                        importer.isReadable = true;
                        needsReimport = true;
                    }

                    // Fix Synty Unity 5 Hips->Root mapping so Mixamo animations rotate Hips instead of Root
                    HumanDescription desc = importer.humanDescription;
                    if (desc.human != null && desc.human.Length > 0)
                    {
                        HumanBone[] humanBones = desc.human;
                        for (int i = 0; i < humanBones.Length; i++)
                        {
                            if (humanBones[i].humanName == "Hips" && humanBones[i].boneName != "Hips")
                            {
                                humanBones[i].boneName = "Hips";
                                desc.human = humanBones;
                                importer.humanDescription = desc;
                                needsReimport = true;
                                break;
                            }
                        }
                    }

                    if (needsReimport)
                    {
                        importer.SaveAndReimport();
                    }
                    else
                    {
                        var testMesh = AssetDatabase.LoadAssetAtPath<Mesh>(PolygonCharactersModelPath);
                        if (testMesh != null && !testMesh.isReadable)
                        {
                            AssetDatabase.ImportAsset(PolygonCharactersModelPath, ImportAssetOptions.ForceUpdate);
                        }
                    }
                }
            }

            // Ensure hair, backpack, and pouch attachment FBX models are imported before instantiating SM_Chr_Cool_Female_01
            string[] attachModels = new[]
            {
                "Assets/PolygonApocalypse/Models/SM_Chr_Attach_Cool_Female_Hair_01.fbx",
                "Assets/PolygonApocalypse/Models/SM_Chr_Attach_Backpack_Large_Extras_01.fbx",
                "Assets/PolygonApocalypse/Models/SM_Chr_Attach_Pouch_01.fbx"
            };
            foreach (string attachPath in attachModels)
            {
                if (File.Exists(attachPath) && AssetDatabase.LoadAssetAtPath<Mesh>(attachPath) == null)
                {
                    AssetDatabase.ImportAsset(attachPath, ImportAssetOptions.ForceUpdate);
                }
            }

            if (File.Exists(PolygonCoolFemalePrefabPath))
            {
                AssetDatabase.ImportAsset(PolygonCoolFemalePrefabPath, ImportAssetOptions.ForceUpdate);
            }
        }

        [MenuItem("Tools/Paramnesia/Setup Player Character & Animations")]
        public static void SetupPlayerCharacterAndAnimations()
        {
            Directory.CreateDirectory(AnimFolder);
            Directory.CreateDirectory(SourceAnimFolder);
            Directory.CreateDirectory("Assets/Materials/Characters");
            Directory.CreateDirectory("Assets/Prefabs/Player");

            EnsurePolygonCharacterHumanoidRig();
            ImportMixamoHumanoidFbxFolder();

            AnimationClip idleClip = LoadClipAtPath(MixamoIdlePath) ?? LoadClipAtPath(KiFemaleIdlePath);
            AnimationClip walkClip = LoadClipAtPath(MixamoWalkPath) ?? LoadClipAtPath(KiFemaleWalkPath);
            AnimationClip runClip = LoadClipAtPath(MixamoRunPath) ?? LoadClipAtPath(KiFemaleRunPath);
            AnimationClip jumpClip = LoadClipAtPath(MixamoJumpPath) ?? LoadClipAtPath(KiFemaleJumpBeginPath);
            AnimationClip fallClip = LoadClipAtPath(KiFemaleFallPath) ?? jumpClip;
            AnimationClip landClip = LoadClipAtPath(KiFemaleJumpLandPath);

            AnimationClip crouchIdleClip = LoadClipAtPath(MixamoCrouchIdlePath) ?? CreateOrUpdateCrouchClip(AnimFolder + "/Player_Crouch_Idle.anim");
            AnimationClip crouchWalkClip = LoadClipAtPath(MixamoCrouchWalkPath) ?? walkClip;

            AnimationClip swordAttackClip = LoadClipAtPath(MixamoSwordAttackPath) ?? LoadClipAtPath(KiFemaleAttack1HPath);
            AnimationClip spearAttackClip = LoadClipAtPath(MixamoSpearAttackPath) ?? LoadClipAtPath(KiFemaleAttackPolearmPath);
            AnimationClip axeAttackClip = LoadClipAtPath(MixamoAxeAttackPath) ?? LoadClipAtPath(KiFemaleAttack2HPath);

            AnimationClip hitClip = LoadClipAtPath(KiCombatDamagePath);
            AnimationClip deathClip = LoadClipAtPath(KiDeathPath);

            AnimatorController controller = CreateOrUpdateAnimatorController(
                idleClip, walkClip, runClip, jumpClip, fallClip, landClip,
                crouchIdleClip, crouchWalkClip,
                swordAttackClip, spearAttackClip, axeAttackClip,
                hitClip, deathClip);

            var playerObj = GameObject.Find("PlayerCapsule");
            if (playerObj != null)
            {
                ConfigurePlayerGameObject(playerObj, controller);
                EditorSceneManager.MarkSceneDirty(playerObj.scene);
                EditorSceneManager.SaveScene(playerObj.scene);
                Debug.Log($"[PlayerCharacterSetup] Upgraded PlayerCapsule (v{TargetSetupVersion}) in '{playerObj.scene.name}' with Mixamo Action Animations (Idle, Walk, Run, Jump, Crouch, and distinct Sword/Spear/Axe Attacks), Attack Spam Buffer, Camera Pan/Tilt Clamping, and No Shield/Stamina/Dash.");
            }
            else
            {
                Debug.Log("[PlayerCharacterSetup] Updated PlayerCharacter.controller with Mixamo animations.");
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static AnimationClip LoadClipAtPath(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath) || !File.Exists(assetPath)) return null;
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
                Debug.LogWarning("[Player Check] No PlayerCapsule found in active scene.");
                return;
            }

            var pcc = playerObj.GetComponent<PlayerCharacterController>();
            bool hasAnimator = pcc != null && pcc.characterAnimator != null && pcc.characterAnimator.runtimeAnimatorController != null;
            bool hasWeapons = pcc != null && pcc.thirdPersonWeapons != null && pcc.thirdPersonWeapons.All(w => w != null && !w.isStatic);
            bool hasMixamoClips = LoadClipAtPath(MixamoIdlePath) != null && LoadClipAtPath(MixamoSwordAttackPath) != null;

            Debug.Log($"[Player Check] Scene='{playerObj.scene.name}' | SetupVersion={pcc?.setupVersion} | HasMixamoClips={hasMixamoClips} | HasAnimator={hasAnimator} | All3WeaponsReady={hasWeapons}");
        }

        private static AnimatorController CreateOrUpdateAnimatorController(
            AnimationClip idleClip,
            AnimationClip walkClip,
            AnimationClip runClip,
            AnimationClip jumpClip,
            AnimationClip fallClip,
            AnimationClip landClip,
            AnimationClip crouchIdleClip,
            AnimationClip crouchWalkClip,
            AnimationClip swordAttackClip,
            AnimationClip spearAttackClip,
            AnimationClip axeAttackClip,
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
            EnsureParameter(controller, "WeaponType", AnimatorControllerParameterType.Int);
            EnsureParameter(controller, "Attack", AnimatorControllerParameterType.Trigger);
            EnsureParameter(controller, "FullBodyAttack", AnimatorControllerParameterType.Trigger);
            EnsureParameter(controller, "Hit", AnimatorControllerParameterType.Trigger);
            EnsureParameter(controller, "Death", AnimatorControllerParameterType.Trigger);

            var baseSm = controller.layers[0].stateMachine;

            // 1. Standing Locomotion BlendTree (Mixamo Breathing_Idle, Walking, Running)
            var locoState = baseSm.states.Select(s => s.state).FirstOrDefault(s => s.name == "Idle Walk Run Blend");
            if (locoState != null && locoState.motion is BlendTree blendTree && idleClip != null && walkClip != null && runClip != null)
            {
                blendTree.blendType = BlendTreeType.Simple1D;
                blendTree.blendParameter = "Speed";
                blendTree.useAutomaticThresholds = false;

                var children = new ChildMotion[3];
                children[0] = new ChildMotion { motion = idleClip, threshold = 0f, timeScale = 1f };
                children[1] = new ChildMotion { motion = walkClip, threshold = 4.0f, timeScale = 1.05f };
                children[2] = new ChildMotion { motion = runClip, threshold = 6.0f, timeScale = 1.05f };
                blendTree.children = children;
                EditorUtility.SetDirty(blendTree);
            }

            // 2. Jump States (Mixamo Jumping — single clean jump returning directly to Idle Walk Run Blend)
            ReplaceStateClipIfFound(baseSm, "JumpStart", jumpClip, 1.85f);
            ReplaceStateClipIfFound(baseSm, "InAir", fallClip, 1.0f);
            ReplaceStateClipIfFound(baseSm, "JumpLand", landClip, 1.25f);

            var jumpStartState = baseSm.states.Select(s => s.state).FirstOrDefault(s => s.name == "JumpStart");
            if (jumpStartState != null && locoState != null)
            {
                jumpStartState.iKOnFeet = false;
                foreach (var oldT in jumpStartState.transitions.ToArray())
                {
                    jumpStartState.RemoveTransition(oldT);
                }
                var jumpToLoco = jumpStartState.AddTransition(locoState);
                jumpToLoco.hasExitTime = true;
                jumpToLoco.exitTime = 0.78f;
                jumpToLoco.duration = 0.16f;
                EditorUtility.SetDirty(jumpStartState);
            }

            // 3. Crouch State with Mixamo Crouch_Idle + Crouched_Walking BlendTree
            var crouchState = baseSm.states.Select(s => s.state).FirstOrDefault(s => s.name == "Crouch Idle");
            if (crouchState == null)
            {
                crouchState = baseSm.AddState("Crouch Idle", new Vector3(-80f, 400f, 0f));
            }

            BlendTree crouchBlendTree = crouchState.motion as BlendTree;
            if (crouchBlendTree == null)
            {
                crouchBlendTree = new BlendTree
                {
                    name = "Crouch Walk Blend",
                    blendType = BlendTreeType.Simple1D,
                    blendParameter = "Speed",
                    useAutomaticThresholds = false,
                    hideFlags = HideFlags.HideInHierarchy
                };
                AssetDatabase.AddObjectToAsset(crouchBlendTree, controller);
                crouchState.motion = crouchBlendTree;
            }
            crouchBlendTree.blendType = BlendTreeType.Simple1D;
            crouchBlendTree.blendParameter = "Speed";
            crouchBlendTree.useAutomaticThresholds = false;
            crouchBlendTree.children = new[]
            {
                new ChildMotion { motion = crouchIdleClip, threshold = 0f, timeScale = 1f },
                new ChildMotion { motion = crouchWalkClip ?? crouchIdleClip, threshold = 2.2f, timeScale = 1.25f }
            };
            crouchState.iKOnFeet = true;
            EditorUtility.SetDirty(crouchBlendTree);

            // Ensure bidirectional transitions between Idle Walk Run Blend and Crouch Idle
            if (locoState != null)
            {
                if (!locoState.transitions.Any(t => t.destinationState == crouchState))
                {
                    var toCrouch = locoState.AddTransition(crouchState);
                    toCrouch.hasExitTime = false;
                    toCrouch.duration = 0.15f;
                    toCrouch.AddCondition(AnimatorConditionMode.If, 0f, "IsCrouching");
                }
                if (!crouchState.transitions.Any(t => t.destinationState == locoState))
                {
                    var fromCrouch = crouchState.AddTransition(locoState);
                    fromCrouch.hasExitTime = false;
                    fromCrouch.duration = 0.15f;
                    fromCrouch.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsCrouching");
                }
            }

            // 4. Clean up legacy attack/parry states and add Weapon-Specific Standing Full-Body Attack States (0=Sword, 1=Spear, 2=Axe)
            RemoveStatesByName(baseSm, new[] { "Melee Slash 1", "Melee Slash 2", "Melee Heavy 3", "Melee Spear Thrust", "Shield Parry" });

            AddOrUpdateWeaponAttackState(baseSm, "Melee Sword Attack", swordAttackClip, 0, 1.50f, "FullBodyAttack", true, new Vector3(520f, 320f, 0f), baseSm.defaultState);
            AddOrUpdateWeaponAttackState(baseSm, "Melee Spear Attack", spearAttackClip, 1, 1.85f, "FullBodyAttack", true, new Vector3(520f, 390f, 0f), baseSm.defaultState);
            AddOrUpdateWeaponAttackState(baseSm, "Melee Axe Attack", axeAttackClip, 2, 1.75f, "FullBodyAttack", true, new Vector3(520f, 460f, 0f), baseSm.defaultState);

            if (hitClip != null) AddOrUpdateTriggerState(baseSm, "Combat Hit", hitClip, "Hit", new Vector3(520f, 540f, 0f), true);
            if (deathClip != null) AddOrUpdateTriggerState(baseSm, "Combat Death", deathClip, "Death", new Vector3(520f, 610f, 0f), false);

            // 5. Upper-Body Combat Mask Layer (Weapon-Specific Attacks while Moving or Crouching)
            AvatarMask upperMask = CreateOrUpdateHumanoidUpperBodyMask();
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

                RemoveStatesByName(upperSm, new[] { "Upper Slash 1", "Upper Slash 2", "Upper Heavy 3", "Upper Spear Thrust", "Upper Shield Parry" });

                AddOrUpdateWeaponAttackState(upperSm, "Upper Sword Attack", swordAttackClip, 0, 1.50f, "Attack", false, new Vector3(500f, 80f, 0f), emptyState);
                AddOrUpdateWeaponAttackState(upperSm, "Upper Spear Attack", spearAttackClip, 1, 1.85f, "Attack", false, new Vector3(500f, 160f, 0f), emptyState);
                AddOrUpdateWeaponAttackState(upperSm, "Upper Axe Attack", axeAttackClip, 2, 1.75f, "Attack", false, new Vector3(500f, 240f, 0f), emptyState);
            }

            EditorUtility.SetDirty(controller);
            return controller;
        }

        private static AvatarMask CreateOrUpdateHumanoidUpperBodyMask()
        {
            var mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(UpperBodyMaskPath);
            bool isNew = false;
            if (mask == null)
            {
                mask = new AvatarMask();
                isNew = true;
            }

            mask.transformCount = 0;
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.Root, false);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.Body, true);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.Head, true);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftLeg, false);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightLeg, false);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftArm, true);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightArm, true);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftFingers, true);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightFingers, true);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftFootIK, false);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightFootIK, false);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftHandIK, false);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightHandIK, false);

            if (isNew)
                AssetDatabase.CreateAsset(mask, UpperBodyMaskPath);
            else
                EditorUtility.SetDirty(mask);

            return mask;
        }

        private static void RemoveStatesByName(AnimatorStateMachine sm, string[] stateNames)
        {
            foreach (string name in stateNames)
            {
                var found = sm.states.Select(s => s.state).FirstOrDefault(s => s.name == name);
                if (found != null)
                {
                    foreach (var t in sm.anyStateTransitions.Where(t => t.destinationState == found).ToArray())
                    {
                        sm.RemoveAnyStateTransition(t);
                    }
                    sm.RemoveState(found);
                }
            }
        }

        private static void AddOrUpdateWeaponAttackState(
            AnimatorStateMachine sm,
            string stateName,
            AnimationClip clip,
            int weaponType,
            float clipSpeed,
            string triggerParam,
            bool interruptOnMovement,
            Vector3 position,
            AnimatorState returnState)
        {
            var state = sm.states.Select(s => s.state).FirstOrDefault(s => s.name == stateName);
            if (state == null)
            {
                state = sm.AddState(stateName, position);
            }
            state.motion = clip;
            state.speed = clipSpeed;

            foreach (var t in sm.anyStateTransitions.Where(t => t.destinationState == state).ToArray())
            {
                sm.RemoveAnyStateTransition(t);
            }

            var anyTransition = sm.AddAnyStateTransition(state);
            anyTransition.hasExitTime = false;
            anyTransition.duration = 0.08f;
            anyTransition.canTransitionToSelf = false;
            anyTransition.AddCondition(AnimatorConditionMode.If, 0f, triggerParam);
            anyTransition.AddCondition(AnimatorConditionMode.Equals, weaponType, "WeaponType");

            if (returnState != null)
            {
                foreach (var oldT in state.transitions.ToArray())
                {
                    state.RemoveTransition(oldT);
                }

                var backTransition = state.AddTransition(returnState);
                backTransition.hasExitTime = true;
                backTransition.exitTime = 0.86f;
                backTransition.duration = 0.14f;

                if (interruptOnMovement)
                {
                    // If the player starts walking/running mid-swing on the Base Layer, blend legs back to locomotion while UpperBodyCombat finishes the swing
                    var moveCancel = state.AddTransition(returnState);
                    moveCancel.hasExitTime = false;
                    moveCancel.duration = 0.15f;
                    moveCancel.AddCondition(AnimatorConditionMode.Greater, 0.35f, "Speed");
                }
            }
        }

        private static void ReplaceStateClipIfFound(AnimatorStateMachine sm, string stateName, AnimationClip newClip, float speed = 1.0f)
        {
            if (newClip == null) return;
            var state = sm.states.Select(s => s.state).FirstOrDefault(s => s.name == stateName);
            if (state != null)
            {
                state.motion = newClip;
                state.speed = speed;
                EditorUtility.SetDirty(state);
            }
        }

        private static void EnsureParameter(AnimatorController controller, string paramName, AnimatorControllerParameterType type)
        {
            if (controller.parameters.Any(p => p.name == paramName)) return;
            controller.AddParameter(paramName, type);
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

            // 2. Instantiate Polygon Apocalypse Woman (or fallback humanoid character) as PlayerVisualModel
            Transform existingVisual = playerObj.transform.Find("PlayerVisualModel");
            if (existingVisual != null)
            {
                Object.DestroyImmediate(existingVisual.gameObject);
            }

            GameObject polygonWomanPrefab = FindPolygonApocalypseWomanPrefab();
            var charPrefab = polygonWomanPrefab
                             ?? AssetDatabase.LoadAssetAtPath<GameObject>(GanzSePrefabPath)
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
                t.gameObject.hideFlags = HideFlags.None;
                t.hideFlags = HideFlags.None;
                GameObjectUtility.SetStaticEditorFlags(t.gameObject, 0);
                t.gameObject.isStatic = false;
            }

            foreach (var comp in visualModelObj.GetComponentsInChildren<Component>(true))
            {
                if (comp != null) comp.hideFlags = HideFlags.None;
            }

            // Configure modular parts only if using GanzSe fallback; if using Polygon Apocalypse Woman, clean inactive meshes & repair hair/backpack attachments
            ModularHeroController modularController = null;
            SkinnedMeshRenderer activeBodySmr = null;
            if (polygonWomanPrefab == null)
            {
                modularController = ConfigureGanzSeModularParts(visualModelObj);
            }
            else
            {
                foreach (var smr in visualModelObj.GetComponentsInChildren<SkinnedMeshRenderer>(true).ToArray())
                {
                    if (smr == null) continue;
                    if (smr.gameObject.name == "SM_Chr_Cool_Female_01" || smr.gameObject.activeSelf)
                    {
                        smr.gameObject.SetActive(true);
                        smr.enabled = true;
                        activeBodySmr = smr;
                    }
                    else
                    {
                        Object.DestroyImmediate(smr.gameObject);
                    }
                }

                // Ensure hair (SM_Chr_Attach_Cool_Female_Hair_01), backpack (SM_Chr_Attach_Backpack_Large_Extras_01), and pouch (SM_Chr_Attach_Pouch_01) meshes are assigned & active
                foreach (var mf in visualModelObj.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (mf == null) continue;
                    mf.gameObject.SetActive(true);
                    if (mf.sharedMesh == null)
                    {
                        string attachPath = $"Assets/PolygonApocalypse/Models/{mf.gameObject.name}.fbx";
                        Mesh loadedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(attachPath);
                        if (loadedMesh != null)
                        {
                            mf.sharedMesh = loadedMesh;
                        }
                    }
                    var mr = mf.GetComponent<MeshRenderer>();
                    if (mr != null) mr.enabled = true;
                }
            }

            foreach (var renderer in visualModelObj.GetComponentsInChildren<Renderer>(true))
            {
                var mats = renderer.sharedMaterials;
                for (int i = 0; i < mats.Length; i++) mats[i] = EnsureUrpMaterial(mats[i]);
                renderer.sharedMaterials = mats;
                if (renderer is SkinnedMeshRenderer smr)
                {
                    smr.updateWhenOffscreen = true;
                    if (activeBodySmr == null && smr.gameObject.activeInHierarchy)
                        activeBodySmr = smr;
                }
            }

            var animator = visualModelObj.GetComponentInChildren<Animator>();
            if (animator == null) animator = visualModelObj.AddComponent<Animator>();
            if (polygonWomanPrefab != null && File.Exists(PolygonCharactersModelPath))
            {
                Avatar charAvatar = AssetDatabase.LoadAssetAtPath<Avatar>(PolygonCharactersModelPath);
                if (charAvatar != null) animator.avatar = charAvatar;
            }
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            var eventReceiver = animator.GetComponent<PlayerAnimationEventReceiver>();
            if (eventReceiver == null) eventReceiver = animator.gameObject.AddComponent<PlayerAnimationEventReceiver>();

            // Build & save First-Person Right-Arm-Only Mesh asset so 1st-person view has zero torso/shoulder/neck clipping or polygon stretching
            Transform rightLowerArm = (animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.RightLowerArm) : null)
                                      ?? FindChildRecursive(visualModelObj.transform, "Elbow_R")
                                      ?? FindChildRecursive(visualModelObj.transform, "LowerArm_R");
            Mesh fullBodyMesh = activeBodySmr != null ? activeBodySmr.sharedMesh : null;
            Mesh fpRightArmMesh = null;
            if (activeBodySmr != null && fullBodyMesh != null && rightLowerArm != null)
            {
                fpRightArmMesh = PlayerCharacterController.BuildFirstPersonRightArmMesh(activeBodySmr, fullBodyMesh, rightLowerArm);
                if (fpRightArmMesh != null)
                {
                    if (File.Exists(FpRightArmMeshAssetPath))
                    {
                        AssetDatabase.DeleteAsset(FpRightArmMeshAssetPath);
                    }
                    AssetDatabase.CreateAsset(fpRightArmMesh, FpRightArmMeshAssetPath);
                }
            }

            // 3. Attach WeaponSocket_TP inside RightHand palm (No Shield on LeftHand)
            Transform rightHand = null;
            if (animator.isHuman)
            {
                rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
            }
            if (rightHand == null)
            {
                rightHand = FindChildRecursive(visualModelObj.transform, "Hand_R")
                            ?? FindChildRecursive(visualModelObj.transform, "hand_r")
                            ?? FindChildRecursive(visualModelObj.transform, "Right_Hand");
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

                PlayerCharacterController.ConfigureWeaponSocketForHandBone(weaponSocket, rightHand, animator);

                tpWeapons = InstantiateWeaponSet(weaponSocket, false);
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
                cameraRoot.localPosition = new Vector3(0f, 1.50f, 0f);
            }

            if (fpc != null)
            {
                fpc.MoveSpeed = 4.0f;
                fpc.SprintSpeed = 6.0f;
                fpc.TopClamp = 70.0f;
                fpc.BottomClamp = -65.0f;
                EditorUtility.SetDirty(fpc);
            }

            // 4. Wire up PlayerCharacterController component
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
            pcc.bodySkinnedMeshRenderer = activeBodySmr;
            pcc.fullBodyMesh = fullBodyMesh;
            pcc.firstPersonRightArmMesh = fpRightArmMesh;
            pcc.cameraRoot = cameraRoot;
            pcc.mainCamera = Camera.main;
            pcc.rightHandWeaponSocket = weaponSocket;
            pcc.firstPersonWeaponPivot = null;
            pcc.thirdPersonWeapons = tpWeapons;
            pcc.firstPersonWeapons = new GameObject[0];
            pcc.thirdPersonCameraDistance = 2.4f;
            pcc.thirdPersonShoulderOffset = new Vector3(0.12f, 0.08f, 0f);
            pcc.thirdPersonTopClamp = 42.0f;
            pcc.thirdPersonBottomClamp = -30.0f;

            var followCamObj = GameObject.Find("PlayerFollowCamera");
            if (followCamObj != null)
            {
                var followCam = followCamObj.GetComponent<CinemachineThirdPersonFollow>();
                pcc.followCameraComponent = followCam;
                if (followCam != null)
                {
                    followCam.CameraDistance = pcc.isThirdPersonView ? pcc.thirdPersonCameraDistance : 0f;
                    followCam.ShoulderOffset = pcc.isThirdPersonView ? pcc.thirdPersonShoulderOffset : Vector3.zero;
                    followCam.VerticalArmLength = pcc.isThirdPersonView ? 0.05f : 0f;
                    followCam.CameraSide = 0.55f;
                    EditorUtility.SetDirty(followCam);
                }

                var vcam = followCamObj.GetComponent<CinemachineCamera>();
                if (vcam != null)
                {
                    var lens = vcam.Lens;
                    lens.FieldOfView = 55f;
                    lens.NearClipPlane = 0.05f;
                    vcam.Lens = lens;
                    EditorUtility.SetDirty(vcam);
                }

                var perlin = followCamObj.GetComponent<CinemachineBasicMultiChannelPerlin>();
                if (perlin != null)
                {
                    perlin.PivotOffset = Vector3.zero;
                    perlin.AmplitudeGain = 0.10f;
                    EditorUtility.SetDirty(perlin);
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
            float[] targetWeaponLengths = new float[] { 0.88f, 1.30f, 0.80f }; // Sword, Spear, Axe length in meters
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
                        else if (i == 1)
                        {
                            Vector3 rotatedCenter = (alignRot * meshBounds.center) * uniformScale;
                            meshObj.transform.localPosition = -rotatedCenter + new Vector3(0f, targetWeaponLengths[i] * 0.16f, 0f);
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

                var trailObj = new GameObject("BladeTrail");
                trailObj.transform.SetParent(holder.transform, false);
                trailObj.transform.localPosition = new Vector3(0f, tipHeights[i], 0f);
                var trail = trailObj.AddComponent<TrailRenderer>();
                trail.time = 0.16f;
                trail.minVertexDistance = 0.02f;
                trail.startWidth = 0.12f;
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
                              (mat.HasProperty("_Texture") ? mat.GetTexture("_Texture") :
                              (mat.HasProperty("_MainTex") ? mat.GetTexture("_MainTex") : null));
            Color baseCol = mat.HasProperty("_BaseColor") ? mat.GetColor("_BaseColor") :
                            (mat.HasProperty("_Color") ? mat.GetColor("_Color") : Color.white);

            if (mainTex != null) newMat.SetTexture("_BaseMap", mainTex);
            newMat.SetColor("_BaseColor", baseCol);
            newMat.SetFloat("_Smoothness", 0.1f);

            AssetDatabase.CreateAsset(newMat, urpMatPath);
            return newMat;
        }

        private static void ImportMixamoHumanoidFbxFolder()
        {
            if (!Directory.Exists(SourceAnimFolder)) return;

            string[] loopingFiles = new[]
            {
                "Breathing_Idle",
                "Walking",
                "Running",
                "Crouch_Idle",
                "Crouched_Walking"
            };

            foreach (string fbxPath in Directory.GetFiles(SourceAnimFolder, "*.fbx"))
            {
                string assetPath = fbxPath.Replace("\\", "/");
                var importer = AssetImporter.GetAtPath(assetPath) as ModelImporter;
                if (importer == null) continue;

                string baseName = Path.GetFileNameWithoutExtension(assetPath);
                bool shouldLoop = loopingFiles.Any(l => baseName.IndexOf(l, System.StringComparison.OrdinalIgnoreCase) >= 0);

                bool needsReimport = false;
                if (importer.animationType != ModelImporterAnimationType.Human)
                {
                    importer.animationType = ModelImporterAnimationType.Human;
                    importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                    importer.SaveAndReimport();
                }

                var clips = importer.clipAnimations;
                if (clips == null || clips.Length == 0)
                {
                    clips = importer.defaultClipAnimations;
                    needsReimport = true;
                }

                if (clips != null && clips.Length > 0)
                {
                    for (int i = 0; i < clips.Length; i++)
                    {
                        if (clips[i].loopTime != shouldLoop ||
                            clips[i].loopPose != shouldLoop ||
                            !clips[i].lockRootRotation ||
                            !clips[i].lockRootHeightY ||
                            !clips[i].lockRootPositionXZ ||
                            clips[i].keepOriginalOrientation ||
                            clips[i].keepOriginalPositionY ||
                            clips[i].keepOriginalPositionXZ)
                        {
                            clips[i].name = baseName;
                            clips[i].loopTime = shouldLoop;
                            clips[i].loopPose = shouldLoop;
                            clips[i].lockRootRotation = true;
                            clips[i].lockRootHeightY = true;
                            clips[i].lockRootPositionXZ = true;
                            clips[i].keepOriginalOrientation = false;
                            clips[i].keepOriginalPositionY = false;
                            clips[i].keepOriginalPositionXZ = false;
                            needsReimport = true;
                        }
                    }
                    if (needsReimport)
                    {
                        importer.clipAnimations = clips;
                        importer.SaveAndReimport();
                    }
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
