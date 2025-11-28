using NUnit.Framework.Interfaces;
using UnityEngine;
using UnityEngine.UI;

public class HotBarUI : MonoBehaviour
{
    [SerializeField] private PlayerInventory inventory;
    [SerializeField] private Image[] slotImages;        // size 9
    [SerializeField] private GameObject[] selectionRings; // optional highlight per slot

    private void OnEnable()
    {
        inventory.OnSlotChanged += HandleSlotChanged;
        inventory.OnSelectionChanged += HandleSelectionChanged;
        RefreshAll();
    }

    private void OnDisable()
    {
        inventory.OnSlotChanged -= HandleSlotChanged;
        inventory.OnSelectionChanged -= HandleSelectionChanged;
    }

    private void RefreshAll()
    {
        var slots = inventory.Slots;
        for (int i = 0; i < slotImages.Length && i < slots.Length; i++)
            ApplyIcon(i, slots[i]?.item);

        HandleSelectionChanged(inventory.SelectedIndex);
    }

    private void HandleSlotChanged(int index, ObjectData newItem)
    {
        ApplyIcon(index, newItem);
    }
     
    private void HandleSelectionChanged(int selectedIndex)
    {
        if (selectionRings == null) return;
        for (int i = 0; i < selectionRings.Length; i++)
            if (selectionRings[i] != null)
                selectionRings[i].SetActive(i == selectedIndex);
    }

    private void ApplyIcon(int index, ObjectData item)
    {
        if (index < 0 || index >= slotImages.Length) return;
        var img = slotImages[index];
        if (img == null) return;

        if (item == null || item.icon == null)
        {
            img.sprite = null;
            img.enabled = false;          // hides empty
        }
        else
        {
            img.sprite = item.icon;
            img.enabled = true;
            var c = img.color;
            c.a = 1f;
            img.color = c;
        }
    }
}
