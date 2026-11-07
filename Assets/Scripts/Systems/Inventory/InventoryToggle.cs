using UnityEngine;

public class InventoryToggle : MonoBehaviour
{
    [Header("UI Reference")]
    [SerializeField] private GameObject inventoryPanel; // Drag your main InventoryPanel here

    private bool isOpen = false;

    private void Start()
    {
        if (inventoryPanel != null)
        {
            inventoryPanel.SetActive(false);
        }
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.I))
        {
            ToggleInventory();
        }
    }

    public void ToggleInventory()
    {
        isOpen = !isOpen;

        if (inventoryPanel != null)
        {
            inventoryPanel.SetActive(isOpen);
        }

        // Pause or resume game time
        Time.timeScale = isOpen ? 0f : 1f;

        // Unlock and show cursor when inventory is open, lock it back when closed
        Cursor.lockState = isOpen ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = isOpen;
    }
}