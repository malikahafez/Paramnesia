using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class LoadingItemDisplayManager : MonoBehaviour
{
    [Header("Available Items")]
    [SerializeField] private MerchantItemData[] merchantItems;

    [Header("UI References")]
    [SerializeField] private Image itemIconImage;
    [SerializeField] private TMP_Text itemNameText;
    [SerializeField] private TMP_Text itemDescriptionText;

    private const string LastIndexKey = "MerchantLoadingItemIndex";

    private void Start()
    {
        DisplayNextSequentialItem();
    }

    private void DisplayNextSequentialItem()
    {
        if (merchantItems == null || merchantItems.Length == 0) return;

        int currentIndex = PlayerPrefs.GetInt(LastIndexKey, -1);
        currentIndex = (currentIndex + 1) % merchantItems.Length;

        PlayerPrefs.SetInt(LastIndexKey, currentIndex);
        PlayerPrefs.Save();

        MerchantItemData selectedItem = merchantItems[currentIndex];

        if (itemIconImage != null && selectedItem.itemIcon != null)
        {
            itemIconImage.gameObject.SetActive(true);
            itemIconImage.sprite = selectedItem.itemIcon;
        }

        if (itemNameText != null) itemNameText.text = selectedItem.itemName;
        if (itemDescriptionText != null) itemDescriptionText.text = selectedItem.itemDescription;
    }
}