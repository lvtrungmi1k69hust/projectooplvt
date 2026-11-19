using System;
using MiniNecesse.Core.Entities;
using MiniNecesse.Core.Enums;

namespace MiniNecesse.Core.Crafting;

/// <summary>
/// Represents a single-use consumable item such as a healing potion.
/// </summary>
public sealed class ConsumableItem : Item
{
    private readonly int _healAmount;

    /// <summary>Gets the amount of health restored when consumed.</summary>
    public int HealAmount => _healAmount;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConsumableItem"/> class.
    /// </summary>
    public ConsumableItem(
        string id,
        string name,
        int healAmount,
        int maxStackSize = 20,
        string description = "")
        : base(id, name, ItemCategory.Consumable, maxStackSize, description)
    {
        if (healAmount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(healAmount), "Heal amount must be positive.");
        }

        _healAmount = healAmount;
    }

    /// <inheritdoc />
    public override bool TryUse(Player player)
    {
        ArgumentNullException.ThrowIfNull(player);
        if (!player.IsAlive || player.CurrentHp >= player.MaxHp)
        {
            return false;
        }

        player.Heal(_healAmount);
        return true;
    }
}
