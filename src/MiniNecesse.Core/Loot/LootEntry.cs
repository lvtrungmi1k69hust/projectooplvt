using System;

namespace MiniNecesse.Core.Loot;

/// <summary>
/// Immutable probabilistic drop rule within an enemy or boss <see cref="LootTable"/>.
/// </summary>
public sealed record LootEntry
{
    public string ItemId { get; }
    public float DropChance { get; }
    public int MinQuantity { get; }
    public int MaxQuantity { get; }
    public bool Guaranteed { get; }

    public LootEntry(string itemId, float dropChance, int minQuantity, int maxQuantity, bool guaranteed = false)
    {
        if (string.IsNullOrWhiteSpace(itemId))
        {
            throw new ArgumentException("Loot ItemId cannot be empty.", nameof(itemId));
        }

        if (dropChance < 0f || dropChance > 1f)
        {
            throw new ArgumentOutOfRangeException(nameof(dropChance), "DropChance must be within [0, 1].");
        }

        if (minQuantity <= 0 || maxQuantity < minQuantity)
        {
            throw new ArgumentOutOfRangeException(nameof(minQuantity), "Loot quantity bounds require 0 < minQuantity <= maxQuantity.");
        }

        ItemId = itemId;
        DropChance = dropChance;
        MinQuantity = minQuantity;
        MaxQuantity = maxQuantity;
        Guaranteed = guaranteed;
    }
}
