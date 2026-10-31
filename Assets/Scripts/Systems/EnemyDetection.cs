using UnityEngine;
using UnityEngine.Events;

namespace StealthGame
{
    [DisallowMultipleComponent]
    public class EnemyDetection : MonoBehaviour
    {
        [Tooltip("Automatically finds the active object tagged Player when empty.")]
        public Transform player;
        public Vector3 eyeOffset = new Vector3(0f, 1.6f, 0f);
        public Vector3 targetOffset = new Vector3(0f, 1f, 0f);
        [Min(0.1f)] public float viewRange = 10f;
        [Range(1f, 179f)] public float viewAngle = 70f;
        [Tooltip("Within this eye-to-target distance, players in front bypass the vision angle. Walls still block sight. Set to zero to disable.")]
        [Min(0f)] public float proximityRange = 1.25f;
        [Tooltip("Solid colliders on these layers block sight. Triggers are ignored.")]
        public LayerMask sightLayers = ~0;
        [Tooltip("Shown above the enemy only while the player is detected.")]
        public GameObject detectionIndicator;
        public Transform detectionIndicatorAnchor;
        [Header("Development")]
        [Tooltip("Draw vision with Scene/Game view Gizmos enabled. Excluded from player builds.")]
        public bool showVision;
        [Tooltip("Log detection changes in the Editor and Development Builds only.")]
        public bool logDetectionChanges;
        public UnityEvent onPlayerDetected = new UnityEvent();
        public UnityEvent onPlayerLost = new UnityEvent();
        public bool IsPlayerDetected { get; private set; }
        readonly RaycastHit[] sightHits = new RaycastHit[32];
        float nextSightCheck, alertTime;
        Vector3 indicatorScale;

        void Awake() { if (detectionIndicator != null) indicatorScale = detectionIndicator.transform.localScale; }

        void Update()
        {
            if (Application.isPlaying && Time.time < nextSightCheck) return;
            nextSightCheck = Time.time + 0.1f;
            if (player == null)
            {
                var found = GameObject.FindGameObjectWithTag("Player");
                if (found != null) player = found.transform;
            }

            bool detected = CanSeePlayer();
            if (detected == IsPlayerDetected) return;
            IsPlayerDetected = detected;
            if (detected) alertTime = Time.time;
            if (detectionIndicator != null) detectionIndicator.SetActive(detected);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (logDetectionChanges)
                Debug.Log($"[{name}] Player {(detected ? "detected" : "lost")}", this);
#endif
            if (detected) onPlayerDetected.Invoke();
            else onPlayerLost.Invoke();
        }

        public bool CanSeePlayer()
        {
            if (player == null || !player.gameObject.activeInHierarchy) return false;
            Vector3 origin = transform.TransformPoint(eyeOffset);
            Vector3 direction = player.TransformPoint(targetOffset) - origin;
            float distance = direction.magnitude;
            if (distance > viewRange) return false;
            Vector3 toPlayer = Vector3.ProjectOnPlane(player.position - transform.position, transform.up);
            bool closeInFront = proximityRange > 0f && distance <= proximityRange &&
                Vector3.Dot(transform.forward, toPlayer) > 0f;
            if (!closeInFront && distance > 0.001f && Vector3.Angle(transform.forward, direction) > viewAngle * 0.5f)
                return false;

            // Examine every hit so our own collider never hides a wall or the player.
            int count = Physics.RaycastNonAlloc(origin, direction.normalized, sightHits, distance,
                sightLayers, QueryTriggerInteraction.Ignore);
            if (count == sightHits.Length) return false;
            for (int i = 0; i < count; i++)
            {
                var hit = sightHits[i];
                if (hit.transform.IsChildOf(transform) || hit.transform.IsChildOf(player)) continue;
                return false;
            }
            return true;
        }

        void LateUpdate()
        {
            if (detectionIndicator != null && detectionIndicator.activeSelf && indicatorScale != Vector3.zero)
                detectionIndicator.transform.localScale = indicatorScale * (1f + 0.5f * Mathf.Sin(Mathf.Clamp01((Time.time - alertTime) / 0.4f) * Mathf.PI));
            if (detectionIndicator != null && detectionIndicator.activeSelf && detectionIndicatorAnchor != null)
                detectionIndicator.transform.SetPositionAndRotation(
                    detectionIndicatorAnchor.position + Vector3.up * 0.45f, Quaternion.identity);
        }

        void OnDisable()
        {
            if (detectionIndicator != null) detectionIndicator.SetActive(false);
            if (!IsPlayerDetected) return;
            IsPlayerDetected = false;
            onPlayerLost.Invoke();
        }

        void OnDrawGizmos()
        {
#if UNITY_EDITOR
            if (!showVision && !UnityEditor.EditorPrefs.GetBool("Paramnesia.ShowEnemyVision", false)) return;
            Vector3 origin = transform.TransformPoint(eyeOffset);
            Color color = IsPlayerDetected ? Color.red : Color.yellow;
            UnityEditor.Handles.color = new Color(color.r, color.g, color.b, 0.08f);
            UnityEditor.Handles.DrawSolidArc(origin, transform.up,
                Quaternion.AngleAxis(-viewAngle * 0.5f, transform.up) * transform.forward, viewAngle, viewRange);
            UnityEditor.Handles.DrawWireArc(origin, transform.up, -transform.right, 180f, Mathf.Min(proximityRange, viewRange));
            Gizmos.color = color;
            float radius = viewRange * Mathf.Sin(viewAngle * 0.5f * Mathf.Deg2Rad);
            float depth = viewRange * Mathf.Cos(viewAngle * 0.5f * Mathf.Deg2Rad);
            Vector3 last = origin + transform.forward * depth + transform.right * radius;
            for (int i = 1; i <= 32; i++)
            {
                float angle = i * Mathf.PI * 2f / 32f;
                Vector3 next = origin + transform.forward * depth +
                    (transform.right * Mathf.Cos(angle) + transform.up * Mathf.Sin(angle)) * radius;
                Gizmos.DrawLine(last, next);
                if (i % 8 == 0) Gizmos.DrawLine(origin, next);
                last = next;
            }
            for (int i = 0; i < 32; i++)
            {
                float start = -viewAngle * 0.5f + viewAngle * i / 32f;
                float end = -viewAngle * 0.5f + viewAngle * (i + 1) / 32f;
                Gizmos.DrawLine(origin + Quaternion.AngleAxis(start, transform.up) * transform.forward * viewRange,
                    origin + Quaternion.AngleAxis(end, transform.up) * transform.forward * viewRange);
                Gizmos.DrawLine(origin + Quaternion.AngleAxis(start, transform.right) * transform.forward * viewRange,
                    origin + Quaternion.AngleAxis(end, transform.right) * transform.forward * viewRange);
            }
            if (IsPlayerDetected && player != null)
                Gizmos.DrawLine(origin, player.TransformPoint(targetOffset));
#endif
        }
    }
}
