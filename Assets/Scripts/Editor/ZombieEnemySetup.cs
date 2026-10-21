using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace StealthGame.Editor
{
    public static class ZombieEnemySetup
    {
        const string Output = "Assets/Prefabs/Enemies";
        const string IdleSource = "Assets/Animations/Zombies/Source/Zombie Idle.fbx";
        const string IdlePath = "Assets/Animations/Zombies/Zombie Idle.anim";
        const string ControllerPath = "Assets/Animations/Zombies/ZombieIdle.controller";

        [MenuItem("Tools/Paramnesia/Create Zombie Enemies")]
        public static void CreateEnemies()
        {
            var idle = AssetDatabase.LoadAssetAtPath<AnimationClip>(IdlePath);
            if (File.Exists(IdleSource))
            {
                var importer = (ModelImporter)AssetImporter.GetAtPath(IdleSource);
                importer.animationType = ModelImporterAnimationType.Human;
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                importer.sourceAvatar = null;
                importer.SaveAndReimport();
                var clips = importer.defaultClipAnimations;
                foreach (var clip in clips)
                {
                    clip.name = "Zombie Idle";
                    clip.loopTime = true;
                    clip.lockRootRotation = true;
                    clip.lockRootHeightY = true;
                    clip.lockRootPositionXZ = true;
                }
                importer.clipAnimations = clips;
                importer.SaveAndReimport();
                var importedIdle = AssetDatabase.LoadAllAssetsAtPath(IdleSource).OfType<AnimationClip>()
                    .First(c => !c.name.StartsWith("__preview__"));
                if (!importedIdle.isHumanMotion) throw new InvalidOperationException("Idle must import as humanoid.");
                idle = AssetDatabase.LoadAssetAtPath<AnimationClip>(IdlePath);
                if (idle == null)
                {
                    idle = UnityEngine.Object.Instantiate(importedIdle);
                    AssetDatabase.CreateAsset(idle, IdlePath);
                }
                else EditorUtility.CopySerialized(importedIdle, idle);
                idle.name = "Zombie Idle";
                EditorUtility.SetDirty(idle);
            }
            if (idle == null || !idle.isHumanMotion)
                throw new InvalidOperationException("A humanoid Zombie Idle.anim or its source FBX is required.");

            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null)
            {
                controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
                controller.layers[0].stateMachine.defaultState = controller.layers[0].stateMachine.AddState("Zombie Idle");
            }
            var idleState = controller.layers[0].stateMachine.defaultState;
            idleState.name = "Zombie Idle";
            idleState.motion = idle;
            idleState.iKOnFeet = true;
            EditorUtility.SetDirty(idleState);
            Directory.CreateDirectory(Output);
            AssetDatabase.Refresh();
            const string alertMaterialPath = Output + "/DetectionAlert.mat";
            var alertMaterial = AssetDatabase.LoadAssetAtPath<Material>(alertMaterialPath);
            if (alertMaterial == null)
            {
                alertMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                alertMaterial.SetColor("_BaseColor", new Color(1f, 0.8f, 0.05f));
                AssetDatabase.CreateAsset(alertMaterial, alertMaterialPath);
            }
            int count = 0;
            foreach (string path in Directory.GetFiles("Assets/PolygonZombies/Prefabs", "Zombie_*.prefab"))
            {
                var source = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                var root = new GameObject("Enemy_" + source.name);
                try
                {
                    var model = (GameObject)PrefabUtility.InstantiatePrefab(source, root.transform);
                    var animator = model.GetComponentInChildren<Animator>();
                    if (animator == null || animator.avatar == null || !animator.avatar.isHuman || !animator.avatar.isValid)
                        throw new InvalidOperationException(source.name + " needs a valid humanoid avatar.");
                    animator.runtimeAnimatorController = controller;
                    animator.applyRootMotion = false;

                    var renderers = model.GetComponentsInChildren<SkinnedMeshRenderer>();
                    if (renderers.Length == 0) throw new InvalidOperationException(source.name + " has no active mesh.");
                    Bounds bounds = renderers[0].bounds;
                    foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
                    foreach (var renderer in model.GetComponentsInChildren<Renderer>(true))
                    {
                        var materials = renderer.sharedMaterials;
                        for (int i = 0; i < materials.Length; i++)
                        {
                            var sourceMaterial = materials[i];
                            if (sourceMaterial == null || sourceMaterial.shader.name != "SyntyStudios/Zombies") continue;
                            string materialPath = Output + "/" + sourceMaterial.name + "_URP.mat";
                            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                            if (material == null)
                            {
                                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                                material.SetTexture("_BaseMap", sourceMaterial.GetTexture("_Texture"));
                                material.SetTextureScale("_BaseMap", sourceMaterial.GetTextureScale("_Texture"));
                                material.SetTextureOffset("_BaseMap", sourceMaterial.GetTextureOffset("_Texture"));
                                material.SetFloat("_Smoothness", 0f);
                                material.SetTexture("_EmissionMap", sourceMaterial.GetTexture("_Emissive"));
                                material.SetColor("_EmissionColor", sourceMaterial.GetColor("_EmissiveColor"));
                                material.EnableKeyword("_EMISSION");
                                AssetDatabase.CreateAsset(material, materialPath);
                            }
                            materials[i] = material;
                        }
                        renderer.sharedMaterials = materials;
                    }
                    var capsule = root.AddComponent<CapsuleCollider>();
                    capsule.height = bounds.size.y;
                    capsule.radius = Mathf.Min(0.35f, bounds.size.y * 0.2f);
                    capsule.center = new Vector3(0f, bounds.center.y, 0f);
                    var body = root.AddComponent<Rigidbody>();
                    body.isKinematic = true;
                    body.useGravity = false;
                    var detection = root.AddComponent<EnemyDetection>();
                    detection.eyeOffset = new Vector3(0f, bounds.max.y - 0.2f, 0f);
                    var indicator = new GameObject("Detection Alert (!)");
                    indicator.transform.SetParent(root.transform, false);
                    indicator.transform.localPosition = new Vector3(0f, bounds.max.y + 0.45f, 0f);
                    indicator.transform.localScale = Vector3.one * 0.65f;
                    var stem = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                    stem.name = "Exclamation Stem";
                    stem.transform.SetParent(indicator.transform, false);
                    stem.transform.localScale = new Vector3(0.12f, 0.18f, 0.12f);
                    var dot = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    dot.name = "Exclamation Dot";
                    dot.transform.SetParent(indicator.transform, false);
                    dot.transform.localPosition = Vector3.down * 0.29f;
                    dot.transform.localScale = Vector3.one * 0.13f;
                    foreach (var part in new[] { stem, dot })
                    {
                        UnityEngine.Object.DestroyImmediate(part.GetComponent<Collider>());
                        var mesh = part.GetComponent<Renderer>();
                        mesh.sharedMaterial = alertMaterial;
                        mesh.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                        mesh.receiveShadows = false;
                    }
                    detection.detectionIndicator = indicator;
                    detection.detectionIndicatorAnchor = animator.GetBoneTransform(HumanBodyBones.Head);
                    indicator.SetActive(false);
                    PrefabUtility.SaveAsPrefabAsset(root, Output + "/" + root.name + ".prefab");
                    count++;
                }
                finally { UnityEngine.Object.DestroyImmediate(root); }
            }
            AssetDatabase.SaveAssets();
            Debug.Log("Created " + count + " humanoid zombie enemy prefabs in " + Output);
        }

        const string VisionMenu = "Tools/Paramnesia/Developer/Show Enemy Vision";

        [MenuItem(VisionMenu)]
        public static void ToggleEnemyVision()
        {
            EditorPrefs.SetBool("Paramnesia.ShowEnemyVision", !EditorPrefs.GetBool("Paramnesia.ShowEnemyVision", false));
            SceneView.RepaintAll();
        }

        [MenuItem(VisionMenu, true)]
        static bool ValidateEnemyVision()
        {
            Menu.SetChecked(VisionMenu, EditorPrefs.GetBool("Paramnesia.ShowEnemyVision", false));
            return true;
        }

        [MenuItem("Tools/Paramnesia/Check Enemy Detection")]
        public static void CheckDetection()
        {
            var previous = SceneManager.GetActiveScene();
            var scene = UnityEditor.SceneManagement.EditorSceneManager.NewScene(
                UnityEditor.SceneManagement.NewSceneSetup.EmptyScene,
                UnityEditor.SceneManagement.NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            try
            {
                // Keep fixtures far from level geometry while using default physics queries.
                var enemy = new GameObject("Detection check");
                enemy.transform.position = new Vector3(10000f, 10000f, 10000f);
                var detection = enemy.AddComponent<EnemyDetection>();
                detection.eyeOffset = Vector3.zero;
                detection.targetOffset = Vector3.zero;
                detection.detectionIndicator = new GameObject("Alert check");
                detection.detectionIndicator.transform.SetParent(enemy.transform, false);
                detection.detectionIndicator.SetActive(false);
                var player = new GameObject("Target check");
                detection.player = player.transform;
                GameObject wall = null;
                try
                {
                    Action<bool, string> check = (condition, message) =>
                    { if (!condition) throw new InvalidOperationException(message); };
                    player.transform.position = enemy.transform.position + Vector3.forward * 5f;
                    check(detection.CanSeePlayer(), "Target inside cone should be detected.");
                    detection.SendMessage("Update");
                    check(detection.detectionIndicator.activeSelf && detection.IsPlayerDetected,
                        "Alert should appear when the target is detected.");
                    player.transform.position = enemy.transform.position - Vector3.forward * 5f;
                    check(!detection.CanSeePlayer(), "Target behind enemy should not be detected.");
                    detection.SendMessage("Update");
                    check(!detection.detectionIndicator.activeSelf && !detection.IsPlayerDetected,
                        "Alert should disappear when detection is lost.");
                    int logCount = 0;
                    Application.LogCallback countLogs = (message, trace, type) =>
                    { if (message.StartsWith("[Detection check] Player ")) logCount++; };
                    Application.logMessageReceived += countLogs;
                    try
                    {
                        detection.logDetectionChanges = true;
                        player.transform.position = enemy.transform.position + Vector3.forward * 5f;
                        detection.SendMessage("Update");
                        detection.SendMessage("Update");
                        player.transform.position = enemy.transform.position - Vector3.forward * 5f;
                        detection.SendMessage("Update");
                        check(logCount == 2, "Logging should report transitions without repeating every frame.");
                    }
                    finally
                    {
                        detection.logDetectionChanges = false;
                        Application.logMessageReceived -= countLogs;
                    }
                    player.transform.position = enemy.transform.position + Vector3.right * 5f;
                    check(!detection.CanSeePlayer(), "Target beside enemy should not be detected.");
                    player.transform.position = enemy.transform.position + Vector3.forward * 11f;
                    check(!detection.CanSeePlayer(), "Target out of range should not be detected.");
                    player.transform.position = enemy.transform.position + Vector3.forward * 5f;
                    var ownCollider = enemy.AddComponent<SphereCollider>();
                    ownCollider.radius = 1f;
                    var child = new GameObject("Target collider");
                    child.transform.SetParent(player.transform, false);
                    child.AddComponent<BoxCollider>();
                    Physics.SyncTransforms();
                    check(detection.CanSeePlayer(), "Enemy and target colliders should not block detection.");
                    wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    wall.transform.position = enemy.transform.position + Vector3.forward * 2f;
                    Physics.SyncTransforms();
                    check(!detection.CanSeePlayer(), "Walls should block detection.");
                    wall.GetComponent<Collider>().isTrigger = true;
                    Physics.SyncTransforms();
                    check(detection.CanSeePlayer(), "Triggers should not block detection.");
                    detection.SendMessage("Update");
                    detection.SendMessage("OnDisable");
                    check(!detection.detectionIndicator.activeSelf && !detection.IsPlayerDetected,
                        "Disabling detection should hide the alert.");
                    player.SetActive(false);
                    check(!detection.CanSeePlayer(), "Inactive targets should not be detected.");
                    detection.player = null;
                    check(!detection.CanSeePlayer(), "Missing target should not be detected.");
                    Debug.Log("Enemy detection: all 13 checks passed.");
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(wall);
                    UnityEngine.Object.DestroyImmediate(player);
                    UnityEngine.Object.DestroyImmediate(enemy);
                }
            }
            finally
            {
                SceneManager.SetActiveScene(previous);
                UnityEditor.SceneManagement.EditorSceneManager.CloseScene(scene, true);
            }
        }
    }
}
