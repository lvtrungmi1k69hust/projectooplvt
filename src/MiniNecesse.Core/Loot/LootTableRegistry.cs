using System;
using System.Collections.Generic;

namespace MiniNecesse.Core.Loot;

/// <summary>
/// Registry of data-driven enemy and boss loot tables supporting zero-allocation drop rolling (OCP).
/// </summary>
public sealed class LootTableRegistry
{
    private readonly Dictionary<string, LootTable> _tables = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Gets total registered archetype loot tables.</summary>
    public int Count => _tables.Count;

    /// <summary>Registers or replaces the loot table for an enemy or boss archetype.</summary>
    public void Register(LootTable table)
    {
        ArgumentNullException.ThrowIfNull(table);
        _tables[table.ArchetypeId] = table;
    }

    /// <summary>Registers a batch of loot tables.</summary>
    public void RegisterRange(IEnumerable<LootTable> tables)
    {
        ArgumentNullException.ThrowIfNull(tables);
        foreach (LootTable table in tables)
        {
            Register(table);
        }
    }

    /// <summary>Attempts to retrieve the registered <see cref="LootTable"/> for <paramref name="archetypeId"/>.</summary>
    public bool TryGetTable(string archetypeId, out LootTable? table)
    {
        if (string.IsNullOrWhiteSpace(archetypeId))
        {
            table = null;
            return false;
        }

        return _tables.TryGetValue(archetypeId, out table);
    }

    /// <summary>
    /// Evaluates probabilistic and guaranteed drops for <paramref name="archetypeId"/>
    /// into caller-owned <paramref name="outputBuffer"/> with zero heap allocations.
    /// </summary>
    public bool RollDrops(string archetypeId, Random rng, List<LootDropResult> outputBuffer)
    {
        ArgumentNullException.ThrowIfNull(rng);
        ArgumentNullException.ThrowIfNull(outputBuffer);
        outputBuffer.Clear();

        if (!TryGetTable(archetypeId, out LootTable? table) || table is null)
        {
            return false;
        }

        IReadOnlyList<LootEntry> entries = table.Entries;
        for (int i = 0; i < entries.Count; i++)
        {
            LootEntry entry = entries[i];
            if (!entry.Guaranteed && (entry.DropChance <= 0f || rng.NextDouble() > entry.DropChance))
            {
                continue;
            }

            int quantity = entry.MinQuantity == entry.MaxQuantity
                ? entry.MinQuantity
                : rng.Next(entry.MinQuantity, entry.MaxQuantity + 1);

            outputBuffer.Add(new LootDropResult(entry.ItemId, quantity));
        }

        return outputBuffer.Count > 0;
    }

    /// <summary>Creates a registry pre-populated with default loot tables for all 8 enemies and 5 bosses.</summary>
    public static LootTableRegistry CreateDefault()
    {
        var registry = new LootTableRegistry();
        registry.Register(new LootTable("Slime", new[]
        {
            new LootEntry("slime_gel", 1.0f, 1, 2, true),
            new LootEntry("berry", 0.30f, 1, 2)
        }));
        registry.Register(new LootTable("ZombieRaider", new[]
        {
            new LootEntry("slime_gel", 0.75f, 1, 2),
            new LootEntry("leather", 0.35f, 1, 2)
        }));
        registry.Register(new LootTable("SkeletonArcher", new[]
        {
            new LootEntry("wood", 0.85f, 1, 3),
            new LootEntry("feather", 0.55f, 1, 2)
        }));
        registry.Register(new LootTable("SwampZombie", new[]
        {
            new LootEntry("slime_gel", 0.85f, 1, 3),
            new LootEntry("coal", 0.35f, 1, 2)
        }));
        registry.Register(new LootTable("FrostRaider", new[]
        {
            new LootEntry("iron_ore", 0.70f, 1, 2),
            new LootEntry("cooked_meat", 0.25f, 1, 1)
        }));
        registry.Register(new LootTable("StoneGolem", new[]
        {
            new LootEntry("copper_ore", 1.0f, 1, 3, true),
            new LootEntry("stone", 0.80f, 2, 4)
        }));
        registry.Register(new LootTable("OrcBrute", new[]
        {
            new LootEntry("iron_ore", 1.0f, 1, 3, true),
            new LootEntry("leather", 0.45f, 1, 2)
        }));
        registry.Register(new LootTable("DarkMage", new[]
        {
            new LootEntry("gold_ore", 1.0f, 1, 2, true),
            new LootEntry("crystal", 0.60f, 1, 2)
        }));
        registry.Register(new LootTable("KingSlime", new[]
        {
            new LootEntry("slime_gel", 1.0f, 4, 6, true),
            new LootEntry("copper_ore", 1.0f, 3, 5, true),
            new LootEntry("health_potion", 1.0f, 2, 2, true)
        }));
        registry.Register(new LootTable("ForestGuardian", new[]
        {
            new LootEntry("wood", 1.0f, 8, 12, true),
            new LootEntry("copper_ore", 1.0f, 4, 6, true)
        }));
        registry.Register(new LootTable("SwampWitch", new[]
        {
            new LootEntry("coal", 1.0f, 5, 8, true),
            new LootEntry("iron_ore", 1.0f, 4, 6, true)
        }));
        registry.Register(new LootTable("CryoGolem", new[]
        {
            new LootEntry("iron_ore", 1.0f, 6, 9, true),
            new LootEntry("gold_ore", 1.0f, 3, 5, true)
        }));
        registry.Register(new LootTable("ShadowLord", new[]
        {
            new LootEntry("crystal", 1.0f, 6, 10, true),
            new LootEntry("gold_ore", 1.0f, 6, 9, true)
        }));
        return registry;
    }
}
