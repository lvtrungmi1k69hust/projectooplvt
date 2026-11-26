using System;
using System.Collections.Generic;
using MiniNecesse.Core.Enums;

namespace MiniNecesse.Core.Settlement;

/// <summary>Represents one item yielded periodically when a settler finishes a work cycle.</summary>
public sealed record SettlerProducedEntry(string ItemId, int Quantity);

/// <summary>
/// Immutable data-driven occupation profile loaded from <c>settlers.json</c>.
/// </summary>
public sealed record SettlerProfessionDefinition
{
    public string ProfessionId { get; }
    public SettlerProfession Profession { get; }
    public string DisplayName { get; }
    public int MaxHp { get; }
    public float MoveSpeed { get; }
    public float WorkIntervalSeconds { get; }
    public string RecruitCostItemId { get; }
    public int RecruitCostQuantity { get; }
    public IReadOnlyList<SettlerProducedEntry> ProducedItems { get; }
    public IReadOnlyList<SettlerTradeOffer> Trades { get; }

    public SettlerProfessionDefinition(
        string professionId,
        SettlerProfession profession,
        string displayName,
        int maxHp,
        float moveSpeed,
        float workIntervalSeconds,
        string recruitCostItemId,
        int recruitCostQuantity,
        IReadOnlyList<SettlerProducedEntry> producedItems,
        IReadOnlyList<SettlerTradeOffer> trades)
    {
        if (string.IsNullOrWhiteSpace(professionId) || string.IsNullOrWhiteSpace(recruitCostItemId))
        {
            throw new ArgumentException("Settler profession and recruit item IDs cannot be empty.");
        }

        if (maxHp <= 0 || moveSpeed <= 0f || workIntervalSeconds <= 0f || recruitCostQuantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxHp), "Settler stats and costs must be positive.");
        }

        ArgumentNullException.ThrowIfNull(producedItems);
        ArgumentNullException.ThrowIfNull(trades);

        ProfessionId = professionId;
        Profession = profession;
        DisplayName = string.IsNullOrWhiteSpace(displayName) ? professionId : displayName;
        MaxHp = maxHp;
        MoveSpeed = moveSpeed;
        WorkIntervalSeconds = workIntervalSeconds;
        RecruitCostItemId = recruitCostItemId;
        RecruitCostQuantity = recruitCostQuantity;
        ProducedItems = producedItems;
        Trades = trades;
    }
}
