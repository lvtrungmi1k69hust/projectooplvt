using System;
using MiniNecesse.Core.Entities;
using MiniNecesse.Core.Enums;
using MiniNecesse.Core.Interfaces;

namespace MiniNecesse.Core.Crafting;

/// <summary>
/// Represents a single-use consumable item such as a healing potion or food dish
/// restoring health, hunger, and optionally applying a temporary <see cref="IFoodBuffStrategy"/>.
/// </summary>
public sealed class ConsumableItem : Item
{
    private readonly int _healAmount;
    private readonly float _hungerRestore;
    private readonly IFoodBuffStrategy? _buffStrategy;

    /// <summary>Gets the amount of health restored when consumed.</summary>
    public int HealAmount => _healAmount;

    /// <summary>Gets the amount of hunger points restored when consumed.</summary>
    public float HungerRestore => _hungerRestore;

    /// <summary>Gets the optional temporary buff strategy applied on consumption.</summary>
    public IFoodBuffStrategy? BuffStrategy => _buffStrategy;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConsumableItem"/> class.
    /// </summary>
    public ConsumableItem(
        string id,
        string name,
        int healAmount,
        int maxStackSize = 20,
        string description = "",
        float hungerRestore = 0f,
        IFoodBuffStrategy? buffStrategy = null)
        : base(id, name, ItemCategory.Consumable, maxStackSize, description)
    {
        if (healAmount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(healAmount), "Heal amount cannot be negative.");
        }

        if (hungerRestore < 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(hungerRestore), "Hunger restore cannot be negative.");
        }

        if (healAmount == 0 && hungerRestore <= 0f && buffStrategy is null)
        {
            throw new ArgumentException("Consumable must restore HP, restore hunger, or apply a buff.");
        }

        _healAmount = healAmount;
        _hungerRestore = hungerRestore;
        _buffStrategy = buffStrategy;
    }

    /// <inheritdoc />
    public override bool TryUse(Player player)
    {
        ArgumentNullException.ThrowIfNull(player);
        if (!player.IsAlive)
        {
            return false;
        }

        bool needsHp = _healAmount > 0 && player.CurrentHp < player.MaxHp;
        bool needsHunger = _hungerRestore > 0f && player.CurrentHunger < player.MaxHunger - 0.01f;
        bool appliesBuff = _buffStrategy is not null;

        if (!needsHp && !needsHunger && !appliesBuff)
        {
            return false;
        }

        if (_healAmount > 0)
        {
            player.Heal(_healAmount);
        }

        if (_hungerRestore > 0f)
        {
            player.RestoreHunger(_hungerRestore);
        }

        if (_buffStrategy is not null)
        {
            player.ApplyBuff(_buffStrategy);
        }

        return true;
    }
}
