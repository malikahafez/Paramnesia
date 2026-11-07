using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class InventoryManager : MonoBehaviour
{
    [Header("Item Database")]
    [Tooltip("Drag all your MerchantItemData assets here (potions, bones, keys, slime).")]
   [SerializeField] private List<ScriptableObject> allGameItems = new List<ScriptableObject>();

    [Header("UI Panels & Setup")]
    [SerializeField] private GameObject inventoryCanvasPanel; // The main UI panel that toggles with 'I'
    [SerializeField] private Transform gridContainer;
    [SerializeField] private GameObject slotPrefab;

    [Header("Details Panel (Right Side)")]
    [SerializeField] private Image detailItemImage;
    [SerializeField] private TextMeshProUGUI detailItemName;
    [SerializeField] private TextMeshProUGUI detailItemDescription;
    [SerializeField] private TextMeshProUGUI detailItemCount;
    [SerializeField] private GameObject detailsPanelGroup;

    [Header("Tab Buttons")]
    [SerializeField] private Button allTabButton;
    [SerializeField] private Button potionsTabButton;
    [SerializeField] private Button droppingsTabButton;

    private ItemCategory currentCategory = ItemCategory.All;
    private MerchantItemData selectedItem;
    private InventorySlotUI selectedSlot;
    private bool isOpen = false;

    private void Awake()
    {
        // Use Awake instead of Start so it initializes even if the GameObject starts disabled
        if (allTabButton) allTabButton.onClick.AddListener(() => FilterCategory(ItemCategory.All));
        if (potionsTabButton) potionsTabButton.onClick.AddListener(() => FilterCategory(ItemCategory.Potion));
        if (droppingsTabButton) droppingsTabButton.onClick.AddListener(() => FilterCategory(ItemCategory.Dropping));

        if (inventoryCanvasPanel != null)
        {
            inventoryCanvasPanel.SetActive(false);
        }
        if (detailsPanelGroup != null)
    {
        detailsPanelGroup.SetActive(false);
    }
    }

    private void Start()
    {
        if (PlayerInventory.Instance != null)
        {
            PlayerInventory.Instance.OnCollectibleChangedEvent += OnInventoryChanged;
        }
        else
        {
            Debug.LogError("[InventoryManager] PlayerInventory.Instance is NULL! Make sure PlayerInventory exists in the scene.");            Debug.LogError("[InventoryManager] PlayerInventory.Instance is NULL! Make sure PlayerInventory exists in the scene.");
        }
    }

    private void OnDestroy()
    {
        if (PlayerInventory.Instance != null)
        {
            PlayerInventory.Instance.OnCollectibleChangedEvent -= OnInventoryChanged;
        }
    }

    private void Update()
    {
        // Toggle inventory with 'I'
        if (Input.GetKeyDown(KeyCode.I))
        {
            ToggleInventory();
        }

        // Press 'E' to use/consume the selected item
        if (isOpen && Input.GetKeyDown(KeyCode.E) && selectedItem != null)
        {
            UseSelectedItem();
        }
    }

    [Header("Camera Control (Optional)")]
[SerializeField] private MonoBehaviour cameraController; // Drag your MouseLook, CinemachineInputAxisDriver, or PlayerCamera script here

public void ToggleInventory()
{
    isOpen = !isOpen;

    if (inventoryCanvasPanel != null)
    {
        inventoryCanvasPanel.SetActive(isOpen);
    }

    // Pause game scale and unlock cursor
    Time.timeScale = isOpen ? 0f : 1f;
    Cursor.lockState = isOpen ? CursorLockMode.None : CursorLockMode.Locked;
    Cursor.visible = isOpen;

    // Freeze / unfreeze camera script if assigned
    if (cameraController != null)
    {
        cameraController.enabled = !isOpen;
    }

    if (isOpen)
    {
        RefreshGrid(); 
    }
    else
    {
        DeselectItem();
    }
}

    public void FilterCategory(ItemCategory category)
    {
        currentCategory = category;
        DeselectItem();
        RefreshGrid();
    }

    private void OnInventoryChanged(CollectibleType type, int newAmount)
    {
        if (isOpen)
        {
            RefreshGrid();
        }

        if (selectedItem != null && selectedItem.collectibleType == type)
        {
            if (newAmount <= 0) DeselectItem();
            else UpdateDetailsPanel();
        }
    }

 public void RefreshGrid()
{
    if (gridContainer == null || slotPrefab == null) return;

    foreach (Transform child in gridContainer)
    {
        Destroy(child.gameObject);
    }

    int spawnedCount = 0;

    foreach (ScriptableObject itemObj in allGameItems)
    {
        if (itemObj == null) continue;

        int count = GetItemCount(itemObj);

        if (count > 0)
        {
            ItemCategory itemCategory = GetItemCategory(itemObj);
            bool matchesCategory = (currentCategory == ItemCategory.All) || (itemCategory == currentCategory);

            if (matchesCategory)
            {
                GameObject slotObj = Instantiate(slotPrefab, gridContainer);
                InventorySlotUI slotUI = slotObj.GetComponent<InventorySlotUI>();

                if (slotUI != null)
                {
                    CollectibleType itemType = GetCollectibleType(itemObj);
                    slotUI.SetupGeneric(itemObj, itemType, count, this);
                    spawnedCount++;
                }
            }
        }
    }

    Debug.Log($"[InventoryManager] Successfully rendered {spawnedCount} items into grid for category {currentCategory}.");    Debug.Log($"[InventoryManager] Successfully rendered {spawnedCount} items into grid for category {currentCategory}.");
}
private int GetItemCount(ScriptableObject item)
{
    if (item is MerchantItemData merchant)
    {
        // Reads owned quantity directly from MerchantItemData field/property
        var field = merchant.GetType().GetField("quantity") ?? merchant.GetType().GetField("ownedCount") ?? merchant.GetType().GetField("count");
        if (field != null) return (int)field.GetValue(merchant);

        var prop = merchant.GetType().GetProperty("quantity") ?? merchant.GetType().GetProperty("ownedCount") ?? merchant.GetType().GetProperty("count");
        if (prop != null) return (int)prop.GetValue(merchant);

        return 1; // Default to 1 if bought merchant items don't track stack counts
    }

    if (PlayerInventory.Instance != null)
    {
        CollectibleType itemType = GetCollectibleType(item);
        return PlayerInventory.Instance.GetCollectibleCount(itemType);
    }

    return 0;
}

private CollectibleType GetCollectibleType(ScriptableObject item)
{
    if (item is MerchantItemData merchant) return merchant.collectibleType;

    var field = item.GetType().GetField("collectibleType", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
    if (field != null) return (CollectibleType)field.GetValue(item);

    return default;
}

public void SelectGenericItem(ScriptableObject item, CollectibleType type, InventorySlotUI slot)
{
    if (selectedSlot != null) selectedSlot.SetSelected(false);
    selectedSlot = slot;
    if (selectedSlot != null) selectedSlot.SetSelected(true);

    if (detailsPanelGroup) detailsPanelGroup.SetActive(true);

    string itemName = type.ToString();
    string itemDescription = $"Collected world item: {type}";

    if (item != null)
    {
        if (item is MerchantItemData merchant)
        {
            itemName = !string.IsNullOrEmpty(merchant.itemName) ? merchant.itemName : merchant.name;
            itemDescription = merchant.itemDescription;
        }
        else
        {
            // Reflection fallback for custom world items if they have custom name/description fields
            var nameField = item.GetType().GetField("itemName") ?? item.GetType().GetField("name");
            if (nameField != null && nameField.GetValue(item) is string n && !string.IsNullOrEmpty(n)) 
                itemName = n;
            else 
                itemName = item.name;

            var descField = item.GetType().GetField("itemDescription") ?? item.GetType().GetField("description");
            if (descField != null && descField.GetValue(item) is string d && !string.IsNullOrEmpty(d))
            {
                itemDescription = d;
            }
        }
    }

    if (detailItemName) detailItemName.text = itemName;
    if (detailItemDescription) detailItemDescription.text = itemDescription;

    // Extract and display sprite icon in details panel
    if (detailItemImage != null && item != null)
    {
        Sprite sprite = null;
        if (item is MerchantItemData merchant)
        {
            sprite = merchant.itemIcon;
        }
        else
        {
            var field = item.GetType().GetField("itemIcon") ?? item.GetType().GetField("icon") ?? item.GetType().GetField("sprite");
            if (field != null) sprite = field.GetValue(item) as Sprite;
        }

        if (sprite != null)
        {
            detailItemImage.gameObject.SetActive(true);
            detailItemImage.sprite = sprite;
        }
        else
        {
            detailItemImage.gameObject.SetActive(false);
        }
    }

    int count = PlayerInventory.Instance != null ? PlayerInventory.Instance.GetCollectibleCount(type) : 0;
    if (detailItemCount) detailItemCount.text = $"Owned: {count}";
}
private ItemCategory GetItemCategory(ScriptableObject item)
{
    if (item == null) return ItemCategory.All;

    if (item is MerchantItemData merchantItem)
    {
        return merchantItem.category;
    }

    // Default world collectibles to Dropping if unspecified
    return ItemCategory.Dropping;
}    public void SelectItem(MerchantItemData item, InventorySlotUI slot)
    {
        if (selectedSlot != null) selectedSlot.SetSelected(false);
        selectedItem = item;
        selectedSlot = slot;
        if (selectedSlot != null) selectedSlot.SetSelected(true);
        UpdateDetailsPanel();
    }
    public void FilterAll()
{
    currentCategory = ItemCategory.All;
    RefreshGrid();
}

public void FilterPotions()
{
    currentCategory = ItemCategory.Potion;
    RefreshGrid();
}

public void FilterDroppings()
{
    currentCategory = ItemCategory.Dropping;
    RefreshGrid();
}

    private void DeselectItem()
    {
        if (selectedSlot != null) selectedSlot.SetSelected(false);
        selectedItem = null;
        selectedSlot = null;
        ClearDetailsPanel();
    }

    private void UpdateDetailsPanel()
    {
        if (selectedItem == null) return;

        if (detailsPanelGroup) detailsPanelGroup.SetActive(true);
        if (detailItemImage) detailItemImage.sprite = selectedItem.itemIcon;
        if (detailItemName) detailItemName.text = selectedItem.itemName;
        if (detailItemDescription) detailItemDescription.text = selectedItem.itemDescription;

        if (PlayerInventory.Instance == null) return;

        int count = PlayerInventory.Instance.GetCollectibleCount(selectedItem.collectibleType);
        if (detailItemCount) detailItemCount.text = $"Owned: {count}";
    }

    private void ClearDetailsPanel()
    {
        if (detailsPanelGroup) detailsPanelGroup.SetActive(false);
        if (detailItemName) detailItemName.text = "";
        if (detailItemDescription) detailItemDescription.text = "";
        if (detailItemCount) detailItemCount.text = "";
    }

    public void UseSelectedItem()
    {
        if (selectedItem == null) return;
        if (PlayerInventory.Instance != null)
        {
            PlayerInventory.Instance.ConsumeCollectible(selectedItem.collectibleType, 1);
        }
    }
}