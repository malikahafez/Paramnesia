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
        [Tooltip("Optional mesh that jiggles and stretches along the flight path, like a glob of goo.")]
        public Transform goo;
        [Tooltip("Optional burst played where the projectile lands.")]
        public ParticleSystem splat;
        EnemyCombatTarget owner;
        int damage;
        Rigidbody body;
        Collider hitCollider;
        Vector3 gooScale;
        float wobblePhase;

        void Awake()
        {
            body = GetComponent<Rigidbody>();
            hitCollider = GetComponent<Collider>();
            if (goo != null) gooScale = goo.localScale;
            wobblePhase = Random.value * 10f;
        }

        void Update()
        {
            if (goo == null) return;
            Vector3 velocity = body.isKinematic ? Vector3.zero : body.linearVelocity;
            if (velocity.sqrMagnitude > 0.01f) goo.rotation = Quaternion.LookRotation(velocity);
            // Wet jiggle; the faster it flies the longer it stretches, thinning to keep roughly its volume.
            float stretch = 1f + Mathf.Min(velocity.magnitude * 0.04f, 0.4f);
            float jiggle = Mathf.Sin(Time.time * 14f + wobblePhase) * 0.1f;
            float side = 1f / Mathf.Sqrt(stretch);
            goo.localScale = Vector3.Scale(gooScale, new Vector3(side * (1f + jiggle), side * (1f - jiggle), stretch));
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
                // Leave the trail behind to fade out instead of vanishing with the projectile.
                trail.transform.SetParent(null, true);
                trail.emitting = false;
                trail.autodestruct = true;
            }
            // Drips and mist drift on and fade too; the splat bursts where it landed.
            foreach (var particles in GetComponentsInChildren<ParticleSystem>())
            {
                if (particles.transform.parent != transform) continue; // nested systems go with their parent
                particles.transform.SetParent(null, true);
                if (particles == splat) particles.Play();
                else particles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                Destroy(particles.gameObject, 2f); // outlives every drip, puff and splat droplet set up for it
            }
            Destroy(gameObject);
        }
    }
}
