using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

public class InventorySlotUI : MonoBehaviour, IPointerClickHandler
{
    [Header("UI References")]
    [SerializeField] private Image iconImage;
    [SerializeField] private TextMeshProUGUI countText;
    [SerializeField] private Image selectionHighlight;

    private ScriptableObject rawItemData;
    private CollectibleType collectibleType;
    private InventoryManager inventoryManager;

    public void SetupGeneric(ScriptableObject itemData, CollectibleType type, int count, InventoryManager manager)
    {
        rawItemData = itemData;
        collectibleType = type;
        inventoryManager = manager;

        if (selectionHighlight != null) selectionHighlight.enabled = false;
        if (countText != null) countText.gameObject.SetActive(false);
        // Safely extract sprite icon via reflection or direct check
        Sprite icon = ExtractIcon(itemData);
        if (iconImage != null)
        {
            if (icon != null)
            {
                iconImage.sprite = icon;
                iconImage.gameObject.SetActive(true);
            }
            else
            {
                iconImage.gameObject.SetActive(false); // Prevents white box if icon is missing
            }
        }
    }

    private Sprite ExtractIcon(ScriptableObject obj)
    {
        if (obj == null) return null;
        if (obj is MerchantItemData merchant) return merchant.itemIcon;

        // Fallback reflection for world collectible asset fields (e.g., itemIcon, icon, sprite)
        var field = obj.GetType().GetField("itemIcon") ?? obj.GetType().GetField("icon") ?? obj.GetType().GetField("sprite");
        if (field != null) return field.GetValue(obj) as Sprite;

        return null;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (inventoryManager != null)
        {
            inventoryManager.SelectGenericItem(rawItemData, collectibleType, this);
        }
    }

    public void SetSelected(bool isSelected)
    {
        if (selectionHighlight != null) selectionHighlight.enabled = isSelected;
    }
}