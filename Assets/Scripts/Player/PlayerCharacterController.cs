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
    /// Controls the 3D Player Character (GanzSe Modular Character), Kevin Iglesias Humanoid Animations
    /// (8-Direction Walk/Run/Sprint 2D BlendTree, Jump, Fall, Land, Crouch, 3-Hit Melee Attack Combos,
    /// Right-Click Shield Block/Timed Parry, Combat Dodge/Sidestep Dash, Stealth Backstab Finisher,
    /// Hit-Stop & Camera Shake), equipped melee weapons (1 = Sword, 2 = Spear/Polearm, 3 = Battle Axe)
    /// with weapon swing trails, off-hand Shield (G to toggle, RMB to block/parry), visible First-Person
    /// armored hands & forearms, modular armor customization (M key), and 1st/3rd-Person camera toggle (V key).
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

        [Header("Player Health & Stamina")]
        public int maxHealth = 100;
        public int currentHealth = 100;
        public float maxStamina = 100f;
        public float currentStamina = 100f;
        public float staminaRegenRate = 22f;

        [Header("Weapons (1 = Sword, 2 = Spear, 3 = Axe) & Off-Hand Shield")]
        public WeaponType equippedWeapon = WeaponType.Sword;
        [Tooltip("Weapon socket attached to the 3D humanoid's right hand palm.")]
        public Transform rightHandWeaponSocket;
        [Tooltip("Weapons attached to the character's right hand (used in both 1st-person and 3rd-person).")]
        public GameObject[] thirdPersonWeapons = new GameObject[3];
        [Tooltip("Shield socket attached to the 3D humanoid's left forearm/hand.")]
        public Transform leftHandShieldSocket;
        [Tooltip("Off-hand shield attached to the left hand (toggle with G, hold RMB to block).")]
        public GameObject leftHandShield;
        public bool shieldEquipped = true;
        [Tooltip("Legacy FP weapon array (kept disabled so the real hand-held weapon is used).")]
        public GameObject[] firstPersonWeapons = new GameObject[3];
        public Transform firstPersonWeaponPivot;

        [Header("Movement, Crouch & Combat Dash Settings")]
        public float normalWalkSpeed = 4.0f;
        public float normalSprintSpeed = 6.5f;
        public float crouchMoveSpeed = 2.2f;
        public float blockMoveSpeed = 2.5f;
        public float dashSpeed = 11.5f;
        public float dashDuration = 0.24f;
        public float dashCooldown = 0.55f;
        public float dashStaminaCost = 25f;
        public float standingHeight = 2.0f;
        public float crouchingHeight = 1.2f;
        public float standingCameraY = 1.375f;
        public float crouchingCameraY = 0.75f;
        public float crouchTransitionSpeed = 10.0f;
        public Vector3 standingDetectionOffset = new Vector3(0f, 1.0f, 0f);
        public Vector3 crouchingDetectionOffset = new Vector3(0f, 0.45f, 0f);

        [Header("Melee Combat, 3-Hit Combo, Backstab & Parry")]
        public float attackRange = 2.8f;
        public float attackRadius = 1.05f;
        public float comboResetTime = 1.4f;
        public float perfectParryWindow = 0.35f;
        public int[] comboBaseDamage = new int[] { 25, 35, 50 };
        public LayerMask enemyHitLayers = ~0;

        [Header("Camera View Mode (Press V to Toggle 1st/3rd Person)")]
        public bool isThirdPersonView = false;
        public float thirdPersonCameraDistance = 2.6f;
        public Vector3 thirdPersonShoulderOffset = new Vector3(0.45f, 0.25f, 0f);
        public bool showWeaponHud = true;

        [Header("Audio")]
        public AudioClip[] footstepAudioClips;
        public AudioClip landingAudioClip;
        [Range(0f, 1f)] public float footstepVolume = 0.5f;

        // Runtime State
        public bool IsCrouching { get; private set; }
        public bool IsAttacking { get; private set; }
        public bool IsBlocking { get; private set; }
        public bool IsDashing { get; private set; }
        public int CurrentComboStep { get; private set; }
        public string LastHitMessage { get; private set; } = "";
        private float _lastHitMessageTime;

        private float _animationBlend;
        private float _moveXBlend;
        private float _moveZBlend;
        private float _currentHeight;
        private float _currentCameraY;
        private float _crouchWeight;
        private float _blockWeight;
        private float _attackTimer;
        private float _attackDuration;
        private float _lastAttackEndTime;
        private float _lastBlockPressTime = -999f;

        // Combat Dash & Hit-Stop / Camera Shake Runtime State
        private float _dashTimer;
        private float _dashCooldownTimer;
        private Vector3 _dashWorldDir;
        private float _dashRollSide;
        private float _currentDashRollAngle;
        private float _hitStopTimer;
        private float _camShakeTimer;
        private float _camShakeDuration;
        private float _camShakeIntensity;
        private float _damageFlashTimer;
        private bool _canBackstabPrompt;

        private bool _wasGrounded = true;
        private float _stepCycleTimer;
        private float _modelYawOffset;

        private readonly TrailRenderer[] _weaponTrails = new TrailRenderer[3];
        private static Material _sharedTrailMaterial;
        private static Texture2D _hudBarTex;

        // First-Person right-hand grip pose (positions both the hand and the upright blade in view)
        private readonly Vector3 _fpGripBaseLocalPos = new Vector3(0.24f, -0.23f, 0.50f);
        private readonly Quaternion _fpGripBaseLocalRot = Quaternion.Euler(14f, -18f, 8f);

        // First-Person left-hand guard pose (visible in lower-left of screen)
        private readonly Vector3 _fpLeftHandBaseLocalPos = new Vector3(-0.22f, -0.27f, 0.42f);
        private readonly Quaternion _fpLeftHandBaseLocalRot = Quaternion.Euler(25f, 28f, -15f);

        // Animator parameter hashes
        private int _animIDSpeed;
        private int _animIDMoveX;
        private int _animIDMoveZ;
        private int _animIDGrounded;
        private int _animIDJump;
        private int _animIDFreeFall;
        private int _animIDMotionSpeed;
        private int _animIDIsCrouching;
        private int _animIDIsBlocking;
        private int _animIDComboStep;
        private int _animIDWeaponType;
        private int _animIDAttack;
        private int _animIDParry;
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
        private readonly List<Transform> _leftThumbBones = new List<Transform>();

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
            currentStamina = maxStamina;
            _currentHeight = standingHeight;
            _currentCameraY = cameraRoot != null ? cameraRoot.localPosition.y : standingCameraY;

            if (mainCamera != null && mainCamera.nearClipPlane > 0.05f)
            {
                mainCamera.nearClipPlane = 0.04f;
            }

            ClearStaticAndColliders(firstPersonWeapons);
            ClearStaticAndColliders(thirdPersonWeapons);
            if (leftHandShield != null)
            {
                ClearStaticAndColliders(new[] { leftHandShield });
            }

            EnsureRuntimeWeaponOrientation();
            EnsureWeaponSwingTrails();
            EnsureSceneEnemiesHaveCombatTargets();

            if (firstPersonWeaponPivot != null)
            {
                firstPersonWeaponPivot.gameObject.SetActive(false);
            }

            EquipWeapon(equippedWeapon);
            UpdateShieldVisibility();
            ApplyCameraViewMode();
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

        private void EnsureRuntimeWeaponOrientation()
        {
            if (rightHandWeaponSocket == null && _rightHandBone != null)
            {
                rightHandWeaponSocket = _rightHandBone.Find("WeaponSocket_TP");
            }
            if (rightHandWeaponSocket != null)
            {
                rightHandWeaponSocket.localPosition = new Vector3(-0.038f, 0.082f, 0.01f);
                rightHandWeaponSocket.localRotation = Quaternion.LookRotation(Vector3.up, Vector3.forward);
            }

            if (leftHandShieldSocket == null && _leftHandBone != null)
            {
                leftHandShieldSocket = _leftHandBone.Find("ShieldSocket_TP");
            }
            if (leftHandShield == null && leftHandShieldSocket != null && leftHandShieldSocket.childCount > 0)
            {
                leftHandShield = leftHandShieldSocket.GetChild(0).gameObject;
            }

            if (thirdPersonWeapons == null) return;
            float[] targetLengths = new float[] { 0.88f, 1.25f, 0.78f };

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
                    else
                        meshObj.localPosition = -(alignRot * m.bounds.center) * uniformScale + new Vector3(0f, targetLengths[i] * 0.22f, 0f);
                }
            }
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
                trail.startWidth = 0.14f;
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
                        new GradientAlphaKey(0.80f, 0f),
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
            _animIDIsBlocking = Animator.StringToHash("IsBlocking");
            _animIDComboStep = Animator.StringToHash("ComboStep");
            _animIDWeaponType = Animator.StringToHash("WeaponType");
            _animIDAttack = Animator.StringToHash("Attack");
            _animIDParry = Animator.StringToHash("Parry");
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
            if (leftHandShieldSocket == null && _leftHandBone != null)
            {
                leftHandShieldSocket = _leftHandBone.Find("ShieldSocket_TP");
            }

            CacheFingerBones();
        }

        private void CacheFingerBones()
        {
            _rightFingerBones.Clear();
            _rightThumbBones.Clear();
            _leftFingerBones.Clear();
            _leftThumbBones.Clear();

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

            HumanBodyBones[] lThumbs = new HumanBodyBones[]
            {
                HumanBodyBones.LeftThumbProximal, HumanBodyBones.LeftThumbIntermediate, HumanBodyBones.LeftThumbDistal
            };
            foreach (var b in lThumbs)
            {
                var t = characterAnimator.GetBoneTransform(b);
                if (t != null) _leftThumbBones.Add(t);
            }
        }

        private void Update()
        {
            UpdateHitStopAndTimers();
            HandleViewAndArmorInput();
            HandleWeaponSwitchInput();
            HandleDashInputAndMovement();
            HandleBlockAndShieldInput();
            HandleCrouchInput();
            CheckStealthBackstabOpportunity();
            HandleMeleeAttackInput();
            UpdateWeaponTrailState();
            UpdateLocomotionAndAnimator();
            UpdateFootstepAudio();
        }

        private void LateUpdate()
        {
            ApplyCameraShakeAndDashRollOffset();
            ApplyProceduralCrouchBlockAndFingerGrip();
            UpdateFirstPersonArmsAndWeaponPose();
        }

        private void UpdateHitStopAndTimers()
        {
            if (_hitStopTimer > 0f)
            {
                _hitStopTimer -= Time.unscaledDeltaTime;
                if (characterAnimator != null)
                {
                    characterAnimator.speed = _hitStopTimer > 0f ? 0.06f : 1.0f;
                }
            }
            else if (characterAnimator != null && characterAnimator.speed < 0.99f)
            {
                characterAnimator.speed = 1.0f;
            }

            if (_camShakeTimer > 0f)
            {
                _camShakeTimer -= Time.deltaTime;
            }

            if (_damageFlashTimer > 0f)
            {
                _damageFlashTimer -= Time.deltaTime;
            }

            if (!IsDashing && !IsAttacking)
            {
                float regen = IsBlocking ? staminaRegenRate * 0.45f : staminaRegenRate;
                currentStamina = Mathf.Min(maxStamina, currentStamina + regen * Time.deltaTime);
            }
        }

        public void TriggerHitStop(float durationSeconds = 0.048f)
        {
            _hitStopTimer = Mathf.Max(_hitStopTimer, durationSeconds);
            if (characterAnimator != null)
            {
                characterAnimator.speed = 0.06f;
            }
        }

        public void TriggerCameraShake(float intensity = 0.04f, float duration = 0.16f)
        {
            _camShakeIntensity = Mathf.Max(_camShakeIntensity * (_camShakeTimer / Mathf.Max(0.01f, _camShakeDuration)), intensity);
            _camShakeDuration = duration;
            _camShakeTimer = duration;
        }

        private void ApplyCameraShakeAndDashRollOffset()
        {
            if (cameraRoot == null) return;

            float targetRoll = IsDashing ? (-_dashRollSide * 5.0f) : 0f;
            _currentDashRollAngle = Mathf.Lerp(_currentDashRollAngle, targetRoll, Time.deltaTime * 16f);

            Vector3 shakeOffset = Vector3.zero;
            float shakePitch = 0f;
            float shakeYaw = 0f;
            if (_camShakeTimer > 0f)
            {
                float decay = Mathf.Clamp01(_camShakeTimer / Mathf.Max(0.01f, _camShakeDuration));
                float amp = _camShakeIntensity * decay;
                shakeOffset = new Vector3(
                    Mathf.Sin(Time.time * 68f) * amp,
                    Mathf.Cos(Time.time * 57f) * amp,
                    0f);
                shakePitch = Mathf.Sin(Time.time * 62f) * amp * 28f;
                shakeYaw = Mathf.Cos(Time.time * 51f) * amp * 24f;
            }

            cameraRoot.localPosition = new Vector3(shakeOffset.x, _currentCameraY + shakeOffset.y, 0f);
            Vector3 currentEuler = cameraRoot.localEulerAngles;
            cameraRoot.localRotation = Quaternion.Euler(currentEuler.x + shakePitch, currentEuler.y + shakeYaw, _currentDashRollAngle);
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

        private static bool WasDashPressed()
        {
            if (Input.GetKeyDown(KeyCode.LeftAlt) || Input.GetKeyDown(KeyCode.E)) return true;
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && (Keyboard.current.leftAltKey.wasPressedThisFrame || Keyboard.current.eKey.wasPressedThisFrame))
                return true;
#endif
            return false;
        }

        private static bool IsBlockHeld()
        {
            if (Input.GetMouseButton(1) || Input.GetKey(KeyCode.Q)) return true;
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null && Mouse.current.rightButton.isPressed) return true;
            if (Keyboard.current != null && Keyboard.current.qKey.isPressed) return true;
#endif
            return false;
        }

        private static bool WasBlockPressedThisFrame()
        {
            if (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Q)) return true;
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame) return true;
            if (Keyboard.current != null && Keyboard.current.qKey.wasPressedThisFrame) return true;
#endif
            return false;
        }

        private void HandleDashInputAndMovement()
        {
            if (_dashCooldownTimer > 0f)
            {
                _dashCooldownTimer -= Time.deltaTime;
            }

            if (IsDashing)
            {
                _dashTimer -= Time.deltaTime;
                if (characterController != null)
                {
                    characterController.Move(_dashWorldDir * (dashSpeed * Time.deltaTime));
                }

                if (_dashTimer <= 0f)
                {
                    IsDashing = false;
                }
                return;
            }

            if (WasDashPressed() && _dashCooldownTimer <= 0f && currentStamina >= dashStaminaCost)
            {
                currentStamina -= dashStaminaCost;
                IsDashing = true;
                IsAttacking = false;
                _dashTimer = dashDuration;
                _dashCooldownTimer = dashCooldown;

                Vector2 moveIn = starterInputs != null ? starterInputs.move : Vector2.zero;
                if (moveIn.sqrMagnitude > 0.01f)
                {
                    _dashWorldDir = (transform.right * moveIn.x + transform.forward * moveIn.y).normalized;
                    _dashRollSide = moveIn.x;
                }
                else
                {
                    // Default quick backstep dodge if no directional key is held
                    _dashWorldDir = -transform.forward;
                    _dashRollSide = 0f;
                }

                TriggerCameraShake(0.025f, 0.14f);
                if (footstepAudioClips != null && footstepAudioClips.Length > 0 && footstepAudioClips[0] != null)
                {
                    AudioSource.PlayClipAtPoint(footstepAudioClips[0], transform.position, 0.65f);
                }

                LastHitMessage = "Combat Dodge / Sidestep Dash! (Invulnerable)";
                _lastHitMessageTime = Time.time;
            }
        }

        public void OnEnemyAttackStrike(EnemyCombatTarget attacker, int incomingDamage)
        {
            if (attacker == null || attacker.IsDead) return;

            // 1. Invulnerability frames during Combat Dodge / Sidestep Dash
            if (IsDashing)
            {
                LastHitMessage = $"EVADED {attacker.name.Replace("Enemy_", "")}'s Strike with Dodge Dash! (0 DMG)";
                _lastHitMessageTime = Time.time;
                return;
            }

            // 2. Shield Block & Timed Perfect Parry
            if (IsBlocking)
            {
                bool isPerfectParry = (Time.time - _lastBlockPressTime) <= perfectParryWindow;
                if (isPerfectParry)
                {
                    currentStamina = Mathf.Min(maxStamina, currentStamina + 20f);
                    attacker.ApplyParryStagger(1.8f);
                    TriggerHitStop(0.055f);
                    TriggerCameraShake(0.05f, 0.2f);
                    if (characterAnimator != null)
                    {
                        characterAnimator.SetTrigger(_animIDParry);
                    }
                    LastHitMessage = $"PERFECT SHIELD PARRY! {attacker.name.Replace("Enemy_", "")} Staggered (0 DMG)!";
                    _lastHitMessageTime = Time.time;
                    return;
                }
                else
                {
                    int blockedDamage = Mathf.Max(2, Mathf.RoundToInt(incomingDamage * 0.20f));
                    currentHealth = Mathf.Max(1, currentHealth - blockedDamage);
                    currentStamina = Mathf.Max(0f, currentStamina - 12f);
                    EnemyCombatTarget.SpawnHitSparkBurst(transform.position + Vector3.up * 1.2f + transform.forward * 0.5f, 16);
                    TriggerHitStop(0.03f);
                    TriggerCameraShake(0.035f, 0.16f);
                    LastHitMessage = $"SHIELD BLOCKED {attacker.name.Replace("Enemy_", "")}! Reduced {incomingDamage} -> {blockedDamage} DMG";
                    _lastHitMessageTime = Time.time;
                    return;
                }
            }

            // 3. Direct Unblocked Hit
            currentHealth = Mathf.Max(0, currentHealth - incomingDamage);
            _damageFlashTimer = 0.35f;
            TriggerCameraShake(0.06f, 0.22f);

            if (characterAnimator != null)
            {
                characterAnimator.SetTrigger(_animIDHit);
            }

            if (currentHealth <= 0)
            {
                currentHealth = maxHealth;
                currentStamina = maxStamina;
                LastHitMessage = "Player Second Wind! Restored to 100/100 HP";
            }
            else
            {
                LastHitMessage = $"Took {incomingDamage} DMG from {attacker.name.Replace("Enemy_", "")}! (Hold RMB to Block / Alt to Dash)";
            }
            _lastHitMessageTime = Time.time;
        }

        private void CheckStealthBackstabOpportunity()
        {
            _canBackstabPrompt = false;
            float effectiveRange = (equippedWeapon == WeaponType.Spear ? attackRange * 1.35f : attackRange) + 0.35f;
            Vector3 checkCenter = transform.position + Vector3.up * 1.0f + transform.forward * (effectiveRange * 0.55f);
            Collider[] hits = Physics.OverlapSphere(checkCenter, attackRadius, enemyHitLayers, QueryTriggerInteraction.Ignore);

            foreach (var hit in hits)
            {
                if (hit.transform.IsChildOf(transform)) continue;
                var combatTarget = hit.GetComponentInParent<EnemyCombatTarget>();
                if (combatTarget != null && !combatTarget.IsDead && combatTarget.CanBeStealthBackstabbed(transform))
                {
                    _canBackstabPrompt = true;
                    break;
                }
            }
        }

        private void HandleViewAndArmorInput()
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

            if (WasKeyPressed(KeyCode.M
#if ENABLE_INPUT_SYSTEM
                , Key.M
#endif
            ) && modularHeroController != null)
            {
                modularHeroController.RandomizeArmorParts();
                modularHeroController.RandomizeFaceParts();
                ApplyCameraViewMode();
                LastHitMessage = "Randomized Modular Character Armor & Gauntlets!";
                _lastHitMessageTime = Time.time;
            }
        }

        private void HandleBlockAndShieldInput()
        {
            if (WasKeyPressed(KeyCode.G
#if ENABLE_INPUT_SYSTEM
                , Key.G
#endif
            ))
            {
                shieldEquipped = !shieldEquipped;
                UpdateShieldVisibility();
                LastHitMessage = shieldEquipped ? "Equipped Off-Hand Shield [G]" : "Stowed Off-Hand Shield [G]";
                _lastHitMessageTime = Time.time;
            }

            bool blockHeld = !IsAttacking && !IsDashing && IsBlockHeld();
            if (WasBlockPressedThisFrame() && !IsAttacking && !IsDashing)
            {
                _lastBlockPressTime = Time.time;
                if (characterAnimator != null)
                {
                    characterAnimator.SetTrigger(_animIDParry);
                }
                LastHitMessage = shieldEquipped ? "Shield Parry Guard! (Timed RMB = Perfect Parry)" : "Weapon Parry Guard!";
                _lastHitMessageTime = Time.time;
            }

            IsBlocking = blockHeld;
            _blockWeight = Mathf.MoveTowards(_blockWeight, IsBlocking ? 1f : 0f, Time.deltaTime * 10f);

            if (characterAnimator != null)
            {
                characterAnimator.SetBool(_animIDIsBlocking, IsBlocking);
            }

            UpdateShieldVisibility();
        }

        private void UpdateShieldVisibility()
        {
            if (leftHandShield != null)
            {
                bool showShield = shieldEquipped || IsBlocking;
                if (leftHandShield.activeSelf != showShield)
                {
                    leftHandShield.SetActive(showShield);
                }
            }
        }

        public void ApplyCameraViewMode()
        {
            if (followCameraComponent != null)
            {
                followCameraComponent.CameraDistance = isThirdPersonView ? thirdPersonCameraDistance : 0f;
                followCameraComponent.ShoulderOffset = isThirdPersonView ? thirdPersonShoulderOffset : Vector3.zero;
                followCameraComponent.VerticalArmLength = isThirdPersonView ? 0.25f : 0f;
            }

            if (firstPersonWeaponPivot != null)
            {
                firstPersonWeaponPivot.gameObject.SetActive(false);
            }

            if (characterModelRoot != null)
            {
                foreach (var smr in characterModelRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    smr.updateWhenOffscreen = true;

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

            if (leftHandShield != null)
            {
                foreach (var r in leftHandShield.GetComponentsInChildren<Renderer>(true))
                {
                    r.enabled = true;
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
            float targetCamY = IsCrouching ? crouchingCameraY : standingCameraY;
            float targetCrouchWeight = IsCrouching ? 1f : 0f;

            _currentHeight = Mathf.Lerp(_currentHeight, targetHeight, Time.deltaTime * crouchTransitionSpeed);
            _currentCameraY = Mathf.Lerp(_currentCameraY, targetCamY, Time.deltaTime * crouchTransitionSpeed);
            _crouchWeight = Mathf.MoveTowards(_crouchWeight, targetCrouchWeight, Time.deltaTime * 6f);

            if (characterController != null)
            {
                characterController.height = _currentHeight;
                characterController.center = new Vector3(0f, _currentHeight * 0.5f, 0f);
            }

            if (firstPersonController != null)
            {
                if (IsCrouching)
                {
                    firstPersonController.MoveSpeed = crouchMoveSpeed;
                    firstPersonController.SprintSpeed = crouchMoveSpeed;
                }
                else if (IsBlocking)
                {
                    firstPersonController.MoveSpeed = blockMoveSpeed;
                    firstPersonController.SprintSpeed = blockMoveSpeed;
                }
                else
                {
                    firstPersonController.MoveSpeed = normalWalkSpeed;
                    firstPersonController.SprintSpeed = normalSprintSpeed;
                }
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

        private void HandleMeleeAttackInput()
        {
            if (IsAttacking)
            {
                if (_hitStopTimer <= 0f)
                {
                    _attackTimer += Time.deltaTime;
                }
                if (_attackTimer >= _attackDuration)
                {
                    IsAttacking = false;
                    _lastAttackEndTime = Time.time;
                }
                return;
            }

            if (Time.time - _lastAttackEndTime > comboResetTime)
            {
                CurrentComboStep = 0;
            }

            if (WasAttackPressed())
            {
                StartMeleeComboStep();
            }
        }

        private void StartMeleeComboStep()
        {
            CurrentComboStep = _canBackstabPrompt ? 3 : ((CurrentComboStep % 3) + 1);
            IsAttacking = true;
            IsBlocking = false;
            _attackTimer = 0f;

            float weaponDurationFactor = equippedWeapon == WeaponType.Sword ? 0.48f :
                                         equippedWeapon == WeaponType.Spear ? 0.54f : 0.60f;
            _attackDuration = CurrentComboStep == 3 ? weaponDurationFactor * 1.2f : weaponDurationFactor;

            if (characterAnimator != null)
            {
                characterAnimator.SetInteger(_animIDWeaponType, (int)equippedWeapon);
                characterAnimator.SetInteger(_animIDComboStep, CurrentComboStep);
                characterAnimator.SetTrigger(_animIDAttack);
            }

            if (footstepAudioClips != null && footstepAudioClips.Length > 0 && footstepAudioClips[0] != null)
            {
                AudioSource.PlayClipAtPoint(footstepAudioClips[0], transform.position + Vector3.up, 0.45f);
            }

            PerformMeleeHitDetection();
        }

        private void PerformMeleeHitDetection()
        {
            float effectiveRange = equippedWeapon == WeaponType.Spear ? attackRange * 1.35f : attackRange;
            int baseDmg = comboBaseDamage[Mathf.Clamp(CurrentComboStep - 1, 0, comboBaseDamage.Length - 1)];
            float weaponMult = equippedWeapon == WeaponType.Axe ? 1.35f :
                               equippedWeapon == WeaponType.Spear ? 1.15f : 1.0f;
            int finalDamage = Mathf.RoundToInt(baseDmg * weaponMult);

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
                        bool isStealthBackstab = combatTarget.CanBeStealthBackstabbed(transform);
                        bool isParryRiposte = combatTarget.IsStaggered;

                        if (isStealthBackstab)
                        {
                            finalDamage = Mathf.Max(combatTarget.maxHealth, finalDamage * 3);
                        }
                        else if (isParryRiposte)
                        {
                            finalDamage = Mathf.RoundToInt(finalDamage * 1.75f);
                        }

                        combatTarget.TakeDamage(finalDamage, isStealthBackstab);

                        // Apply Hit-Stop & Camera Shake scaled by attack weight
                        bool heavyImpact = isStealthBackstab || isParryRiposte || CurrentComboStep == 3 || equippedWeapon == WeaponType.Axe;
                        TriggerHitStop(heavyImpact ? 0.055f : 0.032f);
                        TriggerCameraShake(heavyImpact ? 0.055f : 0.028f, heavyImpact ? 0.20f : 0.12f);

                        string targetName = (enemy != null ? enemy.name : combatTarget.name).Replace("Enemy_", "");
                        if (isStealthBackstab)
                        {
                            LastHitMessage = $"CRITICAL STEALTH BACKSTAB on {targetName} ({finalDamage} DMG) -> ASSASSINATED!";
                        }
                        else if (isParryRiposte)
                        {
                            LastHitMessage = $"PARRY RIPOSTE CRIT on {targetName} ({finalDamage} DMG)! ({combatTarget.currentHealth}/{combatTarget.maxHealth} HP)";
                        }
                        else
                        {
                            string statusSuffix = combatTarget.IsDead
                                ? " -> DEFEATED!"
                                : $" ({combatTarget.currentHealth}/{combatTarget.maxHealth} HP)";
                            LastHitMessage = $"HIT {targetName} with {equippedWeapon} (Combo {CurrentComboStep}/3) for {finalDamage} DMG!{statusSuffix}";
                        }
                        _lastHitMessageTime = Time.time;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                        Debug.Log($"[Player Melee] {LastHitMessage}", combatTarget);
#endif
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
                    TriggerCameraShake(0.018f, 0.09f);
                }

                string moveName = equippedWeapon == WeaponType.Spear ? "Polearm Strike" :
                                  (CurrentComboStep == 1 ? "Right Slash" :
                                  (CurrentComboStep == 2 ? "Left Cross-Slash" : "Heavy 2H Finisher"));
                LastHitMessage = $"{equippedWeapon} — Combo {CurrentComboStep}/3 ({moveName})";
                _lastHitMessageTime = Time.time;
            }
        }

        private void UpdateLocomotionAndAnimator()
        {
            if (characterAnimator == null || firstPersonController == null || starterInputs == null) return;

            bool isGrounded = firstPersonController.Grounded;
            float targetSpeed = 0f;
            Vector2 moveInput = starterInputs.move;

            if (IsDashing)
            {
                targetSpeed = normalSprintSpeed;
            }
            else if (moveInput != Vector2.zero)
            {
                targetSpeed = IsCrouching ? crouchMoveSpeed :
                              (IsBlocking ? blockMoveSpeed :
                              (starterInputs.sprint ? normalSprintSpeed : normalWalkSpeed));
            }

            _modelYawOffset = Mathf.LerpAngle(_modelYawOffset, 0f, Time.deltaTime * 12f);

            float speedRate = firstPersonController.SpeedChangeRate;
            _animationBlend = Mathf.Lerp(_animationBlend, targetSpeed, Time.deltaTime * speedRate);
            if (_animationBlend < 0.01f) _animationBlend = 0f;

            Vector2 moveDir = moveInput.sqrMagnitude > 0.001f ? moveInput.normalized : (IsDashing ? new Vector2(0f, -1f) : Vector2.zero);
            float targetMoveX = moveDir.x * targetSpeed;
            float targetMoveZ = moveDir.y * targetSpeed;

            _moveXBlend = Mathf.Lerp(_moveXBlend, targetMoveX, Time.deltaTime * speedRate);
            _moveZBlend = Mathf.Lerp(_moveZBlend, targetMoveZ, Time.deltaTime * speedRate);
            if (Mathf.Abs(_moveXBlend) < 0.01f) _moveXBlend = 0f;
            if (Mathf.Abs(_moveZBlend) < 0.01f) _moveZBlend = 0f;

            float inputMagnitude = starterInputs.analogMovement ? moveInput.magnitude : (moveInput == Vector2.zero && !IsDashing ? 0f : 1f);

            characterAnimator.SetFloat(_animIDSpeed, _animationBlend);
            characterAnimator.SetFloat(_animIDMoveX, _moveXBlend);
            characterAnimator.SetFloat(_animIDMoveZ, _moveZBlend);
            characterAnimator.SetFloat(_animIDMotionSpeed, inputMagnitude);
            characterAnimator.SetBool(_animIDGrounded, isGrounded);
            characterAnimator.SetBool(_animIDIsCrouching, IsCrouching);

            if (isGrounded)
            {
                characterAnimator.SetBool(_animIDJump, starterInputs.jump && !IsCrouching);
                characterAnimator.SetBool(_animIDFreeFall, false);
            }
            else
            {
                characterAnimator.SetBool(_animIDFreeFall, true);
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

        private void ApplyProceduralCrouchBlockAndFingerGrip()
        {
            if (characterModelRoot != null)
            {
                if (isThirdPersonView)
                {
                    characterModelRoot.localPosition = new Vector3(0f, -0.38f * _crouchWeight, 0f);
                    characterModelRoot.localRotation = Quaternion.Euler(0f, _modelYawOffset, 0f);
                }
                else
                {
                    characterModelRoot.localPosition = new Vector3(0f, -0.22f - 0.38f * _crouchWeight, -0.38f);
                    characterModelRoot.localRotation = Quaternion.identity;
                }
            }

            if (_headBone != null)
            {
                _headBone.localScale = isThirdPersonView ? Vector3.one : Vector3.one * 0.001f;
            }

            if (_crouchWeight > 0.001f && _hipsBone != null)
            {
                if (_spineBone != null)
                    _spineBone.localRotation *= Quaternion.Euler(22f * _crouchWeight, 0f, 0f);
                if (_leftUpperLegBone != null)
                    _leftUpperLegBone.localRotation *= Quaternion.Euler(-48f * _crouchWeight, 0f, 0f);
                if (_leftLowerLegBone != null)
                    _leftLowerLegBone.localRotation *= Quaternion.Euler(75f * _crouchWeight, 0f, 0f);
                if (_rightUpperLegBone != null)
                    _rightUpperLegBone.localRotation *= Quaternion.Euler(-48f * _crouchWeight, 0f, 0f);
                if (_rightLowerLegBone != null)
                    _rightLowerLegBone.localRotation *= Quaternion.Euler(75f * _crouchWeight, 0f, 0f);
            }

            if (isThirdPersonView && _blockWeight > 0.001f)
            {
                if (_leftUpperArmBone != null)
                    _leftUpperArmBone.localRotation *= Quaternion.Euler(-38f * _blockWeight, 28f * _blockWeight, -22f * _blockWeight);
                if (_leftLowerArmBone != null)
                    _leftLowerArmBone.localRotation *= Quaternion.Euler(-45f * _blockWeight, 0f, -35f * _blockWeight);
                if (_rightUpperArmBone != null)
                    _rightUpperArmBone.localRotation *= Quaternion.Euler(-22f * _blockWeight, -18f * _blockWeight, 15f * _blockWeight);
            }

            Quaternion rightFingerCurl = Quaternion.Euler(0f, 0f, 62f);
            for (int i = 0; i < _rightFingerBones.Count; i++)
            {
                if (_rightFingerBones[i] != null)
                    _rightFingerBones[i].localRotation = rightFingerCurl;
            }
            Quaternion rightThumbCurl = Quaternion.Euler(25f, 0f, 38f);
            for (int i = 0; i < _rightThumbBones.Count; i++)
            {
                if (_rightThumbBones[i] != null)
                    _rightThumbBones[i].localRotation *= rightThumbCurl;
            }

            Quaternion leftFingerCurl = Quaternion.Euler(0f, 0f, -55f);
            for (int i = 0; i < _leftFingerBones.Count; i++)
            {
                if (_leftFingerBones[i] != null)
                    _leftFingerBones[i].localRotation = leftFingerCurl;
            }
        }

        private void UpdateFirstPersonArmsAndWeaponPose()
        {
            if (isThirdPersonView) return;
            if (_rightHandBone == null || _rightLowerArmBone == null || _rightUpperArmBone == null) return;

            if (mainCamera == null) mainCamera = Camera.main;
            Transform camTransform = mainCamera != null ? mainCamera.transform : cameraRoot;
            if (camTransform == null) return;

            float moveMag = (starterInputs != null && starterInputs.move != Vector2.zero && firstPersonController != null && firstPersonController.Grounded) ? 1f : 0f;
            float bobFreq = (starterInputs != null && starterInputs.sprint) ? 12f : 8f;
            float bobAmp = (starterInputs != null && starterInputs.sprint) ? 0.022f : 0.012f;

            Vector3 bobOffset = Vector3.zero;
            if (moveMag > 0.1f)
            {
                bobOffset.x = Mathf.Cos(Time.time * bobFreq * 0.5f) * bobAmp;
                bobOffset.y = Mathf.Abs(Mathf.Sin(Time.time * bobFreq)) * bobAmp;
            }

            Vector3 rGripLocalPos = _fpGripBaseLocalPos + bobOffset;
            Quaternion rGripLocalRot = _fpGripBaseLocalRot;

            Vector3 lHandLocalPos = _fpLeftHandBaseLocalPos + new Vector3(-bobOffset.x * 0.8f, bobOffset.y * 0.8f, 0f);
            Quaternion lHandLocalRot = _fpLeftHandBaseLocalRot;

            if (_blockWeight > 0.001f && !IsAttacking)
            {
                Vector3 blockRightPos = new Vector3(0.14f, -0.15f, 0.46f);
                Quaternion blockRightRot = Quaternion.Euler(8f, -42f, 34f);

                Vector3 blockLeftPos = new Vector3(-0.08f, -0.14f, 0.44f);
                Quaternion blockLeftRot = Quaternion.Euler(12f, 52f, -22f);

                rGripLocalPos = Vector3.Lerp(rGripLocalPos, blockRightPos + bobOffset * 0.35f, _blockWeight);
                rGripLocalRot = Quaternion.Slerp(rGripLocalRot, blockRightRot, _blockWeight);

                lHandLocalPos = Vector3.Lerp(lHandLocalPos, blockLeftPos + bobOffset * 0.35f, _blockWeight);
                lHandLocalRot = Quaternion.Slerp(lHandLocalRot, blockLeftRot, _blockWeight);
            }

            if (IsAttacking)
            {
                float t = Mathf.Clamp01(_attackTimer / Mathf.Max(0.01f, _attackDuration));
                float arc = Mathf.Sin(t * Mathf.PI);

                if (CurrentComboStep == 1)
                {
                    rGripLocalPos = _fpGripBaseLocalPos + new Vector3(Mathf.Lerp(0.18f, -0.36f, t) * arc, 0.04f * arc, 0.16f * arc);
                    rGripLocalRot = _fpGripBaseLocalRot * Quaternion.Euler(58f * arc, Mathf.Lerp(32f, -72f, t) * arc, -62f * arc);
                    lHandLocalPos = _fpLeftHandBaseLocalPos + new Vector3(-0.08f * arc, -0.04f * arc, -0.06f * arc);
                }
                else if (CurrentComboStep == 2)
                {
                    rGripLocalPos = _fpGripBaseLocalPos + new Vector3(Mathf.Lerp(-0.34f, 0.26f, t) * arc, 0.06f * arc, 0.20f * arc);
                    rGripLocalRot = _fpGripBaseLocalRot * Quaternion.Euler(55f * arc, Mathf.Lerp(-62f, 48f, t) * arc, 58f * arc);
                    lHandLocalPos = _fpLeftHandBaseLocalPos + new Vector3(0.06f * arc, 0.03f * arc, 0.10f * arc);
                }
                else
                {
                    rGripLocalPos = _fpGripBaseLocalPos + new Vector3(-0.10f * arc, Mathf.Lerp(0.24f, -0.20f, t) * arc, 0.28f * arc);
                    rGripLocalRot = _fpGripBaseLocalRot * Quaternion.Euler(Mathf.Lerp(-42f, 82f, t) * arc, -16f * arc, 0f);
                    lHandLocalPos = _fpLeftHandBaseLocalPos + new Vector3(0.08f * arc, Mathf.Lerp(0.16f, -0.12f, t) * arc, 0.20f * arc);
                }
            }

            Vector3 rGripWorldPos = camTransform.TransformPoint(rGripLocalPos);
            Quaternion rGripWorldRot = camTransform.rotation * rGripLocalRot;

            Quaternion socketLocalRot = rightHandWeaponSocket != null
                ? rightHandWeaponSocket.localRotation
                : Quaternion.LookRotation(Vector3.up, Vector3.forward);
            Vector3 socketLocalPos = rightHandWeaponSocket != null
                ? rightHandWeaponSocket.localPosition
                : new Vector3(-0.038f, 0.082f, 0.01f);

            Quaternion targetRightHandRot = rGripWorldRot * Quaternion.Inverse(socketLocalRot);
            Vector3 targetRightWristPos = rGripWorldPos - (targetRightHandRot * socketLocalPos);

            Vector3 rElbowAnchor = camTransform.TransformPoint(new Vector3(0.42f, -0.44f, 0.16f));
            Vector3 rForearmDir = (targetRightWristPos - rElbowAnchor).normalized;
            float rForearmLen = Mathf.Clamp(_rightHandBone.localPosition.magnitude, 0.22f, 0.34f);
            Vector3 rElbowWorldPos = targetRightWristPos - rForearmDir * rForearmLen;

            Vector3 rShoulderAnchor = camTransform.TransformPoint(new Vector3(0.42f, -0.34f, -0.14f));
            Vector3 rUpperArmDir = (rElbowWorldPos - rShoulderAnchor).normalized;
            float rUpperArmLen = Mathf.Clamp(_rightLowerArmBone.localPosition.magnitude, 0.22f, 0.34f);

            _rightUpperArmBone.position = rElbowWorldPos - rUpperArmDir * rUpperArmLen;
            _rightUpperArmBone.rotation = Quaternion.LookRotation(camTransform.up, rUpperArmDir);

            _rightLowerArmBone.position = rElbowWorldPos;
            _rightLowerArmBone.rotation = Quaternion.LookRotation(targetRightHandRot * Vector3.left, rForearmDir);

            _rightHandBone.position = targetRightWristPos;
            _rightHandBone.rotation = targetRightHandRot;

            if (_leftHandBone != null && _leftLowerArmBone != null && _leftUpperArmBone != null)
            {
                Vector3 lWristWorldPos = camTransform.TransformPoint(lHandLocalPos);
                Quaternion lHandWorldRot = camTransform.rotation * lHandLocalRot * Quaternion.Inverse(Quaternion.LookRotation(Vector3.up, Vector3.forward));

                Vector3 lElbowAnchor = camTransform.TransformPoint(new Vector3(-0.42f, -0.46f, 0.14f));
                Vector3 lForearmDir = (lWristWorldPos - lElbowAnchor).normalized;
                float lForearmLen = Mathf.Clamp(_leftHandBone.localPosition.magnitude, 0.22f, 0.34f);
                Vector3 lElbowWorldPos = lWristWorldPos - lForearmDir * lForearmLen;

                Vector3 lShoulderAnchor = camTransform.TransformPoint(new Vector3(-0.42f, -0.34f, -0.14f));
                Vector3 lUpperArmDir = (lElbowWorldPos - lShoulderAnchor).normalized;
                float lUpperArmLen = Mathf.Clamp(_leftLowerArmBone.localPosition.magnitude, 0.22f, 0.34f);

                _leftUpperArmBone.position = lElbowWorldPos - lUpperArmDir * lUpperArmLen;
                _leftUpperArmBone.rotation = Quaternion.LookRotation(camTransform.up, lUpperArmDir);

                _leftLowerArmBone.position = lElbowWorldPos;
                _leftLowerArmBone.rotation = Quaternion.LookRotation(lHandWorldRot * Vector3.right, lForearmDir);

                _leftHandBone.position = lWristWorldPos;
                _leftHandBone.rotation = lHandWorldRot;
            }
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

            // Red damage flash border on direct hit
            if (_damageFlashTimer > 0f)
            {
                GUI.color = new Color(0.9f, 0.1f, 0.1f, (_damageFlashTimer / 0.35f) * 0.28f);
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

            // Player Health & Stamina Bars above the weapon box
            float hudX = 16f;
            float hudY = Screen.height - 102f;

            GUI.color = new Color(0f, 0f, 0f, 0.78f);
            GUI.DrawTexture(new Rect(hudX, hudY, 320f, 20f), _hudBarTex);
            GUI.DrawTexture(new Rect(hudX + 332f, hudY, 320f, 20f), _hudBarTex);

            float hpRatio = Mathf.Clamp01((float)currentHealth / Mathf.Max(1, maxHealth));
            GUI.color = new Color(0.85f, 0.18f, 0.22f, 0.95f);
            GUI.DrawTexture(new Rect(hudX + 2f, hudY + 2f, 316f * hpRatio, 16f), _hudBarTex);

            float stamRatio = Mathf.Clamp01(currentStamina / Mathf.Max(1f, maxStamina));
            GUI.color = new Color(0.18f, 0.72f, 0.95f, 0.95f);
            GUI.DrawTexture(new Rect(hudX + 334f, hudY + 2f, 316f * stamRatio, 16f), _hudBarTex);
            GUI.color = Color.white;

            GUIStyle barLabelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            barLabelStyle.normal.textColor = Color.white;
            GUI.Label(new Rect(hudX, hudY, 320f, 20f), $"HP: {currentHealth} / {maxHealth}", barLabelStyle);
            GUI.Label(new Rect(hudX + 332f, hudY, 320f, 20f), $"STAMINA: {Mathf.RoundToInt(currentStamina)} / {Mathf.RoundToInt(maxStamina)}  [Alt / E: Dash]", barLabelStyle);

            string weaponStr = equippedWeapon == WeaponType.Sword ? "[1] SWORD  |  2 Spear  |  3 Axe" :
                               equippedWeapon == WeaponType.Spear ? "1 Sword  |  [2] SPEAR  |  3 Axe" :
                               "1 Sword  |  2 Spear  |  [3] AXE";
            string stanceStr = IsDashing ? "DASHING (Invulnerable)" :
                               (IsBlocking ? "BLOCKING [RMB]" : (IsCrouching ? "CROUCHING [C]" : "STANDING [C]"));
            string shieldStr = shieldEquipped ? "ON [G]" : "OFF [G]";
            string viewStr = isThirdPersonView ? "3RD PERSON [V]" : "1ST PERSON [V]";
            string comboStr = IsAttacking
                ? $"  >> COMBO {CurrentComboStep}/3! <<"
                : (IsBlocking ? "  >> SHIELD GUARD <<" : "  [LMB] Attack | [RMB] Block/Parry");

            GUI.Box(new Rect(16, Screen.height - 76, 652, 58),
                $" Weapon: {weaponStr}{comboStr}\n Stance: {stanceStr}  |  Shield: {shieldStr}  |  Camera: {viewStr}  |  Armor: [M]",
                boxStyle);

            if (_canBackstabPrompt && !IsAttacking)
            {
                GUIStyle critStyle = new GUIStyle(boxStyle);
                critStyle.normal.textColor = new Color(1f, 0.85f, 0.2f);
                GUI.Box(new Rect(16, Screen.height - 176, 460, 30),
                    " >> STEALTH BACKSTAB READY! Press [LMB / F] for 3x Instant Takedown <<", critStyle);
            }

            if (!string.IsNullOrEmpty(LastHitMessage) && Time.time - _lastHitMessageTime < 2.6f)
            {
                GUI.Box(new Rect(16, Screen.height - 140, 620, 32), $" {LastHitMessage}", boxStyle);
            }
        }
    }
}
