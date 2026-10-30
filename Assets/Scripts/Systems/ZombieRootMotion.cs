using UnityEngine;

namespace StealthGame
{
    [RequireComponent(typeof(Animator))]
    public class ZombieRootMotion : MonoBehaviour
    {
        Animator animator;
        ZombieAI ai;

        void Awake()
        {
            animator = GetComponent<Animator>();
            ai = GetComponentInParent<ZombieAI>();
        }

        void OnAnimatorMove()
        {
            if (ai != null && ai.enabled) ai.ApplyRootMotion(animator.deltaPosition);
        }
    }
}
