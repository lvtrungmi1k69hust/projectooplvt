using System;
using System.Collections.Generic;

namespace MiniNecesse.Core.Loot;

/// <summary>
/// Data-driven drop table associated with an enemy or boss archetype.
/// </summary>
public sealed record LootTable
{
    public string ArchetypeId { get; }
    public IReadOnlyList<LootEntry> Entries { get; }

    public LootTable(string archetypeId, IReadOnlyList<LootEntry> entries)
    {
        if (string.IsNullOrWhiteSpace(archetypeId))
        {
            throw new ArgumentException("ArchetypeId cannot be empty.", nameof(archetypeId));
        }

        ArgumentNullException.ThrowIfNull(entries);
        ArchetypeId = archetypeId;
        Entries = entries;
    }
}
