using UnityEngine;
public enum ShopItemEffect
{
    HealthPotion,
    AttackPlusPotion,
    DefensePlusPotion,
    InvulnerabilityPotion
}
[CreateAssetMenu(fileName = "NewMerchantItem", menuName = "GhostMerchant/ItemData")]
public class MerchantItemData : ScriptableObject
{
    public string itemName;
    [TextArea] public string itemDescription;
    public Sprite itemIcon;
    public GameObject itemPrefab; // 3D model if spawning it physically
    public CollectibleType collectibleType;
    public ItemCategory category;
    
    [Header("Shop Effect & Pricing (Monster Drops)")]
    public ShopItemEffect effectType;
    public int bonesCost;
    public int slimeCost;
    public int heartsCost;
    public int maxCarryLimit = 2;

}