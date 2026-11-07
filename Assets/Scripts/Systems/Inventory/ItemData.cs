using UnityEngine;

public enum ItemCategory

{

All,

Potion,

Dropping

}



[CreateAssetMenu(fileName = "NewItemData", menuName = "Inventory/Item Data")]

public class ItemData : ScriptableObject

{

public string itemName;

public CollectibleType collectibleType; // Must match your PlayerInventory enum type

public ItemCategory category;


[TextArea(3, 5)] public string description;

public Sprite icon; // 2D Image shown in the Details Panel on the right

public GameObject modelPrefab; // 3D Model spawned inside the UI grid cell

} 