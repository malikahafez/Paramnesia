using UnityEngine;
using UnityEngine.AI;

namespace StealthGame
{
    /// <summary>
    /// Thrower (the hazmat zombie): never closes to melee. It walks up to throwing range, plants its feet and throws
    /// (EnemyCombatTarget handles the throw itself), and when the player gets inside its minimum range it turns and
    /// walks off at a catchable pace, then turns back to throw again. It stays alert (indicator up) the whole time it is
    /// backing off, even with its back to the player; once it has made room it turns and looks, and only if the player is
    /// nowhere in sight does it go back to roaming.
    /// Roaming, noticing and searching come from ZombieBrain, shared with the melee ZombieAI.
    /// </summary>
    [DefaultExecutionOrder(100)]
    public class RangedZombieAI : ZombieBrain
    {
        [Header("Ranged")]
        [Tooltip("Pace it backs off at when the player gets inside its minimum throwing range. Keep it well under the player's 4 m/s walk so it can be caught.")]
        public float retreatSpeed = 1.6f;
        [Tooltip("Metres past the minimum throwing range it backs off to before turning round to throw again.")]
        public float retreatMargin = 1.5f;
        [Tooltip("After backing off it turns to where the player is and looks for this many seconds; if it still can't see them it goes back to roaming.")]
        public float recheckTime = 1.5f;
        [Tooltip("Seconds it keeps facing a player who slips out of view mid-fight (not while backing off) before it goes searching.")]
        public float trackingMemory = 2.5f;

        /// <summary>Walking away to regain room: EnemyCombatTarget neither turns it round nor starts a throw meanwhile.</summary>
        public bool IsRetreating => retreating && State == "Chasing";

        bool retreating, approaching, rechecking;
        float trackUntil, recheckUntil;
        static readonly float[] RetreatAngles = { 0f, 40f, -40f, 80f, -80f };

        // Backing off and the look-round after it stay alert whatever its eyes say; only the recheck decides it lost the player.
        protected override bool StillTracking => detection.player != null && (retreating || rechecking || Time.time < trackUntil);

        protected override void Engage(Transform player, bool busy)
        {
            if (detection.IsPlayerDetected) { trackUntil = Time.time + trackingMemory; rechecking = false; }
            else if (rechecking && !retreating && Time.time >= recheckUntil)
            {
                // Done making room and the player is nowhere in sight: give up rather than hunt around.
                rechecking = false;
                detection.HoldAlert = false;
                StandDown();
                return;
            }
            detection.HoldAlert = retreating || rechecking;
            Look(player.position + Vector3.up * 1.5f, 1f);
            if (busy) { SetRetreating(false); return; } // throwing, flinching or staggered: ZombieBrain holds the agent still

            Vector3 offset = player.position - transform.position;
            offset.y = 0f;
            float gap = offset.magnitude;
            float throwRange = combat.attackReach * 0.85f; // EnemyCombatTarget starts throws inside this

            // Every band has slack at its edge so it commits to a move instead of flickering between two.
            if (gap >= combat.minProjectileRange + (retreating ? retreatMargin : 0f)) SetRetreating(false);
            else if (Time.time >= nextPath)
            {
                SetRetreating(Retreat(player.position));
                nextPath = Time.time + 0.25f;
            }

            if (retreating)
            {
                approaching = false;
                agent.speed = retreatSpeed;
                TurnTowards(agent.steeringTarget, 8f); // swing round promptly rather than shuffling backwards into the player
                return;
            }
            approaching = gap > throwRange || (approaching && gap > throwRange - 1f);
            if (approaching)
            {
                agent.speed = runSpeed;
                agent.stoppingDistance = throwRange - 1f;
                if (Time.time >= nextPath) { agent.SetDestination(player.position); nextPath = Time.time + 0.25f; }
            }
            else
            {
                // In range, or boxed in too close: plant its feet and face the player so it sees them and throws.
                if (agent.hasPath) agent.ResetPath();
                TurnTowards(player.position, 6f);
            }
        }

        void SetRetreating(bool value)
        {
            // Finished making room (or cut short): turn back and look for the player before deciding anything.
            if (retreating && !value) { rechecking = true; recheckUntil = Time.time + recheckTime; }
            retreating = value;
            detection.HoldAlert = retreating || rechecking; // now, before it turns away and its eyes lose the player
        }

        /// <summary>Head ~3 m away from the threat: straight back first, then veering off walls and out of corners. False when boxed in.</summary>
        bool Retreat(Vector3 threat)
        {
            var away = transform.position - threat;
            away.y = 0f;
            away = away.sqrMagnitude > 0.01f ? away.normalized : -transform.forward;
            foreach (float angle in RetreatAngles)
            {
                var target = transform.position + Quaternion.Euler(0f, angle, 0f) * away * 3f;
                if (NavMesh.Raycast(transform.position, target, out var edge, agent.areaMask)) target = edge.position;
                if ((target - transform.position).sqrMagnitude < 2.25f) continue; // under 1.5 m of room that way
                agent.stoppingDistance = 0.2f;
                return agent.SetDestination(target);
            }
            return false;
        }
    }
}
