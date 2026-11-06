using UnityEngine;

public class CollectibleItem : MonoBehaviour
{
    [Header("Collectible Settings")]
    [SerializeField] private CollectibleType collectibleType;
    [SerializeField] private int dropAmount = 1;
    [Tooltip("Played in place when picked up; outlives the collectible.")]
    [SerializeField] private ParticleSystem pickupBurst;

    private void OnTriggerEnter(Collider other)
    {
        // Check if the player collided with it (assuming player has tag "Player")
        if (other.CompareTag("Player"))
        {
            // Add to the inventory manager
            if (PlayerInventory.Instance != null)
            {
                PlayerInventory.Instance.AddCollectible(collectibleType, dropAmount);
            }

            // Play pickup sound/effect here if desired
            if (pickupBurst != null)
            {
                pickupBurst.transform.SetParent(null, true);
                pickupBurst.Play(true);
                Destroy(pickupBurst.gameObject, 1.5f);
            }

            // Destroy the pickup object from the world
            Destroy(gameObject);
        }
    }
}