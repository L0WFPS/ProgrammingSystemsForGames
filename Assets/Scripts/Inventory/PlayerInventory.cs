using NUnit.Framework;
using NUnit.Framework.Interfaces;
using System;
using UnityEngine;

[Serializable]
public class ItemSlot
{
    public ObjectData item;
    public bool IsEmpty => item == null;
}

public class PlayerInventory : MonoBehaviour
{
    [Header("Hotbar")]
    [SerializeField] private int slotCount = 9; // slots for 1..9 keys
    [SerializeField] private ItemSlot[] slots;

    [Header("Equip")]
    [SerializeField] private EquipmentHandler equipment;

    public int SelectedIndex { get; private set; } = -1;

    public event Action<int, ObjectData> OnSlotChanged;      // (slotIndex, newItemOrNull)
    public event Action<int> OnSelectionChanged;

    private void Awake()
    {
        // ensure array exists and has the right length
        if (slots == null || slots.Length != slotCount)
            slots = new ItemSlot[slotCount];

        for (int i = 0; i < slotCount; i++)
            if (slots[i] == null) slots[i] = new ItemSlot();

        if (equipment == null) equipment = GetComponent<EquipmentHandler>();
    }

    
    public bool AddItem(ObjectData data, int amount = 1)
    {
        if (data == null) return false;

        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i].IsEmpty)
            {
                slots[i].item = data;
                OnSlotChanged?.Invoke(i, data);   //  tell UI
                // Optional: auto-select/equip when you pick the first item
                // if (IsCompletelyEmptyExcept(i)) SelectSlot(i);
                return true;
            }
        }
        return false; // inventory full
    }

    public void ToggleSlot(int index)
    {
        var current = GetSelectedSlot();

        // If pressing the already selected slot and there is an item => toggle off
        if (SelectedIndex == index && current != null && !current.IsEmpty)
        {
            equipment.Unequip();
            SelectedIndex = -1;                // no slot selected
            OnSelectionChanged?.Invoke(-1);    // notify UI if used
            return;
        }

        // Otherwise select & equip this slot
        SelectSlot(index);
    }

    public ItemSlot GetSelectedSlot()
    {
       
        if (SelectedIndex < 0 || SelectedIndex >= slots.Length) return null;
        return slots[SelectedIndex];
        
    }
    public ObjectData GetSelectedItem() => GetSelectedSlot()?.item;

    public void SelectSlot(int index)
    {
        index = Mathf.Clamp(index, 0, slots.Length - 1);
        SelectedIndex = index;
        EquipSelected();
        OnSelectionChanged?.Invoke(SelectedIndex); //  tell UI
    }

    public void EquipSelected()
    {
        var slot = GetSelectedSlot();

        // If slot exists and has an item -> equip it, else unequip
        if (slot != null && !slot.IsEmpty && slot.item != null)
        {
            equipment.Equip(slot.item);
        }
        else
        {
            equipment.Unequip();
        }
    }

    public bool DropSelectedAndThrow(Transform origin, float force)
    {
        var slot = GetSelectedSlot();
        if (slot == null || slot.IsEmpty) return false;

        // Throw the currently equipped instance (if any)
        var thrown = equipment.ThrowHeld(origin, force);

        // Remove item from inventory slot
        slot.item = null;

        OnSlotChanged?.Invoke(SelectedIndex, null); //  update UI

        // Ensure our “in hand” visuals are cleared
        EnsureValidSelection();

        return thrown != null;
    }
    /// <summary>
    /// Clears the selected slot and updates equipped item.
    /// </summary>
    public void ClearSelected()
    {
        var s = GetSelectedSlot();
        if (s == null) return;
        s.item = null;
        OnSlotChanged?.Invoke(SelectedIndex, null); //  tell UI
        EnsureValidSelection();
    }

    private bool IsAllEmpty()
    {
        for (int i = 0; i < slots.Length; i++)
            if (!slots[i].IsEmpty) return false;
        return true;
    }

    private void EnsureValidSelection()
    {
        // If all empty -> no selection
        if (IsAllEmpty())
        {
            if (SelectedIndex != -1)
            {
                SelectedIndex = -1;
                OnSelectionChanged?.Invoke(-1);
            }
            equipment.Unequip();
            return;
        }

        // If current selection points at an empty slot -> clear selection
        if (SelectedIndex >= 0 && SelectedIndex < slots.Length && slots[SelectedIndex].IsEmpty)
        {
            SelectedIndex = -1;
            OnSelectionChanged?.Invoke(-1);
            equipment.Unequip();
        }
    }

    // Expose slots for UI
    public ItemSlot[] Slots => slots;
}
