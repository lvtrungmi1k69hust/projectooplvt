using System;
using MiniNecesse.Core.Enums;

namespace MiniNecesse.Core.Crafting;

/// <summary>
/// Manages the player's equipped Head, Body, and Legs armor slots in O(1) time with zero allocations.
/// </summary>
public sealed class EquipmentLoadout
{
    private readonly ArmorItem?[] _slots = new ArmorItem?[3];

    /// <summary>Gets the equipped head armor piece, if any.</summary>
    public ArmorItem? Head => _slots[(int)ArmorSlot.Head];

    /// <summary>Gets the equipped body armor piece, if any.</summary>
    public ArmorItem? Body => _slots[(int)ArmorSlot.Body];

    /// <summary>Gets the equipped legs armor piece, if any.</summary>
    public ArmorItem? Legs => _slots[(int)ArmorSlot.Legs];

    /// <summary>Gets the summed defense value across all equipped armor slots.</summary>
    public int TotalDefense => (Head?.Defense ?? 0) + (Body?.Defense ?? 0) + (Legs?.Defense ?? 0);

    /// <summary>Raised whenever any equipped armor slot changes.</summary>
    public event Action? OnEquipmentChanged;

    /// <summary>Gets the equipped armor item in <paramref name="slot"/>, or <see langword="null"/>.</summary>
    public ArmorItem? GetSlot(ArmorSlot slot) => _slots[(int)slot];

    /// <summary>Equips <paramref name="armor"/> into its slot and returns any displaced piece.</summary>
    public void Equip(ArmorItem armor, out ArmorItem? previous)
    {
        ArgumentNullException.ThrowIfNull(armor);
        int index = (int)armor.Slot;
        previous = _slots[index];
        _slots[index] = armor;
        OnEquipmentChanged?.Invoke();
    }

    /// <summary>Unequips and returns any armor item in <paramref name="slot"/>.</summary>
    public ArmorItem? Unequip(ArmorSlot slot)
    {
        int index = (int)slot;
        ArmorItem? removed = _slots[index];
        if (removed is not null)
        {
            _slots[index] = null;
            OnEquipmentChanged?.Invoke();
        }

        return removed;
    }

    /// <summary>Restores all three armor slots cleanly without extra change churn.</summary>
    public void Restore(ArmorItem? head, ArmorItem? body, ArmorItem? legs)
    {
        _slots[(int)ArmorSlot.Head] = head;
        _slots[(int)ArmorSlot.Body] = body;
        _slots[(int)ArmorSlot.Legs] = legs;
        OnEquipmentChanged?.Invoke();
    }

    /// <summary>Clears all equipped armor slots.</summary>
    public void Clear()
    {
        bool any = _slots[0] is not null || _slots[1] is not null || _slots[2] is not null;
        _slots[0] = null;
        _slots[1] = null;
        _slots[2] = null;
        if (any)
        {
            OnEquipmentChanged?.Invoke();
        }
    }
}
