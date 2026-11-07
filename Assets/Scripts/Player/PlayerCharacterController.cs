using System.Collections.Generic;
using UnityEngine;
using Unity.Cinemachine;
using StarterAssets;
using GanzSe;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace StealthGame.Player
{
    /// <summary>
    /// Controls the 3D Player Character, Mixamo Humanoid Animations (Idle, Walk, Run, Jump, Crouch Idle,
    /// Crouch Walk, and Weapon-Specific Melee Attacks for Sword, Spear, and Axe), Attack Cooldown & Input
    /// Buffer to prevent spam, Weapon Placement in the Right Hand, and 1st/3rd-Person Camera Positioning
    /// with Pan & Tilt clamping.
    /// </summary>
    [DisallowMultipleComponent]
    public class PlayerCharacterController : MonoBehaviour
    {
        public enum WeaponType
        {
            Sword = 0,
            Spear = 1,
            Axe = 2
        }

        [Header("Setup Version (Auto-Managed)")]
        public int setupVersion = 0;

        [Header("References")]
        public FirstPersonController firstPersonController;
        public CharacterController characterController;
        public StarterAssetsInputs starterInputs;
        public Animator characterAnimator;
        public Transform characterModelRoot;
        public ModularHeroController modularHeroController;
        public Transform cameraRoot;
        public Camera mainCamera;
        public CinemachineThirdPersonFollow followCameraComponent;

        [Header("Player Health")]
        public int maxHealth = 100;
        public int currentHealth = 100;

        [Header("Weapons (1 = Sword, 2 = Spear, 3 = Axe)")]
        public WeaponType equippedWeapon = WeaponType.Sword;
        [Tooltip("Weapon socket attached to the 3D humanoid's right hand palm.")]
        public Transform rightHandWeaponSocket;
        [Tooltip("Weapons attached to the character's right hand (used in both 1st-person and 3rd-person).")]
        public GameObject[] thirdPersonWeapons = new GameObject[3];
        [Tooltip("Legacy FP weapon array (kept disabled so the real hand-held weapon is used).")]
        public GameObject[] firstPersonWeapons = new GameObject[3];
        public Transform firstPersonWeaponPivot;

        [Header("First-Person & Third-Person Character Meshes")]
        public SkinnedMeshRenderer bodySkinnedMeshRenderer;
        public Mesh fullBodyMesh;
        public Mesh firstPersonRightArmMesh;

        [Header("Movement & Crouch Settings (Walk, Run, Jump, Crouch)")]
        public float normalWalkSpeed = 4.0f;
        public float normalSprintSpeed = 6.0f;
        public float crouchMoveSpeed = 2.2f;
        public float standingHeight = 2.0f;
        public float crouchingHeight = 1.2f;
        public float standingCameraY = 1.50f;
        public float crouchingCameraY = 0.90f;
        public float crouchTransitionSpeed = 10.0f;
        public Vector3 standingDetectionOffset = new Vector3(0f, 1.0f, 0f);
        public Vector3 crouchingDetectionOffset = new Vector3(0f, 0.45f, 0f);

        [Header("Melee Combat & Attack Spam Buffer")]
        public float swordAttackRange = 2.6f;
        public float spearAttackRange = 3.5f;
        public float axeAttackRange = 2.7f;
        public float attackRadius = 1.0f;
        public int swordDamage = 25;
        public int spearDamage = 32;
        public int axeDamage = 45;
        [Tooltip("Minimum recovery pause after an attack finishes before the next attack can start (prevents spam).")]
        public float attackCooldownBuffer = 0.35f;
        [Tooltip("If the player presses attack slightly before cooldown ends, buffer 1 attack within this window.")]
        public float attackInputBufferWindow = 0.22f;
        public LayerMask enemyHitLayers = ~0;

        [Header("Camera View Mode & Pan/Tilt Limits (Press V to Toggle)")]
        public bool isThirdPersonView = false;
        public float thirdPersonCameraDistance = 2.4f;
        public Vector3 thirdPersonShoulderOffset = new Vector3(0.12f, 0.08f, 0f);
        public float firstPersonTopClamp = 70.0f;
        public float firstPersonBottomClamp = -65.0f;
        public float thirdPersonTopClamp = 42.0f;
        public float thirdPersonBottomClamp = -30.0f;
        public bool showWeaponHud = true;

        [Header("Audio")]
        public AudioClip[] footstepAudioClips;
        public AudioClip landingAudioClip;
        [Range(0f, 1f)] public float footstepVolume = 0.5f;

        // Runtime State
        public bool IsCrouching { get; private set; }
        public bool IsAttacking { get; private set; }
        public string LastHitMessage { get; private set; } = "";
        private float _lastHitMessageTime;

        private float _animationBlend;
        private float _moveXBlend;
        private float _moveZBlend;
        private float _currentHeight;
        private float _currentCameraY;
        private float _crouchWeight;
        private float _attackTimer;
        private float _attackDuration;
        private float _attackCooldownTimer;
        private float _bufferedAttackTimer;
        private bool _hasTriggeredHitThisSwing;
        private float _damageFlashTimer;
        private bool _wasGrounded = true;
        private float _stepCycleTimer;

        private readonly TrailRenderer[] _weaponTrails = new TrailRenderer[3];
        private static Material _sharedTrailMaterial;
        private static Texture2D _hudBarTex;

        // First-Person right-hand weapon ready poses per weapon type (Sword, Spear, Axe)
        private readonly Vector3 _fpSwordBaseLocalPos = new Vector3(0.24f, -0.22f, 0.46f);
        private readonly Quaternion _fpSwordBaseLocalRot = Quaternion.Euler(14f, -14f, 8f);

        private readonly Vector3 _fpSpearBaseLocalPos = new Vector3(0.26f, -0.24f, 0.50f);
        private readonly Quaternion _fpSpearBaseLocalRot = Quaternion.Euler(22f, -12f, 6f);

        private readonly Vector3 _fpAxeBaseLocalPos = new Vector3(0.25f, -0.22f, 0.45f);
        private readonly Quaternion _fpAxeBaseLocalRot = Quaternion.Euler(16f, -18f, 10f);

        // First-Person procedural bob & look sway state
        private float _fpBobBlend;
        private float _fpBobPhase;
        private Vector3 _fpSwayOffset;
        private Vector3 _fpSwayAngles;
        private float _lastCamYaw;
        private float _lastCamPitch;
        private bool _camAnglesInitialized;

        // Animator parameter hashes
        private int _animIDSpeed;
        private int _animIDMoveX;
        private int _animIDMoveZ;
        private int _animIDGrounded;
        private int _animIDJump;
        private int _animIDFreeFall;
        private int _animIDMotionSpeed;
        private int _animIDIsCrouching;
        private int _animIDWeaponType;
        private int _animIDAttack;
        private int _animIDFullBodyAttack;
        private int _animIDHit;

        // Cached humanoid bones
        private Transform _hipsBone;
        private Transform _spineBone;
        private Transform _chestBone;
        private Transform _neckBone;
        private Transform _headBone;
        private Transform _rightUpperArmBone;
        private Transform _rightLowerArmBone;
        private Transform _rightHandBone;
        private Transform _leftUpperArmBone;
        private Transform _leftLowerArmBone;
        private Transform _leftHandBone;
        private Transform _leftUpperLegBone;
        private Transform _leftLowerLegBone;
        private Transform _rightUpperLegBone;
        private Transform _rightLowerLegBone;

        private readonly List<Transform> _rightFingerBones = new List<Transform>();
        private readonly List<Transform> _rightThumbBones = new List<Transform>();
        private readonly List<Transform> _leftFingerBones = new List<Transform>();

        private void Awake()
        {
            if (firstPersonController == null) firstPersonController = GetComponent<FirstPersonController>();
            if (characterController == null) characterController = GetComponent<CharacterController>();
            if (starterInputs == null) starterInputs = GetComponent<StarterAssetsInputs>();
            if (cameraRoot == null && firstPersonController != null && firstPersonController.CinemachineCameraTarget != null)
                cameraRoot = firstPersonController.CinemachineCameraTarget.transform;
            if (characterAnimator == null) characterAnimator = GetComponentInChildren<Animator>();
            if (modularHeroController == null) modularHeroController = GetComponentInChildren<ModularHeroController>();
            if (mainCamera == null) mainCamera = Camera.main;

            if (followCameraComponent == null)
            {
                var followCamObj = GameObject.Find("PlayerFollowCamera");
                if (followCamObj != null)
                    followCameraComponent = followCamObj.GetComponent<CinemachineThirdPersonFollow>();
            }

            if (characterAnimator != null && characterAnimator.GetComponent<PlayerAnimationEventReceiver>() == null)
            {
                var receiver = characterAnimator.gameObject.AddComponent<PlayerAnimationEventReceiver>();
                receiver.owner = this;
            }

            AssignAnimatorHashes();
            CacheHumanoidBones();
        }

        private void Start()
        {
            currentHealth = maxHealth;
            _currentHeight = standingHeight;
            _currentCameraY = standingCameraY;

            if (mainCamera != null && mainCamera.nearClipPlane > 0.05f)
            {
                mainCamera.nearClipPlane = 0.04f;
            }

            RemoveAnyLeftoverShield();
            ClearStaticAndColliders(firstPersonWeapons);
            ClearStaticAndColliders(thirdPersonWeapons);
            EnsureFirstPersonArmMeshReady();
            EnsureRuntimeWeaponPlacement();
            EnsureWeaponSwingTrails();
            EnsureSceneEnemiesHaveCombatTargets();

            if (firstPersonWeaponPivot != null)
            {
                firstPersonWeaponPivot.gameObject.SetActive(false);
            }

            EquipWeapon(equippedWeapon);
            ApplyCameraViewMode();
        }

        private void EnsureFirstPersonArmMeshReady()
        {
#if UNITY_EDITOR
            if (characterModelRoot != null)
            {
                Material urpBodyMat = null;
                foreach (var smr in characterModelRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    if (smr != null && smr.gameObject.activeInHierarchy && smr.sharedMaterial != null)
                    {
                        urpBodyMat = smr.sharedMaterial;
                        break;
                    }
                }

                foreach (var mf in characterModelRoot.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (mf == null) continue;
                    if (rightHandWeaponSocket != null && mf.transform.IsChildOf(rightHandWeaponSocket)) continue;

                    mf.gameObject.SetActive(true);
                    if (mf.sharedMesh == null)
                    {
                        string attachPath = $"Assets/PolygonApocalypse/Models/{mf.gameObject.name}.fbx";
                        Mesh loadedMesh = UnityEditor.AssetDatabase.LoadAssetAtPath<Mesh>(attachPath);
                        if (loadedMesh != null)
                        {
                            mf.sharedMesh = loadedMesh;
                        }
                    }

                    var mr = mf.GetComponent<MeshRenderer>();
                    if (mr != null && urpBodyMat != null && (mr.sharedMaterial == null || mr.sharedMaterial.shader == null || !mr.sharedMaterial.shader.name.StartsWith("Universal Render Pipeline")))
                    {
                        mr.sharedMaterial = urpBodyMat;
                    }
                }
            }
#endif
            if (bodySkinnedMeshRenderer == null && characterModelRoot != null)
            {
                foreach (var smr in characterModelRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    if (smr != null && smr.gameObject.activeInHierarchy && smr.sharedMesh != null)
                    {
                        bodySkinnedMeshRenderer = smr;
                        break;
                    }
                }
            }

            if (bodySkinnedMeshRenderer != null)
            {
                if (fullBodyMesh == null && bodySkinnedMeshRenderer.sharedMesh != null && !bodySkinnedMeshRenderer.sharedMesh.name.EndsWith("_FPArm"))
                {
                    fullBodyMesh = bodySkinnedMeshRenderer.sharedMesh;
                }

                if (firstPersonRightArmMesh == null && fullBodyMesh != null && _rightLowerArmBone != null)
                {
                    firstPersonRightArmMesh = BuildFirstPersonRightArmMesh(bodySkinnedMeshRenderer, fullBodyMesh, _rightLowerArmBone);
                }
            }
        }

        public static Mesh BuildFirstPersonRightArmMesh(SkinnedMeshRenderer bodySmr, Mesh sourceMesh, Transform rightLowerArmBone)
        {
            if (bodySmr == null || sourceMesh == null || rightLowerArmBone == null || !sourceMesh.isReadable)
                return null;

            Transform[] bones = bodySmr.bones;
            if (bones == null || bones.Length == 0)
                return null;

            int elbowBoneIdx = System.Array.IndexOf(bones, rightLowerArmBone);
            if (elbowBoneIdx < 0)
                return null;

            HashSet<int> rightForearmAndHandBoneIndices = new HashSet<int>();
            for (int i = 0; i < bones.Length; i++)
            {
                if (bones[i] != null && (bones[i] == rightLowerArmBone || bones[i].IsChildOf(rightLowerArmBone)))
                {
                    rightForearmAndHandBoneIndices.Add(i);
                }
            }

            BoneWeight[] srcWeights = sourceMesh.boneWeights;
            if (srcWeights == null || srcWeights.Length != sourceMesh.vertexCount)
                return null;

            bool[] keepVertex = new bool[srcWeights.Length];
            BoneWeight[] remappedWeights = new BoneWeight[srcWeights.Length];

            for (int v = 0; v < srcWeights.Length; v++)
            {
                BoneWeight bw = srcWeights[v];
                float forearmWeight = 0f;
                if (rightForearmAndHandBoneIndices.Contains(bw.boneIndex0)) forearmWeight += bw.weight0;
                if (rightForearmAndHandBoneIndices.Contains(bw.boneIndex1)) forearmWeight += bw.weight1;
                if (rightForearmAndHandBoneIndices.Contains(bw.boneIndex2)) forearmWeight += bw.weight2;
                if (rightForearmAndHandBoneIndices.Contains(bw.boneIndex3)) forearmWeight += bw.weight3;

                if (forearmWeight >= 0.45f)
                {
                    keepVertex[v] = true;
                    // Reassign any upper-arm blend weight at the elbow crease to the elbow bone so 0% is anchored to the torso/shoulder
                    if (bw.weight0 > 0f && !rightForearmAndHandBoneIndices.Contains(bw.boneIndex0)) bw.boneIndex0 = elbowBoneIdx;
                    if (bw.weight1 > 0f && !rightForearmAndHandBoneIndices.Contains(bw.boneIndex1)) bw.boneIndex1 = elbowBoneIdx;
                    if (bw.weight2 > 0f && !rightForearmAndHandBoneIndices.Contains(bw.boneIndex2)) bw.boneIndex2 = elbowBoneIdx;
                    if (bw.weight3 > 0f && !rightForearmAndHandBoneIndices.Contains(bw.boneIndex3)) bw.boneIndex3 = elbowBoneIdx;
                }
                remappedWeights[v] = bw;
            }

            Mesh fpMesh = Object.Instantiate(sourceMesh);
            fpMesh.name = sourceMesh.name + "_FPArm";
            fpMesh.boneWeights = remappedWeights;

            int subMeshCount = sourceMesh.subMeshCount;
            int totalKeptTris = 0;
            for (int s = 0; s < subMeshCount; s++)
            {
                int[] tris = sourceMesh.GetTriangles(s);
                List<int> keptTris = new List<int>(tris.Length / 4);
                for (int t = 0; t + 2 < tris.Length; t += 3)
                {
                    int i0 = tris[t];
                    int i1 = tris[t + 1];
                    int i2 = tris[t + 2];
                    if (keepVertex[i0] && keepVertex[i1] && keepVertex[i2])
                    {
                        keptTris.Add(i0);
                        keptTris.Add(i1);
                        keptTris.Add(i2);
                    }
                }
                totalKeptTris += keptTris.Count;
                fpMesh.SetTriangles(keptTris, s);
            }

            if (totalKeptTris == 0)
            {
                Object.DestroyImmediate(fpMesh);
                return null;
            }

            fpMesh.RecalculateBounds();
            return fpMesh;
        }

        private void RemoveAnyLeftoverShield()
        {
            if (_leftHandBone != null)
            {
                Transform oldShieldSocket = _leftHandBone.Find("ShieldSocket_TP");
                if (oldShieldSocket != null)
                {
                    Destroy(oldShieldSocket.gameObject);
                }
            }
            if (characterModelRoot != null)
            {
                foreach (var t in characterModelRoot.GetComponentsInChildren<Transform>(true))
                {
                    if (t != null && (t.name == "ShieldSocket_TP" || t.name == "Shield_OffHand"))
                    {
                        Destroy(t.gameObject);
                    }
                }
            }
        }

        private static void EnsureSceneEnemiesHaveCombatTargets()
        {
            var enemies = FindObjectsByType<EnemyDetection>(FindObjectsSortMode.None);
            foreach (var det in enemies)
            {
                if (det != null && det.GetComponent<EnemyCombatTarget>() == null)
                {
                    det.gameObject.AddComponent<EnemyCombatTarget>();
                }
            }
        }

        private void EnsureRuntimeWeaponPlacement()
        {
            if (rightHandWeaponSocket == null && _rightHandBone != null)
            {
                rightHandWeaponSocket = _rightHandBone.Find("WeaponSocket_TP");
            }

            if (rightHandWeaponSocket != null && _rightHandBone != null)
            {
                ConfigureWeaponSocketForHandBone(rightHandWeaponSocket, _rightHandBone, characterAnimator);
            }

            if (thirdPersonWeapons == null) return;
            float[] targetLengths = new float[] { 0.88f, 1.30f, 0.80f }; // Sword, Spear, Axe

            for (int i = 0; i < thirdPersonWeapons.Length && i < 3; i++)
            {
                var holder = thirdPersonWeapons[i];
                if (holder == null || holder.transform.childCount == 0) continue;

                Transform meshObj = holder.transform.GetChild(0);
                var mf = meshObj.GetComponentInChildren<MeshFilter>();
                if (mf == null || mf.sharedMesh == null) continue;

                Mesh m = mf.sharedMesh;
                Vector3 size = m.bounds.size;
                float maxDim = Mathf.Max(size.x, size.y, size.z);
                float uniformScale = maxDim > 0.001f ? (targetLengths[i] / maxDim) : 1f;

                Quaternion alignRot = Quaternion.identity;
                if (size.z >= size.x && size.z >= size.y)
                    alignRot = Quaternion.Euler(90f, 0f, 0f);
                else if (size.x >= size.y && size.x >= size.z)
                    alignRot = Quaternion.Euler(0f, 0f, 90f);

                if (i == 2)
                    alignRot = Quaternion.Euler(0f, 90f, 0f) * alignRot;

                if (m.subMeshCount >= 2)
                {
                    var sm0 = m.GetSubMesh(0).bounds;
                    var sm1 = m.GetSubMesh(1).bounds;
                    Vector3 c0 = alignRot * sm0.center;
                    Vector3 c1 = alignRot * sm1.center;

                    int bladeIdx = (i == 0) ? 0 : (i == 1 ? 1 : (sm0.size.magnitude <= sm1.size.magnitude ? 0 : 1));
                    int handleIdx = 1 - bladeIdx;
                    Vector3 bladeCenter = (bladeIdx == 0) ? c0 : c1;
                    Vector3 handleCenter = (handleIdx == 0) ? c0 : c1;

                    if (bladeCenter.y < handleCenter.y)
                    {
                        alignRot = Quaternion.Euler(180f, 0f, 0f) * alignRot;
                        c0 = alignRot * sm0.center;
                        c1 = alignRot * sm1.center;
                        handleCenter = (handleIdx == 0) ? c0 : c1;
                    }

                    meshObj.localRotation = alignRot;
                    meshObj.localScale = Vector3.one * uniformScale;
                    if (i == 0)
                        meshObj.localPosition = -handleCenter * uniformScale;
                    else if (i == 1)
                        meshObj.localPosition = -(alignRot * m.bounds.center) * uniformScale + new Vector3(0f, targetLengths[i] * 0.16f, 0f);
                    else
                        meshObj.localPosition = -(alignRot * m.bounds.center) * uniformScale + new Vector3(0f, targetLengths[i] * 0.22f, 0f);
                }
            }
        }

        public static void ConfigureWeaponSocketForHandBone(Transform weaponSocket, Transform rightHand, Animator animator)
        {
            if (weaponSocket == null || rightHand == null) return;

            // Detect wrist-to-knuckles axis and pinky/middle-to-index axis automatically for any humanoid rig (Synty 3-finger or GanzSe 5-finger)
            Transform middleProximal = animator != null && animator.isHuman
                ? animator.GetBoneTransform(HumanBodyBones.RightMiddleProximal)
                : null;
            Transform indexProximal = animator != null && animator.isHuman
                ? animator.GetBoneTransform(HumanBodyBones.RightIndexProximal)
                : null;
            Transform pinkyProximal = animator != null && animator.isHuman
                ? animator.GetBoneTransform(HumanBodyBones.RightLittleProximal)
                : null;

            if (indexProximal == null)
                indexProximal = rightHand.Find("IndexFinger_01 1") ?? rightHand.Find("IndexFinger_01");
            if (middleProximal == null)
                middleProximal = rightHand.Find("Finger_01 1") ?? rightHand.Find("Finger_01");

            Transform knuckleRef = middleProximal ?? indexProximal;
            Vector3 wristToKnuckles = (knuckleRef != null && knuckleRef.parent == rightHand && knuckleRef.localPosition.sqrMagnitude > 0.0001f)
                ? knuckleRef.localPosition.normalized
                : Vector3.right;

            Vector3 pinkyToIndex = Vector3.back;
            Transform outerFingerRef = pinkyProximal ?? middleProximal;
            if (indexProximal != null && outerFingerRef != null && indexProximal.parent == rightHand && outerFingerRef.parent == rightHand)
            {
                Vector3 diff = indexProximal.localPosition - outerFingerRef.localPosition;
                Vector3 projected = Vector3.ProjectOnPlane(diff, wristToKnuckles);
                if (projected.sqrMagnitude > 0.00001f)
                    pinkyToIndex = projected.normalized;
            }

            Vector3 palmInward = -Vector3.Cross(wristToKnuckles, pinkyToIndex).normalized;
            float handLength = (knuckleRef != null && knuckleRef.parent == rightHand)
                ? knuckleRef.localPosition.magnitude
                : 0.10f;

            // Position handle right inside the closed palm and point blade out the top of the fist (pinkyToIndex) with a natural 26-degree ergonomic wrist tilt
            weaponSocket.localPosition = wristToKnuckles * (handLength * 0.78f) + palmInward * 0.032f + pinkyToIndex * 0.008f;
            weaponSocket.localRotation = Quaternion.LookRotation(wristToKnuckles, pinkyToIndex) * Quaternion.Euler(26f, 0f, 0f);
        }

        private void EnsureWeaponSwingTrails()
        {
            if (thirdPersonWeapons == null) return;
            float[] tipHeights = new float[] { 0.72f, 1.05f, 0.62f };

            if (_sharedTrailMaterial == null)
            {
                Shader trailShader = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                                     ?? Shader.Find("Universal Render Pipeline/Unlit")
                                     ?? Shader.Find("Sprites/Default");
                if (trailShader != null)
                {
                    _sharedTrailMaterial = new Material(trailShader);
                    if (_sharedTrailMaterial.HasProperty("_BaseColor"))
                        _sharedTrailMaterial.SetColor("_BaseColor", new Color(0.85f, 0.95f, 1f, 0.75f));
                }
            }

            for (int i = 0; i < thirdPersonWeapons.Length && i < 3; i++)
            {
                var holder = thirdPersonWeapons[i];
                if (holder == null) continue;

                Transform trailTransform = holder.transform.Find("BladeTrail");
                if (trailTransform == null)
                {
                    var trailObj = new GameObject("BladeTrail");
                    trailTransform = trailObj.transform;
                    trailTransform.SetParent(holder.transform, false);
                }
                trailTransform.localPosition = new Vector3(0f, tipHeights[i], 0f);
                trailTransform.localRotation = Quaternion.identity;

                var trail = trailTransform.GetComponent<TrailRenderer>();
                if (trail == null) trail = trailTransform.gameObject.AddComponent<TrailRenderer>();

                trail.time = 0.16f;
                trail.minVertexDistance = 0.02f;
                trail.startWidth = 0.12f;
                trail.endWidth = 0.0f;
                trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                trail.receiveShadows = false;
                if (_sharedTrailMaterial != null)
                {
                    trail.sharedMaterial = _sharedTrailMaterial;
                }

                Gradient grad = new Gradient();
                grad.SetKeys(
                    new[]
                    {
                        new GradientColorKey(new Color(0.9f, 0.96f, 1f), 0f),
                        new GradientColorKey(new Color(0.45f, 0.78f, 1f), 1f)
                    },
                    new[]
                    {
                        new GradientAlphaKey(0.75f, 0f),
                        new GradientAlphaKey(0.0f, 1f)
                    });
                trail.colorGradient = grad;
                trail.emitting = false;
                _weaponTrails[i] = trail;
            }
        }

        private static void ClearStaticAndColliders(GameObject[] weapons)
        {
            if (weapons == null) return;
            foreach (var w in weapons)
            {
                if (w == null) continue;
                foreach (var t in w.GetComponentsInChildren<Transform>(true))
                {
                    t.gameObject.isStatic = false;
                }
                foreach (var c in w.GetComponentsInChildren<Collider>(true))
                {
                    c.enabled = false;
                }
            }
        }

        private void AssignAnimatorHashes()
        {
            _animIDSpeed = Animator.StringToHash("Speed");
            _animIDMoveX = Animator.StringToHash("MoveX");
            _animIDMoveZ = Animator.StringToHash("MoveZ");
            _animIDGrounded = Animator.StringToHash("Grounded");
            _animIDJump = Animator.StringToHash("Jump");
            _animIDFreeFall = Animator.StringToHash("FreeFall");
            _animIDMotionSpeed = Animator.StringToHash("MotionSpeed");
            _animIDIsCrouching = Animator.StringToHash("IsCrouching");
            _animIDWeaponType = Animator.StringToHash("WeaponType");
            _animIDAttack = Animator.StringToHash("Attack");
            _animIDFullBodyAttack = Animator.StringToHash("FullBodyAttack");
            _animIDHit = Animator.StringToHash("Hit");
        }

        private void CacheHumanoidBones()
        {
            if (characterAnimator == null || !characterAnimator.isHuman) return;

            _hipsBone = characterAnimator.GetBoneTransform(HumanBodyBones.Hips);
            _spineBone = characterAnimator.GetBoneTransform(HumanBodyBones.Spine);
            _chestBone = characterAnimator.GetBoneTransform(HumanBodyBones.Chest);
            _neckBone = characterAnimator.GetBoneTransform(HumanBodyBones.Neck);
            _headBone = characterAnimator.GetBoneTransform(HumanBodyBones.Head);

            _rightUpperArmBone = characterAnimator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            _rightLowerArmBone = characterAnimator.GetBoneTransform(HumanBodyBones.RightLowerArm);
            _rightHandBone = characterAnimator.GetBoneTransform(HumanBodyBones.RightHand);

            _leftUpperArmBone = characterAnimator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            _leftLowerArmBone = characterAnimator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
            _leftHandBone = characterAnimator.GetBoneTransform(HumanBodyBones.LeftHand);

            _leftUpperLegBone = characterAnimator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
            _leftLowerLegBone = characterAnimator.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
            _rightUpperLegBone = characterAnimator.GetBoneTransform(HumanBodyBones.RightUpperLeg);
            _rightLowerLegBone = characterAnimator.GetBoneTransform(HumanBodyBones.RightLowerLeg);

            if (rightHandWeaponSocket == null && _rightHandBone != null)
            {
                rightHandWeaponSocket = _rightHandBone.Find("WeaponSocket_TP");
            }

            CacheFingerBones();
        }

        private void CacheFingerBones()
        {
            _rightFingerBones.Clear();
            _rightThumbBones.Clear();
            _leftFingerBones.Clear();

            HumanBodyBones[] rFingers = new HumanBodyBones[]
            {
                HumanBodyBones.RightIndexProximal, HumanBodyBones.RightIndexIntermediate, HumanBodyBones.RightIndexDistal,
                HumanBodyBones.RightMiddleProximal, HumanBodyBones.RightMiddleIntermediate, HumanBodyBones.RightMiddleDistal,
                HumanBodyBones.RightRingProximal, HumanBodyBones.RightRingIntermediate, HumanBodyBones.RightRingDistal,
                HumanBodyBones.RightLittleProximal, HumanBodyBones.RightLittleIntermediate, HumanBodyBones.RightLittleDistal
            };
            foreach (var b in rFingers)
            {
                var t = characterAnimator.GetBoneTransform(b);
                if (t != null) _rightFingerBones.Add(t);
            }

            HumanBodyBones[] rThumbs = new HumanBodyBones[]
            {
                HumanBodyBones.RightThumbProximal, HumanBodyBones.RightThumbIntermediate, HumanBodyBones.RightThumbDistal
            };
            foreach (var b in rThumbs)
            {
                var t = characterAnimator.GetBoneTransform(b);
                if (t != null) _rightThumbBones.Add(t);
            }

            HumanBodyBones[] lFingers = new HumanBodyBones[]
            {
                HumanBodyBones.LeftIndexProximal, HumanBodyBones.LeftIndexIntermediate, HumanBodyBones.LeftIndexDistal,
                HumanBodyBones.LeftMiddleProximal, HumanBodyBones.LeftMiddleIntermediate, HumanBodyBones.LeftMiddleDistal,
                HumanBodyBones.LeftRingProximal, HumanBodyBones.LeftRingIntermediate, HumanBodyBones.LeftRingDistal,
                HumanBodyBones.LeftLittleProximal, HumanBodyBones.LeftLittleIntermediate, HumanBodyBones.LeftLittleDistal
            };
            foreach (var b in lFingers)
            {
                var t = characterAnimator.GetBoneTransform(b);
                if (t != null) _leftFingerBones.Add(t);
            }
        }

        private void Update()
        {
            if (_animIDSpeed == 0) AssignAnimatorHashes();
            if (_hipsBone == null) CacheHumanoidBones();

            if (_damageFlashTimer > 0f)
            {
                _damageFlashTimer -= Time.deltaTime;
            }

            HandleCameraViewToggleInput();
            HandleWeaponSwitchInput();
            HandleCrouchInput();
            HandleMeleeAttackWithBuffer();
            UpdateWeaponTrailState();
            UpdateLocomotionAndAnimator();
            UpdateFootstepAudio();
        }

        private void LateUpdate()
        {
            if (_hipsBone == null) CacheHumanoidBones();
            ApplyProceduralModelAndFingerGrip();
            UpdateFirstPersonArmsAndWeaponPose();
        }

        private static bool WasKeyPressed(KeyCode legacyKey
#if ENABLE_INPUT_SYSTEM
            , Key newInputKey
#endif
        )
        {
            if (Input.GetKeyDown(legacyKey)) return true;
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && Keyboard.current[newInputKey].wasPressedThisFrame) return true;
#endif
            return false;
        }

        private static bool WasAttackPressed()
        {
            if (Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.F)) return true;
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) return true;
            if (Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame) return true;
#endif
            return false;
        }

        private void HandleCameraViewToggleInput()
        {
            if (WasKeyPressed(KeyCode.V
#if ENABLE_INPUT_SYSTEM
                , Key.V
#endif
            ))
            {
                isThirdPersonView = !isThirdPersonView;
                ApplyCameraViewMode();
            }
        }

        public void ApplyCameraViewMode()
        {
            // Normalize any legacy serialized 3rd-person offsets that placed the pivot above the character's head
            if (thirdPersonShoulderOffset.y > 0.15f || thirdPersonShoulderOffset.x > 0.22f)
            {
                thirdPersonShoulderOffset = new Vector3(0.12f, 0.08f, 0f);
            }
            if (thirdPersonCameraDistance > 2.6f || thirdPersonCameraDistance < 1.8f)
            {
                thirdPersonCameraDistance = 2.4f;
            }
            if (thirdPersonTopClamp > 45f)
            {
                thirdPersonTopClamp = 42f;
            }

            if (firstPersonController != null)
            {
                firstPersonController.TopClamp = isThirdPersonView ? thirdPersonTopClamp : firstPersonTopClamp;
                firstPersonController.BottomClamp = isThirdPersonView ? thirdPersonBottomClamp : firstPersonBottomClamp;
            }

            if (followCameraComponent != null)
            {
                followCameraComponent.CameraDistance = isThirdPersonView ? thirdPersonCameraDistance : 0f;
                followCameraComponent.ShoulderOffset = isThirdPersonView ? thirdPersonShoulderOffset : Vector3.zero;
                followCameraComponent.VerticalArmLength = isThirdPersonView ? 0.05f : 0f;
                followCameraComponent.CameraSide = 0.55f;

                var vcam = followCameraComponent.GetComponent<CinemachineCamera>();
                if (vcam != null)
                {
                    var lens = vcam.Lens;
                    lens.FieldOfView = 55f;
                    lens.NearClipPlane = 0.05f;
                    vcam.Lens = lens;
                }

                var perlin = followCameraComponent.GetComponent<CinemachineBasicMultiChannelPerlin>();
                if (perlin != null)
                {
                    perlin.PivotOffset = Vector3.zero;
                    perlin.AmplitudeGain = isThirdPersonView ? 0.12f : 0.05f;
                }
            }

            if (firstPersonWeaponPivot != null)
            {
                firstPersonWeaponPivot.gameObject.SetActive(false);
            }

            EnsureFirstPersonArmMeshReady();

            if (characterModelRoot != null)
            {
                foreach (var smr in characterModelRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    smr.updateWhenOffscreen = true;

                    if (smr == bodySkinnedMeshRenderer && fullBodyMesh != null && firstPersonRightArmMesh != null)
                    {
                        if (isThirdPersonView)
                        {
                            smr.sharedMesh = fullBodyMesh;
                            smr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                            smr.enabled = true;
                        }
                        else
                        {
                            // In First-Person View, swap to the right-forearm-and-hand-only mesh so there is ZERO torso/shoulder/neck clipping or polygon stretching
                            smr.sharedMesh = firstPersonRightArmMesh;
                            smr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                            smr.enabled = true;
                        }
                        continue;
                    }

                    bool isHeadOrChestPart = IsUnderNamedParent(smr.transform, "HEADS") ||
                                             IsUnderNamedParent(smr.transform, "FACE DETAILS PARTS") ||
                                             IsUnderNamedParent(smr.transform, "CHESTS");

                    if (!isThirdPersonView && isHeadOrChestPart)
                    {
                        smr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
                    }
                    else
                    {
                        smr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                    }
                }

                // Show hair (dreadlocks), backpack, and hip pouch in 3rd-person view; hide them in 1st-person view so they never clip the camera
                foreach (var mr in characterModelRoot.GetComponentsInChildren<MeshRenderer>(true))
                {
                    if (rightHandWeaponSocket != null && mr.transform.IsChildOf(rightHandWeaponSocket)) continue;
                    mr.enabled = isThirdPersonView;
                    mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                }
            }

            if (thirdPersonWeapons != null)
            {
                for (int i = 0; i < thirdPersonWeapons.Length; i++)
                {
                    if (thirdPersonWeapons[i] == null) continue;
                    foreach (var r in thirdPersonWeapons[i].GetComponentsInChildren<Renderer>(true))
                    {
                        r.enabled = true;
                    }
                }
            }
        }

        private static bool IsUnderNamedParent(Transform t, string parentName)
        {
            Transform curr = t;
            while (curr != null)
            {
                if (curr.name == parentName) return true;
                curr = curr.parent;
            }
            return false;
        }

        private void HandleWeaponSwitchInput()
        {
            if (IsAttacking) return;

            if (WasKeyPressed(KeyCode.Alpha1
#if ENABLE_INPUT_SYSTEM
                , Key.Digit1
#endif
            ) || WasKeyPressed(KeyCode.Keypad1
#if ENABLE_INPUT_SYSTEM
                , Key.Numpad1
#endif
            ))
            {
                EquipWeapon(WeaponType.Sword);
            }
            else if (WasKeyPressed(KeyCode.Alpha2
#if ENABLE_INPUT_SYSTEM
                , Key.Digit2
#endif
            ) || WasKeyPressed(KeyCode.Keypad2
#if ENABLE_INPUT_SYSTEM
                , Key.Numpad2
#endif
            ))
            {
                EquipWeapon(WeaponType.Spear);
            }
            else if (WasKeyPressed(KeyCode.Alpha3
#if ENABLE_INPUT_SYSTEM
                , Key.Digit3
#endif
            ) || WasKeyPressed(KeyCode.Keypad3
#if ENABLE_INPUT_SYSTEM
                , Key.Numpad3
#endif
            ))
            {
                EquipWeapon(WeaponType.Axe);
            }
            else
            {
                float scroll = Input.GetAxis("Mouse ScrollWheel");
#if ENABLE_INPUT_SYSTEM
                if (Mathf.Abs(scroll) < 0.01f && Mouse.current != null)
                {
                    scroll = Mouse.current.scroll.ReadValue().y * 0.01f;
                }
#endif
                if (scroll > 0.04f)
                    EquipWeapon((WeaponType)(((int)equippedWeapon + 1) % 3));
                else if (scroll < -0.04f)
                    EquipWeapon((WeaponType)(((int)equippedWeapon + 2) % 3));
            }
        }

        public void EquipWeapon(WeaponType weapon)
        {
            equippedWeapon = weapon;
            int activeIdx = (int)weapon;

            if (thirdPersonWeapons != null)
            {
                for (int i = 0; i < thirdPersonWeapons.Length; i++)
                {
                    if (thirdPersonWeapons[i] != null)
                        thirdPersonWeapons[i].SetActive(i == activeIdx);
                }
            }

            if (characterAnimator != null)
            {
                characterAnimator.SetInteger(_animIDWeaponType, activeIdx);
            }
        }

        private void UpdateWeaponTrailState()
        {
            int activeIdx = (int)equippedWeapon;
            for (int i = 0; i < _weaponTrails.Length; i++)
            {
                if (_weaponTrails[i] != null)
                {
                    _weaponTrails[i].emitting = IsAttacking && (i == activeIdx);
                }
            }
        }

        private void HandleCrouchInput()
        {
            bool crouchPressed = WasKeyPressed(KeyCode.C
#if ENABLE_INPUT_SYSTEM
                , Key.C
#endif
            ) || WasKeyPressed(KeyCode.LeftControl
#if ENABLE_INPUT_SYSTEM
                , Key.LeftCtrl
#endif
            );

            if (crouchPressed)
            {
                IsCrouching = !IsCrouching;
                UpdateEnemyDetectionTargetOffset();
            }

            if (firstPersonController != null && starterInputs != null && starterInputs.jump && IsCrouching)
            {
                IsCrouching = false;
                UpdateEnemyDetectionTargetOffset();
            }

            float targetHeight = IsCrouching ? crouchingHeight : standingHeight;
            float standingRootY = isThirdPersonView ? 1.05f : Mathf.Max(1.46f, standingCameraY);
            float crouchingRootY = isThirdPersonView ? 0.68f : Mathf.Max(0.85f, crouchingCameraY);
            float targetCamY = IsCrouching ? crouchingRootY : standingRootY;
            float targetCrouchWeight = IsCrouching ? 1f : 0f;

            _currentHeight = Mathf.Lerp(_currentHeight, targetHeight, Time.deltaTime * crouchTransitionSpeed);
            _currentCameraY = Mathf.Lerp(_currentCameraY, targetCamY, Time.deltaTime * crouchTransitionSpeed);
            _crouchWeight = Mathf.MoveTowards(_crouchWeight, targetCrouchWeight, Time.deltaTime * 6f);

            if (characterController != null)
            {
                characterController.height = _currentHeight;
                characterController.center = new Vector3(0f, _currentHeight * 0.5f, 0f);
            }

            if (cameraRoot != null)
            {
                cameraRoot.localPosition = new Vector3(0f, _currentCameraY, 0f);
            }

            if (followCameraComponent != null && isThirdPersonView && followCameraComponent.VerticalArmLength > 0.10f)
            {
                ApplyCameraViewMode();
            }

            if (firstPersonController != null)
            {
                firstPersonController.MoveSpeed = IsCrouching ? crouchMoveSpeed : normalWalkSpeed;
                firstPersonController.SprintSpeed = IsCrouching ? crouchMoveSpeed : normalSprintSpeed;
            }
        }

        private void UpdateEnemyDetectionTargetOffset()
        {
            Vector3 offset = IsCrouching ? crouchingDetectionOffset : standingDetectionOffset;
            var detectors = FindObjectsByType<EnemyDetection>(FindObjectsSortMode.None);
            foreach (var detector in detectors)
            {
                if (detector != null)
                    detector.targetOffset = offset;
            }
        }

        private void HandleMeleeAttackWithBuffer()
        {
            if (_bufferedAttackTimer > 0f)
            {
                _bufferedAttackTimer -= Time.deltaTime;
            }

            if (_attackCooldownTimer > 0f)
            {
                _attackCooldownTimer -= Time.deltaTime;
            }

            // Record attack input into a short buffer window so spamming LMB queues at most 1 attack after recovery
            if (WasAttackPressed())
            {
                _bufferedAttackTimer = attackInputBufferWindow;
            }

            if (IsAttacking)
            {
                _attackTimer += Time.deltaTime;

                // Trigger hit detection at 35% of the weapon swing
                if (!_hasTriggeredHitThisSwing && _attackTimer >= _attackDuration * 0.35f)
                {
                    _hasTriggeredHitThisSwing = true;
                    PerformMeleeHitDetection();
                }

                if (_attackTimer >= _attackDuration)
                {
                    IsAttacking = false;
                    // Enforce buffer pause after attack finishes before next attack can start
                    _attackCooldownTimer = attackCooldownBuffer;
                }
                return;
            }

            // Only start the next attack when both the swing and the buffer cooldown have completed
            if (_bufferedAttackTimer > 0f && _attackCooldownTimer <= 0f)
            {
                _bufferedAttackTimer = 0f;
                StartWeaponAttack();
            }
        }

        private void StartWeaponAttack()
        {
            IsAttacking = true;
            _attackTimer = 0f;
            _hasTriggeredHitThisSwing = false;

            // Distinct duration per weapon type matching the Mixamo attack animations
            _attackDuration = equippedWeapon == WeaponType.Sword ? 0.68f :
                              equippedWeapon == WeaponType.Spear ? 0.78f : 0.84f;

            if (characterAnimator != null)
            {
                characterAnimator.SetInteger(_animIDWeaponType, (int)equippedWeapon);
                characterAnimator.ResetTrigger(_animIDAttack);
                characterAnimator.SetTrigger(_animIDAttack);

                bool isStandingStill = _animationBlend < 0.25f && !IsCrouching && (firstPersonController == null || firstPersonController.Grounded);
                if (isStandingStill)
                {
                    characterAnimator.ResetTrigger(_animIDFullBodyAttack);
                    characterAnimator.SetTrigger(_animIDFullBodyAttack);
                }
            }

            if (footstepAudioClips != null && footstepAudioClips.Length > 0 && footstepAudioClips[0] != null)
            {
                AudioSource.PlayClipAtPoint(footstepAudioClips[0], transform.position + Vector3.up, 0.45f);
            }
        }

        private void PerformMeleeHitDetection()
        {
            float effectiveRange = equippedWeapon == WeaponType.Spear ? spearAttackRange :
                                   (equippedWeapon == WeaponType.Axe ? axeAttackRange : swordAttackRange);
            int finalDamage = equippedWeapon == WeaponType.Axe ? axeDamage :
                              (equippedWeapon == WeaponType.Spear ? spearDamage : swordDamage);

            Vector3 hitCenter = transform.position + Vector3.up * 1.0f + transform.forward * (effectiveRange * 0.55f);
            Collider[] hits = Physics.OverlapSphere(hitCenter, attackRadius, enemyHitLayers, QueryTriggerInteraction.Ignore);

            bool hitEnemy = false;
            bool hitSurface = false;
            Vector3 surfaceSparkPos = hitCenter;

            foreach (var hit in hits)
            {
                if (hit.transform.IsChildOf(transform)) continue;

                var enemy = hit.GetComponentInParent<EnemyDetection>();
                var combatTarget = hit.GetComponentInParent<EnemyCombatTarget>();

                if (enemy != null || combatTarget != null)
                {
                    if (combatTarget == null && enemy != null)
                    {
                        combatTarget = enemy.gameObject.AddComponent<EnemyCombatTarget>();
                    }

                    if (combatTarget != null && !combatTarget.IsDead)
                    {
                        hitEnemy = true;
                        combatTarget.TakeDamage(finalDamage, false);

                        string targetName = (enemy != null ? enemy.name : combatTarget.name).Replace("Enemy_", "");
                        string statusSuffix = combatTarget.IsDead
                            ? " -> DEFEATED!"
                            : $" ({combatTarget.currentHealth}/{combatTarget.maxHealth} HP)";
                        LastHitMessage = $"Hit {targetName} with {equippedWeapon} for {finalDamage} DMG!{statusSuffix}";
                        _lastHitMessageTime = Time.time;
                        break;
                    }
                }
                else
                {
                    hitSurface = true;
                    surfaceSparkPos = hit.ClosestPoint(hitCenter);
                }
            }

            if (!hitEnemy)
            {
                if (hitSurface)
                {
                    EnemyCombatTarget.SpawnHitSparkBurst(surfaceSparkPos);
                }

                string attackStyle = equippedWeapon == WeaponType.Sword ? "Sword Slash" :
                                     (equippedWeapon == WeaponType.Spear ? "Spear Horizontal Strike" : "Axe Downward Cleave");
                LastHitMessage = $"{equippedWeapon} — {attackStyle}";
                _lastHitMessageTime = Time.time;
            }
        }

        public void OnEnemyAttackStrike(EnemyCombatTarget attacker, int incomingDamage)
        {
            if (attacker != null && attacker.IsDead) return;
            if (incomingDamage <= 0) return;

            currentHealth = Mathf.Max(0, currentHealth - incomingDamage);
            _damageFlashTimer = 0.30f;

            if (characterAnimator != null)
            {
                characterAnimator.SetTrigger(_animIDHit);
            }

            string attackerName = attacker != null ? attacker.name.Replace("Enemy_", "") : "Enemy";
            if (currentHealth <= 0)
            {
                currentHealth = maxHealth;
                LastHitMessage = "Player Respawned (100/100 HP)";
            }
            else
            {
                LastHitMessage = $"Took {incomingDamage} DMG from {attackerName}! ({currentHealth}/{maxHealth} HP)";
            }
            _lastHitMessageTime = Time.time;
        }

        private void UpdateLocomotionAndAnimator()
        {
            if (characterAnimator == null || firstPersonController == null || starterInputs == null) return;

            bool isGrounded = firstPersonController.Grounded;
            float targetSpeed = 0f;
            Vector2 moveInput = starterInputs.move;

            if (moveInput != Vector2.zero)
            {
                targetSpeed = IsCrouching ? crouchMoveSpeed :
                              (starterInputs.sprint ? normalSprintSpeed : normalWalkSpeed);
            }

            float speedRate = firstPersonController.SpeedChangeRate;
            if (targetSpeed > 0.01f)
            {
                _animationBlend = Mathf.Lerp(_animationBlend, targetSpeed, Time.deltaTime * speedRate);
            }
            else
            {
                _animationBlend = Mathf.MoveTowards(_animationBlend, 0f, Time.deltaTime * 28f);
                if (_animationBlend < 0.15f) _animationBlend = 0f;
            }

            Vector2 moveDir = moveInput.sqrMagnitude > 0.001f ? moveInput.normalized : Vector2.zero;
            float targetMoveX = moveDir.x * targetSpeed;
            float targetMoveZ = moveDir.y * targetSpeed;

            if (targetSpeed > 0.01f)
            {
                _moveXBlend = Mathf.Lerp(_moveXBlend, targetMoveX, Time.deltaTime * speedRate);
                _moveZBlend = Mathf.Lerp(_moveZBlend, targetMoveZ, Time.deltaTime * speedRate);
            }
            else
            {
                _moveXBlend = Mathf.MoveTowards(_moveXBlend, 0f, Time.deltaTime * 28f);
                _moveZBlend = Mathf.MoveTowards(_moveZBlend, 0f, Time.deltaTime * 28f);
            }
            if (Mathf.Abs(_moveXBlend) < 0.05f) _moveXBlend = 0f;
            if (Mathf.Abs(_moveZBlend) < 0.05f) _moveZBlend = 0f;

            float inputMagnitude = (starterInputs.analogMovement && moveInput != Vector2.zero)
                ? Mathf.Max(0.5f, moveInput.magnitude)
                : 1f;

            characterAnimator.SetFloat(_animIDSpeed, _animationBlend);
            characterAnimator.SetFloat(_animIDMoveX, _moveXBlend);
            characterAnimator.SetFloat(_animIDMoveZ, _moveZBlend);
            characterAnimator.SetFloat(_animIDMotionSpeed, inputMagnitude);
            characterAnimator.SetBool(_animIDGrounded, isGrounded);
            characterAnimator.SetBool(_animIDIsCrouching, IsCrouching);
            characterAnimator.SetInteger(_animIDWeaponType, (int)equippedWeapon);

            bool jumpTriggered = firstPersonController.JumpTriggeredThisFrame;
            if (jumpTriggered)
            {
                firstPersonController.JumpTriggeredThisFrame = false;
            }

            // Always keep Jump and FreeFall bools false so JumpStart never loops or triggers a second fall/land cycle;
            // trigger JumpStart once via CrossFadeInFixedTime on the exact frame Space is pressed.
            characterAnimator.SetBool(_animIDJump, false);
            characterAnimator.SetBool(_animIDFreeFall, false);

            if (jumpTriggered && !IsCrouching)
            {
                characterAnimator.CrossFadeInFixedTime("JumpStart", 0.06f, 0, 0.25f);
            }

            if (!_wasGrounded && isGrounded && landingAudioClip != null)
            {
                AudioSource.PlayClipAtPoint(landingAudioClip, transform.position, footstepVolume);
            }
            _wasGrounded = isGrounded;
        }

        private void UpdateFootstepAudio()
        {
            if (firstPersonController == null || !firstPersonController.Grounded || starterInputs == null) return;
            if (starterInputs.move == Vector2.zero || footstepAudioClips == null || footstepAudioClips.Length == 0)
            {
                _stepCycleTimer = 0f;
                return;
            }

            float speed = IsCrouching ? crouchMoveSpeed : (starterInputs.sprint ? normalSprintSpeed : normalWalkSpeed);
            float stepInterval = IsCrouching ? 0.65f : (starterInputs.sprint ? 0.34f : 0.48f);

            _stepCycleTimer += Time.deltaTime;
            if (_stepCycleTimer >= stepInterval && speed > 0.1f)
            {
                _stepCycleTimer = 0f;
                int idx = Random.Range(0, footstepAudioClips.Length);
                if (footstepAudioClips[idx] != null)
                {
                    float vol = IsCrouching ? footstepVolume * 0.4f : footstepVolume;
                    AudioSource.PlayClipAtPoint(footstepAudioClips[idx], transform.position, vol);
                }
            }
        }

        private void ApplyProceduralModelAndFingerGrip()
        {
            bool usingFpArmOnlyMesh = !isThirdPersonView && firstPersonRightArmMesh != null && bodySkinnedMeshRenderer != null;

            if (characterModelRoot != null)
            {
                if (isThirdPersonView || usingFpArmOnlyMesh)
                {
                    characterModelRoot.localPosition = Vector3.zero;
                    characterModelRoot.localRotation = Quaternion.identity;
                }
                else
                {
                    // Fallback if using non-Synty modular mesh without FP arm submesh
                    characterModelRoot.localPosition = new Vector3(0f, -0.22f - 0.30f * _crouchWeight, -0.38f);
                    characterModelRoot.localRotation = Quaternion.identity;
                }
            }

            if (_headBone != null)
            {
                _headBone.localScale = (isThirdPersonView || usingFpArmOnlyMesh) ? Vector3.one : Vector3.one * 0.001f;
            }

            // Lock Root & Hips in characterModelRoot space so non-"In Place" Mixamo clips
            // (Walking, Running, Crouched_Walking, Jumping, Attacks) stay 100% in-place inside PlayerCapsule
            // and never walk/run forward or bounce vertically out of the capsule and snap/loop back.
            if (_hipsBone != null && characterModelRoot != null)
            {
                if (_hipsBone.parent != null && _hipsBone.parent != characterModelRoot)
                {
                    _hipsBone.parent.localPosition = new Vector3(0f, 0.028f, 0f);
                    _hipsBone.parent.localRotation = Quaternion.identity;
                }

                Vector3 hipsLocal = characterModelRoot.InverseTransformPoint(_hipsBone.position);
                hipsLocal.x = 0f;
                hipsLocal.z = 0f;
                float minHipsY = IsCrouching ? 0.52f : 0.84f;
                hipsLocal.y = Mathf.Clamp(hipsLocal.y, minHipsY, 0.94f);
                _hipsBone.position = characterModelRoot.TransformPoint(hipsLocal);
            }

            // Detect whether the rig uses +X along the right arm/hand (Synty Polygon Apocalypse) or +Y (GanzSe)
            bool isSyntyXAxisRig = _rightHandBone != null && Mathf.Abs(_rightHandBone.localPosition.x) > Mathf.Abs(_rightHandBone.localPosition.y);

            // Curl the right-hand fingers tightly into the palm around the weapon handle
            Quaternion rightFingerCurl = isSyntyXAxisRig
                ? Quaternion.Euler(0f, 0f, -54f)
                : Quaternion.Euler(0f, 0f, 62f);
            for (int i = 0; i < _rightFingerBones.Count; i++)
            {
                if (_rightFingerBones[i] != null)
                    _rightFingerBones[i].localRotation = rightFingerCurl;
            }

            Quaternion rightThumbCurl = isSyntyXAxisRig
                ? Quaternion.Euler(-16f, 18f, -34f)
                : Quaternion.Euler(25f, 0f, 38f);
            for (int i = 0; i < _rightThumbBones.Count; i++)
            {
                if (_rightThumbBones[i] != null)
                    _rightThumbBones[i].localRotation *= rightThumbCurl;
            }
        }

        private static Quaternion AlignBoneAxisWithWorldDir(Vector3 boneLocalAxis, Vector3 localPerpAxis, Vector3 worldDir, Vector3 worldPerp)
        {
            if (boneLocalAxis.sqrMagnitude < 0.0001f) boneLocalAxis = Vector3.right;
            boneLocalAxis.Normalize();
            localPerpAxis = Vector3.ProjectOnPlane(localPerpAxis, boneLocalAxis);
            if (localPerpAxis.sqrMagnitude < 0.0001f)
            {
                localPerpAxis = Mathf.Abs(boneLocalAxis.y) < 0.9f ? Vector3.up : Vector3.forward;
            }
            localPerpAxis.Normalize();

            if (worldDir.sqrMagnitude < 0.0001f) worldDir = Vector3.forward;
            worldDir.Normalize();
            worldPerp = Vector3.ProjectOnPlane(worldPerp, worldDir);
            if (worldPerp.sqrMagnitude < 0.0001f) worldPerp = Vector3.up;
            worldPerp.Normalize();

            return Quaternion.LookRotation(worldPerp, worldDir) * Quaternion.Inverse(Quaternion.LookRotation(localPerpAxis, boneLocalAxis));
        }

        private void EvaluateFirstPersonAttackPose(float normalizedTime, out Vector3 posOffset, out Quaternion rotOffset)
        {
            float t = Mathf.Clamp01(normalizedTime);

            if (equippedWeapon == WeaponType.Sword)
            {
                // Sword: 3-phase diagonal slash (Windup -> Fast Diagonal Slash -> Follow-Through & Recovery)
                Vector3 windupPos = new Vector3(0.11f, 0.09f, -0.05f);
                Quaternion windupRot = Quaternion.Euler(-22f, 26f, 28f);

                Vector3 strikePos = new Vector3(-0.34f, -0.06f, 0.14f);
                Quaternion strikeRot = Quaternion.Euler(52f, -64f, -56f);

                if (t < 0.22f)
                {
                    float p = Mathf.SmoothStep(0f, 1f, t / 0.22f);
                    posOffset = Vector3.Lerp(Vector3.zero, windupPos, p);
                    rotOffset = Quaternion.Slerp(Quaternion.identity, windupRot, p);
                }
                else if (t < 0.54f)
                {
                    float p = Mathf.SmoothStep(0f, 1f, (t - 0.22f) / 0.32f);
                    posOffset = Vector3.Lerp(windupPos, strikePos, p) + new Vector3(0f, 0.03f * Mathf.Sin(p * Mathf.PI), 0.08f * Mathf.Sin(p * Mathf.PI));
                    rotOffset = Quaternion.Slerp(windupRot, strikeRot, p);
                }
                else
                {
                    float p = Mathf.SmoothStep(0f, 1f, (t - 0.54f) / 0.46f);
                    posOffset = Vector3.Lerp(strikePos, Vector3.zero, p);
                    rotOffset = Quaternion.Slerp(strikeRot, Quaternion.identity, p);
                }
            }
            else if (equippedWeapon == WeaponType.Spear)
            {
                // Spear: 3-phase polearm draw-back -> long forward piercing thrust + horizontal sweep -> recovery
                Vector3 windupPos = new Vector3(0.10f, -0.04f, -0.14f);
                Quaternion windupRot = Quaternion.Euler(14f, 22f, 18f);

                Vector3 thrustPos = new Vector3(-0.18f, 0.04f, 0.28f);
                Quaternion thrustRot = Quaternion.Euler(56f, -38f, -36f);

                if (t < 0.22f)
                {
                    float p = Mathf.SmoothStep(0f, 1f, t / 0.22f);
                    posOffset = Vector3.Lerp(Vector3.zero, windupPos, p);
                    rotOffset = Quaternion.Slerp(Quaternion.identity, windupRot, p);
                }
                else if (t < 0.54f)
                {
                    float p = Mathf.SmoothStep(0f, 1f, (t - 0.22f) / 0.32f);
                    posOffset = Vector3.Lerp(windupPos, thrustPos, p) + new Vector3(0.06f * Mathf.Sin(p * Mathf.PI), 0f, 0.06f * Mathf.Sin(p * Mathf.PI));
                    rotOffset = Quaternion.Slerp(windupRot, thrustRot, p);
                }
                else
                {
                    float p = Mathf.SmoothStep(0f, 1f, (t - 0.54f) / 0.46f);
                    posOffset = Vector3.Lerp(thrustPos, Vector3.zero, p);
                    rotOffset = Quaternion.Slerp(thrustRot, Quaternion.identity, p);
                }
            }
            else
            {
                // Axe: 3-phase heavy overhead raise -> powerful vertical downward cleave -> weighted recovery
                Vector3 windupPos = new Vector3(-0.04f, 0.22f, -0.08f);
                Quaternion windupRot = Quaternion.Euler(-42f, 8f, -10f);

                Vector3 cleavePos = new Vector3(-0.10f, -0.18f, 0.18f);
                Quaternion cleaveRot = Quaternion.Euler(76f, -20f, -14f);

                if (t < 0.26f)
                {
                    float p = Mathf.SmoothStep(0f, 1f, t / 0.26f);
                    posOffset = Vector3.Lerp(Vector3.zero, windupPos, p);
                    rotOffset = Quaternion.Slerp(Quaternion.identity, windupRot, p);
                }
                else if (t < 0.56f)
                {
                    float p = Mathf.SmoothStep(0f, 1f, (t - 0.26f) / 0.30f);
                    posOffset = Vector3.Lerp(windupPos, cleavePos, p) + new Vector3(0f, 0f, 0.08f * Mathf.Sin(p * Mathf.PI));
                    rotOffset = Quaternion.Slerp(windupRot, cleaveRot, p);
                }
                else
                {
                    float p = Mathf.SmoothStep(0f, 1f, (t - 0.56f) / 0.44f);
                    posOffset = Vector3.Lerp(cleavePos, Vector3.zero, p);
                    rotOffset = Quaternion.Slerp(cleaveRot, Quaternion.identity, p);
                }
            }
        }

        private void UpdateFirstPersonArmsAndWeaponPose()
        {
            if (isThirdPersonView) return;
            if (_rightHandBone == null || _rightLowerArmBone == null) return;

            if (mainCamera == null) mainCamera = Camera.main;
            Transform camTransform = mainCamera != null ? mainCamera.transform : cameraRoot;
            if (camTransform == null) return;

            // 1. Smooth camera look sway
            Vector3 camEuler = camTransform.eulerAngles;
            if (!_camAnglesInitialized)
            {
                _lastCamYaw = camEuler.y;
                _lastCamPitch = camEuler.x;
                _camAnglesInitialized = true;
            }
            float deltaYaw = Mathf.DeltaAngle(_lastCamYaw, camEuler.y);
            float deltaPitch = Mathf.DeltaAngle(_lastCamPitch, camEuler.x);
            _lastCamYaw = camEuler.y;
            _lastCamPitch = camEuler.x;

            Vector3 targetSwayPos = new Vector3(
                Mathf.Clamp(-deltaYaw * 0.0022f, -0.035f, 0.035f),
                Mathf.Clamp(deltaPitch * 0.0020f, -0.030f, 0.030f),
                0f);
            Vector3 targetSwayRot = new Vector3(
                Mathf.Clamp(-deltaPitch * 0.45f, -7f, 7f),
                Mathf.Clamp(deltaYaw * 0.55f, -9f, 9f),
                Mathf.Clamp(-deltaYaw * 0.65f, -10f, 10f));

            float swayLerp = Mathf.Clamp01(Time.deltaTime * 10f);
            _fpSwayOffset = Vector3.Lerp(_fpSwayOffset, targetSwayPos, swayLerp);
            _fpSwayAngles = Vector3.Lerp(_fpSwayAngles, targetSwayRot, swayLerp);

            // 2. Smooth idle breathing + walk/sprint bobbing
            bool isMovingOnGround = starterInputs != null && starterInputs.move.sqrMagnitude > 0.01f &&
                                    firstPersonController != null && firstPersonController.Grounded;
            float targetBobWeight = isMovingOnGround ? 1f : 0f;
            _fpBobBlend = Mathf.MoveTowards(_fpBobBlend, targetBobWeight, Time.deltaTime * 6f);

            float bobFreq = IsCrouching ? 5.5f : ((starterInputs != null && starterInputs.sprint) ? 11.0f : 7.8f);
            float bobAmp = IsCrouching ? 0.007f : ((starterInputs != null && starterInputs.sprint) ? 0.018f : 0.011f);
            _fpBobPhase += Time.deltaTime * bobFreq;

            Vector3 breatheOffset = new Vector3(
                Mathf.Cos(Time.time * 1.1f) * 0.0025f,
                Mathf.Sin(Time.time * 2.2f) * 0.0040f,
                0f);

            Vector3 walkBobOffset = new Vector3(
                Mathf.Cos(_fpBobPhase * 0.5f) * bobAmp,
                Mathf.Abs(Mathf.Sin(_fpBobPhase)) * bobAmp,
                0f) * _fpBobBlend;

            // 3. Base weapon ready pose per weapon type + crouch offset
            Vector3 basePos = equippedWeapon == WeaponType.Spear ? _fpSpearBaseLocalPos :
                              (equippedWeapon == WeaponType.Axe ? _fpAxeBaseLocalPos : _fpSwordBaseLocalPos);
            Quaternion baseRot = equippedWeapon == WeaponType.Spear ? _fpSpearBaseLocalRot :
                                 (equippedWeapon == WeaponType.Axe ? _fpAxeBaseLocalRot : _fpSwordBaseLocalRot);

            Vector3 crouchPosOffset = new Vector3(-0.02f, -0.025f, -0.015f) * _crouchWeight;
            Quaternion crouchRotOffset = Quaternion.Euler(5f * _crouchWeight, -8f * _crouchWeight, 6f * _crouchWeight);

            Vector3 rGripLocalPos = basePos + crouchPosOffset + breatheOffset + walkBobOffset + _fpSwayOffset;
            Quaternion rGripLocalRot = baseRot * crouchRotOffset * Quaternion.Euler(_fpSwayAngles);

            // 4. Multi-phase First-Person attack animation per WeaponType
            if (IsAttacking)
            {
                float normTime = _attackTimer / Mathf.Max(0.01f, _attackDuration);
                EvaluateFirstPersonAttackPose(normTime, out Vector3 swingPos, out Quaternion swingRot);
                rGripLocalPos += swingPos;
                rGripLocalRot = rGripLocalRot * swingRot;
            }

            Vector3 rGripWorldPos = camTransform.TransformPoint(rGripLocalPos);
            Quaternion rGripWorldRot = camTransform.rotation * rGripLocalRot;

            Quaternion socketLocalRot = rightHandWeaponSocket != null
                ? rightHandWeaponSocket.localRotation
                : Quaternion.LookRotation(Vector3.right, Vector3.back) * Quaternion.Euler(26f, 0f, 0f);
            Vector3 socketLocalPos = rightHandWeaponSocket != null
                ? rightHandWeaponSocket.localPosition
                : new Vector3(0.078f, -0.032f, -0.008f);

            Quaternion targetRightHandRot = rGripWorldRot * Quaternion.Inverse(socketLocalRot);
            Vector3 targetRightWristPos = rGripWorldPos - (targetRightHandRot * socketLocalPos);

            Vector3 rArmBoneLocalAxis = _rightHandBone.localPosition.sqrMagnitude > 0.0001f
                ? _rightHandBone.localPosition.normalized
                : Vector3.right;
            bool isSyntyRig = Mathf.Abs(rArmBoneLocalAxis.x) > Mathf.Abs(rArmBoneLocalAxis.y);
            Vector3 rPalmLocalAxis = isSyntyRig ? Vector3.down : Vector3.left;

            // Orient the right forearm from the lower-right camera corner smoothly into the wrist without detaching Hand_R.localPosition
            Vector3 rElbowAnchor = camTransform.TransformPoint(new Vector3(0.38f, -0.42f, 0.14f));
            Vector3 rForearmDir = (targetRightWristPos - rElbowAnchor).normalized;

            _rightLowerArmBone.rotation = AlignBoneAxisWithWorldDir(
                rArmBoneLocalAxis,
                rPalmLocalAxis,
                rForearmDir,
                targetRightHandRot * rPalmLocalAxis);

            // Translate Elbow_R so its child Hand_R lands at targetRightWristPos while preserving exact bindpose bone length
            _rightLowerArmBone.position += (targetRightWristPos - _rightHandBone.position);
            _rightHandBone.rotation = targetRightHandRot;
        }

        private void OnGUI()
        {
            if (!showWeaponHud) return;

            if (_hudBarTex == null)
            {
                _hudBarTex = new Texture2D(1, 1);
                _hudBarTex.SetPixel(0, 0, Color.white);
                _hudBarTex.Apply();
            }

            if (_damageFlashTimer > 0f)
            {
                GUI.color = new Color(0.9f, 0.1f, 0.1f, (_damageFlashTimer / 0.30f) * 0.25f);
                GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), _hudBarTex);
                GUI.color = Color.white;
            }

            GUIStyle boxStyle = new GUIStyle(GUI.skin.box)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft
            };
            boxStyle.normal.textColor = Color.white;

            // Player Health Bar
            float hudX = 16f;
            float hudY = Screen.height - 102f;

            GUI.color = new Color(0f, 0f, 0f, 0.78f);
            GUI.DrawTexture(new Rect(hudX, hudY, 320f, 20f), _hudBarTex);

            float hpRatio = Mathf.Clamp01((float)currentHealth / Mathf.Max(1, maxHealth));
            GUI.color = new Color(0.85f, 0.18f, 0.22f, 0.95f);
            GUI.DrawTexture(new Rect(hudX + 2f, hudY + 2f, 316f * hpRatio, 16f), _hudBarTex);
            GUI.color = Color.white;

            GUIStyle barLabelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            barLabelStyle.normal.textColor = Color.white;
            GUI.Label(new Rect(hudX, hudY, 320f, 20f), $"HP: {currentHealth} / {maxHealth}", barLabelStyle);

            string weaponStr = equippedWeapon == WeaponType.Sword ? "[1] SWORD  |  2 Spear  |  3 Axe" :
                               equippedWeapon == WeaponType.Spear ? "1 Sword  |  [2] SPEAR  |  3 Axe" :
                               "1 Sword  |  2 Spear  |  [3] AXE";
            string stanceStr = IsCrouching ? "CROUCHING [C]" : "STANDING [C]";
            string viewStr = isThirdPersonView ? "3RD PERSON [V]" : "1ST PERSON [V]";
            string attackStr = IsAttacking ? "  >> ATTACKING <<" :
                               (_attackCooldownTimer > 0f ? "  [Recovering...]" : "  [LMB / F] Attack");

            GUI.Box(new Rect(16, Screen.height - 76, 560, 58),
                $" Weapon: {weaponStr}{attackStr}\n Stance: {stanceStr}  |  Camera: {viewStr}",
                boxStyle);

            if (!string.IsNullOrEmpty(LastHitMessage) && Time.time - _lastHitMessageTime < 2.4f)
            {
                GUI.Box(new Rect(16, Screen.height - 140, 480, 32), $" {LastHitMessage}", boxStyle);
            }
        }
    }
}
