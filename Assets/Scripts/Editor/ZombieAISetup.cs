using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using Unity.AI.Navigation;

namespace StealthGame.Editor
{
    public static class ZombieAISetup
    {
        const string Folder = "Assets/Animations/Zombies";
        const string Sources = Folder + "/Scary Zombie Pack";

        static AnimationClip Extract(string source, string name, bool loop)
        {
            string output = Folder + "/" + name + ".anim";
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(output);
            string path = Sources + "/" + source + ".fbx";
            if (AssetImporter.GetAtPath(path) is ModelImporter importer)
            {
                importer.animationType = ModelImporterAnimationType.Human;
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                importer.sourceAvatar = null;
                importer.importCameras = false;
                importer.importLights = false;
                importer.materialImportMode = ModelImporterMaterialImportMode.None;
                importer.SaveAndReimport();
                var clips = importer.defaultClipAnimations;
                foreach (var settings in clips)
                {
                    settings.name = name;
                    settings.loopTime = loop;
                    settings.loopPose = loop;
                    settings.lockRootRotation = true;
                    settings.lockRootHeightY = true;
                    settings.lockRootPositionXZ = source != "zombie walk" && source != "zombie run";
                }
                importer.clipAnimations = clips;
                importer.SaveAndReimport();
                var imported = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>()
                    .First(c => !c.name.StartsWith("__preview__"));
                if (!imported.isHumanMotion) throw new InvalidOperationException(name + " is not humanoid.");
                if (clip == null) { clip = UnityEngine.Object.Instantiate(imported); AssetDatabase.CreateAsset(clip, output); }
                else EditorUtility.CopySerialized(imported, clip);
                clip.name = name;
                AnimationUtility.SetAnimationEvents(clip, Array.Empty<AnimationEvent>());
                EditorUtility.SetDirty(clip);
            }
            if (clip == null || !clip.isHumanMotion) throw new InvalidOperationException("Missing humanoid " + name);
            var motionSettings = AnimationUtility.GetAnimationClipSettings(clip);
            motionSettings.loopBlendPositionXZ = source != "zombie walk" && source != "zombie run";
            AnimationUtility.SetAnimationClipSettings(clip, motionSettings);
            EditorUtility.SetDirty(clip);
            return clip;
        }

        [MenuItem("Tools/Paramnesia/Set Up Zombie Roaming AI")]
        public static void Setup()
        {
            var idle = Extract("zombie idle", "Zombie Idle", true);
            var walk = Extract("zombie walk", "Zombie Walk", true);
            var run = Extract("zombie run", "Zombie Run", true);
            var attack = Extract("zombie attack", "Zombie Attack", false);
            var death = Extract("zombie death", "Zombie Death", false);
            float walkSpeed = walk.averageSpeed.magnitude;
            float runSpeed = run.averageSpeed.magnitude;
            if (walkSpeed < 0.1f || runSpeed < 0.1f) throw new InvalidOperationException("Locomotion clips need forward root motion.");
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(Folder + "/ZombieIdle.controller");
            foreach (string parameter in new[] { "MoveX", "MoveZ" })
                if (!controller.parameters.Any(p => p.name == parameter)) controller.AddParameter(parameter, AnimatorControllerParameterType.Float);
            if (!controller.parameters.Any(p => p.name == "Attack")) controller.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
            var sm = controller.layers[0].stateMachine;
            var state = sm.defaultState;
            if (state.motion is BlendTree old) UnityEngine.Object.DestroyImmediate(old, true);
            var tree = new BlendTree { name = "Zombie Locomotion", blendType = BlendTreeType.FreeformCartesian2D,
                blendParameter = "MoveX", blendParameterY = "MoveZ", useAutomaticThresholds = false };
            AssetDatabase.AddObjectToAsset(tree, controller);
            tree.AddChild(idle, Vector2.zero);
            // shortcut: the pack only has forward gaits; steering turns the body, add authored strafes if enemies must face away from travel.
            for (int i = 0; i < 8; i++)
            {
                float angle = i * Mathf.PI / 4f;
                var direction = new Vector2(Mathf.Sin(angle), Mathf.Cos(angle));
                tree.AddChild(walk, direction * walkSpeed);
                tree.AddChild(run, direction * runSpeed);
            }
            state.name = "Zombie Locomotion";
            state.motion = tree;
            state.iKOnFeet = true;
            var attackState = sm.states.Select(s => s.state).FirstOrDefault(s => s.name == "Zombie Attack") ?? sm.AddState("Zombie Attack");
            attackState.motion = attack;
            attackState.speed = attack.length / 0.8f;
            if (!sm.anyStateTransitions.Any(t => t.destinationState == attackState))
            {
                var enter = sm.AddAnyStateTransition(attackState);
                enter.hasExitTime = false; enter.duration = 0.12f; enter.canTransitionToSelf = false;
                enter.AddCondition(AnimatorConditionMode.If, 0f, "Attack");
                var exit = attackState.AddTransition(state);
                exit.hasExitTime = true; exit.exitTime = 0.9f; exit.duration = 0.15f;
            }
            var deathState = sm.states.Select(s => s.state).FirstOrDefault(s => s.name == "Zombie Death");
            if (deathState != null) deathState.motion = death;
            EditorUtility.SetDirty(controller);
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs/Enemies" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var root = PrefabUtility.LoadPrefabContents(path);
                try { Configure(root); PrefabUtility.SaveAsPrefabAsset(root, path); }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            foreach (var detector in UnityEngine.Object.FindObjectsByType<EnemyDetection>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                Configure(detector.gameObject);
                detector.gameObject.SetActive(true);
                SceneVisibilityManager.instance.Show(detector.gameObject, true);
                PrefabUtility.RecordPrefabInstancePropertyModifications(detector.gameObject);
            }
            var surface = UnityEngine.Object.FindFirstObjectByType<NavMeshSurface>();
            if (surface == null) surface = new GameObject("Zombie Navigation").AddComponent<NavMeshSurface>();
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.collectObjects = CollectObjects.Volume;
            surface.center = new Vector3(0f, 2f, 0f);
            surface.size = new Vector3(60f, 12f, 60f);
            var previousData = surface.navMeshData;
            surface.BuildNavMesh();
            if (surface.navMeshData == null) throw new InvalidOperationException("Navigation bake failed.");
            if (previousData != null && AssetDatabase.Contains(previousData))
            {
                var baked = surface.navMeshData;
                surface.RemoveData();
                EditorUtility.CopySerialized(baked, previousData);
                surface.navMeshData = previousData;
                surface.AddData();
                UnityEngine.Object.DestroyImmediate(baked);
                EditorUtility.SetDirty(previousData);
            }
            if (!AssetDatabase.Contains(surface.navMeshData)) AssetDatabase.CreateAsset(surface.navMeshData, AssetDatabase.GenerateUniqueAssetPath("Assets/Scenes/ZombieNavigation.asset"));
            EditorUtility.SetDirty(surface);
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(surface.gameObject.scene);
            EditorSceneManager.SaveScene(surface.gameObject.scene);
            Check();
            // Delete only imported sources once every retained clip and prefab has been validated and saved.
            if (AssetDatabase.IsValidFolder(Sources)) AssetDatabase.DeleteAsset(Sources);
            Debug.Log("Zombie roaming AI configured; source FBXs removed, humanoid clips retained.");
        }

        public static void Configure(GameObject root)
        {
            if (root.GetComponent<EnemyDetection>() == null) return;
            var combat = root.GetComponent<EnemyCombatTarget>();
            if (combat == null) combat = root.AddComponent<EnemyCombatTarget>();
            var agent = root.GetComponent<NavMeshAgent>();
            if (agent == null) agent = root.AddComponent<NavMeshAgent>();
            agent.radius = 0.3f; agent.height = 1.8f; agent.speed = 0.85f; agent.angularSpeed = 360f; agent.acceleration = 10f;
            agent.stoppingDistance = 0.15f;
            if (root.GetComponent<ZombieAI>() == null) root.AddComponent<ZombieAI>();
            var ai = root.GetComponent<ZombieAI>();
            ai.roamSpeed = AssetDatabase.LoadAssetAtPath<AnimationClip>(Folder + "/Zombie Walk.anim").averageSpeed.magnitude;
            ai.runSpeed = AssetDatabase.LoadAssetAtPath<AnimationClip>(Folder + "/Zombie Run.anim").averageSpeed.magnitude;
            agent.updatePosition = false;
            combat.enemyDetection = root.GetComponent<EnemyDetection>();
            combat.enemyAnimator = root.GetComponentInChildren<Animator>(true);
            combat.enemyCollider = root.GetComponent<Collider>();
            combat.enemyAnimator.applyRootMotion = true;
            if (combat.enemyAnimator.GetComponent<ZombieRootMotion>() == null) combat.enemyAnimator.gameObject.AddComponent<ZombieRootMotion>();
            if (!combat.enemyAnimator.isHuman || !combat.enemyAnimator.avatar.isValid) throw new InvalidOperationException(root.name + " needs a valid humanoid avatar.");
        }

        [MenuItem("Tools/Paramnesia/Check Zombie AI Setup")]
        public static void Check()
        {
            foreach (var ai in UnityEngine.Object.FindObjectsByType<ZombieAI>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var anim = ai.GetComponentInChildren<Animator>(true);
                if (!anim.isHuman || !anim.avatar.isValid) throw new InvalidOperationException("Invalid zombie avatar");
                if (!NavMesh.SamplePosition(ai.transform.position, out var hit, 1f, NavMesh.AllAreas)) throw new InvalidOperationException(ai.name + " is outside the baked navigation.");
                if (!(anim.runtimeAnimatorController is AnimatorController c) || !(c.layers[0].stateMachine.defaultState.motion is BlendTree tree) || tree.children.Length != 17)
                    throw new InvalidOperationException("Missing directional locomotion blend tree");
            }
            Debug.Log("Zombie AI setup checks passed.");
        }

        [MenuItem("Tools/Paramnesia/Check Zombie AI In Play Mode")]
        public static void CheckPlayBehavior()
        {
            if (!Application.isPlaying) throw new InvalidOperationException("Enter Play mode first.");
            var ai = UnityEngine.Object.FindFirstObjectByType<ZombieAI>();
            var detection = ai.GetComponent<EnemyDetection>();
            var combat = ai.GetComponent<EnemyCombatTarget>();
            var previous = detection.player;
            bool counterAttack = combat.enableCounterAttack;
            var target = new GameObject("Zombie AI check target");
            target.transform.position = ai.transform.TransformPoint(detection.eyeOffset) + ai.transform.forward * 0.8f - detection.targetOffset;
            detection.player = target.transform;
            combat.enableCounterAttack = false;
            float start = Time.time;
            int stage = 0;
            EditorApplication.CallbackFunction tick = null;
            tick = () =>
            {
                bool done = false;
                try
                {
                    if (!Application.isPlaying) { done = true; return; }
                    if (Time.time - start < (stage == 2 ? ai.lostSightWait + 0.4f : 0.3f)) return;
                    if (stage == 0)
                    {
                        if (ai.State != "Chasing" || !detection.detectionIndicator.activeSelf) throw new Exception("Detection/alert check failed.");
                        target.SetActive(false);
                    }
                    else if (stage == 1)
                    {
                        if (ai.State != "Waiting" || !ai.IsAlerted || detection.detectionIndicator.activeSelf || !ai.GetComponent<NavMeshAgent>().isStopped)
                            throw new Exception("Lost-sight wait check failed.");
                    }
                    else
                    {
                        if (ai.State != "Roaming" || ai.IsAlerted) throw new Exception("Return-to-roaming check failed.");
                        Debug.Log("Zombie AI play checks passed: detect, alert, chase, stop on sight loss, wait, resume roaming.");
                        done = true;
                    }
                    stage++;
                    start = Time.time;
                }
                catch (Exception e) { Debug.LogException(e); done = true; }
                finally
                {
                    if (done)
                    {
                        EditorApplication.update -= tick;
                        if (detection != null) detection.player = previous;
                        if (combat != null) combat.enableCounterAttack = counterAttack;
                        if (target != null) UnityEngine.Object.DestroyImmediate(target);
                    }
                }
            };
            EditorApplication.update += tick;
        }
    }
}
