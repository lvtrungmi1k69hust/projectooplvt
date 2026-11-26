using System;
using System.Collections.Generic;
using MiniNecesse.Core.Enums;

namespace MiniNecesse.Core.Settlement;

/// <summary>
/// Registry storing settler profession definitions and recruitable names loaded from <c>settlers.json</c>.
/// </summary>
public sealed class SettlerCatalog
{
    private readonly Dictionary<string, SettlerProfessionDefinition> _byId = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<SettlerProfessionDefinition> _orderedDefinitions = new();
    private readonly IReadOnlyList<SettlerProfessionDefinition> _definitionsView;
    private readonly List<string> _names = new();
    private readonly IReadOnlyList<string> _namesView;

    /// <summary>Gets all registered settler profession definitions.</summary>
    public IReadOnlyList<SettlerProfessionDefinition> Professions => _definitionsView;

    /// <summary>Gets configured settler display names used when recruiting.</summary>
    public IReadOnlyList<string> Names => _namesView;

    /// <summary>Initializes a new instance of the <see cref="SettlerCatalog"/> class.</summary>
    public SettlerCatalog()
    {
        _definitionsView = _orderedDefinitions.AsReadOnly();
        _namesView = _names.AsReadOnly();
    }

    /// <summary>Registers or updates a settler profession definition.</summary>
    public void RegisterProfession(SettlerProfessionDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        if (_byId.ContainsKey(definition.ProfessionId))
        {
            for (int i = 0; i < _orderedDefinitions.Count; i++)
            {
                if (string.Equals(_orderedDefinitions[i].ProfessionId, definition.ProfessionId, StringComparison.OrdinalIgnoreCase))
                {
                    _orderedDefinitions[i] = definition;
                    break;
                }
            }
        }
        else
        {
            _orderedDefinitions.Add(definition);
        }

        _byId[definition.ProfessionId] = definition;
    }

    /// <summary>Replaces the candidate settler name pool.</summary>
    public void SetNames(IEnumerable<string> names)
    {
        ArgumentNullException.ThrowIfNull(names);
        _names.Clear();
        foreach (string name in names)
        {
            if (!string.IsNullOrWhiteSpace(name))
            {
                _names.Add(name.Trim());
            }
        }
    }

    /// <summary>Looks up a profession definition by id (e.g. "Miner" or "Hunter").</summary>
    public bool TryGetProfession(string professionId, out SettlerProfessionDefinition? definition)
    {
        if (string.IsNullOrWhiteSpace(professionId))
        {
            definition = null;
            return false;
        }

        return _byId.TryGetValue(professionId, out definition);
    }

    /// <summary>Deterministically chooses a settler name for recruitment sequence <paramref name="index"/>.</summary>
    public string PickName(int index)
    {
        if (_names.Count == 0)
        {
            return $"Settler {index + 1}";
        }

        return _names[global::System.Math.Abs(index) % _names.Count];
    }

    /// <summary>Creates a default catalog matching <c>settlers.json</c> for offline/test usage.</summary>
    public static SettlerCatalog CreateDefault()
    {
        var catalog = new SettlerCatalog();
        catalog.SetNames(new[] { "Aldric", "Freya", "Bram", "Elara", "Torin", "Mira", "Garrick", "Sera" });

        catalog.RegisterProfession(new SettlerProfessionDefinition(
            "Miner",
            SettlerProfession.Miner,
            "Miner",
            maxHp: 90,
            moveSpeed: 82f,
            workIntervalSeconds: 12f,
            recruitCostItemId: "bread",
            recruitCostQuantity: 2,
            producedItems: new[]
            {
                new SettlerProducedEntry("stone", 2),
                new SettlerProducedEntry("copper_ore", 1),
                new SettlerProducedEntry("coal", 1)
            },
            trades: new[]
            {
                new SettlerTradeOffer("miner_copper", "wood", 4, "copper_ore", 2),
                new SettlerTradeOffer("miner_iron", "stone", 6, "iron_ore", 2),
                new SettlerTradeOffer("miner_coal", "wood", 3, "coal", 2)
            }));

        catalog.RegisterProfession(new SettlerProfessionDefinition(
            "Hunter",
            SettlerProfession.Hunter,
            "Hunter",
            maxHp: 95,
            moveSpeed: 92f,
            workIntervalSeconds: 12f,
            recruitCostItemId: "cooked_meat",
            recruitCostQuantity: 1,
            producedItems: new[]
            {
                new SettlerProducedEntry("raw_meat", 1),
                new SettlerProducedEntry("leather", 1),
                new SettlerProducedEntry("feather", 2)
            },
            trades: new[]
            {
                new SettlerTradeOffer("hunter_meat", "wood", 3, "raw_meat", 2),
                new SettlerTradeOffer("hunter_leather", "wheat", 4, "leather", 2),
                new SettlerTradeOffer("hunter_feathers", "stone", 3, "feather", 3)
            }));

        return catalog;
    }
}
