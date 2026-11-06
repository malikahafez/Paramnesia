using UnityEngine;
using UnityEngine.AI;

namespace StealthGame
{
    /// <summary>
    /// What every zombie shares outside a fight: roams near home, takes a beat to notice the player (or plays a
    /// notice animation such as the Brute's roar), and when sight is lost jogs to where the player was heading and
    /// looks around before giving up. Being hit turns it on the attacker. The fight itself ("Chasing") is each
    /// zombie type's own: ZombieAI closes to claw range, RangedZombieAI keeps throwing distance.
    /// Root motion drives all movement, so starts, stops and the momentum carried into attacks follow the
    /// animation; the head tracks whatever has its attention through look-at IK.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent), typeof(EnemyDetection), typeof(EnemyCombatTarget))]
    public abstract class ZombieBrain : MonoBehaviour
    {
        [Tooltip("Optional center; otherwise the spawn position is used.")]
        public Transform roamCenter;
        [Min(0.5f)] public float roamRadius = 4f;
        [HideInInspector] public float roamSpeed = 0.85f;
        [HideInInspector] public float runSpeed = 3.2f;
        [Tooltip("Average pause between roaming destinations; each pause is half to one and a half times this.")]
        [Min(0f)] public float idlePause = 2f;
        [Tooltip("Seconds spent looking around where the player was last seen before giving up.")]
        [Min(0f)] public float lostSightWait = 3f;
        [Tooltip("Random pause to register the player before charging, in seconds (min, max).")]
        public Vector2 reactionTime = new Vector2(0.35f, 0.7f);
        [Header("Notice animation (optional)")]
        [Tooltip("Animator state played on first spotting the player more than 4 m away, e.g. the Brute's roar.")]
        public string noticeState = "";
        [Tooltip("Seconds into the notice clip to start from.")]
        public float noticeOffset;
        public float noticeDuration = 2f;
        public bool IsAlerted { get; private set; }
        public string State { get; private set; } = "Roaming";
        /// <summary>Where the head looks and how strongly (0..1); applied in ZombieRootMotion's IK pass.</summary>
        public Vector3 LookPosition { get; private set; }
        public float LookWeight { get; private set; }

        protected NavMeshAgent agent;
        protected EnemyDetection detection;
        protected EnemyCombatTarget combat;
        protected float nextPath;
        Animator animator;
        Vector3 home, lastSeen, lastSeenVelocity, lookTarget;
        float waitUntil, noticeUntil, searchUntil, nextGlance, nextNoticeAnimation, lookTargetWeight;
        bool wasVisible, searchArrived;
        static readonly int MoveX = Animator.StringToHash("MoveX");
        static readonly int MoveZ = Animator.StringToHash("MoveZ");

        void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            detection = GetComponent<EnemyDetection>();
            combat = GetComponent<EnemyCombatTarget>();
            animator = GetComponentInChildren<Animator>();
            home = transform.position;
            agent.updatePosition = false;
            LookPosition = lookTarget = transform.position + transform.forward * 3f + Vector3.up * 1.6f;
        }

        void OnEnable() { waitUntil = Time.time + Pause(); }

        void Update()
        {
            if (combat.IsDead)
            {
                if (State != "Dead" && agent.enabled && agent.isOnNavMesh) agent.ResetPath();
                State = "Dead";
                agent.enabled = false;
                LookWeight = 0f;
                return;
            }
            if (!agent.isOnNavMesh) return;
            agent.nextPosition = transform.position;
            var player = detection.player;
            bool visible = detection.IsPlayerDetected && player != null;
            bool busy = combat.IsStaggered || combat.IsWindingUpAttack || combat.IsFlinching;
            if (visible)
            {
                if (wasVisible && Time.deltaTime > 0f)
                    lastSeenVelocity = Vector3.Lerp(lastSeenVelocity, (player.position - lastSeen) / Time.deltaTime, 0.25f);
                lastSeen = player.position;
                if (!IsAlerted) Notice();
                else if (State != "Noticing" || Time.time >= noticeUntil) State = "Chasing";
            }
            // Head for where the player was going, not just where they vanished.
            else if (IsAlerted && State != "Searching" && !StillTracking) StartSearch(lastSeen + Vector3.ClampMagnitude(lastSeenVelocity, 3f));
            wasVisible = visible;

            agent.isStopped = busy || State == "Noticing" || (State == "Searching" && searchArrived);
            switch (State)
            {
                case "Noticing":
                    TurnTowards(lastSeen, 4f); // the head snaps round first, the body follows
                    Look(lastSeen + Vector3.up * 1.5f, 1f);
                    break;
                case "Chasing":
                    Engage(player, busy);
                    break;
                case "Searching":
                    agent.speed = Mathf.Lerp(roamSpeed, runSpeed, 0.45f); // a wary jog
                    agent.stoppingDistance = 0.4f;
                    if (!searchArrived && !agent.pathPending && (!agent.hasPath || agent.remainingDistance <= agent.stoppingDistance + 0.2f))
                    {
                        searchArrived = true;
                        searchUntil = Time.time + lostSightWait;
                    }
                    if (Time.time >= searchUntil) StandDown();
                    if (searchArrived) LookAround(1f);
                    else Look(lastSeen + Vector3.up * 1.5f, 0.8f);
                    break;
                default:
                    Roam(busy);
                    break;
            }

            if (animator != null && animator.runtimeAnimatorController != null)
            {
                var velocity = agent.velocity;
                // Ease off into sharp turns so bodies arc round corners instead of pivoting at full stride.
                if (velocity.sqrMagnitude > 0.01f)
                    velocity *= Mathf.Lerp(0.3f, 1f, Mathf.Clamp01(Vector3.Dot(transform.forward, velocity.normalized)));
                var local = transform.InverseTransformDirection(velocity);
                animator.SetFloat(MoveX, local.x, 0.2f, Time.deltaTime);
                animator.SetFloat(MoveZ, local.z, 0.2f, Time.deltaTime);
            }
            LookPosition = Vector3.Lerp(LookPosition, lookTarget, 1f - Mathf.Exp(-5f * Time.deltaTime));
            LookWeight = Mathf.MoveTowards(LookWeight, lookTargetWeight, 1.5f * Time.deltaTime);
        }

        /// <summary>Called by EnemyCombatTarget when hit: turn on the attacker even if it was never seen.</summary>
        public void OnDamaged(Vector3 attacker)
        {
            if (combat.IsDead) return;
            lastSeen = attacker;
            lastSeenVelocity = Vector3.zero;
            IsAlerted = true;
            nextNoticeAnimation = Time.time + 20f; // no roaring mid-fight
            if (!detection.IsPlayerDetected && !StillTracking) StartSearch(attacker);
        }

        /// <summary>The fight, run every frame while "Chasing": move, face and look as this zombie type fights.</summary>
        protected abstract void Engage(Transform player, bool busy);

        /// <summary>True while the zombie keeps track of an unseen player instead of going to search for them.</summary>
        protected virtual bool StillTracking => false;

        /// <summary>Drop the alert and go back to roaming.</summary>
        protected void StandDown()
        {
            IsAlerted = false;
            State = "Roaming";
            agent.ResetPath();
            waitUntil = Time.time + Pause();
        }

        void Notice()
        {
            IsAlerted = true;
            State = "Noticing";
            agent.ResetPath();
            bool far = (lastSeen - transform.position).sqrMagnitude > 16f;
            if (far && !string.IsNullOrEmpty(noticeState) && Time.time >= nextNoticeAnimation && animator != null)
            {
                animator.CrossFadeInFixedTime(noticeState, 0.3f, 0, noticeOffset);
                noticeUntil = Time.time + noticeDuration;
                nextNoticeAnimation = Time.time + 20f;
            }
            else noticeUntil = Time.time + Random.Range(reactionTime.x, reactionTime.y);
        }

        void StartSearch(Vector3 target)
        {
            State = "Searching";
            searchArrived = false;
            searchUntil = Time.time + 10f; // give up if the trip itself drags on
            if (!NavMesh.SamplePosition(target, out var hit, 2f, agent.areaMask) || !agent.SetDestination(hit.position))
            {
                searchArrived = true;
                searchUntil = Time.time + lostSightWait;
            }
        }

        void Roam(bool busy)
        {
            if (agent.hasPath && !agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.15f)
            {
                agent.ResetPath();
                waitUntil = Time.time + Pause();
            }
            if (!busy && Time.time >= waitUntil && !agent.pathPending && !agent.hasPath)
            {
                agent.speed = roamSpeed;
                agent.stoppingDistance = 0.15f;
                var center = roamCenter != null ? roamCenter.position : home;
                for (int i = 0; i < 8; i++)
                {
                    var offset = Random.insideUnitCircle * roamRadius;
                    if (!NavMesh.SamplePosition(center + new Vector3(offset.x, 0f, offset.y), out var hit, 1f, agent.areaMask)) continue;
                    if (Vector3.Distance(hit.position, center) > roamRadius) continue;
                    var path = new NavMeshPath();
                    if (!agent.CalculatePath(hit.position, path) || path.status != NavMeshPathStatus.PathComplete) continue;
                    agent.SetPath(path);
                    break;
                }
                waitUntil = Time.time + Pause();
            }
            if (agent.hasPath) Look(agent.steeringTarget + Vector3.up * 1.5f, 0.35f);
            else LookAround(0.7f);
        }

        float Pause() => idlePause * Random.Range(0.5f, 1.5f);

        protected void Look(Vector3 target, float weight)
        {
            lookTarget = target;
            lookTargetWeight = weight;
        }

        void LookAround(float weight)
        {
            if (Time.time >= nextGlance)
            {
                var direction = Quaternion.Euler(0f, Random.Range(-75f, 75f), 0f) * transform.forward;
                lookTarget = transform.position + direction * 4f + Vector3.up * Random.Range(1.1f, 1.8f);
                nextGlance = Time.time + Random.Range(1f, 2.2f);
            }
            lookTargetWeight = weight;
        }

        protected void TurnTowards(Vector3 point, float rate)
        {
            var to = point - transform.position;
            to.y = 0f;
            if (to.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(to), 1f - Mathf.Exp(-rate * Time.deltaTime));
        }

        void OnDisable() { if (agent != null && agent.enabled && agent.isOnNavMesh) agent.ResetPath(); }

        public void ApplyRootMotion(Vector3 displacement)
        {
            // Animation owns movement, including momentum carried into stops and attacks; only death and hit reactions freeze it.
            if (agent == null || !agent.enabled || !agent.isOnNavMesh || combat.IsDead || combat.IsStaggered || combat.IsFlinching) return;
            displacement.y = 0f;
            var destination = transform.position + displacement;
            // Animation supplies distance; navigation only constrains it to walkable ground.
            if (NavMesh.Raycast(transform.position, destination, out var edge, agent.areaMask)) destination = edge.position;
            if (NavMesh.SamplePosition(destination, out var ground, 0.3f, agent.areaMask))
            {
                transform.position = ground.position;
                agent.nextPosition = ground.position;
            }
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(roamCenter != null ? roamCenter.position : Application.isPlaying ? home : transform.position, roamRadius);
            if (IsAlerted) { Gizmos.color = Color.yellow; Gizmos.DrawWireSphere(lastSeen, 0.3f); }
        }
    }
}
