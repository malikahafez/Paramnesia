using UnityEngine;

[CreateAssetMenu(fileName = "NewMerchantItem", menuName = "GhostMerchant/ItemData")]
public class MerchantItemData : ScriptableObject
{
    public string itemName;
    [TextArea] public string itemDescription;
    public Sprite itemIcon;
    public GameObject itemPrefab; // 3D model if spawning it physically
    public CollectibleType collectibleType;
    public ItemCategory category;

}