using UnityEngine;
using StealthGame.Player;

namespace StealthGame
{
    /// <summary>
    /// Handles enemy health, melee hit reactions (crimson flash, hit-spark burst, knockback & humanoid
    /// stagger animation), stealth backstab detection, active enemy pursuit & counter-attacks when the
    /// player is detected, parry stagger, floating health bar UI, and humanoid death animation.
    /// </summary>
    [DisallowMultipleComponent]
    public class EnemyCombatTarget : MonoBehaviour
    {
        [Header("Health")]
        public int maxHealth = 100;
        public int currentHealth = 100;
        public bool IsDead { get; private set; }

        [Header("Combat & Counter-Attack")]
        public bool enableCounterAttack = true;
        public float chaseSpeed = 1.75f;
        public float attackReach = 2.05f;
        public float attackCooldown = 2.2f;
        public int attackDamage = 18;

        [Header("References")]
        public EnemyDetection enemyDetection;
        public Animator enemyAnimator;
        public Collider enemyCollider;

        private Renderer[] _renderers;
        private MaterialPropertyBlock _propBlock;
        private float _flashTimer;
        private Color _flashTintColor = new Color(1.0f, 0.22f, 0.22f, 1f);
        private float _flinchTimer;
        private float _staggerTimer;
        private float _aggroTimer;
        private float _attackCooldownTimer;
        private float _attackWindupTimer;
        private bool _isWindingUpAttack;
        private float _deathTime = -999f;
        private float _lastDamagedTime = -999f;
        private bool _lastHitWasCrit;
        private Vector3 _flinchDirection;

        private Transform _spineBone;
        private Transform _rightUpperArmBone;
        private Transform _rightLowerArmBone;
        private Transform _leftUpperLegBone;
        private Transform _rightUpperLegBone;
        private float _walkCycle;

        private static Texture2D _barTex;
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int HitTriggerId = Animator.StringToHash("Hit");
        private static readonly int DeathTriggerId = Animator.StringToHash("Death");
        private static readonly int IsDeadBoolId = Animator.StringToHash("IsDead");

        public bool IsStaggered => _staggerTimer > 0f;
        public bool IsWindingUpAttack => _isWindingUpAttack;

        private void Awake()
        {
            currentHealth = maxHealth;
            if (enemyDetection == null) enemyDetection = GetComponent<EnemyDetection>();
            if (enemyAnimator == null) enemyAnimator = GetComponentInChildren<Animator>();
            if (enemyCollider == null) enemyCollider = GetComponent<Collider>();

            _renderers = GetComponentsInChildren<Renderer>(true);
            _propBlock = new MaterialPropertyBlock();

            if (enemyAnimator != null && enemyAnimator.isHuman)
            {
                _spineBone = enemyAnimator.GetBoneTransform(HumanBodyBones.Spine)
                             ?? enemyAnimator.GetBoneTransform(HumanBodyBones.Chest);
                _rightUpperArmBone = enemyAnimator.GetBoneTransform(HumanBodyBones.RightUpperArm);
                _rightLowerArmBone = enemyAnimator.GetBoneTransform(HumanBodyBones.RightLowerArm);
                _leftUpperLegBone = enemyAnimator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
                _rightUpperLegBone = enemyAnimator.GetBoneTransform(HumanBodyBones.RightUpperLeg);
            }
        }

        public bool CanBeStealthBackstabbed(Transform attacker)
        {
            if (IsDead || attacker == null) return false;

            // Undetected enemy or attacker sneaking behind the enemy's back
            bool undetected = enemyDetection != null && !enemyDetection.IsPlayerDetected && _aggroTimer <= 0f;
            Vector3 toAttacker = (attacker.position - transform.position);
            toAttacker.y = 0f;
            bool isBehind = toAttacker.sqrMagnitude > 0.001f &&
                            Vector3.Dot(transform.forward, toAttacker.normalized) < -0.10f;

            return undetected || isBehind;
        }

        public void TakeDamage(int damage)
        {
            TakeDamage(damage, false);
        }

        public void TakeDamage(int damage, bool isCriticalBackstab)
        {
            if (IsDead || damage <= 0) return;

            currentHealth = Mathf.Max(0, currentHealth - damage);
            _lastDamagedTime = Time.time;
            _lastHitWasCrit = isCriticalBackstab;
            _aggroTimer = 6.0f;
            _isWindingUpAttack = false;
            _flashTintColor = isCriticalBackstab
                ? new Color(1.0f, 0.72f, 0.15f, 1f)
                : new Color(1.0f, 0.22f, 0.22f, 1f);
            _flashTimer = 0.24f;
            _flinchTimer = 0.35f;

            Transform playerTransform = GetPlayerTransform();
            if (playerTransform != null)
            {
                Vector3 away = transform.position - playerTransform.position;
                away.y = 0f;
                _flinchDirection = away.sqrMagnitude > 0.001f ? away.normalized : -transform.forward;

                if (currentHealth > 0 && away.sqrMagnitude > 0.001f)
                {
                    transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(-_flinchDirection), 0.65f);
                }

                float knockDist = isCriticalBackstab ? 0.32f : 0.18f;
                transform.position += _flinchDirection * knockDist;
            }
            else
            {
                _flinchDirection = -transform.forward;
            }

            ApplyFlashColor(true);
            SpawnHitSparkBurst(transform.position + Vector3.up * 1.15f, isCriticalBackstab ? 30 : 18);

            if (currentHealth <= 0)
            {
                Die();
            }
            else if (enemyAnimator != null && HasParameter(enemyAnimator, HitTriggerId))
            {
                enemyAnimator.SetTrigger(HitTriggerId);
            }
        }

        public void ApplyParryStagger(float duration = 1.8f)
        {
            if (IsDead) return;

            _isWindingUpAttack = false;
            _staggerTimer = duration;
            _flinchTimer = 0.45f;
            _attackCooldownTimer = duration + 0.6f;
            _flashTintColor = new Color(0.35f, 0.9f, 1.0f, 1f);
            _flashTimer = 0.3f;

            Transform playerTransform = GetPlayerTransform();
            if (playerTransform != null)
            {
                Vector3 away = transform.position - playerTransform.position;
                away.y = 0f;
                if (away.sqrMagnitude > 0.001f)
                {
                    transform.position += away.normalized * 0.42f;
                }
            }

            ApplyFlashColor(true);
            SpawnHitSparkBurst(transform.position + Vector3.up * 1.25f + transform.forward * 0.4f, 26);

            if (enemyAnimator != null && HasParameter(enemyAnimator, HitTriggerId))
            {
                enemyAnimator.SetTrigger(HitTriggerId);
            }
        }

        private Transform GetPlayerTransform()
        {
            if (enemyDetection != null && enemyDetection.player != null)
                return enemyDetection.player;
            var found = GameObject.FindGameObjectWithTag("Player");
            return found != null ? found.transform : null;
        }

        private void Die()
        {
            IsDead = true;
            _isWindingUpAttack = false;
            _deathTime = Time.time;

            if (enemyDetection != null)
            {
                if (enemyDetection.detectionIndicator != null)
                    enemyDetection.detectionIndicator.SetActive(false);
                enemyDetection.enabled = false;
            }

            if (enemyCollider != null)
            {
                enemyCollider.enabled = false;
            }

            if (enemyAnimator != null)
            {
                if (HasParameter(enemyAnimator, IsDeadBoolId))
                    enemyAnimator.SetBool(IsDeadBoolId, true);
                if (HasParameter(enemyAnimator, DeathTriggerId))
                    enemyAnimator.SetTrigger(DeathTriggerId);
            }
        }

        private static bool HasParameter(Animator anim, int nameHash)
        {
            if (anim == null || anim.runtimeAnimatorController == null) return false;
            foreach (var p in anim.parameters)
            {
                if (p.nameHash == nameHash) return true;
            }
            return false;
        }

        private void Update()
        {
            if (_flashTimer > 0f)
            {
                _flashTimer -= Time.deltaTime;
                if (_flashTimer <= 0f)
                {
                    ApplyFlashColor(false);
                }
            }

            if (_flinchTimer > 0f) _flinchTimer -= Time.deltaTime;
            if (_staggerTimer > 0f) _staggerTimer -= Time.deltaTime;
            if (_aggroTimer > 0f) _aggroTimer -= Time.deltaTime;
            if (_attackCooldownTimer > 0f) _attackCooldownTimer -= Time.deltaTime;

            if (!IsDead && enableCounterAttack)
            {
                UpdateCombatBehavior();
            }
        }

        private void UpdateCombatBehavior()
        {
            if (_staggerTimer > 0f || _flinchTimer > 0.15f)
            {
                _isWindingUpAttack = false;
                return;
            }

            bool playerDetected = (enemyDetection != null && enemyDetection.IsPlayerDetected) || _aggroTimer > 0f;
            if (!playerDetected)
            {
                _isWindingUpAttack = false;
                _walkCycle = Mathf.MoveTowards(_walkCycle, 0f, Time.deltaTime * 4f);
                return;
            }

            Transform playerTransform = GetPlayerTransform();
            if (playerTransform == null) return;

            Vector3 toPlayer = playerTransform.position - transform.position;
            toPlayer.y = 0f;
            float dist = toPlayer.magnitude;

            if (dist > 9.5f)
            {
                _isWindingUpAttack = false;
                return;
            }

            if (dist > 0.05f)
            {
                Quaternion targetRot = Quaternion.LookRotation(toPlayer.normalized);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * 7f);
            }

            if (_isWindingUpAttack)
            {
                _attackWindupTimer -= Time.deltaTime;
                if (_attackWindupTimer <= 0f)
                {
                    _isWindingUpAttack = false;
                    _attackCooldownTimer = attackCooldown;

                    if (dist <= attackReach + 0.55f)
                    {
                        var pcc = playerTransform.GetComponent<PlayerCharacterController>();
                        if (pcc != null)
                        {
                            pcc.OnEnemyAttackStrike(this, attackDamage);
                        }
                    }
                }
                return;
            }

            if (dist > attackReach * 0.85f)
            {
                Vector3 moveStep = toPlayer.normalized * (chaseSpeed * Time.deltaTime);
                transform.position += moveStep;
                _walkCycle += Time.deltaTime * 7.5f;
            }
            else
            {
                _walkCycle = Mathf.MoveTowards(_walkCycle, 0f, Time.deltaTime * 5f);
                if (_attackCooldownTimer <= 0f)
                {
                    _isWindingUpAttack = true;
                    _attackWindupTimer = 0.48f;
                }
            }
        }

        private void LateUpdate()
        {
            if (IsDead) return;

            // 1. Procedural upper-body stagger recoil
            if (_flinchTimer > 0f && _spineBone != null)
            {
                float t = _flinchTimer / 0.35f;
                float recoilAngle = Mathf.Sin(t * Mathf.PI) * 26f;
                _spineBone.localRotation *= Quaternion.Euler(-recoilAngle, recoilAngle * 0.35f, 0f);
            }
            else if (_staggerTimer > 0f && _spineBone != null)
            {
                float sway = Mathf.Sin(Time.time * 10f) * 12f;
                _spineBone.localRotation *= Quaternion.Euler(-14f, sway, 0f);
            }

            // 2. Procedural zombie walk stride when pursuing player
            if (_walkCycle > 0.01f && !_isWindingUpAttack && _staggerTimer <= 0f)
            {
                float legSwing = Mathf.Sin(_walkCycle) * 24f;
                if (_leftUpperLegBone != null)
                    _leftUpperLegBone.localRotation *= Quaternion.Euler(legSwing, 0f, 0f);
                if (_rightUpperLegBone != null)
                    _rightUpperLegBone.localRotation *= Quaternion.Euler(-legSwing, 0f, 0f);
            }

            // 3. Procedural enemy claw/arm strike windup & swing
            if (_isWindingUpAttack && _rightUpperArmBone != null)
            {
                float p = 1f - Mathf.Clamp01(_attackWindupTimer / 0.48f);
                float armPitch = p < 0.65f
                    ? Mathf.Lerp(0f, -75f, p / 0.65f)
                    : Mathf.Lerp(-75f, 55f, (p - 0.65f) / 0.35f);
                _rightUpperArmBone.localRotation *= Quaternion.Euler(armPitch, -18f, 0f);
                if (_rightLowerArmBone != null)
                    _rightLowerArmBone.localRotation *= Quaternion.Euler(-35f, 0f, 0f);
            }
        }

        private void ApplyFlashColor(bool flashActive)
        {
            if (_renderers == null || _propBlock == null) return;

            foreach (var r in _renderers)
            {
                if (r == null) continue;
                if (enemyDetection != null && enemyDetection.detectionIndicator != null &&
                    r.transform.IsChildOf(enemyDetection.detectionIndicator.transform))
                {
                    continue;
                }

                r.GetPropertyBlock(_propBlock);
                if (flashActive)
                {
                    _propBlock.SetColor(BaseColorId, _flashTintColor);
                    _propBlock.SetColor(ColorId, _flashTintColor);
                }
                else
                {
                    _propBlock.Clear();
                }
                r.SetPropertyBlock(_propBlock);
            }
        }

        public static void SpawnHitSparkBurst(Vector3 worldPos, int particleCount = 18)
        {
            GameObject burstObj = new GameObject("MeleeHitSparkBurst");
            burstObj.transform.position = worldPos;

            var ps = burstObj.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.duration = 0.35f;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.15f, 0.32f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(2.2f, 5.8f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.09f);
            main.gravityModifier = 1.6f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(1f, 0.85f, 0.3f, 1f),
                new Color(0.95f, 0.2f, 0.15f, 1f));

            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)particleCount) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.12f;

            var psRenderer = burstObj.GetComponent<ParticleSystemRenderer>();
            if (psRenderer != null)
            {
                Shader unlitShader = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                                     ?? Shader.Find("Universal Render Pipeline/Unlit")
                                     ?? Shader.Find("Sprites/Default");
                if (unlitShader != null)
                {
                    psRenderer.material = new Material(unlitShader);
                }
            }

            ps.Play();
            Destroy(burstObj, 0.6f);
        }

        private void OnGUI()
        {
            bool isEngaged = !IsDead && (currentHealth < maxHealth || Time.time - _lastDamagedTime < 6f || _isWindingUpAttack || _staggerTimer > 0f);
            bool showBar = isEngaged || (IsDead && Time.time - _deathTime < 2.4f);
            if (!showBar) return;

            Camera cam = Camera.main;
            if (cam == null) return;

            Vector3 worldHead = transform.position + Vector3.up * 2.15f;
            float dist = Vector3.Distance(cam.transform.position, worldHead);
            if (dist > 18f) return;

            Vector3 screenPos = cam.WorldToScreenPoint(worldHead);
            if (screenPos.z <= 0.05f) return;

            if (_barTex == null)
            {
                _barTex = new Texture2D(1, 1);
                _barTex.SetPixel(0, 0, Color.white);
                _barTex.Apply();
            }

            float barWidth = 128f;
            float barHeight = 14f;
            float x = screenPos.x - barWidth * 0.5f;
            float y = (Screen.height - screenPos.y) - 24f;

            string displayName = name.Replace("Enemy_", "").Replace("_", " ");
            GUIStyle labelStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 11,
                fontStyle = FontStyle.Bold
            };

            string statusText;
            if (IsDead)
            {
                labelStyle.normal.textColor = new Color(1f, 0.45f, 0.35f);
                statusText = _lastHitWasCrit ? $"{displayName} [ASSASSINATED!]" : $"{displayName} [DEFEATED]";
            }
            else if (_staggerTimer > 0f)
            {
                labelStyle.normal.textColor = new Color(0.4f, 0.95f, 1f);
                statusText = $"{displayName} [PARRIED / STAGGERED!]";
            }
            else if (_isWindingUpAttack)
            {
                labelStyle.normal.textColor = new Color(1f, 0.85f, 0.2f);
                statusText = $"[!] {displayName} STRIKING! (RMB Block / Alt Dash)";
            }
            else
            {
                labelStyle.normal.textColor = Color.white;
                statusText = $"{displayName} ({currentHealth}/{maxHealth})";
            }

            GUI.color = new Color(0f, 0f, 0f, 0.78f);
            GUI.DrawTexture(new Rect(x - 2f, y - 18f, barWidth + 4f, barHeight + 20f), _barTex);

            float ratio = Mathf.Clamp01((float)currentHealth / Mathf.Max(1, maxHealth));
            GUI.color = IsDead ? new Color(0.45f, 0.1f, 0.1f, 0.9f) :
                        (_staggerTimer > 0f ? new Color(0.25f, 0.75f, 0.95f, 0.95f) : new Color(0.85f, 0.18f, 0.18f, 0.95f));
            GUI.DrawTexture(new Rect(x, y, barWidth * ratio, barHeight), _barTex);

            GUI.color = Color.white;
            GUI.Label(new Rect(x - 36f, y - 18f, barWidth + 72f, 18f), statusText, labelStyle);
        }
    }
}
