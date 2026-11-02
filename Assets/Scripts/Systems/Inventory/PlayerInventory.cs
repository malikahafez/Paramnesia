using System.Collections.Generic;
using UnityEngine;

public class PlayerInventory : MonoBehaviour
{
    public static PlayerInventory Instance { get; private set; }

    // Dictionary to keep track of counts: CollectibleType -> Amount Owned
    private Dictionary<CollectibleType, int> collectibleCounts = new Dictionary<CollectibleType, int>();

    // Optional: Events so UI updates automatically when counts change
    public delegate void OnCollectibleChanged(CollectibleType type, int newAmount);
    public event OnCollectibleChanged OnCollectibleChangedEvent;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); // Optional: keeps inventory across scenes
        }
        else
        {
            Destroy(gameObject);
        }
    }

    // Add collectibles when picked up
    public void AddCollectible(CollectibleType type, int amount = 1)
    {
        if (collectibleCounts.ContainsKey(type))
        {
            collectibleCounts[type] += amount;
        }
        else
        {
            collectibleCounts[type] = amount;
        }

        Debug.Log($"Collected {amount} of {type}. Total: {collectibleCounts[type]}");
        OnCollectibleChangedEvent?.Invoke(type, GetCollectibleCount(type));
    }

    // Check how many the player owns
    public int GetCollectibleCount(CollectibleType type)
    {
        if (collectibleCounts.TryGetValue(type, out int count))
        {
            return count;
        }
        return 0; // Return 0 if player hasn't picked up any yet
    }

    // Spend collectibles (for the store)
    public bool ConsumeCollectible(CollectibleType type, int amount)
    {
        int currentCount = GetCollectibleCount(type);
        if (currentCount >= amount)
        {
            collectibleCounts[type] -= amount;
            Debug.Log($"Spent {amount} of {type}. Remaining: {collectibleCounts[type]}");
            OnCollectibleChangedEvent?.Invoke(type, GetCollectibleCount(type));
            return true; // Purchase successful
        }

        Debug.Log($"Not enough {type} to buy this item!");
        return false; // Not enough currency
    }
}