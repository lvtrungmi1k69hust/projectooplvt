using System;
using MiniNecesse.Core.Entities;
using MiniNecesse.Core.Enums;

namespace MiniNecesse.Core.Crafting;

/// <summary>
/// Represents an equippable armor piece providing defense in <see cref="ArmorSlot"/>.
/// </summary>
public sealed class ArmorItem : Item
{
    /// <summary>Gets the target equipment slot (Head, Body, or Legs).</summary>
    public ArmorSlot Slot { get; }

    /// <summary>Gets the flat defense bonus contributed while equipped.</summary>
    public int Defense { get; }

    /// <summary>Gets the optional maximum HP bonus metadata.</summary>
    public int MaxHpBonus { get; }

    /// <summary>Initializes a new instance of the <see cref="ArmorItem"/> class.</summary>
    public ArmorItem(
        string id,
        string name,
        ArmorSlot slot,
        int defense,
        int maxHpBonus = 0,
        string description = "")
        : base(id, name, ItemCategory.Armor, maxStackSize: 1, description)
    {
        if (defense < 0 || maxHpBonus < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(defense), "Armor stats cannot be negative.");
        }

        Slot = slot;
        Defense = defense;
        MaxHpBonus = maxHpBonus;
    }

    /// <inheritdoc />
    public override ArmorItem? AsArmorItem() => this;

    /// <inheritdoc />
    public override bool TryUse(Player player)
    {
        ArgumentNullException.ThrowIfNull(player);
        return player.TryEquipArmor(this);
    }
}
