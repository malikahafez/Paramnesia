using UnityEngine;
using UnityEngine.AI;

namespace StealthGame
{
    [RequireComponent(typeof(NavMeshAgent), typeof(EnemyDetection), typeof(EnemyCombatTarget))]
    [DefaultExecutionOrder(100)]
    public class ZombieAI : MonoBehaviour
    {
        [Tooltip("Optional center; otherwise the spawn position is used.")]
        public Transform roamCenter;
        [Min(0.5f)] public float roamRadius = 4f;
        [HideInInspector] public float roamSpeed = 0.85f;
        [HideInInspector] public float runSpeed = 3.2f;
        [Min(0f)] public float idlePause = 2f;
        [Min(0f)] public float lostSightWait = 3f;
        public bool IsAlerted { get; private set; }
        public string State { get; private set; } = "Roaming";
        NavMeshAgent agent;
        EnemyDetection detection;
        EnemyCombatTarget combat;
        Animator animator;
        Vector3 home;
        float waitUntil, nextPath;
        bool wasVisible;
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
        }

        void OnEnable() { waitUntil = Time.time + Random.Range(0f, idlePause); }

        void Update()
        {
            if (!agent.isOnNavMesh) return;
            agent.nextPosition = transform.position;
            bool visible = detection.IsPlayerDetected;
            if (visible) { IsAlerted = true; waitUntil = Time.time + lostSightWait; }
            else if (wasVisible) { waitUntil = Time.time + lostSightWait; agent.ResetPath(); }
            wasVisible = visible;
            bool blocked = combat.IsDead || combat.IsStaggered || combat.IsWindingUpAttack || combat.IsFlinching;
            agent.isStopped = blocked || (IsAlerted && !visible);
            if (combat.IsDead) { State = "Dead"; agent.ResetPath(); agent.enabled = false; return; }
            if (!visible && IsAlerted && Time.time >= waitUntil) { IsAlerted = false; agent.isStopped = blocked; }
            State = IsAlerted ? (visible ? "Chasing" : "Waiting") : "Roaming";
            if (!IsAlerted && !agent.pathPending && agent.hasPath &&
                agent.remainingDistance <= agent.stoppingDistance + 0.15f)
            {
                agent.ResetPath();
                waitUntil = Time.time + idlePause;
            }
            if (!blocked && visible && detection.player != null)
            {
                agent.speed = runSpeed;
                agent.stoppingDistance = combat.attackReach * 0.8f;
                if (Time.time >= nextPath) { agent.SetDestination(detection.player.position); nextPath = Time.time + 0.2f; }
            }
            else if (!blocked && !IsAlerted && Time.time >= waitUntil && !agent.pathPending &&
                     (!agent.hasPath || agent.remainingDistance <= agent.stoppingDistance + 0.15f))
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
                waitUntil = Time.time + idlePause;
            }
            if (animator != null && animator.runtimeAnimatorController != null)
            {
                var velocity = transform.InverseTransformDirection(agent.velocity);
                animator.SetFloat(MoveX, velocity.x, 0.16f, Time.deltaTime);
                animator.SetFloat(MoveZ, velocity.z, 0.16f, Time.deltaTime);
            }
        }

        void OnDisable() { if (agent != null && agent.enabled && agent.isOnNavMesh) agent.ResetPath(); }

        public void ApplyRootMotion(Vector3 displacement)
        {
            if (agent == null || !agent.enabled || !agent.isOnNavMesh || agent.isStopped || !agent.hasPath ||
                combat.IsDead || combat.IsStaggered || combat.IsFlinching || combat.IsWindingUpAttack) return;
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
        }
    }
}
