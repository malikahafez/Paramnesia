using UnityEngine;

namespace StealthGame
{
    [RequireComponent(typeof(Animator))]
    public class ZombieRootMotion : MonoBehaviour
    {
        Animator animator;
        ZombieBrain ai;

        void Awake()
        {
            animator = GetComponent<Animator>();
            ai = GetComponentInParent<ZombieBrain>();
        }

        void OnAnimatorMove()
        {
            if (ai != null && ai.enabled) ai.ApplyRootMotion(animator.deltaPosition);
        }

        // Needs IK Pass on the controller's base layer.
        void OnAnimatorIK(int layerIndex)
        {
            if (ai == null || !ai.enabled) return;
            animator.SetLookAtWeight(ai.LookWeight, 0.2f, 0.9f, 0f, 0.6f);
            animator.SetLookAtPosition(ai.LookPosition);
        }
    }
}
