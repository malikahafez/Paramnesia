using UnityEngine;

[CreateAssetMenu(fileName = "NewMerchantItem", menuName = "GhostMerchant/ItemData")]
public class MerchantItemData : ScriptableObject
{
    public string itemName;
    [TextArea] public string itemDescription;
    public Sprite itemIcon;
    public GameObject itemPrefab; // 3D model if spawning it physically

    [Header("Inventory Integration")]
    public CollectibleType collectibleType; // Matches your PlayerInventory enum
    public ItemCategory category;           // Used for filtering tabs (All, Potion, Dropping)
}