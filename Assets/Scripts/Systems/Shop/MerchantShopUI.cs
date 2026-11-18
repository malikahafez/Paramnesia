using UnityEngine;
using TMPro;

public class MerchantShopUI : MonoBehaviour
{
    [Header("Shop Feedback UI")]
    public TextMeshProUGUI merchantFeedbackText;
    public TextMeshProUGUI resourceCounterText;

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

    public void TryPurchaseItem(MerchantItemData item)
    {
        if (item == null || PlayerInventory.Instance == null) return;
        var inv = PlayerInventory.Instance;

        // 1. Check monster drop costs
        bool hasEnough = inv.GetCollectibleCount(CollectibleType.Bone) >= item.bonesCost &&
                         inv.GetCollectibleCount(CollectibleType.Slime) >= item.slimeCost &&
                         inv.GetCollectibleCount(CollectibleType.Heart) >= item.heartsCost;

        if (!hasEnough)
        {
            if (merchantFeedbackText != null)
                merchantFeedbackText.text = notEnoughResourcesMessage;
            return;
        }

        // 2. Enforce max carry limit (max 2)
        if (!inv.CanCarryMore(item.collectibleType, item.maxCarryLimit))
        {
            if (merchantFeedbackText != null)
                merchantFeedbackText.text = maxCapacityMessage;
            return;
        }

        // 3. Deduct monster drops and add the potion to PlayerInventory
        inv.ConsumeCollectible(CollectibleType.Bone, item.bonesCost);
        inv.ConsumeCollectible(CollectibleType.Slime, item.slimeCost);
        inv.ConsumeCollectible(CollectibleType.Heart, item.heartsCost);
        inv.AddCollectible(item.collectibleType, 1);

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
            $"Slime: {inv.GetCollectibleCount(CollectibleType.Slime)}  |  " +
            $"Hearts: {inv.GetCollectibleCount(CollectibleType.Heart)}";
    }
}