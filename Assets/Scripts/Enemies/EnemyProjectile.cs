using UnityEngine;
using StealthGame.Player;

namespace StealthGame
{
    /// <summary>
    /// Thrown enemy projectile. Rides in the thrower's hand until released, then arcs onto its target
    /// and lands as an enemy strike, so dash, block and parry apply exactly as they do to melee hits.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class EnemyProjectile : MonoBehaviour
    {
        public float lifetime = 4f;
        [Tooltip("End-over-end tumble in degrees per second.")]
        public float spinSpeed = 900f;
        public TrailRenderer trail;
        EnemyCombatTarget owner;
        int damage;
        Rigidbody body;
        Collider hitCollider;

        void Awake()
        {
            body = GetComponent<Rigidbody>();
            hitCollider = GetComponent<Collider>();
        }

        /// <summary>Carried by the hand: no physics, no hits, no trail.</summary>
        public void Hold(Transform hand)
        {
            transform.SetParent(hand, true);
            body.isKinematic = true;
            if (hitCollider != null) hitCollider.enabled = false;
            if (trail != null) trail.emitting = false;
        }

        public void Launch(EnemyCombatTarget thrower, int strikeDamage, Vector3 target, float speed)
        {
            owner = thrower;
            damage = strikeDamage;
            transform.SetParent(null, true);
            body.isKinematic = false;
            if (hitCollider != null) hitCollider.enabled = true;
            Vector3 toTarget = target - transform.position;
            float time = Mathf.Max(0.1f, toTarget.magnitude / speed);
            // Ballistic velocity that reaches the target after `time` seconds under gravity.
            body.linearVelocity = toTarget / time - 0.5f * time * Physics.gravity;
            body.maxAngularVelocity = spinSpeed * Mathf.Deg2Rad;
            body.angularVelocity = Vector3.Cross(Vector3.up, toTarget).normalized * spinSpeed * Mathf.Deg2Rad;
            if (trail != null) { trail.Clear(); trail.emitting = true; }
            Destroy(gameObject, lifetime);
        }

        void OnTriggerEnter(Collider other)
        {
            if (other.isTrigger || (owner != null && other.transform.IsChildOf(owner.transform))) return;
            var player = other.GetComponentInParent<PlayerCharacterController>();
            if (player != null) player.OnEnemyAttackStrike(owner, damage);
            if (trail != null)
            {
                // Leave the trail behind to fade out instead of vanishing with the bone.
                trail.transform.SetParent(null, true);
                trail.emitting = false;
                trail.autodestruct = true;
            }
            Destroy(gameObject);
        }
    }
}
