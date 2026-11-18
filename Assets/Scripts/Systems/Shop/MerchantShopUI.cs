using UnityEngine;
using TMPro;

public class MerchantShopUI : MonoBehaviour
{
    [Header("Shop Feedback UI")]
    public TextMeshProUGUI merchantFeedbackText;
    public TextMeshProUGUI resourceCounterText; // Optional: shows current Bones / Slime / Hearts

    [TextArea]
    public string notEnoughResourcesMessage = "Sorry, but the magic recipe book says I need more ingredients to make this for you.";
    public string maxCapacityMessage = "You cannot carry any more of that potion!";
    public string purchaseSuccessMessage = "Pleasure doing business with you! ^-^";

    private void OnEnable()
    {
        if (merchantFeedbackText != null)
            merchantFeedbackText.text = "Select a potion or buff to craft:";
        RefreshResourceDisplay();
    }

    /// <summary>
    /// Wire each Shop Item Button's OnClick() to this method and pass its MerchantItemData asset!
    /// </summary>
    public void TryPurchaseItem(MerchantItemData item)
    {
        if (item == null) return;

        // NOTE: Match these method names to your PlayerInventory.cs from MCP Task 2
        var inv = PlayerInventory.Instance;
        if (inv == null)
        {
            Debug.LogError("PlayerInventory.Instance not found in scene!");
            return;
        }

        // 1. Check if player has enough monster drops
        bool hasEnough = inv.GetCollectibleCount(CollectibleType.Bone) >= item.bonesCost &&
                         inv.GetCollectibleCount(CollectibleType.SlimeFlask) >= item.slimeCost &&
                         inv.GetCollectibleCount(CollectibleType.Heart) >= item.heartsCost;

        if (!hasEnough)
        {
            if (merchantFeedbackText != null)
                merchantFeedbackText.text = notEnoughResourcesMessage;
            return;
        }

        // 2. Check carry limit (max 2 potions/powerups per GDD Rule 3)
        if (inv.GetPotionCount(item.effectType) >= item.maxCarryLimit)
        {
            if (merchantFeedbackText != null)
                merchantFeedbackText.text = maxCapacityMessage;
            return;
        }

        // 3. Deduct resources & grant item
        inv.ConsumeCollectible(CollectibleType.Bone, item.bonesCost);
        inv.ConsumeCollectible(CollectibleType.SlimeFlask, item.slimeCost);
        inv.ConsumeCollectible(CollectibleType.Heart, item.heartsCost);
        inv.AddPotion(item.effectType, 1);

        if (merchantFeedbackText != null)
            merchantFeedbackText.text = $"{purchaseSuccessMessage} (+1 {item.itemName})";

        RefreshResourceDisplay();
    }

    public void RefreshResourceDisplay()
    {
        if (resourceCounterText == null || PlayerInventory.Instance == null) return;
        var inv = PlayerInventory.Instance;
        resourceCounterText.text =
            $"Bones: {inv.GetCollectibleCount(CollectibleType.Bone)}  |  " +
            $"Slime: {inv.GetCollectibleCount(CollectibleType.SlimeFlask)}  |  " +
            $"Hearts: {inv.GetCollectibleCount(CollectibleType.Heart)}";
    }
}