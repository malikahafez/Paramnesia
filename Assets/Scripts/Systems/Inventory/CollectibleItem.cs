using UnityEngine;

public class CollectibleItem : MonoBehaviour
{
    [Header("Collectible Settings")]
    [SerializeField] private CollectibleType collectibleType;
    [SerializeField] private int dropAmount = 1;

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

            // Destroy the pickup object from the world
            Destroy(gameObject);
        }
    }
}