using System;
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

    // Subscribe your PlayerHealth / PlayerCombat script to this event to apply healing & 10s buffs!
    public event Action<CollectibleType> OnPotionUsedEvent;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); // Keeps inventory across scenes
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        // Reset inventory counts fresh every time the game/scene starts
        ResetInventory();
    }

    // Reset all inventory counts and notify UI
    public void ResetInventory()
    {
        collectibleCounts.Clear();
        Debug.Log("[PlayerInventory] Inventory has been reset.");
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
        if (type == CollectibleType.Key)
        {
            Debug.Log("Cannot spend Key Fragments!");
            return false;
        }

        int currentCount = GetCollectibleCount(type);
        if (currentCount >= amount)
        {
            collectibleCounts[type] -= amount;
            Debug.Log($"Used/Spent {amount} of {type}. Remaining: {collectibleCounts[type]}");
            OnCollectibleChangedEvent?.Invoke(type, GetCollectibleCount(type));
            return true;
        }

        Debug.Log($"Not enough {type}!");
        return false;
    }

    // Check if the player has enough key fragments to open a door/gate
    public bool CheckKeyCount(CollectibleType type, int amount, int reqAmount)
    {
        return type == CollectibleType.Key && amount >= reqAmount;
    }
    public bool IsPotion(CollectibleType type)
    {
        return type == CollectibleType.HealthPotion ||
               type == CollectibleType.AttackPlusPotion ||
               type == CollectibleType.DefensePlusPotion ||
               type == CollectibleType.InvulnerabilityPotion;
    }
    public bool CanCarryMore(CollectibleType type, int maxLimit = 2)
    {
        return GetCollectibleCount(type) < maxLimit;
    }
    public bool UsePotion(CollectibleType type)
    {
        if (!IsPotion(type)) return false;

        if (ConsumeCollectible(type, 1))
        {
            Debug.Log($"[PlayerInventory] Activated {type}!");
            OnPotionUsedEvent?.Invoke(type);
            return true;
        }
        return false;
    }
}