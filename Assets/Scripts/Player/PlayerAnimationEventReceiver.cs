using UnityEngine;

namespace StealthGame.Player
{
    /// <summary>
    /// Attached to the child GameObject holding the humanoid Animator (PlayerVisualModel)
    /// to receive AnimationEvents (OnFootstep, OnLand) baked into Starter Assets FBX clips without Console warnings.
    /// </summary>
    public class PlayerAnimationEventReceiver : MonoBehaviour
    {
        public PlayerCharacterController owner;

        private void OnFootstep(AnimationEvent animationEvent)
        {
            // Footstep audio is driven by PlayerCharacterController
        }

        private void OnLand(AnimationEvent animationEvent)
        {
            // Landing audio is driven by PlayerCharacterController
        }
    }
}
