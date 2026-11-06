using UnityEngine;

namespace StealthGame
{
    /// <summary>Melee zombie: runs the player down into claw range; roaming and searching come from ZombieBrain.</summary>
    [DefaultExecutionOrder(100)]
    public class ZombieAI : ZombieBrain
    {
        protected override void Engage(Transform player, bool busy)
        {
            agent.speed = runSpeed;
            agent.stoppingDistance = combat.attackReach * 0.8f;
            if (!busy && Time.time >= nextPath) { agent.SetDestination(player.position); nextPath = Time.time + 0.2f; }
            Look(player.position + Vector3.up * 1.5f, 1f);
        }
    }
}
